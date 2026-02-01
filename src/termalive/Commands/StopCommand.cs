// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Termalive;

/// <summary>
/// Stops the termalive daemon.
/// </summary>
internal static class StopCommand
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

        return Task.FromResult(DaemonManager.StopDaemon());
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive stop

            Stop the termalive host daemon.

            Examples:
              termalive stop
            """);
    }
}
