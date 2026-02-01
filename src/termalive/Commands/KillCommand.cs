// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Terminates a session.
/// </summary>
internal static class KillCommand
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
        bool force = false;

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
                case "--force" or "-f":
                    force = true;
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        await using var client = await SessionClient.ConnectAsync(uri);
        await client.KillSessionAsync(sessionId, force);

        Console.WriteLine($"Session '{sessionId}' terminated.");

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive kill <session-id> [options]

            Terminate a session.

            Arguments:
              <session-id>         Session identifier to terminate

            Options:
              -f, --force          Force kill (SIGKILL instead of SIGTERM)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -h, --help           Show this help message

            Examples:
              termalive kill my-session
              termalive kill stuck-session --force
            """);
    }
}
