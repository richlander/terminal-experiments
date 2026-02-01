// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Writes protocol messages to a stream.
/// </summary>
/// <remarks>
/// Message format:
/// - 4 bytes: payload length (big-endian)
/// - 1 byte: message type
/// - N bytes: payload (depends on message type)
/// </remarks>
internal sealed class ProtocolWriter
{
    private readonly Stream _stream;

    public ProtocolWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Writes a Hello message with protocol version.
    /// </summary>
    public async ValueTask WriteHelloAsync(byte version, CancellationToken cancellationToken = default)
    {
        await WriteMessageAsync(MessageType.Hello, new byte[] { version }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a ListSessions request.
    /// </summary>
    public async ValueTask WriteListSessionsAsync(CancellationToken cancellationToken = default)
    {
        await WriteMessageAsync(MessageType.ListSessions, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a SessionList response.
    /// </summary>
    public async ValueTask WriteSessionListAsync(IReadOnlyList<SessionInfo> sessions, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write((ushort)sessions.Count);
        foreach (var session in sessions)
        {
            WriteSessionInfo(writer, session);
        }

        await WriteMessageAsync(MessageType.SessionList, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a CreateSession request.
    /// </summary>
    public async ValueTask WriteCreateSessionAsync(string id, PtyOptions options, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write(id);
        writer.Write(options.Command);
        writer.Write(options.WorkingDirectory ?? string.Empty);
        writer.Write((ushort)options.Columns);
        writer.Write((ushort)options.Rows);

        // Arguments
        var args = options.Arguments ?? [];
        writer.Write((ushort)args.Length);
        foreach (var arg in args)
        {
            writer.Write(arg);
        }

        // Environment
        var env = options.Environment ?? new Dictionary<string, string>();
        writer.Write((ushort)env.Count);
        foreach (var kvp in env)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }

        await WriteMessageAsync(MessageType.CreateSession, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a SessionCreated response.
    /// </summary>
    public async ValueTask WriteSessionCreatedAsync(SessionInfo session, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        WriteSessionInfo(writer, session);

        await WriteMessageAsync(MessageType.SessionCreated, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an Attach request.
    /// </summary>
    public async ValueTask WriteAttachAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var payload = Encoding.UTF8.GetBytes(sessionId);
        await WriteMessageAsync(MessageType.Attach, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an Attached response with buffered output.
    /// </summary>
    public async ValueTask WriteAttachedAsync(SessionInfo session, ReadOnlyMemory<byte> bufferedOutput, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        WriteSessionInfo(writer, session);
        writer.Write(bufferedOutput.Length);
        writer.Write(bufferedOutput.Span);

        await WriteMessageAsync(MessageType.Attached, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a Detach request.
    /// </summary>
    public async ValueTask WriteDetachAsync(CancellationToken cancellationToken = default)
    {
        await WriteMessageAsync(MessageType.Detach, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes input data.
    /// </summary>
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await WriteMessageAsync(MessageType.Input, data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes output data.
    /// </summary>
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await WriteMessageAsync(MessageType.Output, data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a Resize message.
    /// </summary>
    public async ValueTask WriteResizeAsync(int columns, int rows, CancellationToken cancellationToken = default)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), (ushort)columns);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), (ushort)rows);
        await WriteMessageAsync(MessageType.Resize, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a KillSession request.
    /// </summary>
    public async ValueTask WriteKillSessionAsync(string sessionId, bool force, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write(sessionId);
        writer.Write(force);

        await WriteMessageAsync(MessageType.KillSession, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a SessionExited notification.
    /// </summary>
    public async ValueTask WriteSessionExitedAsync(string sessionId, int exitCode, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write(sessionId);
        writer.Write(exitCode);

        await WriteMessageAsync(MessageType.SessionExited, ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an Error message.
    /// </summary>
    public async ValueTask WriteErrorAsync(string message, CancellationToken cancellationToken = default)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        await WriteMessageAsync(MessageType.Error, payload, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteMessageAsync(MessageType type, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        // Header: 4 bytes length + 1 byte type
        var header = new byte[5];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), payload.Length);
        header[4] = (byte)type;

        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (payload.Length > 0)
        {
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteSessionInfo(BinaryWriter writer, SessionInfo session)
    {
        writer.Write(session.Id);
        writer.Write(session.Command);
        writer.Write(session.WorkingDirectory ?? string.Empty);
        writer.Write((byte)session.State);
        writer.Write(session.Created.ToUnixTimeMilliseconds());
        writer.Write(session.ExitCode ?? -1);
        writer.Write((ushort)session.Columns);
        writer.Write((ushort)session.Rows);
    }
}
