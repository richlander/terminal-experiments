// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Test patterns inspired by:
// - terminal/src/winconpty/ft_pty/ConPtyTests.cpp
// - tmux/regress/input-keys.sh

using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// ManagedSession tests - these require actual process spawning.
/// Run with: dotnet test --filter "Category=Pty"
/// </summary>
[Trait("Category", "Pty")]
public class ManagedSessionTests : IDisposable
{
    private readonly List<ManagedSession> _sessionsToCleanup = [];

    public void Dispose()
    {
        foreach (var session in _sessionsToCleanup)
        {
            session.Kill(force: true);
            session.Dispose();
        }
    }

    private static bool CanRunTests =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private ManagedSession CreateSession(string id, PtyOptions options)
    {
        var session = new ManagedSession(id, options);
        _sessionsToCleanup.Add(session);
        return session;
    }

    /// <summary>
    /// Verifies that ManagedSession can be created with valid options.
    /// Inspired by ConPtyTests::GoodCreate.
    /// </summary>
    [SkippableFact]
    public void Constructor_ValidOptions_CreatesSession()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"ctor-valid-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 80,
            Rows = 24
        });

        Assert.Equal(SessionState.Running, session.State);
        Assert.Equal(80, session.Columns);
        Assert.Equal(24, session.Rows);
    }

    /// <summary>
    /// Verifies that null ID throws.
    /// </summary>
    [SkippableFact]
    public void Constructor_NullId_Throws()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        Assert.Throws<ArgumentNullException>(() =>
            new ManagedSession(null!, new PtyOptions
            {
                Command = "/bin/sh",
                Columns = 80,
                Rows = 24
            }));
    }

    /// <summary>
    /// Verifies that empty ID throws.
    /// </summary>
    [SkippableFact]
    public void Constructor_EmptyId_Throws()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        Assert.Throws<ArgumentException>(() =>
            new ManagedSession("", new PtyOptions
            {
                Command = "/bin/sh",
                Columns = 80,
                Rows = 24
            }));
    }

    /// <summary>
    /// Verifies that null options throws.
    /// </summary>
    [SkippableFact]
    public void Constructor_NullOptions_Throws()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        Assert.Throws<ArgumentNullException>(() =>
            new ManagedSession("test-id", null!));
    }

    /// <summary>
    /// Verifies that invalid buffer size throws.
    /// Inspired by ConPtyTests::CreateConPtyBadSize.
    /// </summary>
    [SkippableFact]
    public void Constructor_InvalidBufferSize_Throws()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManagedSession("test-id", new PtyOptions
            {
                Command = "/bin/sh",
                Columns = 80,
                Rows = 24
            }, bufferSize: 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManagedSession("test-id", new PtyOptions
            {
                Command = "/bin/sh",
                Columns = 80,
                Rows = 24
            }, bufferSize: -1));
    }

    /// <summary>
    /// Verifies that session reads output from process.
    /// </summary>
    [SkippableFact]
    public async Task Session_ReadsOutput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"reads-output-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo OUTPUT_TEST_DATA"],
            Columns = 80,
            Rows = 24
        });

        await session.WaitForExitAsync();
        await Task.Delay(100); // Let buffer fill

        var output = session.GetBufferedOutput();
        var text = Encoding.UTF8.GetString(output);

        Assert.Contains("OUTPUT_TEST_DATA", text);
    }

    /// <summary>
    /// Verifies that session accepts input.
    /// Inspired by tmux/regress/input-keys.sh.
    /// </summary>
    [SkippableFact]
    public async Task Session_AcceptsInput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"accepts-input-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/cat",
            Columns = 80,
            Rows = 24
        });

        await session.SendInputAsync(Encoding.UTF8.GetBytes("ECHO_THIS\n"));
        await Task.Delay(200);

        var output = session.GetBufferedOutput();
        var text = Encoding.UTF8.GetString(output);

        Assert.Contains("ECHO_THIS", text);

        // Send EOF to exit cat
        await session.SendInputAsync(new byte[] { 0x04 }); // Ctrl+D
    }

    /// <summary>
    /// Verifies that session can be resized.
    /// </summary>
    [SkippableFact]
    public void Session_CanBeResized()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"resize-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 80,
            Rows = 24
        });

        Assert.Equal(80, session.Columns);
        Assert.Equal(24, session.Rows);

        session.Resize(200, 50);

        Assert.Equal(200, session.Columns);
        Assert.Equal(50, session.Rows);
    }

    /// <summary>
    /// Verifies that session can be killed.
    /// </summary>
    [SkippableFact]
    public async Task Session_CanBeKilled()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"kill-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24
        });

        Assert.Equal(SessionState.Running, session.State);

        session.Kill(force: true);

        await session.WaitForExitAsync();

        Assert.NotEqual(SessionState.Running, session.State);
    }

    /// <summary>
    /// Verifies that subscribing to output works.
    /// </summary>
    [SkippableFact]
    public async Task Session_SubscribesToOutput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"subscribe-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo SUBSCRIBE_TEST; sleep 1"],
            Columns = 80,
            Rows = 24
        });

        var output = new StringBuilder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            await foreach (var data in session.SubscribeAsync(cts.Token))
            {
                output.Append(Encoding.UTF8.GetString(data.Span));
                if (output.ToString().Contains("SUBSCRIBE_TEST"))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if timeout
        }

        Assert.Contains("SUBSCRIBE_TEST", output.ToString());
    }

    /// <summary>
    /// Verifies that idle timeout is correctly tracked.
    /// </summary>
    [SkippableFact]
    public async Task Session_IdleTimeout_IsTracked()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"idle-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24,
            IdleTimeout = TimeSpan.FromMilliseconds(100)
        });

        Assert.Equal(TimeSpan.FromMilliseconds(100), session.IdleTimeout);

        // Initially not idle
        Assert.False(session.IsIdleTimedOut);

        // Wait for idle timeout
        await Task.Delay(200);

        // Should now be idle
        Assert.True(session.IsIdleTimedOut);
    }

    /// <summary>
    /// Verifies that RenderScreen returns content.
    /// </summary>
    [SkippableFact]
    public async Task Session_RenderScreen_ReturnsContent()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = CreateSession($"render-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo RENDER_CONTENT; sleep 1"],
            Columns = 80,
            Rows = 24
        });

        await Task.Delay(300);

        var rendered = session.RenderScreen();

        Assert.NotEmpty(rendered);
        // Rendered output should contain ANSI sequences or the text
    }
}
