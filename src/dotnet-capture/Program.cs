// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Terminal.Multiplexing;
using Microsoft.Extensions.Terminal.Parser;

// ============================================================================
// dotnet-capture: Run a command via PTY, parse VT output, extract screen text
//
// This tool demonstrates the VT parser by:
// 1. Spawning a command in a pseudo-terminal (PTY)
// 2. Capturing all VT escape sequences
// 3. Parsing them through ScreenBuffer
// 4. Extracting the final rendered screen as plain text
//
// Use cases:
// - CI testing of TUI apps (assert on screen content)
// - Documentation screenshots (text-based)
// - Accessibility (extract readable content from TUI apps)
// - Debugging VT output
// ============================================================================

const string Version = "0.1.0";

// Parse arguments
string? command = null;
List<string> commandArgs = [];
int columns = 80;
int rows = 24;
double durationSeconds = 2.0;
string? outputFile = null;
string? assertContains = null;
bool showSequences = false;
bool showHelp = false;

int i = 0;
while (i < args.Length)
{
    switch (args[i])
    {
        case "--help" or "-h":
            showHelp = true;
            i++;
            break;
        case "--version" or "-v":
            Console.WriteLine($"dotnet-capture {Version}");
            return 0;
        case "--duration" or "-d":
            if (i + 1 < args.Length && double.TryParse(args[i + 1].TrimEnd('s'), out var dur))
            {
                durationSeconds = dur;
                i += 2;
            }
            else
            {
                Console.Error.WriteLine("Error: --duration requires a number");
                return 1;
            }
            break;
        case "--output" or "-o":
            if (i + 1 < args.Length)
            {
                outputFile = args[i + 1];
                i += 2;
            }
            else
            {
                Console.Error.WriteLine("Error: --output requires a filename");
                return 1;
            }
            break;
        case "--size" or "-s":
            if (i + 1 < args.Length)
            {
                var parts = args[i + 1].Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var c) && int.TryParse(parts[1], out var r))
                {
                    columns = c;
                    rows = r;
                }
                i += 2;
            }
            else
            {
                Console.Error.WriteLine("Error: --size requires WxH format (e.g., 120x40)");
                return 1;
            }
            break;
        case "--assert":
            if (i + 1 < args.Length)
            {
                assertContains = args[i + 1];
                i += 2;
            }
            else
            {
                Console.Error.WriteLine("Error: --assert requires a string to search for");
                return 1;
            }
            break;
        case "--sequences":
            showSequences = true;
            i++;
            break;
        case "--":
            // Everything after -- is the command
            i++;
            if (i < args.Length)
            {
                command = args[i++];
                while (i < args.Length)
                {
                    commandArgs.Add(args[i++]);
                }
            }
            break;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
            // First non-option is the command
            command = args[i++];
            while (i < args.Length && !args[i].StartsWith('-'))
            {
                commandArgs.Add(args[i++]);
            }
            break;
    }
}

if (showHelp || command == null)
{
    PrintHelp();
    return showHelp ? 0 : 1;
}

// Create the screen buffer and parser
var screenBuffer = new ScreenBuffer(columns, rows);
var parser = new VtParser(screenBuffer);
var sequenceLog = new StringBuilder();

// Track sequences if requested
IParserHandler handler = showSequences 
    ? new LoggingHandler(screenBuffer, sequenceLog) 
    : screenBuffer;

var loggingParser = showSequences ? new VtParser(handler) : parser;

// Create PTY options
var ptyOptions = new PtyOptions
{
    Command = command,
    Arguments = commandArgs.ToArray(),
    Columns = columns,
    Rows = rows,
    Environment = new Dictionary<string, string>
    {
        ["TERM"] = "xterm-256color",
        ["COLORTERM"] = "truecolor",
        ["NO_COLOR"] = "" // Ensure colors are enabled
    }
};

// Remove NO_COLOR if it exists in environment (we want colors)
ptyOptions.Environment.Remove("NO_COLOR");

Console.Error.WriteLine($"Running: {command} {string.Join(' ', commandArgs)}");
Console.Error.WriteLine($"Size: {columns}x{rows}, Duration: {durationSeconds}s");
Console.Error.WriteLine();

try
{
    // Create and run PTY
    await using var pty = Pty.Create(ptyOptions);
    
    var buffer = new byte[8192];
    var stopwatch = Stopwatch.StartNew();
    var timeout = TimeSpan.FromSeconds(durationSeconds);
    
    using var cts = new CancellationTokenSource(timeout);
    
    try
    {
        while (!pty.HasExited && stopwatch.Elapsed < timeout)
        {
            try
            {
                var readTask = pty.ReadAsync(buffer, cts.Token);
                int bytesRead = await readTask;
                
                if (bytesRead == 0)
                    break;
                
                // Parse the VT output
                loggingParser.Parse(buffer.AsSpan(0, bytesRead));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected when duration expires
    }
    
    // Extract screen content
    var screenContent = ExtractScreenContent(screenBuffer);
    
    // Output results
    if (outputFile != null)
    {
        await File.WriteAllTextAsync(outputFile, screenContent);
        Console.Error.WriteLine($"Screen content written to: {outputFile}");
    }
    else
    {
        Console.WriteLine(screenContent);
    }
    
    // Show sequences if requested
    if (showSequences && sequenceLog.Length > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== VT Sequences Parsed ===");
        Console.Error.WriteLine(sequenceLog.ToString());
    }
    
    // Assert if requested
    if (assertContains != null)
    {
        if (screenContent.Contains(assertContains, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"✓ Assertion passed: screen contains \"{assertContains}\"");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"✗ Assertion failed: screen does not contain \"{assertContains}\"");
            return 1;
        }
    }
    
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static string ExtractScreenContent(ScreenBuffer buffer)
{
    var sb = new StringBuilder();
    
    for (int row = 0; row < buffer.Height; row++)
    {
        var line = new StringBuilder();
        int lastNonSpace = -1;
        
        for (int col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(col, row);
            char c = cell.Character;
            
            // Handle null/empty as space
            if (c == '\0')
                c = ' ';
            
            line.Append(c);
            
            if (c != ' ')
                lastNonSpace = line.Length;
        }
        
        // Trim trailing spaces but keep the line
        string lineText = lastNonSpace >= 0 ? line.ToString(0, lastNonSpace) : "";
        sb.AppendLine(lineText);
    }
    
    // Trim trailing empty lines
    var result = sb.ToString().TrimEnd('\r', '\n');
    return result + Environment.NewLine;
}

static void PrintHelp()
{
    Console.WriteLine("""
        dotnet-capture - Capture and parse terminal output from TUI apps
        
        Usage: dotnet-capture [options] <command> [args...]
               dotnet-capture [options] -- <command> [args...]
        
        Options:
          -d, --duration <sec>   Capture duration in seconds (default: 2)
          -s, --size <WxH>       Terminal size (default: 80x24)
          -o, --output <file>    Write screen content to file
          --assert <text>        Assert screen contains text (exit 1 if not)
          --sequences            Show parsed VT sequences (debug)
          -h, --help             Show this help
          -v, --version          Show version
        
        Examples:
          # Capture 2 seconds of dashboard output
          dotnet-capture dotnet-dashboard --duration 2
        
          # Save to file with larger terminal
          dotnet-capture -s 120x40 -o screen.txt dotnet-dashboard
        
          # CI assertion - verify SDK version appears
          dotnet-capture --assert "SDK:" --duration 3 dotnet-dashboard
        
          # Debug mode - show what VT sequences were parsed
          dotnet-capture --sequences -- htop -d 1
        
        The tool runs the command in a pseudo-terminal (PTY), captures all
        VT escape sequences, parses them through the terminal emulator's
        screen buffer, and extracts the final rendered screen as plain text.
        """);
}

/// <summary>
/// Parser handler that logs sequences while delegating to ScreenBuffer.
/// </summary>
sealed class LoggingHandler : IParserHandler
{
    private readonly ScreenBuffer _buffer;
    private readonly StringBuilder _log;
    
    public LoggingHandler(ScreenBuffer buffer, StringBuilder log)
    {
        _buffer = buffer;
        _log = log;
    }
    
    public void Print(char c)
    {
        _buffer.Print(c);
    }
    
    public void Execute(byte controlCode)
    {
        _log.AppendLine($"Execute: 0x{controlCode:X2} ({ControlCodeName(controlCode)})");
        _buffer.Execute(controlCode);
    }
    
    public void EscDispatch(byte intermediates, char command)
    {
        _log.AppendLine($"ESC: intermediates=0x{intermediates:X2} command='{command}'");
        _buffer.EscDispatch(intermediates, command);
    }
    
    public void CsiDispatch(ReadOnlySpan<int> parameters, byte privateMarker, byte intermediates, char command)
    {
        _log.AppendLine($"CSI: params=[{string.Join(";", parameters.ToArray())}] marker={(privateMarker == 0 ? "none" : $"'{(char)privateMarker}'")} command='{command}'");
        _buffer.CsiDispatch(parameters, privateMarker, intermediates, command);
    }
    
    public void OscDispatch(int command, ReadOnlySpan<byte> data)
    {
        _log.AppendLine($"OSC: command={command} data=\"{Encoding.UTF8.GetString(data)}\"");
        _buffer.OscDispatch(command, data);
    }
    
    public void DcsHook(ReadOnlySpan<int> parameters, byte intermediates, char command)
    {
        _log.AppendLine($"DCS Hook: params=[{string.Join(";", parameters.ToArray())}] command='{command}'");
        _buffer.DcsHook(parameters, intermediates, command);
    }
    
    public void DcsPut(byte b)
    {
        _buffer.DcsPut(b);
    }
    
    public void DcsUnhook()
    {
        _log.AppendLine("DCS Unhook");
        _buffer.DcsUnhook();
    }
    
    private static string ControlCodeName(byte code) => code switch
    {
        0x07 => "BEL",
        0x08 => "BS",
        0x09 => "HT",
        0x0A => "LF",
        0x0B => "VT",
        0x0C => "FF",
        0x0D => "CR",
        0x1B => "ESC",
        _ => $"C0:{code}"
    };
    
    private static string FormatBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return "";
        return string.Join(",", bytes.ToArray().Select(b => $"'{(char)b}'"));
    }
}
