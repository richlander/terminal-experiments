// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Integration tests for the full termalive attach flow.
/// These tests verify that terminal output is correctly captured and streamed.
/// </summary>
[Trait("Category", "Integration")]
public class AttachIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SessionHost? _host;
    private readonly int _port = 17777 + Random.Shared.Next(1000); // Randomize port to avoid conflicts

    public AttachIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        if (!CanRunTests)
        {
            return;
        }

        var options = new SessionHostOptions
        {
            WebSocketPort = _port,
            PipeName = $"termalive-test-{Guid.NewGuid():N}"
        };

        _host = new SessionHost(options);
        await _host.StartAsync();
        _output.WriteLine($"Started test host on port {_port}");
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
    }

    private static bool CanRunTests =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [SkippableFact]
    public async Task AttachReceivesSimpleOutput()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        // Create a session that outputs simple text
        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo 'Hello from PTY'; sleep 0.1"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        // Connect and attach
        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        // Read output
        var output = new StringBuilder();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));
            _output.WriteLine($"[{sw.ElapsedMilliseconds}ms] Received {data.Length} bytes");

            if (output.ToString().Contains("Hello from PTY"))
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                break;
            }
        }

        _output.WriteLine($"Total output:\n{output}");
        Assert.Contains("Hello from PTY", output.ToString());
    }

    [SkippableFact]
    public async Task AttachReceivesAnsiColors()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[31mRed\\033[0m \\033[32mGreen\\033[0m'; sleep 0.1"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        var output = new StringBuilder();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));

            if (output.ToString().Contains("Green") && sw.ElapsedMilliseconds > 200)
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                break;
            }
        }

        var text = output.ToString();
        _output.WriteLine($"Output ({text.Length} chars):\n{EscapeForDisplay(text)}");

        Assert.Contains("\x1b[31m", text); // Red color code
        Assert.Contains("Red", text);
        Assert.Contains("\x1b[32m", text); // Green color code
        Assert.Contains("Green", text);
    }

    [SkippableFact]
    public async Task AttachReceivesAlternateScreenBuffer()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "printf '\\033[?1049hAlternate\\033[?1049l'; sleep 0.1"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        var output = new StringBuilder();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));

            if (output.ToString().Contains("\x1b[?1049l") && sw.ElapsedMilliseconds > 200)
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                break;
            }
        }

        var text = output.ToString();
        _output.WriteLine($"Output:\n{EscapeForDisplay(text)}");

        Assert.Contains("\x1b[?1049h", text); // Enter alternate buffer
        Assert.Contains("Alternate", text);
        Assert.Contains("\x1b[?1049l", text); // Exit alternate buffer
    }

    [SkippableFact]
    public async Task AttachReceivesContinuousOutput()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "for i in 1 2 3 4 5; do echo \"Line $i\"; sleep 0.05; done"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        var output = new StringBuilder();
        var chunks = new List<(long Time, int Length)>();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            chunks.Add((sw.ElapsedMilliseconds, data.Length));
            output.Append(Encoding.UTF8.GetString(data.Span));

            if (output.ToString().Contains("Line 5"))
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                break;
            }
        }

        foreach (var chunk in chunks)
        {
            _output.WriteLine($"[{chunk.Time}ms] {chunk.Length} bytes");
        }
        _output.WriteLine($"Total output:\n{output}");

        Assert.Contains("Line 1", output.ToString());
        Assert.Contains("Line 5", output.ToString());
        Assert.True(chunks.Count >= 2, "Should receive multiple chunks for streaming output");
    }

    [SkippableFact]
    public async Task AttachAndSendInput()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "read line; echo \"You said: $line\""],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        // Give it a moment to start
        await Task.Delay(100);

        // Send input
        await attachment.SendInputAsync(Encoding.UTF8.GetBytes("hello world\n"), CancellationToken.None);

        // Read output
        var output = new StringBuilder();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));

            if (output.ToString().Contains("You said: hello world"))
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                break;
            }
        }

        _output.WriteLine($"Output:\n{output}");
        Assert.Contains("You said: hello world", output.ToString());
    }

    [SkippableFact]
    public async Task BufferedOutputDeliveredOnAttach()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        var sessionId = $"test-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo 'Buffered content'; sleep 5"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        // Wait for command to produce output
        await Task.Delay(500);

        // Now attach - should receive buffered output
        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        // Check buffered output
        var buffered = Encoding.UTF8.GetString(attachment.BufferedOutput.Span);
        _output.WriteLine($"Buffered output ({attachment.BufferedOutput.Length} bytes):\n{buffered}");

        Assert.Contains("Buffered content", buffered);
    }

    [SkippableFact]
    public async Task SimulateTuiApp()
    {
        Skip.IfNot(CanRunTests, "Integration tests only run on Unix");

        // This test simulates what a TUI app like Claude does
        var sessionId = $"test-{Guid.NewGuid():N}";
        var script = """
            printf '\033[?1049h'        # Enter alternate buffer
            printf '\033[2J'            # Clear screen
            printf '\033[H'             # Home cursor
            printf '\033[1;44;37m Header \033[0m\n'
            printf '\033[36m┌─────┐\033[0m\n'
            printf '\033[36m│ Box │\033[0m\n'
            printf '\033[36m└─────┘\033[0m\n'
            sleep 0.5
            printf '\033[10;1HContent at row 10'
            sleep 0.5
            printf '\033[?1049l'        # Exit alternate buffer
            echo 'Done'
            """;

        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", script],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string>
            {
                ["TERM"] = "xterm-256color",
                ["LANG"] = "en_US.UTF-8"
            }
        };

        await _host!.CreateSessionAsync(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        var outputBytes = new List<byte>();
        var chunks = new List<(long Time, int Length, string Preview)>();
        var sw = Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            var bytes = data.ToArray();
            outputBytes.AddRange(bytes);

            var preview = Encoding.UTF8.GetString(bytes);
            chunks.Add((sw.ElapsedMilliseconds, bytes.Length, EscapeForDisplay(preview).Substring(0, Math.Min(50, preview.Length))));

            if (Encoding.UTF8.GetString(outputBytes.ToArray()).Contains("Done"))
            {
                break;
            }

            if (sw.ElapsedMilliseconds > 10000)
            {
                break;
            }
        }

        var fullOutput = Encoding.UTF8.GetString(outputBytes.ToArray());

        _output.WriteLine("=== Chunks received ===");
        foreach (var chunk in chunks)
        {
            _output.WriteLine($"[{chunk.Time,5}ms] {chunk.Length,4} bytes: {chunk.Preview}...");
        }

        _output.WriteLine("\n=== Raw output (escaped) ===");
        _output.WriteLine(EscapeForDisplay(fullOutput));

        _output.WriteLine("\n=== Hex dump (first 200 bytes) ===");
        _output.WriteLine(HexDump(outputBytes.Take(200).ToArray()));

        // Verify key escape sequences are present
        Assert.Contains("\x1b[?1049h", fullOutput); // Enter alternate screen
        Assert.Contains("\x1b[2J", fullOutput);     // Clear screen
        Assert.Contains("Header", fullOutput);
        Assert.Contains("Box", fullOutput);
        Assert.Contains("\x1b[?1049l", fullOutput); // Exit alternate screen
        Assert.Contains("Done", fullOutput);
    }

    private static string EscapeForDisplay(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (c == '\x1b')
                sb.Append("\\e");
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c < 32)
                sb.Append($"\\x{(int)c:X2}");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string HexDump(byte[] bytes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X4}: ");

            for (int j = 0; j < 16 && i + j < bytes.Length; j++)
            {
                sb.Append($"{bytes[i + j]:X2} ");
            }

            sb.Append(" ");

            for (int j = 0; j < 16 && i + j < bytes.Length; j++)
            {
                var c = (char)bytes[i + j];
                sb.Append(c >= 32 && c < 127 ? c : '.');
            }

            sb.AppendLine();
        }
        return sb.ToString();
    }
}
