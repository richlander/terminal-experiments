// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Starts a detached session (like docker run -d).
/// </summary>
/// <remarks>
/// Design: This creates a session in the background and returns immediately.
/// Use 'termalive attach' to connect to it later.
/// 
/// Inspired by: docker run -d
/// </remarks>
internal static class RunCommand
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
        string? termOverride = null;
        var environment = new Dictionary<string, string>();
        bool attach = false;

        // Check for -- separator for command
        int dashDashIndex = Array.IndexOf(args, "--");
        if (dashDashIndex > 0 && dashDashIndex < args.Length - 1)
        {
            // Everything after -- is the command
            command = string.Join(" ", args[(dashDashIndex + 1)..]);
        }

        for (int i = 1; i < args.Length && (dashDashIndex < 0 || i < dashDashIndex); i++)
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
                case "-a" or "--attach":
                    attach = true;
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

        // Ensure TERM is set
        if (!environment.ContainsKey("TERM"))
        {
            if (termOverride != null)
            {
                environment["TERM"] = termOverride;
            }
            else
            {
                var parentTerm = Environment.GetEnvironmentVariable("TERM");
                var safeTerm = parentTerm switch
                {
                    "xterm" or "xterm-256color" or "xterm-color" => parentTerm,
                    "screen" or "screen-256color" => parentTerm,
                    "tmux" or "tmux-256color" => parentTerm,
                    "linux" or "vt100" or "vt220" => parentTerm,
                    _ => "xterm-256color"
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

        if (attach)
        {
            // If -a/--attach specified, attach immediately (like 'termalive new' used to do)
            return await AttachCommand.RunAsync([sessionId, "--uri", uri]);
        }

        // Default: just print session info and exit (detached mode)
        Console.WriteLine($"Started session: {session.Id}");
        Console.WriteLine($"  Command: {session.Command}");
        Console.WriteLine($"  Working Directory: {session.WorkingDirectory}");
        Console.WriteLine($"  Size: {session.Columns}x{session.Rows}");
        Console.WriteLine();
        Console.WriteLine($"To attach: termalive attach {session.Id}");
        Console.WriteLine($"To view logs: termalive logs -f {session.Id}");
        
        return 0;
    }

    private static TimeSpan? ParseTimeout(string value)
    {
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
            return TimeSpan.FromMinutes(defaultMinutes);
        }

        Console.Error.WriteLine($"Warning: Invalid timeout format '{value}'. Use format like '10m', '1h', or '30s'.");
        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive run <session-id> [options] [-- command]

            Start a detached terminal session (like docker run -d).
            The session runs in the background; use 'attach' to connect later.

            Arguments:
              <session-id>         Unique identifier for the session

            Options:
              -c, --command <cmd>  Command to run (default: $SHELL or /bin/sh)
              -a, --attach         Attach immediately after creating (like old 'new' behavior)
              --term <value>       Set TERM (default: xterm-256color for exotic terminals)
              --cwd <dir>          Working directory (default: current directory)
              -e, --env <K=V>      Set environment variable (can be repeated)
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -t, --idle-timeout   Terminate session after idle time (e.g., 10m, 1h, 30s)
              -h, --help           Show this help message

            Examples:
              termalive run my-session                    # Start shell detached
              termalive run build -- npm run build        # Run npm build detached
              termalive run claude -- claude              # Start claude CLI detached
              termalive run dev -a -- npm run dev         # Start and attach immediately
              termalive run worker -t 30m                 # Auto-terminate after 30m idle
            """);
    }
}
