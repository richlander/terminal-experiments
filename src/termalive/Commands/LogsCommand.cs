// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Reads output from a session with various streaming options.
/// </summary>
internal static class LogsCommand
{
    // Exit codes for different stop reasons
    public const int ExitPattern = 0;          // Pattern matched
    public const int ExitError = 1;            // Error occurred
    public const int ExitTimeout = 2;          // Timeout reached
    public const int ExitIdle = 3;             // Idle timeout reached
    public const int ExitSessionEnded = 4;     // Session exited
    public const int ExitComplete = 0;         // Normal completion (no follow)

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("-"))
        {
            PrintHelp();
            return ExitError;
        }

        string sessionId = args[0];
        string uri = "pipe://termalive";
        bool follow = false;
        bool json = false;
        int? tail = null;
        string? untilPattern = null;
        TimeSpan? waitIdle = null;
        TimeSpan? timeout = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--follow" or "-f":
                    follow = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--tail" or "-n":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var t))
                    {
                        tail = t;
                    }
                    break;
                case "--until":
                    if (i + 1 < args.Length)
                    {
                        untilPattern = args[++i];
                    }
                    break;
                case "--wait-idle":
                    if (i + 1 < args.Length && TryParseTimeSpan(args[++i], out var idle))
                    {
                        waitIdle = idle;
                    }
                    break;
                case "--timeout":
                    if (i + 1 < args.Length && TryParseTimeSpan(args[++i], out var to))
                    {
                        timeout = to;
                    }
                    break;
                case "--uri" or "-u":
                    if (i + 1 < args.Length)
                    {
                        uri = args[++i];
                    }
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return ExitComplete;
            }
        }

        await using var client = await SessionClient.ConnectAsync(uri);
        await using var attachment = await client.AttachAsync(sessionId);

        var outputBuilder = new StringBuilder();

        // Write start event (JSON only)
        if (json)
        {
            WriteJsonEvent(new LogEvent("start", sessionId));
        }

        // First, output buffered content
        if (attachment.BufferedOutput.Length > 0)
        {
            var buffered = attachment.BufferedOutput;

            // Apply tail if specified
            if (tail.HasValue && buffered.Length > tail.Value)
            {
                buffered = buffered.Slice(buffered.Length - tail.Value);
            }

            var content = Encoding.UTF8.GetString(buffered.Span);
            outputBuilder.Append(content);

            if (json)
            {
                WriteJsonEvent(new LogEvent("data", sessionId) { Content = content });
            }
            else
            {
                Console.Write(content);
            }

            // Check for pattern in buffered content
            if (untilPattern != null && content.Contains(untilPattern))
            {
                if (json)
                {
                    WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "pattern", Pattern = untilPattern });
                }
                return ExitPattern;
            }
        }

        // If not following, we're done
        if (!follow)
        {
            if (json)
            {
                WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "complete" });
            }
            return ExitComplete;
        }

        // Set up cancellation for timeout
        using var timeoutCts = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            timeoutCts.CancelAfter(timeout.Value);
        }

        // Track idle time
        var lastDataTime = DateTime.UtcNow;
        using var idleCts = new CancellationTokenSource();

        // Combined cancellation
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, idleCts.Token);

        int exitCode = ExitComplete;

        try
        {
            // Start idle timer task if needed
            Task? idleTimerTask = null;
            if (waitIdle.HasValue)
            {
                idleTimerTask = Task.Run(async () =>
                {
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        var elapsed = DateTime.UtcNow - lastDataTime;
                        if (elapsed >= waitIdle.Value)
                        {
                            await idleCts.CancelAsync();
                            return;
                        }
                        await Task.Delay(100, linkedCts.Token);
                    }
                }, linkedCts.Token);
            }

            // Stream output
            await foreach (var data in attachment.ReadOutputAsync(linkedCts.Token))
            {
                lastDataTime = DateTime.UtcNow;
                var content = Encoding.UTF8.GetString(data.Span);
                outputBuilder.Append(content);

                if (json)
                {
                    WriteJsonEvent(new LogEvent("data", sessionId) { Content = content });
                }
                else
                {
                    Console.Write(content);
                }

                // Check for pattern
                if (untilPattern != null && outputBuilder.ToString().Contains(untilPattern))
                {
                    if (json)
                    {
                        WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "pattern", Pattern = untilPattern });
                    }
                    return ExitPattern;
                }
            }

            // Stream ended (session exited)
            if (json)
            {
                WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "exit" });
            }
            exitCode = ExitSessionEnded;
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.Token.IsCancellationRequested)
            {
                if (json)
                {
                    WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "timeout" });
                }
                exitCode = ExitTimeout;
            }
            else if (idleCts.Token.IsCancellationRequested)
            {
                if (json)
                {
                    WriteJsonEvent(new LogEvent("end", sessionId) { Reason = "idle" });
                }
                exitCode = ExitIdle;
            }
        }

        return exitCode;
    }

    private static void WriteJsonEvent(LogEvent evt)
    {
        var jsonStr = JsonSerializer.Serialize(evt, LogEventContext.Default.LogEvent);
        Console.WriteLine(jsonStr);
    }

    private static bool TryParseTimeSpan(string value, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value[..^2], out var ms))
            {
                result = TimeSpan.FromMilliseconds(ms);
                return true;
            }
        }
        else if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value[..^1], out var s))
            {
                result = TimeSpan.FromSeconds(s);
                return true;
            }
        }
        else if (value.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value[..^1], out var m))
            {
                result = TimeSpan.FromMinutes(m);
                return true;
            }
        }
        else if (int.TryParse(value, out var seconds))
        {
            result = TimeSpan.FromSeconds(seconds);
            return true;
        }

        return false;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive logs <session-id> [options]

            Read output from a session.

            Arguments:
              <session-id>         Session identifier

            Options:
              -f, --follow         Stream output continuously
              --json               Output as JSONL (default: raw text)
              -n, --tail <bytes>   Only show last N bytes of buffer
              --until <pattern>    Stop when pattern is found in output
              --wait-idle <dur>    Stop after duration of no output (e.g., 5s, 500ms)
              --timeout <dur>      Stop after total duration (e.g., 60s, 5m)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -h, --help           Show this help message

            Exit codes:
              0  Pattern matched or complete
              1  Error
              2  Timeout reached
              3  Idle timeout reached
              4  Session exited

            Duration format: 500ms, 5s, 5m, or just seconds (e.g., 30)

            Examples:
              termalive logs my-session
              termalive logs my-session --follow --wait-idle 5s
              termalive logs worker --follow --until "DONE" --timeout 60s
              termalive logs worker -f --json
            """);
    }
}

/// <summary>
/// Log event for JSONL output.
/// </summary>
internal sealed class LogEvent
{
    public LogEvent(string @event, string session)
    {
        Event = @event;
        Session = session;
        Ts = DateTime.UtcNow.ToString("O");
    }

    public string Ts { get; set; }
    public string Session { get; set; }
    public string Event { get; set; }
    public string? Content { get; set; }
    public string? Reason { get; set; }
    public string? Pattern { get; set; }
}

/// <summary>
/// Source-generated JSON context for NativeAOT compatibility.
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(LogEvent))]
internal partial class LogEventContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
