// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Termalive;

/// <summary>
/// Shows the status of the termalive daemon.
/// </summary>
internal static class StatusCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--help" or "-h")
            {
                PrintHelp();
                return Task.FromResult(0);
            }
        }

        var (running, pid, port) = DaemonManager.GetStatus();

        if (!running)
        {
            Console.WriteLine("termalive host is not running");
            return Task.FromResult(1);
        }

        Console.WriteLine($"termalive host running (pid {pid})");
        if (port.HasValue)
        {
            Console.WriteLine($"  WebSocket: ws://localhost:{port}/");
        }

        return Task.FromResult(0);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive status

            Show the status of the termalive host daemon.

            Examples:
              termalive status
            """);
    }
}
