// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                case "--cwd" or "-d":
                    if (i + 1 < args.Length)
                    {
                        workingDirectory = args[++i];
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
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        // Default to user's shell
        command ??= Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";

        var options = new PtyOptions
        {
            Command = command,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            Environment = environment.Count > 0 ? environment : null,
            Columns = Console.WindowWidth,
            Rows = Console.WindowHeight
        };

        Console.WriteLine($"Connecting to {uri}...");

        await using var client = await SessionClient.ConnectAsync(uri);
        var session = await client.CreateSessionAsync(sessionId, options);

        Console.WriteLine($"Created session: {session.Id}");
        Console.WriteLine($"  Command: {session.Command}");
        Console.WriteLine($"  Working Directory: {session.WorkingDirectory}");
        Console.WriteLine($"  Size: {session.Columns}x{session.Rows}");

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive new <session-id> [options]

            Create a new terminal session.

            Arguments:
              <session-id>         Unique identifier for the session

            Options:
              -c, --command <cmd>  Command to run (default: $SHELL or /bin/sh)
              -d, --cwd <dir>      Working directory (default: current directory)
              -e, --env <K=V>      Set environment variable (can be repeated)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -h, --help           Show this help message

            Examples:
              termalive new my-session
              termalive new dev --command "bash" --cwd ~/projects
              termalive new work --env "DEBUG=1" --env "NODE_ENV=development"
              termalive new remote --uri ws://server:7777
            """);
    }
}
