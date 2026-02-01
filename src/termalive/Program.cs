// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// termalive - Terminal session multiplexer
/// </summary>
public static class Program
{
    private const string Version = "1.0.0";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "start" => await StartCommand.RunAsync(args[1..]),
                "new" => await NewCommand.RunAsync(args[1..]),
                "list" or "ls" => await ListCommand.RunAsync(args[1..]),
                "attach" or "a" => await AttachCommand.RunAsync(args[1..]),
                "logs" => await LogsCommand.RunAsync(args[1..]),
                "send" => await SendCommand.RunAsync(args[1..]),
                "kill" => await KillCommand.RunAsync(args[1..]),
                "help" or "-h" or "--help" => PrintUsage(),
                "version" or "-v" or "--version" => PrintVersion(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            termalive - Terminal session multiplexer

            Usage: termalive <command> [options]

            Commands:
              start              Start the session host daemon
              new <id>           Create a new session
              list, ls           List active sessions
              attach, a <id>     Attach to a session (interactive)
              logs <id>          Read/stream session output (JSONL)
              send <id> <text>   Send input to a session without attaching
              kill <id>          Terminate a session
              help               Show this help message
              version            Show version information

            Examples:
              termalive start --port 7777
              termalive new my-session --command bash
              termalive list
              termalive attach my-session
              termalive logs worker --follow --wait-idle 5s
              termalive send my-session "echo hello"
              termalive kill my-session

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
