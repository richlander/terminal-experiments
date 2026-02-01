// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Reads protocol messages from a stream.
/// </summary>
internal sealed class ProtocolReader
{
    private readonly Stream _stream;
    private readonly byte[] _headerBuffer = new byte[5];

    public ProtocolReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Reads the next message from the stream.
    /// </summary>
    /// <returns>The message type and payload, or null if the stream is closed.</returns>
    public async ValueTask<(MessageType Type, byte[] Payload)?> ReadMessageAsync(CancellationToken cancellationToken = default)
    {
        // Read header
        int bytesRead = await ReadExactlyAsync(_headerBuffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead == 0)
        {
            return null; // Stream closed
        }

        if (bytesRead < 5)
        {
            throw new ProtocolException("Incomplete message header");
        }

        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(_headerBuffer.AsSpan(0));
        var type = (MessageType)_headerBuffer[4];

        if (payloadLength < 0 || payloadLength > 10 * 1024 * 1024) // 10MB max
        {
            throw new ProtocolException($"Invalid payload length: {payloadLength}");
        }

        // Read payload
        var payload = new byte[payloadLength];
        if (payloadLength > 0)
        {
            bytesRead = await ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            if (bytesRead < payloadLength)
            {
                throw new ProtocolException("Incomplete message payload");
            }
        }

        return (type, payload);
    }

    /// <summary>
    /// Parses a Hello message.
    /// </summary>
    public static byte ParseHello(byte[] payload)
    {
        if (payload.Length < 1)
        {
            throw new ProtocolException("Invalid Hello payload");
        }
        return payload[0];
    }

    /// <summary>
    /// Parses a SessionList message.
    /// </summary>
    public static IReadOnlyList<SessionInfo> ParseSessionList(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        ushort count = reader.ReadUInt16();
        var sessions = new List<SessionInfo>(count);

        for (int i = 0; i < count; i++)
        {
            sessions.Add(ReadSessionInfo(reader));
        }

        return sessions;
    }

    /// <summary>
    /// Parses a CreateSession message.
    /// </summary>
    public static (string Id, PtyOptions Options) ParseCreateSession(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        string id = reader.ReadString();
        string command = reader.ReadString();
        string workingDirectory = reader.ReadString();
        ushort columns = reader.ReadUInt16();
        ushort rows = reader.ReadUInt16();

        ushort argCount = reader.ReadUInt16();
        var args = new string[argCount];
        for (int i = 0; i < argCount; i++)
        {
            args[i] = reader.ReadString();
        }

        ushort envCount = reader.ReadUInt16();
        var env = new Dictionary<string, string>(envCount);
        for (int i = 0; i < envCount; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            env[key] = value;
        }

        var options = new PtyOptions
        {
            Command = command,
            Arguments = args.Length > 0 ? args : null,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
            Environment = env.Count > 0 ? env : null,
            Columns = columns,
            Rows = rows
        };

        return (id, options);
    }

    /// <summary>
    /// Parses a SessionCreated message.
    /// </summary>
    public static SessionInfo ParseSessionCreated(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        return ReadSessionInfo(reader);
    }

    /// <summary>
    /// Parses an Attach message.
    /// </summary>
    public static string ParseAttach(byte[] payload)
    {
        return Encoding.UTF8.GetString(payload);
    }

    /// <summary>
    /// Parses an Attached message.
    /// </summary>
    public static (SessionInfo Session, byte[] BufferedOutput) ParseAttached(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        var session = ReadSessionInfo(reader);
        int outputLength = reader.ReadInt32();
        var output = reader.ReadBytes(outputLength);

        return (session, output);
    }

    /// <summary>
    /// Parses a Resize message.
    /// </summary>
    public static (int Columns, int Rows) ParseResize(byte[] payload)
    {
        if (payload.Length < 4)
        {
            throw new ProtocolException("Invalid Resize payload");
        }

        ushort columns = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0));
        ushort rows = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2));
        return (columns, rows);
    }

    /// <summary>
    /// Parses a KillSession message.
    /// </summary>
    public static (string SessionId, bool Force) ParseKillSession(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        string sessionId = reader.ReadString();
        bool force = reader.ReadBoolean();

        return (sessionId, force);
    }

    /// <summary>
    /// Parses a SessionExited message.
    /// </summary>
    public static (string SessionId, int ExitCode) ParseSessionExited(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        string sessionId = reader.ReadString();
        int exitCode = reader.ReadInt32();

        return (sessionId, exitCode);
    }

    /// <summary>
    /// Parses an Error message.
    /// </summary>
    public static string ParseError(byte[] payload)
    {
        return Encoding.UTF8.GetString(payload);
    }

    private async ValueTask<int> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await _stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return totalRead; // Stream closed
            }
            totalRead += read;
        }
        return totalRead;
    }

    private static SessionInfo ReadSessionInfo(BinaryReader reader)
    {
        string id = reader.ReadString();
        string command = reader.ReadString();
        string workingDirectory = reader.ReadString();
        var state = (SessionState)reader.ReadByte();
        long createdMs = reader.ReadInt64();
        int exitCode = reader.ReadInt32();
        ushort columns = reader.ReadUInt16();
        ushort rows = reader.ReadUInt16();

        return new SessionInfo(
            id,
            command,
            string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
            state,
            DateTimeOffset.FromUnixTimeMilliseconds(createdMs),
            exitCode == -1 ? null : exitCode,
            columns,
            rows);
    }
}

/// <summary>
/// Exception thrown when a protocol error occurs.
/// </summary>
public sealed class ProtocolException : Exception
{
    /// <summary>
    /// Creates a new protocol exception.
    /// </summary>
    public ProtocolException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new protocol exception with an inner exception.
    /// </summary>
    public ProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
