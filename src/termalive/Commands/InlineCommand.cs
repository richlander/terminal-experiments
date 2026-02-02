// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Starts an inline attached session (like running bash in bash).
/// This is the default mode when running 'termalive' with no command.
/// </summary>
/// <remarks>
/// Design: Unlike 'attach' which uses alternate screen buffer, inline mode
/// streams output directly to the terminal without clearing the screen.
/// This provides a more natural experience similar to running 'su' or 'ssh'.
/// 
/// Inspired by: bash subshells, su, ssh inline behavior.
/// </remarks>
internal static class InlineCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? command = null;
        string? workingDirectory = null;
        string uri = "pipe://termalive";
        TimeSpan? idleTimeout = null;
        string? logFile = null;
        string? termOverride = null;
        var environment = new Dictionary<string, string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--command" or "-c":
                    if (i + 1 < args.Length)
                    {
                        command = args[++i];
                    }
                    break;
                case "--cwd":
                    if (i + 1 < args.Length)
                    {
                        workingDirectory = args[++i];
                    }
                    break;
                case "--log" or "-l":
                    if (i + 1 < args.Length)
                    {
                        logFile = args[++i];
                    }
                    break;
                case "--term":
                    if (i + 1 < args.Length)
                    {
                        termOverride = args[++i];
                    }
                    break;
                case "--env" or "-e":
                    if (i + 1 < args.Length)
                    {
                        var env = args[++i];
                        var eqIndex = env.IndexOf('=');
                        if (eqIndex > 0)
                        {
                            environment[env[..eqIndex]] = env[(eqIndex + 1)..];
                        }
                    }
                    break;
                case "--uri" or "-u":
                    if (i + 1 < args.Length)
                    {
                        uri = args[++i];
                    }
                    break;
                case "--idle-timeout" or "-t":
                    if (i + 1 < args.Length)
                    {
                        idleTimeout = ParseTimeout(args[++i]);
                    }
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        // Default to user's shell
        command ??= Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";

        // Ensure TERM is set - use xterm-256color as safe default
        if (!environment.ContainsKey("TERM"))
        {
            if (termOverride != null)
            {
                environment["TERM"] = termOverride;
            }
            else
            {
                var parentTerm = Environment.GetEnvironmentVariable("TERM");
                var safeTerm = parentTerm switch
                {
                    "xterm" or "xterm-256color" or "xterm-color" => parentTerm,
                    "screen" or "screen-256color" => parentTerm,
                    "tmux" or "tmux-256color" => parentTerm,
                    "linux" or "vt100" or "vt220" => parentTerm,
                    _ => "xterm-256color"
                };
                environment["TERM"] = safeTerm;
            }
        }

        // Generate a session ID
        var sessionId = $"inline-{Guid.NewGuid():N}"[..16];

        var options = new PtyOptions
        {
            Command = command,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            Environment = environment,
            Columns = Console.WindowWidth,
            Rows = Console.WindowHeight,
            IdleTimeout = idleTimeout
        };

        await using var client = await SessionClient.ConnectAsync(uri);
        var session = await client.CreateSessionAsync(sessionId, options);

        // Auto-attach to the new session
        await using var attachment = await client.AttachAsync(sessionId, Console.WindowWidth, Console.WindowHeight);

        // Open log file if specified
        FileStream? logStream = null;
        if (logFile != null)
        {
            logStream = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            Console.Error.WriteLine($"Logging to: {logFile}");
        }

        try
        {
            // Show inline indicator - no alternate screen buffer!
            // This is the key difference from AttachCommand
            Console.WriteLine($"[termalive: {sessionId}] (Ctrl+B, D to detach)");

            // Display any buffered output inline (no screen clearing)
            if (attachment.BufferedOutput.Length > 0)
            {
                using var stdout = Console.OpenStandardOutput();
                await stdout.WriteAsync(attachment.BufferedOutput);
                await stdout.FlushAsync();
            }

            // Resize PTY to match our terminal
            await attachment.ResizeAsync(Console.WindowWidth, Console.WindowHeight);

            // Enter raw terminal mode for byte-level passthrough
            Console.TreatControlCAsInput = true;
            if (!RawTerminal.EnterRawMode())
            {
                Console.Error.WriteLine("Warning: Could not enter raw mode.");
            }

            try
            {
                using var cts = new CancellationTokenSource();

                var inputTask = Task.Run(() => ReadInputLoop(attachment, cts));
                var outputTask = WriteOutputAsync(attachment, logStream, cts.Token);

                await Task.WhenAny(inputTask, outputTask);
                await cts.CancelAsync();

                try { await inputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
                try { await outputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
            }
            finally
            {
                RawTerminal.RestoreMode();
                Console.TreatControlCAsInput = false;
                
                // No alternate screen to exit - just print detach message
            }

            Console.WriteLine();
            Console.WriteLine($"[detached from {sessionId}]");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine($"[detached from {sessionId}]");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (logStream != null)
            {
                await logStream.FlushAsync();
                await logStream.DisposeAsync();
            }
        }

        return 0;
    }

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern nint read(int fd, byte[] buf, nint count);

    private static void ReadInputLoop(ISessionAttachment attachment, CancellationTokenSource cts)
    {
        var buffer = new byte[256];
        bool ctrlB = false;

        while (!cts.Token.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = (int)read(0, buffer, (nint)buffer.Length);
            }
            catch
            {
                return;
            }

            if (bytesRead <= 0) continue;

            int sendStart = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                var b = buffer[i];

                if (ctrlB)
                {
                    ctrlB = false;
                    if (b == 'd' || b == 'D')
                    {
                        // Detach!
                        if (sendStart < i - 1)
                        {
                            attachment.SendInputAsync(buffer.AsMemory(sendStart, i - 1 - sendStart), cts.Token).AsTask().Wait();
                        }
                        attachment.DetachAsync(cts.Token).AsTask().Wait();
                        cts.Cancel();
                        return;
                    }
                    else
                    {
                        attachment.SendInputAsync(new byte[] { 0x02 }, cts.Token).AsTask().Wait();
                    }
                    continue;
                }

                if (b == 0x02) // Ctrl+B
                {
                    if (sendStart < i)
                    {
                        attachment.SendInputAsync(buffer.AsMemory(sendStart, i - sendStart), cts.Token).AsTask().Wait();
                    }
                    ctrlB = true;
                    sendStart = i + 1;
                    continue;
                }
            }

            if (sendStart < bytesRead && !ctrlB)
            {
                attachment.SendInputAsync(buffer.AsMemory(sendStart, bytesRead - sendStart), cts.Token).AsTask().Wait();
            }
        }
    }

    private static async Task WriteOutputAsync(ISessionAttachment attachment, FileStream? logStream, CancellationToken cancellationToken)
    {
        using var stdout = Console.OpenStandardOutput();

        await foreach (var data in attachment.ReadOutputAsync(cancellationToken))
        {
            await stdout.WriteAsync(data, cancellationToken);
            await stdout.FlushAsync(cancellationToken);

            if (logStream != null)
            {
                await logStream.WriteAsync(data, cancellationToken);
            }
        }
    }

    private static TimeSpan? ParseTimeout(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().ToLowerInvariant();

        if (value.EndsWith('s'))
        {
            if (int.TryParse(value[..^1], out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        else if (value.EndsWith('m'))
        {
            if (int.TryParse(value[..^1], out var minutes))
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }
        else if (value.EndsWith('h'))
        {
            if (int.TryParse(value[..^1], out var hours))
            {
                return TimeSpan.FromHours(hours);
            }
        }
        else if (int.TryParse(value, out var defaultMinutes))
        {
            return TimeSpan.FromMinutes(defaultMinutes);
        }

        Console.Error.WriteLine($"Warning: Invalid timeout format '{value}'. Use format like '10m', '1h', or '30s'.");
        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive [options]

            Start an inline terminal session that can be detached and resumed.
            Unlike 'attach', this runs inline in your current terminal (like bash or su).

            Options:
              -c, --command <cmd>  Command to run (default: $SHELL or /bin/sh)
              -l, --log <file>     Log all terminal output to file (raw bytes)
              --term <value>       Set TERM (default: xterm-256color for exotic terminals)
              --cwd <dir>          Working directory (default: current directory)
              -e, --env <K=V>      Set environment variable (can be repeated)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -t, --idle-timeout   Terminate session after idle time (e.g., 10m, 1h, 30s)
              -h, --help           Show this help message

            Keybindings:
              Ctrl+B, D            Detach from session (session continues running)

            Examples:
              termalive                          # Start default shell
              termalive -c python                # Start Python REPL
              termalive --command "npm run dev"  # Start dev server

            To reattach to a detached session:
              termalive list                     # Find session ID
              termalive attach <session-id>      # Reattach
            """);
    }
}
