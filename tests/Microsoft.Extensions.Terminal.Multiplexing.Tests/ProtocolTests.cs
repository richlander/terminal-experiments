// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

public class ProtocolTests
{
    private static ProtocolWriter CreateWriter(Stream stream)
    {
        var type = typeof(SessionHost).Assembly.GetType("Microsoft.Extensions.Terminal.Multiplexing.ProtocolWriter")!;
        return (ProtocolWriter)Activator.CreateInstance(type, stream)!;
    }

    private static ProtocolReader CreateReader(Stream stream)
    {
        var type = typeof(SessionHost).Assembly.GetType("Microsoft.Extensions.Terminal.Multiplexing.ProtocolReader")!;
        return (ProtocolReader)Activator.CreateInstance(type, stream)!;
    }

    [Fact]
    public async Task WriteAndRead_HelloMessage()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteHelloAsync(1);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Hello, msg.Value.Type);
        Assert.Equal(1, ProtocolReader.ParseHello(msg.Value.Payload));
    }

    [Fact]
    public async Task WriteAndRead_SessionList()
    {
        using var stream = new MemoryStream();

        var sessions = new List<SessionInfo>
        {
            new("id-1", "cmd", "/tmp", SessionState.Running, DateTimeOffset.UtcNow, null, 80, 24),
            new("id-2", "sh", null, SessionState.Exited, DateTimeOffset.UtcNow.AddMinutes(-5), 0, 120, 40)
        };

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionListAsync(sessions);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.SessionList, msg.Value.Type);

        var parsed = ProtocolReader.ParseSessionList(msg.Value.Payload);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("id-1", parsed[0].Id);
        Assert.Equal("cmd", parsed[0].Command);
        Assert.Equal(SessionState.Running, parsed[0].State);
        Assert.Equal("id-2", parsed[1].Id);
        Assert.Equal(SessionState.Exited, parsed[1].State);
        Assert.Equal(0, parsed[1].ExitCode);
    }

    [Fact]
    public async Task WriteAndRead_CreateSession()
    {
        using var stream = new MemoryStream();

        var options = new PtyOptions
        {
            Command = "/bin/bash",
            Arguments = new[] { "-c", "echo hello" },
            WorkingDirectory = "/home/user",
            Environment = new Dictionary<string, string> { ["FOO"] = "bar" },
            Columns = 100,
            Rows = 30
        };

        var writer = new ProtocolWriter(stream);
        await writer.WriteCreateSessionAsync("my-session", options);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.CreateSession, msg.Value.Type);

        var (id, parsed) = ProtocolReader.ParseCreateSession(msg.Value.Payload);
        Assert.Equal("my-session", id);
        Assert.Equal("/bin/bash", parsed.Command);
        Assert.Equal(new[] { "-c", "echo hello" }, parsed.Arguments);
        Assert.Equal("/home/user", parsed.WorkingDirectory);
        Assert.NotNull(parsed.Environment);
        Assert.Equal("bar", parsed.Environment["FOO"]);
        Assert.Equal(100, parsed.Columns);
        Assert.Equal(30, parsed.Rows);
    }

    [Fact]
    public async Task WriteAndRead_Attach()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteAttachAsync("session-xyz");

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Attach, msg.Value.Type);
        var (sessionId, cols, rows) = ProtocolReader.ParseAttach(msg.Value.Payload);
        Assert.Equal("session-xyz", sessionId);
    }

    [Fact]
    public async Task WriteAndRead_InputOutput()
    {
        using var stream = new MemoryStream();

        var inputData = new byte[] { 1, 2, 3, 4, 5 };
        var outputData = new byte[] { 10, 20, 30 };

        var writer = new ProtocolWriter(stream);
        await writer.WriteInputAsync(inputData);
        await writer.WriteOutputAsync(outputData);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);

        var msg1 = await reader.ReadMessageAsync();
        Assert.NotNull(msg1);
        Assert.Equal(MessageType.Input, msg1.Value.Type);
        Assert.Equal(inputData, msg1.Value.Payload);

        var msg2 = await reader.ReadMessageAsync();
        Assert.NotNull(msg2);
        Assert.Equal(MessageType.Output, msg2.Value.Type);
        Assert.Equal(outputData, msg2.Value.Payload);
    }

    [Fact]
    public async Task WriteAndRead_Resize()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteResizeAsync(200, 50);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Resize, msg.Value.Type);

        var (cols, rows) = ProtocolReader.ParseResize(msg.Value.Payload);
        Assert.Equal(200, cols);
        Assert.Equal(50, rows);
    }

    [Fact]
    public async Task WriteAndRead_Error()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteErrorAsync("Something went wrong");

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Error, msg.Value.Type);
        Assert.Equal("Something went wrong", ProtocolReader.ParseError(msg.Value.Payload));
    }

    [Fact]
    public async Task WriteAndRead_SessionExited()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionExitedAsync("dead-session", 127);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.SessionExited, msg.Value.Type);

        var (sessionId, exitCode) = ProtocolReader.ParseSessionExited(msg.Value.Payload);
        Assert.Equal("dead-session", sessionId);
        Assert.Equal(127, exitCode);
    }

    [Fact]
    public async Task ReadMessageAsync_ReturnsNull_OnEmptyStream()
    {
        using var stream = new MemoryStream();
        var reader = new ProtocolReader(stream);

        var msg = await reader.ReadMessageAsync();

        Assert.Null(msg);
    }

    #region Additional Protocol Tests
    // Additional tests inspired by tmux/regress/* and terminal/ConPtyTests.cpp

    /// <summary>
    /// Verifies that attach with size parameters is correctly serialized.
    /// Inspired by tmux/regress/new-session-size.sh.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_AttachWithSize()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteAttachAsync("sized-session", 120, 40);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Attach, msg.Value.Type);

        var (sessionId, cols, rows) = ProtocolReader.ParseAttach(msg.Value.Payload);
        Assert.Equal("sized-session", sessionId);
        Assert.Equal(120, cols);
        Assert.Equal(40, rows);
    }

    /// <summary>
    /// Verifies that KillSession message is correctly serialized.
    /// Inspired by tmux/regress/kill-session-process-exit.sh.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_KillSession()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteKillSessionAsync("kill-target", true);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.KillSession, msg.Value.Type);

        var (sessionId, force) = ProtocolReader.ParseKillSession(msg.Value.Payload);
        Assert.Equal("kill-target", sessionId);
        Assert.True(force);
    }

    /// <summary>
    /// Verifies that KillSession with force=false is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_KillSession_NotForced()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteKillSessionAsync("gentle-kill", false);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);

        var (sessionId, force) = ProtocolReader.ParseKillSession(msg.Value.Payload);
        Assert.Equal("gentle-kill", sessionId);
        Assert.False(force);
    }

    /// <summary>
    /// Verifies that Detach message is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_Detach()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteDetachAsync();

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Detach, msg.Value.Type);
        Assert.Empty(msg.Value.Payload);
    }

    /// <summary>
    /// Verifies that ListSessions message is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_ListSessions()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteListSessionsAsync();

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.ListSessions, msg.Value.Type);
        Assert.Empty(msg.Value.Payload);
    }

    /// <summary>
    /// Verifies that empty session list is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_EmptySessionList()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionListAsync([]);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.SessionList, msg.Value.Type);

        var parsed = ProtocolReader.ParseSessionList(msg.Value.Payload);
        Assert.Empty(parsed);
    }

    /// <summary>
    /// Verifies that SessionCreated message is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_SessionCreated()
    {
        using var stream = new MemoryStream();

        var sessionInfo = new SessionInfo(
            "created-session",
            "/bin/sh",
            "/home/user",
            SessionState.Running,
            DateTimeOffset.UtcNow,
            null,
            80,
            24);

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionCreatedAsync(sessionInfo);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.SessionCreated, msg.Value.Type);

        var parsed = ProtocolReader.ParseSessionCreated(msg.Value.Payload);
        Assert.Equal("created-session", parsed.Id);
        Assert.Equal("/bin/sh", parsed.Command);
        Assert.Equal("/home/user", parsed.WorkingDirectory);
        Assert.Equal(SessionState.Running, parsed.State);
        Assert.Equal(80, parsed.Columns);
        Assert.Equal(24, parsed.Rows);
    }

    /// <summary>
    /// Verifies that Attached message with buffered output is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_Attached()
    {
        using var stream = new MemoryStream();

        var sessionInfo = new SessionInfo(
            "attached-session",
            "/bin/bash",
            null,
            SessionState.Running,
            DateTimeOffset.UtcNow,
            null,
            100,
            30);

        var bufferedOutput = new byte[] { 65, 66, 67, 68, 69 }; // "ABCDE"

        var writer = new ProtocolWriter(stream);
        await writer.WriteAttachedAsync(sessionInfo, bufferedOutput);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Attached, msg.Value.Type);

        var (parsed, output) = ProtocolReader.ParseAttached(msg.Value.Payload);
        Assert.Equal("attached-session", parsed.Id);
        Assert.Equal("/bin/bash", parsed.Command);
        Assert.Equal(bufferedOutput, output);
    }

    /// <summary>
    /// Verifies that CreateSession with no optional fields works.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_CreateSession_MinimalOptions()
    {
        using var stream = new MemoryStream();

        var options = new PtyOptions
        {
            Command = "/bin/echo",
            Columns = 80,
            Rows = 24
        };

        var writer = new ProtocolWriter(stream);
        await writer.WriteCreateSessionAsync("minimal-session", options);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.CreateSession, msg.Value.Type);

        var (id, parsed) = ProtocolReader.ParseCreateSession(msg.Value.Payload);
        Assert.Equal("minimal-session", id);
        Assert.Equal("/bin/echo", parsed.Command);
        Assert.Null(parsed.Arguments);
        Assert.Null(parsed.WorkingDirectory);
        Assert.Null(parsed.Environment);
    }

    /// <summary>
    /// Verifies that large output data is correctly serialized.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_LargeOutput()
    {
        using var stream = new MemoryStream();

        var largeData = new byte[64 * 1024]; // 64KB
        Random.Shared.NextBytes(largeData);

        var writer = new ProtocolWriter(stream);
        await writer.WriteOutputAsync(largeData);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(MessageType.Output, msg.Value.Type);
        Assert.Equal(largeData, msg.Value.Payload);
    }

    /// <summary>
    /// Verifies that all SessionState values can be round-tripped.
    /// </summary>
    [Theory]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.Running)]
    [InlineData(SessionState.Exited)]
    [InlineData(SessionState.Failed)]
    public async Task WriteAndRead_AllSessionStates(SessionState state)
    {
        using var stream = new MemoryStream();

        var sessions = new List<SessionInfo>
        {
            new("state-test", "cmd", null, state, DateTimeOffset.UtcNow, state == SessionState.Exited ? 0 : null, 80, 24)
        };

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionListAsync(sessions);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);

        var parsed = ProtocolReader.ParseSessionList(msg.Value.Payload);
        Assert.Single(parsed);
        Assert.Equal(state, parsed[0].State);
    }

    /// <summary>
    /// Verifies that multiple messages can be read sequentially.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_MultipleMessages()
    {
        using var stream = new MemoryStream();
        var writer = new ProtocolWriter(stream);

        await writer.WriteHelloAsync(1);
        await writer.WriteListSessionsAsync();
        await writer.WriteResizeAsync(100, 50);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);

        var msg1 = await reader.ReadMessageAsync();
        Assert.NotNull(msg1);
        Assert.Equal(MessageType.Hello, msg1.Value.Type);

        var msg2 = await reader.ReadMessageAsync();
        Assert.NotNull(msg2);
        Assert.Equal(MessageType.ListSessions, msg2.Value.Type);

        var msg3 = await reader.ReadMessageAsync();
        Assert.NotNull(msg3);
        Assert.Equal(MessageType.Resize, msg3.Value.Type);

        var msg4 = await reader.ReadMessageAsync();
        Assert.Null(msg4); // End of stream
    }

    /// <summary>
    /// Verifies that RequestScreen and ScreenContent messages work.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_ScreenContent()
    {
        using var stream = new MemoryStream();

        var screenData = new byte[] { 0x1b, 0x5b, 0x32, 0x4a }; // ESC[2J (clear screen)

        var writer = new ProtocolWriter(stream);
        await writer.WriteRequestScreenAsync();
        await writer.WriteScreenContentAsync(screenData);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);

        var msg1 = await reader.ReadMessageAsync();
        Assert.NotNull(msg1);
        Assert.Equal(MessageType.RequestScreen, msg1.Value.Type);
        Assert.Empty(msg1.Value.Payload);

        var msg2 = await reader.ReadMessageAsync();
        Assert.NotNull(msg2);
        Assert.Equal(MessageType.ScreenContent, msg2.Value.Type);
        Assert.Equal(screenData, msg2.Value.Payload);
    }

    /// <summary>
    /// Verifies that negative exit codes are handled correctly.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_NegativeExitCode()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteSessionExitedAsync("signaled-session", -9);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);

        var (sessionId, exitCode) = ProtocolReader.ParseSessionExited(msg.Value.Payload);
        Assert.Equal("signaled-session", sessionId);
        Assert.Equal(-9, exitCode);
    }

    /// <summary>
    /// Verifies that Unicode in error messages is preserved.
    /// </summary>
    [Fact]
    public async Task WriteAndRead_UnicodeError()
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteErrorAsync("エラー: 接続失敗 🔥");

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal("エラー: 接続失敗 🔥", ProtocolReader.ParseError(msg.Value.Payload));
    }

    /// <summary>
    /// Verifies that Hello message with different protocol versions works.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(255)]
    public async Task WriteAndRead_HelloVersions(byte version)
    {
        using var stream = new MemoryStream();

        var writer = new ProtocolWriter(stream);
        await writer.WriteHelloAsync(version);

        stream.Position = 0;

        var reader = new ProtocolReader(stream);
        var msg = await reader.ReadMessageAsync();

        Assert.NotNull(msg);
        Assert.Equal(version, ProtocolReader.ParseHello(msg.Value.Payload));
    }

    #endregion
}
