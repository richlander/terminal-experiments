// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Creates a new session.
/// </summary>
internal static class NewCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("-"))
        {
            PrintHelp();
            return 1;
        }

        string sessionId = args[0];
        string? command = null;
        string? workingDirectory = null;
        string uri = "pipe://termalive";
        TimeSpan? idleTimeout = null;
        bool detach = false;
        string? logFile = null;
        string? termOverride = null;
        var environment = new Dictionary<string, string>();

        for (int i = 1; i < args.Length; i++)
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
                case "-d" or "--detach":
                    detach = true;
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
        // Many exotic terminals (ghostty, kitty, alacritty) set custom TERM values
        // that may not be in the terminfo database on remote/container systems
        if (!environment.ContainsKey("TERM"))
        {
            if (termOverride != null)
            {
                // Explicit override
                environment["TERM"] = termOverride;
            }
            else
            {
                var parentTerm = Environment.GetEnvironmentVariable("TERM");

                // Use parent TERM only if it's a common/safe value
                var safeTerm = parentTerm switch
                {
                    "xterm" or "xterm-256color" or "xterm-color" => parentTerm,
                    "screen" or "screen-256color" => parentTerm,
                    "tmux" or "tmux-256color" => parentTerm,
                    "linux" or "vt100" or "vt220" => parentTerm,
                    _ => "xterm-256color" // Safe fallback for exotic terminals
                };

                environment["TERM"] = safeTerm;
            }
        }

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

        if (detach)
        {
            // Just print session info and exit
            Console.WriteLine($"Created session: {session.Id}");
            Console.WriteLine($"  Command: {session.Command}");
            Console.WriteLine($"  Working Directory: {session.WorkingDirectory}");
            Console.WriteLine($"  Size: {session.Columns}x{session.Rows}");
            return 0;
        }

        // Auto-attach to the new session (like tmux)
        await using var attachment = await client.AttachAsync(sessionId);

        // Open log file if specified
        FileStream? logStream = null;
        if (logFile != null)
        {
            logStream = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            Console.Error.WriteLine($"Logging to: {logFile}");
        }

        try
        {
            // Enter alternate screen buffer (like tmux/less) - gives us a clean isolated screen
            Console.Write("\x1b[?1049h");  // Enter alternate screen
            Console.Write("\x1b[H");        // Home cursor
            Console.Out.Flush();

            // Resize PTY to match our terminal
            await attachment.ResizeAsync(Console.WindowWidth, Console.WindowHeight);

            // Enter raw terminal mode for byte-level passthrough
            Console.TreatControlCAsInput = true;
            if (!RawTerminal.EnterRawMode())
            {
                Console.Error.WriteLine("Warning: Could not enter raw mode.");
            }

            // Send Ctrl+L to trigger shell redraw
            await attachment.SendInputAsync(new byte[] { 0x0C }, CancellationToken.None);

            try
            {
                using var cts = new CancellationTokenSource();

                var inputTask = Task.Run(() => ReadInputLoop(attachment, cts));
                var outputTask = WriteOutputAsync(attachment, logStream, cts.Token);

                await Task.WhenAny(inputTask, outputTask);
                await cts.CancelAsync();

                // Wait for tasks, ignoring cancellation
                try { await inputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
                try { await outputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
            }
            finally
            {
                RawTerminal.RestoreMode();
                Console.TreatControlCAsInput = false;
                
                // Exit alternate screen buffer - restores original terminal content
                Console.Write("\x1b[?1049l");
                Console.Out.Flush();
            }

            Console.WriteLine("[detached]");
        }
        catch (OperationCanceledException)
        {
            // Normal detach - still need to exit alternate screen
            Console.Write("\x1b[?1049l");
            Console.Out.Flush();
            Console.WriteLine("[detached]");
        }
        catch (Exception ex)
        {
            Console.Write("\x1b[?1049l");
            Console.Out.Flush();
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
        // Read raw bytes from fd 0 directly (bypass .NET's stream buffering)
        var buffer = new byte[256];
        bool ctrlB = false;

        while (!cts.Token.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                // Direct syscall to read from stdin
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
                        // Not D, send the Ctrl+B we were holding
                        attachment.SendInputAsync(new byte[] { 0x02 }, cts.Token).AsTask().Wait();
                    }
                    continue;
                }

                if (b == 0x02) // Ctrl+B
                {
                    // Send any pending bytes before the Ctrl+B
                    if (sendStart < i)
                    {
                        attachment.SendInputAsync(buffer.AsMemory(sendStart, i - sendStart), cts.Token).AsTask().Wait();
                    }
                    ctrlB = true;
                    sendStart = i + 1;
                    continue;
                }
            }

            // Send remaining bytes
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

            // Also write to log file if specified
            if (logStream != null)
            {
                await logStream.WriteAsync(data, cancellationToken);
            }
        }
    }

    private static TimeSpan? ParseTimeout(string value)
    {
        // Support formats: "10m", "1h", "30s", "5" (minutes)
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
            // Default to minutes if no suffix
            return TimeSpan.FromMinutes(defaultMinutes);
        }

        Console.Error.WriteLine($"Warning: Invalid timeout format '{value}'. Use format like '10m', '1h', or '30s'.");
        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive new <session-id> [options]

            Create a new terminal session and attach to it.

            Arguments:
              <session-id>         Unique identifier for the session

            Options:
              -c, --command <cmd>  Command to run (default: $SHELL or /bin/sh)
              -d, --detach         Create session without attaching
              -l, --log <file>     Log all terminal output to file (raw bytes)
              --term <value>       Set TERM (default: xterm-256color for exotic terminals)
              --cwd <dir>          Working directory (default: current directory)
              -e, --env <K=V>      Set environment variable (can be repeated)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -t, --idle-timeout   Terminate session after idle time (e.g., 10m, 1h, 30s)
              -h, --help           Show this help message

            Examples:
              termalive new my-session                       # Create and attach
              termalive new dev -d                           # Create detached
              termalive new claude-test --command claude --log /tmp/claude.log
              termalive new work --command "bash" --cwd ~/projects
            """);
    }
}
