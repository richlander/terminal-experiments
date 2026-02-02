// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Test patterns inspired by:
// - tmux/regress/has-session-return.sh (session existence checks)
// - tmux/regress/kill-session-process-exit.sh (session termination)
// - tmux/regress/new-session-command.sh (session creation)
// - terminal/src/winconpty/ft_pty/ConPtyTests.cpp (PTY lifecycle)

using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for session lifecycle operations: create, attach, detach, destroy.
/// Inspired by tmux regress tests and Windows ConPty tests.
/// </summary>
public class SessionLifecycleTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SessionHost? _host;
    private readonly int _port = 18000 + Random.Shared.Next(1000);

    public SessionLifecycleTests(ITestOutputHelper output)
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
            PipeName = $"lifecycle-test-{Guid.NewGuid():N}"
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

    #region Session Creation Tests

    /// <summary>
    /// Verifies that a session can be created with basic options.
    /// Inspired by tmux/regress/new-session-command.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateSession_WithBasicOptions_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"create-basic-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 1"],
            Columns = 80,
            Rows = 24
        };

        var session = _host!.CreateSession(sessionId, ptyOptions);

        Assert.NotNull(session);
        Assert.Equal(sessionId, session.Id);
        Assert.Equal(SessionState.Running, session.State);
        Assert.Equal(80, session.Columns);
        Assert.Equal(24, session.Rows);
    }

    /// <summary>
    /// Verifies that duplicate session IDs are rejected.
    /// Inspired by tmux has-session checks.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateSession_DuplicateId_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"dup-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24
        };

        _host!.CreateSession(sessionId, ptyOptions);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _host.CreateSession(sessionId, ptyOptions));

        Assert.Contains("already exists", ex.Message);
    }

    /// <summary>
    /// Verifies that sessions can be created with custom terminal sizes.
    /// Inspired by tmux/regress/new-session-size.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateSession_CustomSize_UsesSpecifiedDimensions()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"sized-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 1"],
            Columns = 200,
            Rows = 50
        };

        var session = _host!.CreateSession(sessionId, ptyOptions);

        Assert.Equal(200, session.Columns);
        Assert.Equal(50, session.Rows);
    }

    /// <summary>
    /// Verifies that sessions respect environment variables.
    /// Inspired by tmux/regress/new-session-environment.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task CreateSession_WithEnvironment_PassesVariables()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"env-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo \"TEST_VAR=$TEST_VAR\""],
            Environment = new Dictionary<string, string>
            {
                ["TEST_VAR"] = "test_value_123",
                ["TERM"] = "xterm-256color"
            },
            Columns = 80,
            Rows = 24
        };

        _host!.CreateSession(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        var output = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));
            if (output.ToString().Contains("test_value_123") || sw.ElapsedMilliseconds > 3000)
            {
                break;
            }
        }

        Assert.Contains("TEST_VAR=test_value_123", output.ToString());
    }

    #endregion

    #region Session Existence Tests

    /// <summary>
    /// Verifies has-session equivalent behavior.
    /// Inspired by tmux/regress/has-session-return.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void GetSession_ExistingSession_ReturnsSession()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"exists-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 80,
            Rows = 24
        };

        _host!.CreateSession(sessionId, ptyOptions);

        var session = _host.GetSession(sessionId);

        Assert.NotNull(session);
        Assert.Equal(sessionId, session.Id);
    }

    /// <summary>
    /// Verifies that non-existent sessions return null.
    /// Inspired by tmux/regress/has-session-return.sh.
    /// </summary>
    [Fact]
    public void GetSession_NonExistent_ReturnsNull()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        using var host = new SessionHost(options);
        var session = host.GetSession("nonexistent-session");

        Assert.Null(session);
    }

    #endregion

    #region Session Termination Tests

    /// <summary>
    /// Verifies that killing a session terminates the process.
    /// Inspired by tmux/regress/kill-session-process-exit.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task KillSession_RunningSession_TerminatesProcess()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"kill-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 1000"],
            Columns = 80,
            Rows = 24
        };

        _host!.CreateSession(sessionId, ptyOptions);

        // Verify session exists
        Assert.NotNull(_host.GetSession(sessionId));

        // Kill the session
        var killed = await _host.KillSessionAsync(sessionId, force: true);

        Assert.True(killed);
        Assert.Null(_host.GetSession(sessionId));
    }

    /// <summary>
    /// Verifies that killing a non-existent session returns false.
    /// </summary>
    [Fact]
    public async Task KillSession_NonExistent_ReturnsFalse()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        await using var host = new SessionHost(options);
        var killed = await host.KillSessionAsync("nonexistent", force: true);

        Assert.False(killed);
    }

    /// <summary>
    /// Verifies that a session exits naturally and reports exit code.
    /// Inspired by ConPtyTests::DiesOnClose.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Session_NaturalExit_ReportsExitCode()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"exit-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "exit 42"],
            Columns = 80,
            Rows = 24
        };

        var session = _host!.CreateSession(sessionId, ptyOptions);

        // Wait for exit
        var exitCode = await session.WaitForExitAsync();

        Assert.Equal(42, exitCode);
        Assert.Equal(SessionState.Exited, session.State);
    }

    #endregion

    #region Attach/Detach Tests

    /// <summary>
    /// Verifies that clients can attach to a running session.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task AttachSession_RunningSession_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"attach-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo attached; sleep 2"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        _host!.CreateSession(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        Assert.NotNull(attachment);

        var output = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await foreach (var data in attachment.ReadOutputAsync(CancellationToken.None))
        {
            output.Append(Encoding.UTF8.GetString(data.Span));
            if (output.ToString().Contains("attached") || sw.ElapsedMilliseconds > 3000)
            {
                break;
            }
        }

        Assert.Contains("attached", output.ToString());
    }

    /// <summary>
    /// Verifies that detach cleans up the attachment.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task DetachSession_AfterAttach_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"detach-{Guid.NewGuid():N}";
        var ptyOptions = new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };

        _host!.CreateSession(sessionId, ptyOptions);

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        var attachment = await client.AttachAsync(sessionId);

        // Dispose triggers detach
        await attachment.DisposeAsync();

        // Session should still exist
        var session = _host.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(SessionState.Running, session.State);
    }

    #endregion

    #region Session Count Tests

    /// <summary>
    /// Verifies that session count is tracked correctly.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task SessionCount_AfterOperations_ReflectsCorrectCount()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        Assert.Equal(0, _host!.SessionCount);

        var session1 = _host.CreateSession($"count1-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24
        });
        Assert.Equal(1, _host.SessionCount);

        var session2 = _host.CreateSession($"count2-{Guid.NewGuid():N}", new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24
        });
        Assert.Equal(2, _host.SessionCount);

        await _host.KillSessionAsync(session1.Id, force: true);
        Assert.Equal(1, _host.SessionCount);

        await _host.KillSessionAsync(session2.Id, force: true);
        Assert.Equal(0, _host.SessionCount);
    }

    #endregion
}
