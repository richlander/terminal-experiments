// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Lists active sessions.
/// </summary>
internal static class ListCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string uri = "pipe://termalive";
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--uri" or "-u":
                    if (i + 1 < args.Length)
                    {
                        uri = args[++i];
                    }
                    break;
                case "--verbose" or "-v":
                    verbose = true;
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        await using var client = await SessionClient.ConnectAsync(uri);
        var sessions = await client.ListSessionsAsync();

        if (sessions.Count == 0)
        {
            Console.WriteLine("No active sessions.");
            return 0;
        }

        // Print header
        Console.WriteLine($"{"ID",-20} {"STATE",-10} {"COMMAND",-20} {"CREATED",-20}");
        Console.WriteLine(new string('-', 72));

        foreach (var session in sessions)
        {
            var created = FormatTimeAgo(session.Created);
            var state = session.State.ToString().ToLowerInvariant();

            Console.WriteLine($"{Truncate(session.Id, 20),-20} {state,-10} {Truncate(session.Command, 20),-20} {created,-20}");

            if (verbose)
            {
                Console.WriteLine($"  Working Directory: {session.WorkingDirectory ?? "(not set)"}");
                Console.WriteLine($"  Size: {session.Columns}x{session.Rows}");
                if (session.ExitCode.HasValue)
                {
                    Console.WriteLine($"  Exit Code: {session.ExitCode}");
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {sessions.Count} session(s)");

        return 0;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }
        return value[..(maxLength - 3)] + "...";
    }

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var elapsed = DateTimeOffset.UtcNow - time;

        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }
        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }
        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours} hour(s) ago";
        }
        if (elapsed.TotalDays < 7)
        {
            return $"{(int)elapsed.TotalDays} day(s) ago";
        }

        return time.LocalDateTime.ToString("yyyy-MM-dd");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive list [options]

            List all active sessions.

            Options:
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -v, --verbose        Show detailed information
              -h, --help           Show this help message

            Examples:
              termalive list
              termalive ls -v
              termalive list --uri ws://server:7777
            """);
    }
}
