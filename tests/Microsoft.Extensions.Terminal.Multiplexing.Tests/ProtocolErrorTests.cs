// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Test patterns inspired by:
// - tmux/regress/control-client-sanity.sh (protocol interaction tests)
// - terminal/src/winconpty/ft_pty/ConPtyTests.cpp (error handling)

using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for protocol message handling and error cases.
/// </summary>
public class ProtocolErrorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SessionHost? _host;
    private readonly int _port = 18200 + Random.Shared.Next(1000);

    public ProtocolErrorTests(ITestOutputHelper output)
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
            PipeName = $"proto-test-{Guid.NewGuid():N}"
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

    #region Attach Error Cases

    /// <summary>
    /// Verifies that attaching to a non-existent session returns an error.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Attach_NonExistentSession_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AttachAsync("does-not-exist"));

        Assert.Contains("not found", ex.Message);
    }

    /// <summary>
    /// Verifies that double attach throws exception.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task Attach_WhenAlreadyAttached_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"double-attach-{Guid.NewGuid():N}";
        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 10"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        await using var attachment = await client.AttachAsync(sessionId);

        // Try to attach again without detaching
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AttachAsync(sessionId));

        Assert.Contains("Already attached", ex.Message);
    }

    #endregion

    #region Create Session Error Cases

    /// <summary>
    /// Verifies that creating a session with invalid command fails gracefully.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task CreateSession_InvalidCommand_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateSessionAsync($"invalid-cmd-{Guid.NewGuid():N}", new PtyOptions
            {
                Command = "/nonexistent/command/path/xyz",
                Columns = 80,
                Rows = 24
            }));

        _output.WriteLine($"Exception: {ex.Message}");
        // Should fail to start the process
    }

    /// <summary>
    /// Verifies that creating a session via client works.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task CreateSession_ViaClient_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");

        var sessionId = $"client-create-{Guid.NewGuid():N}";
        var sessionInfo = await client.CreateSessionAsync(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 5"],
            Columns = 100,
            Rows = 30
        });

        Assert.Equal(sessionId, sessionInfo.Id);
        Assert.Equal("/bin/sh", sessionInfo.Command);
        Assert.Equal(SessionState.Running, sessionInfo.State);
        Assert.Equal(100, sessionInfo.Columns);
        Assert.Equal(30, sessionInfo.Rows);
    }

    #endregion

    #region Kill Session Error Cases

    /// <summary>
    /// Verifies that kill session via client works.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task KillSession_ViaClient_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"kill-via-client-{Guid.NewGuid():N}";
        _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24
        });

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");

        // Kill via client
        await client.KillSessionAsync(sessionId, force: true);

        // Give it a moment to process
        await Task.Delay(100);

        // Verify it's gone
        var sessions = await client.ListSessionsAsync();
        Assert.DoesNotContain(sessions, s => s.Id == sessionId);
    }

    #endregion

    #region Connection Error Cases

    /// <summary>
    /// Verifies connection to unavailable host throws an exception.
    /// </summary>
    [Fact]
    public async Task Connect_UnavailableHost_ThrowsException()
    {
        // Use a port that's unlikely to be in use
        // May throw either TimeoutException or WebSocketException depending on how fast connection fails
        await Assert.ThrowsAnyAsync<Exception>(() =>
            SessionClient.ConnectAsync("ws://localhost:19999/", TimeSpan.FromMilliseconds(500)));
    }

    /// <summary>
    /// Verifies invalid URI scheme throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task Connect_InvalidScheme_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SessionClient.ConnectAsync("http://localhost:8080/"));

        Assert.Contains("Unsupported URI scheme", ex.Message);
    }

    #endregion

    #region Input After Exit

    /// <summary>
    /// Verifies that sending input to an exited session throws.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task SendInput_AfterSessionExit_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessionId = $"input-after-exit-{Guid.NewGuid():N}";
        var session = _host!.CreateSession(sessionId, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "exit 0"],
            Columns = 80,
            Rows = 24
        });

        // Wait for exit
        await session.WaitForExitAsync();

        // Try to send input - should throw
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.SendInputAsync(Encoding.UTF8.GetBytes("test")).AsTask());

        Assert.Contains("Cannot send input", ex.Message);
    }

    #endregion
}
