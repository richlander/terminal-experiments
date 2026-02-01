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
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        Console.WriteLine($"Connecting to {uri}...");

        await using var client = await SessionClient.ConnectAsync(uri);
        await using var attachment = await client.AttachAsync(sessionId);

        Console.WriteLine($"Attached to session: {attachment.SessionInfo.Id}");
        Console.WriteLine($"Press Ctrl+B, D to detach.");
        Console.WriteLine();

        // Write buffered output
        if (attachment.BufferedOutput.Length > 0)
        {
            Console.Write(Encoding.UTF8.GetString(attachment.BufferedOutput.Span));
        }

        // Set terminal to raw mode
        var originalMode = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;

        try
        {
            // Resize to match terminal
            await attachment.ResizeAsync(Console.WindowWidth, Console.WindowHeight);

            using var cts = new CancellationTokenSource();

            // Start tasks for input and output
            var inputTask = ReadInputAsync(attachment, cts.Token);
            var outputTask = WriteOutputAsync(attachment, cts.Token);

            // Wait for either to complete (detach or session exit)
            await Task.WhenAny(inputTask, outputTask);
            await cts.CancelAsync();

            // Wait for both to finish
            try { await inputTask; } catch (OperationCanceledException) { }
            try { await outputTask; } catch (OperationCanceledException) { }
        }
        finally
        {
            Console.TreatControlCAsInput = originalMode;
        }

        Console.WriteLine();
        Console.WriteLine("Detached.");

        return 0;
    }

    private static async Task ReadInputAsync(ISessionAttachment attachment, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        bool ctrlB = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(10, cancellationToken);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            // Check for detach sequence: Ctrl+B, D
            if (ctrlB)
            {
                ctrlB = false;
                if (key.Key == ConsoleKey.D)
                {
                    // Detach
                    await attachment.DetachAsync(cancellationToken);
                    return;
                }
                else
                {
                    // Send Ctrl+B followed by this key
                    await attachment.SendInputAsync(new byte[] { 0x02 }, cancellationToken);
                }
            }

            if (key.Key == ConsoleKey.B && key.Modifiers == ConsoleModifiers.Control)
            {
                ctrlB = true;
                continue;
            }

            // Convert key to bytes
            byte[] bytes;
            if (key.KeyChar != '\0')
            {
                bytes = Encoding.UTF8.GetBytes(new[] { key.KeyChar });
            }
            else
            {
                // Handle special keys
                bytes = key.Key switch
                {
                    ConsoleKey.UpArrow => "\x1b[A"u8.ToArray(),
                    ConsoleKey.DownArrow => "\x1b[B"u8.ToArray(),
                    ConsoleKey.RightArrow => "\x1b[C"u8.ToArray(),
                    ConsoleKey.LeftArrow => "\x1b[D"u8.ToArray(),
                    ConsoleKey.Home => "\x1b[H"u8.ToArray(),
                    ConsoleKey.End => "\x1b[F"u8.ToArray(),
                    ConsoleKey.Insert => "\x1b[2~"u8.ToArray(),
                    ConsoleKey.Delete => "\x1b[3~"u8.ToArray(),
                    ConsoleKey.PageUp => "\x1b[5~"u8.ToArray(),
                    ConsoleKey.PageDown => "\x1b[6~"u8.ToArray(),
                    ConsoleKey.F1 => "\x1bOP"u8.ToArray(),
                    ConsoleKey.F2 => "\x1bOQ"u8.ToArray(),
                    ConsoleKey.F3 => "\x1bOR"u8.ToArray(),
                    ConsoleKey.F4 => "\x1bOS"u8.ToArray(),
                    ConsoleKey.F5 => "\x1b[15~"u8.ToArray(),
                    ConsoleKey.F6 => "\x1b[17~"u8.ToArray(),
                    ConsoleKey.F7 => "\x1b[18~"u8.ToArray(),
                    ConsoleKey.F8 => "\x1b[19~"u8.ToArray(),
                    ConsoleKey.F9 => "\x1b[20~"u8.ToArray(),
                    ConsoleKey.F10 => "\x1b[21~"u8.ToArray(),
                    ConsoleKey.F11 => "\x1b[23~"u8.ToArray(),
                    ConsoleKey.F12 => "\x1b[24~"u8.ToArray(),
                    _ => []
                };
            }

            if (bytes.Length > 0)
            {
                await attachment.SendInputAsync(bytes, cancellationToken);
            }
        }
    }

    private static async Task WriteOutputAsync(ISessionAttachment attachment, CancellationToken cancellationToken)
    {
        await foreach (var data in attachment.ReadOutputAsync(cancellationToken))
        {
            Console.Write(Encoding.UTF8.GetString(data.Span));
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
              -h, --help           Show this help message

            Keybindings:
              Ctrl+B, D            Detach from session

            Examples:
              termalive attach my-session
              termalive a dev
              termalive attach remote --uri ws://server:7777
            """);
    }
}
