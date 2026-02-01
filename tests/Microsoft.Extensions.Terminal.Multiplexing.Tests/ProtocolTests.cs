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
        Assert.Equal("session-xyz", ProtocolReader.ParseAttach(msg.Value.Payload));
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
}
