// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Terminal.Multiplexing;

namespace Termalive;

/// <summary>
/// Sends input to a session without attaching.
/// </summary>
internal static class SendCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            PrintHelp();
            return 1;
        }

        string sessionId = args[0];
        string uri = "pipe://termalive";
        bool appendNewline = true;
        var textParts = new List<string>();

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
                case "--no-newline" or "-n":
                    appendNewline = false;
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
                default:
                    if (!args[i].StartsWith("-"))
                    {
                        textParts.Add(args[i]);
                    }
                    break;
            }
        }

        if (textParts.Count == 0)
        {
            Console.Error.WriteLine("Error: No text to send.");
            return 1;
        }

        var text = string.Join(" ", textParts);
        if (appendNewline)
        {
            text += "\n";
        }

        await using var client = await SessionClient.ConnectAsync(uri);
        await using var attachment = await client.AttachAsync(sessionId);

        await attachment.SendInputAsync(Encoding.UTF8.GetBytes(text));

        Console.WriteLine($"Sent {text.Length} bytes to session '{sessionId}'");

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: termalive send <session-id> <text> [options]

            Send input to a session without attaching.

            Arguments:
              <session-id>         Session identifier
              <text>               Text to send

            Options:
              -n, --no-newline     Don't append newline
              -u, --uri <uri>      Host URI (default: pipe://termalive)
              -h, --help           Show this help message

            Examples:
              termalive send my-session "echo hello"
              termalive send dev "npm run build"
              termalive send work "exit" -n
            """);
    }
}
