// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Starts the session host daemon.
/// </summary>
internal static class StartCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        int port = 7777;
        string? pipeName = "termalive";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port" or "-p":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var p))
                    {
                        port = p;
                    }
                    break;
                case "--pipe":
                    if (i + 1 < args.Length)
                    {
                        pipeName = args[++i];
                    }
                    break;
                case "--no-pipe":
                    pipeName = null;
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        var options = new SessionHostOptions
        {
            WebSocketPort = port,
            PipeName = pipeName
        };

        Console.WriteLine($"Starting termalive host...");
        Console.WriteLine($"  WebSocket: ws://localhost:{port}/");
        if (pipeName != null)
        {
            Console.WriteLine($"  Named pipe: {pipeName}");
        }

        var host = new SessionHost(options);
        await host.StartAsync();

        Console.WriteLine("Host started. Press Ctrl+C to stop.");

        // Wait for shutdown signal
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }

        await host.DisposeAsync();
        Console.WriteLine("Host stopped.");

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive start [options]

            Start the session host daemon.

            Options:
              -p, --port <port>    WebSocket port (default: 7777)
              --pipe <name>        Named pipe name (default: termalive)
              --no-pipe            Disable named pipe server
              -h, --help           Show this help message

            Examples:
              termalive start
              termalive start --port 8080
              termalive start --no-pipe
            """);
    }
}
