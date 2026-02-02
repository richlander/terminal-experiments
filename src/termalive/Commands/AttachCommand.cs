// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Attaches to an existing session.
/// </summary>
internal static class AttachCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("-"))
        {
            PrintHelp();
            return 1;
        }

        string sessionId = args[0];
        string uri = "pipe://termalive";
        string? logFile = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--uri" or "-u":
                    if (i + 1 < args.Length)
                    {
                        uri = args[++i];
                    }
                    break;
                case "--log" or "-l":
                    if (i + 1 < args.Length)
                    {
                        logFile = args[++i];
                    }
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        Console.WriteLine($"Connecting to {uri}...");

        await using var client = await SessionClient.ConnectAsync(uri);
        await using var attachment = await client.AttachAsync(sessionId, Console.WindowWidth, Console.WindowHeight);

        // Open log file if specified
        FileStream? logStream = null;
        if (logFile != null)
        {
            logStream = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        try
        {
            // Enter alternate screen buffer (like tmux/less) - gives us a clean isolated screen
            Console.Write("\x1b[?1049h");  // Enter alternate screen
            Console.Out.Flush();

            // Display the initial rendered screen content
            if (attachment.BufferedOutput.Length > 0)
            {
                using var stdout = Console.OpenStandardOutput();
                await stdout.WriteAsync(attachment.BufferedOutput);
                await stdout.FlushAsync();
            }

            // Resize PTY to match our terminal (in case it changed)
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

                // Start tasks for input and output
                var inputTask = Task.Run(() => ReadInputLoop(attachment, cts));
                var outputTask = WriteOutputAsync(attachment, logStream, cts.Token);

                // Wait for either to complete (detach or session exit)
                await Task.WhenAny(inputTask, outputTask);
                await cts.CancelAsync();

                // Wait for both to finish, ignoring cancellation
                try { await inputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
                try { await outputTask; } catch (OperationCanceledException) { } catch (AggregateException) { }
            }
            finally
            {
                RawTerminal.RestoreMode();
                Console.TreatControlCAsInput = false;
                
                // Exit alternate screen buffer
                Console.Write("\x1b[?1049l");
                Console.Out.Flush();
            }

            Console.WriteLine("[detached]");
        }
        catch (OperationCanceledException)
        {
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

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive attach <session-id> [options]

            Attach to an existing session.

            Arguments:
              <session-id>         Session identifier to attach to

            Options:
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -l, --log <file>     Log all terminal output to file (raw bytes)
              -h, --help           Show this help message

            Keybindings:
              Ctrl+B, D            Detach from session

            Examples:
              termalive attach my-session
              termalive attach dev --log /tmp/session.log
              termalive attach remote --uri ws://server:7777
            """);
    }
}
