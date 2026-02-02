// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Test patterns inspired by:
// - tmux/regress/new-session-no-client.sh (session persistence without client)
// - terminal/src/winconpty/ft_pty/ConPtyTests.cpp::SurvivesOnBreakOutput

using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for session state persistence and buffer management.
/// </summary>
public class SessionStateTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SessionHost? _host;
    private readonly int _port = 18300 + Random.Shared.Next(1000);

    public SessionStateTests(ITestOutputHelper output)
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
            PipeName = $"state-test-{Guid.NewGuid():N}",
            DefaultBufferSize = 64 * 1024
        };

        _host = new SessionHost(options);
        await _host.StartAsync();
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

    #region Session State Transitions

    /// <summary>
    /// Verifies that session starts in Running state.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void NewSession_StartsInRunningState()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = _host!.CreateSession($"state-running-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24
        });

        Assert.Equal(SessionState.Running, session.State);
    }

    /// <summary>
    /// Verifies that session transitions to Exited state on normal exit.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_NormalExit_TransitionsToExitedState()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = _host!.CreateSession($"state-exit-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "exit 0"],
            Columns = 80,
            Rows = 24
        });

        await session.WaitForExitAsync();

        Assert.Equal(SessionState.Exited, session.State);
        Assert.Equal(0, session.Info.ExitCode);
    }

    /// <summary>
    /// Verifies that session reports correct exit code.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_ExitWithCode_ReportsCorrectCode()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = _host!.CreateSession($"state-exitcode-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "exit 127"],
            Columns = 80,
            Rows = 24
        });

        var exitCode = await session.WaitForExitAsync();

        Assert.Equal(127, exitCode);
        Assert.Equal(127, session.Info.ExitCode);
    }

    #endregion

    #region Session Persistence Without Client

    /// <summary>
    /// Verifies that session continues running without attached client.
    /// Inspired by tmux/regress/new-session-no-client.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_NoClient_ContinuesRunning()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"persist-noclient-{Guid.NewGuid():N}";

        // Create session but never attach
        var session = _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 80,
            Rows = 24
        });

        // Wait a bit
        await Task.Delay(500);

        // Session should still be running
        Assert.Equal(SessionState.Running, session.State);
        Assert.NotNull(_host.GetSession(sessionId));
    }

    /// <summary>
    /// Verifies that session survives client disconnect.
    /// Inspired by ConPtyTests::SurvivesOnBreakOutput.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_ClientDisconnects_SessionSurvives()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"survive-disconnect-{Guid.NewGuid():N}";

        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        // Connect, attach, then disconnect
        using (var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/"))
        {
            await using var attachment = await client.AttachAsync(sessionId);
            // Client disposes here
        }

        // Wait a moment
        await Task.Delay(200);

        // Session should still exist and be running
        var session = _host.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(SessionState.Running, session.State);
    }

    /// <summary>
    /// Verifies that session survives multiple attach/detach cycles.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_MultipleAttachDetachCycles_Survives()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"multi-attach-{Guid.NewGuid():N}";

        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 20"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        for (int i = 0; i < 3; i++)
        {
            using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
            await using var attachment = await client.AttachAsync(sessionId);
            await Task.Delay(100);
        }

        // Session should still be running
        var session = _host.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(SessionState.Running, session.State);
    }

    #endregion

    #region Output Buffer Tests

    /// <summary>
    /// Verifies that buffered output is delivered on attach.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_BufferedOutput_DeliveredOnAttach()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"buffered-{Guid.NewGuid():N}";

        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo 'BUFFERED_OUTPUT_MARKER'; sleep 5"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        // Wait for output to be buffered
        await Task.Delay(500);

        // Now attach
        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        // Check buffered output
        var buffered = Encoding.UTF8.GetString(attachment.BufferedOutput.Span);
        _output.WriteLine($"Buffered: {buffered}");

        Assert.Contains("BUFFERED_OUTPUT_MARKER", buffered);
    }

    /// <summary>
    /// Verifies that GetBufferedOutput returns session output.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_GetBufferedOutput_ReturnsOutput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"getbuffer-{Guid.NewGuid():N}";

        var session = _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo 'BUFFER_TEST_DATA'; sleep 2"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        // Wait for output
        await Task.Delay(500);

        var buffered = session.GetBufferedOutput();
        var text = Encoding.UTF8.GetString(buffered);

        Assert.Contains("BUFFER_TEST_DATA", text);
    }

    #endregion

    #region Session Info Tests

    /// <summary>
    /// Verifies that SessionInfo is correctly populated.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void SessionInfo_HasCorrectValues()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"info-{Guid.NewGuid():N}";
        var beforeCreate = DateTimeOffset.UtcNow;

        var session = _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            WorkingDirectory = "/tmp",
            Columns = 120,
            Rows = 40
        });

        var afterCreate = DateTimeOffset.UtcNow;
        var info = session.Info;

        Assert.Equal(sessionId, info.Id);
        Assert.Equal("/bin/sh", info.Command);
        Assert.Equal("/tmp", info.WorkingDirectory);
        Assert.Equal(SessionState.Running, info.State);
        Assert.Null(info.ExitCode);
        Assert.Equal(120, info.Columns);
        Assert.Equal(40, info.Rows);
        Assert.InRange(info.Created, beforeCreate, afterCreate);
    }

    /// <summary>
    /// Verifies that sessions from ListSessions have correct info.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void ListSessions_ContainsCorrectInfo()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"listinfo-{Guid.NewGuid():N}";

        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 100,
            Rows = 30
        });

        var sessions = _host.ListSessions();
        var sessionInfo = sessions.Single(s => s.Id == sessionId);

        Assert.Equal(sessionId, sessionInfo.Id);
        Assert.Equal("/bin/sh", sessionInfo.Command);
        Assert.Equal(SessionState.Running, sessionInfo.State);
        Assert.Equal(100, sessionInfo.Columns);
        Assert.Equal(30, sessionInfo.Rows);
    }

    #endregion

    #region Resize Tests

    /// <summary>
    /// Verifies that session can be resized.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void Session_Resize_UpdatesDimensions()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = _host!.CreateSession($"resize-{Guid.NewGuid():N}", new PtyOptions
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
    /// Verifies that resize via client works.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_ResizeViaClient_UpdatesDimensions()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"resize-client-{Guid.NewGuid():N}";

        var session = _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        await attachment.ResizeAsync(150, 40, CancellationToken.None);

        // Give it a moment to process
        await Task.Delay(100);

        Assert.Equal(150, session.Columns);
        Assert.Equal(40, session.Rows);
    }

    #endregion

    #region Last Activity Time Tests

    /// <summary>
    /// Verifies that last activity time is tracked.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_ActivityTime_UpdatesOnInputOutput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session = _host!.CreateSession($"activity-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "cat"],
            Columns = 80,
            Rows = 24
        });

        var initialTime = session.LastActivityTime;

        await Task.Delay(100);

        // Send input
        await session.SendInputAsync(Encoding.UTF8.GetBytes("test\n"));

        await Task.Delay(100);

        // Activity time should be updated
        Assert.True(session.LastActivityTime > initialTime);
    }

    #endregion
}
