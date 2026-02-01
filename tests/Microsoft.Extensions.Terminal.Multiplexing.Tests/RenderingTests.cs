// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for PTY rendering and escape sequence handling.
/// These tests verify that terminal output (colors, cursor movement, etc.)
/// is correctly captured and passed through.
/// </summary>
[Trait("Category", "Rendering")]
public class RenderingTests
{
    private static bool CanRunPtyTests =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [SkippableFact]
    public async Task CapturesAnsiColorCodes()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[31mRed\\033[0m'"],
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        // Should contain ANSI red color code
        Assert.Contains("\x1b[31m", output);
        Assert.Contains("Red", output);
        Assert.Contains("\x1b[0m", output);
    }

    [SkippableFact]
    public async Task CapturesCursorMovement()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[5;10HHello'"], // Move to row 5, col 10
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        // Should contain cursor position escape sequence
        Assert.Contains("\x1b[5;10H", output);
        Assert.Contains("Hello", output);
    }

    [SkippableFact]
    public async Task CapturesClearScreen()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[2J'"], // Clear screen
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        // Should contain clear screen escape sequence
        Assert.Contains("\x1b[2J", output);
    }

    [SkippableFact]
    public async Task CapturesBoldAndStyles()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[1mBold\\033[0m \\033[4mUnderline\\033[0m'"],
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("\x1b[1m", output); // Bold
        Assert.Contains("Bold", output);
        Assert.Contains("\x1b[4m", output); // Underline
        Assert.Contains("Underline", output);
    }

    [SkippableFact]
    public async Task Captures256Colors()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[38;5;196mBright Red\\033[0m'"], // 256-color red
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("\x1b[38;5;196m", output);
        Assert.Contains("Bright Red", output);
    }

    [SkippableFact]
    public async Task CapturesTrueColor()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[38;2;255;100;50mTrueColor\\033[0m'"], // RGB color
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("\x1b[38;2;255;100;50m", output);
        Assert.Contains("TrueColor", output);
    }

    [SkippableFact]
    public async Task CapturesAlternateScreenBuffer()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[?1049hAlternate\\033[?1049l'"], // Enter/exit alternate buffer
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("\x1b[?1049h", output); // Enter alternate
        Assert.Contains("Alternate", output);
        Assert.Contains("\x1b[?1049l", output); // Exit alternate
    }

    [SkippableFact]
    public async Task CapturesUnicodeCharacters()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '→ ● ✓ ★'"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["LANG"] = "en_US.UTF-8" }
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("→", output);
        Assert.Contains("●", output);
        Assert.Contains("✓", output);
        Assert.Contains("★", output);
    }

    [SkippableFact]
    public async Task CapturesBoxDrawingCharacters()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '┌─┐\\n│ │\\n└─┘'"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["LANG"] = "en_US.UTF-8" }
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("┌", output);
        Assert.Contains("─", output);
        Assert.Contains("┐", output);
        Assert.Contains("│", output);
        Assert.Contains("└", output);
        Assert.Contains("┘", output);
    }

    [SkippableFact]
    public async Task PreservesNewlinesAndCarriageReturns()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf 'Line1\\r\\nLine2\\nLine3'"],
            Columns = 80,
            Rows = 24
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("Line1", output);
        Assert.Contains("Line2", output);
        Assert.Contains("Line3", output);
    }

    [SkippableFact]
    public async Task TermEnvironmentIsSet()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo $TERM"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        var (output, _) = await RunPtyToCompletion(options);

        Assert.Contains("xterm-256color", output);
    }

    [SkippableFact]
    public async Task WindowSizeIsRespected()
    {
        Skip.IfNot(CanRunPtyTests, "PTY tests only run on Unix");

        var options = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "stty size"],
            Columns = 120,
            Rows = 40
        };

        var (output, _) = await RunPtyToCompletion(options);

        // stty size outputs "rows cols"
        Assert.Contains("40 120", output);
    }

    private static async Task<(string Output, int ExitCode)> RunPtyToCompletion(PtyOptions options, int timeoutMs = 5000)
    {
        using var pty = Pty.Create(options);
        var output = new StringBuilder();
        var buffer = new byte[4096];

        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            while (!pty.HasExited)
            {
                var bytesRead = await pty.ReadAsync(buffer, cts.Token);
                if (bytesRead > 0)
                {
                    output.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }

                // Small delay to prevent tight loop
                await Task.Delay(10, cts.Token);
            }

            // Read any remaining output
            while (true)
            {
                try
                {
                    var bytesRead = await pty.ReadAsync(buffer, cts.Token);
                    if (bytesRead <= 0) break;
                    output.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
                catch
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout - return what we have
        }

        var exitCode = await pty.WaitForExitAsync(CancellationToken.None);
        return (output.ToString(), exitCode);
    }
}
