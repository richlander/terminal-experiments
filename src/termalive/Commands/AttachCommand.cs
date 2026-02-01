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
        await using var attachment = await client.AttachAsync(sessionId);

        // Open log file if specified
        FileStream? logStream = null;
        if (logFile != null)
        {
            logStream = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        try
        {
            Console.WriteLine($"Attached to session: {attachment.SessionInfo.Id}");
            Console.WriteLine($"Press Ctrl+B, D to detach.");
            if (logFile != null)
            {
                Console.WriteLine($"Logging to: {logFile}");
            }
            Console.WriteLine();

            // Write buffered output
            if (attachment.BufferedOutput.Length > 0)
            {
                using var stdout = Console.OpenStandardOutput();
                await stdout.WriteAsync(attachment.BufferedOutput);
                await stdout.FlushAsync();

                // Also log buffered output
                if (logStream != null)
                {
                    await logStream.WriteAsync(attachment.BufferedOutput);
                }
            }

            // Resize to match terminal before entering raw mode
            await attachment.ResizeAsync(Console.WindowWidth, Console.WindowHeight);

            // Enter raw terminal mode for proper PTY passthrough
            if (!RawTerminal.EnterRawMode())
            {
                Console.Error.WriteLine("Warning: Could not enter raw mode. Some features may not work correctly.");
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

                // Wait for both to finish
                try { await inputTask; } catch (OperationCanceledException) { }
                try { await outputTask; } catch (OperationCanceledException) { }
            }
            finally
            {
                RawTerminal.RestoreMode();
            }

            Console.WriteLine();
            Console.WriteLine("Detached.");
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

    private static void ReadInputLoop(ISessionAttachment attachment, CancellationTokenSource cts)
    {
        // Use blocking reads on a thread - more reliable with raw mode
        using var stdin = Console.OpenStandardInput();
        var buffer = new byte[256];
        bool ctrlB = false;

        while (!cts.Token.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = stdin.Read(buffer, 0, buffer.Length);
            }
            catch
            {
                return;
            }

            if (bytesRead <= 0)
            {
                continue;
            }

            // Process input byte by byte for detach detection
            int sendStart = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                var b = buffer[i];

                if (ctrlB)
                {
                    ctrlB = false;
                    if (b == 'd' || b == 'D')
                    {
                        // Detach - send any pending bytes first
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
                        // Send Ctrl+B that we were holding, then continue
                        attachment.SendInputAsync(new byte[] { 0x02 }, cts.Token).AsTask().Wait();
                        // Current byte will be sent with the batch
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
