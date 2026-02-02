// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// termalive - Terminal session orchestration
/// </summary>
/// <remarks>
/// Design philosophy:
/// - 'termalive' alone starts an inline attached session (like bash in bash)
/// - 'termalive run' starts a detached session (like docker run -d)
/// - 'termalive attach' reconnects to a session (like tmux attach)
/// 
/// See docs/termalive-design.md for full design rationale.
/// </remarks>
public static class Program
{
    private static string Version => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static async Task<int> Main(string[] args)
    {
        // No arguments or first arg is an option = inline mode
        if (args.Length == 0 || (args[0].StartsWith("-") && args[0] != "-h" && args[0] != "--help"))
        {
            return await InlineCommand.RunAsync(args);
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                // Session management
                "run" => await RunCommand.RunAsync(args[1..]),
                "new" => await NewCommand.RunAsync(args[1..]),  // Legacy, kept for compatibility
                "attach" or "a" => await AttachCommand.RunAsync(args[1..]),
                "list" or "ls" => await ListCommand.RunAsync(args[1..]),
                "logs" => await LogsCommand.RunAsync(args[1..]),
                "send" => await SendCommand.RunAsync(args[1..]),
                "kill" => await KillCommand.RunAsync(args[1..]),
                
                // Daemon management
                "start" => await StartCommand.RunAsync(args[1..]),
                "status" => await StatusCommand.RunAsync(args[1..]),
                "stop" => await StopCommand.RunAsync(args[1..]),
                
                // Help
                "help" or "-h" or "--help" => PrintUsage(),
                "version" or "-v" or "--version" => PrintVersion(),
                
                _ => UnknownCommand(command)
            };
        }
        catch (TimeoutException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Hint: Run 'termalive start' to start the daemon.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine($"""
            termalive - Terminal session orchestration
            
            Usage: termalive [command] [options]

            Quick Start:
              termalive                    Start an inline session (like bash in bash)
              termalive run <id>           Start a detached session (like docker run -d)
              termalive attach <id>        Attach to a session (like tmux attach)

            Session Commands:
              run <id> [-- cmd]    Start a detached session
              attach, a <id>       Attach to a session (interactive, full screen)
              list, ls             List active sessions
              logs <id>            View session output (use -f to follow)
              send <id> <text>     Send input to a session without attaching
              kill <id>            Terminate a session

            Daemon Commands:
              start                Start the session host daemon
              status               Show daemon status  
              stop                 Stop the daemon

            Other:
              help                 Show this help message
              version              Show version information

            Examples:
              termalive                              # Start inline shell session
              termalive run claude -- claude         # Start claude detached
              termalive attach claude                # Attach to claude session
              termalive logs -f claude               # Stream claude output
              termalive run build -- npm run build   # Run build in background

            Run 'termalive <command> --help' for more information on a command.
            """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine($"termalive version {Version}");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'termalive help' for usage information.");
        return 1;
    }
}
