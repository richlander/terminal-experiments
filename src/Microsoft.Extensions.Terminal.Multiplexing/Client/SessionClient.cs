// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Client for connecting to a session host.
/// </summary>
public sealed class SessionClient : ISessionClient
{
    private readonly Stream _stream;
    private readonly ProtocolReader _reader;
    private readonly ProtocolWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly byte _serverVersion;
    private bool _disposed;
    private SessionAttachment? _currentAttachment;

    private SessionClient(Stream stream, byte serverVersion)
    {
        _stream = stream;
        _reader = new ProtocolReader(stream);
        _writer = new ProtocolWriter(stream);
        _serverVersion = serverVersion;
    }

    /// <summary>
    /// Connects to a session host.
    /// </summary>
    /// <param name="uri">The URI of the session host (ws://host:port or pipe://pipename).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A connected session client.</returns>
    public static async Task<SessionClient> ConnectAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        Stream stream;

        if (uri.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(uri), cancellationToken).ConfigureAwait(false);
            stream = new WebSocketStream(ws);
        }
        else if (uri.StartsWith("pipe://", StringComparison.OrdinalIgnoreCase))
        {
            var pipeName = uri[7..]; // Remove "pipe://"
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            stream = pipe;
        }
        else
        {
            throw new ArgumentException($"Unsupported URI scheme: {uri}. Use ws:// or pipe://", nameof(uri));
        }

        var reader = new ProtocolReader(stream);
        var writer = new ProtocolWriter(stream);

        // Read server hello
        var msg = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        if (msg is null || msg.Value.Type != MessageType.Hello)
        {
            stream.Dispose();
            throw new ProtocolException("Expected Hello message from server");
        }

        byte serverVersion = ProtocolReader.ParseHello(msg.Value.Payload);

        // Send client hello
        await writer.WriteHelloAsync(1, cancellationToken).ConfigureAwait(false);

        return new SessionClient(stream, serverVersion);
    }

    /// <inheritdoc/>
    public bool IsConnected => !_disposed;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteListSessionsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        var msg = await _reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        if (msg is null)
        {
            throw new ProtocolException("Connection closed");
        }

        if (msg.Value.Type == MessageType.Error)
        {
            throw new InvalidOperationException(ProtocolReader.ParseError(msg.Value.Payload));
        }

        if (msg.Value.Type != MessageType.SessionList)
        {
            throw new ProtocolException($"Expected SessionList, got {msg.Value.Type}");
        }

        return ProtocolReader.ParseSessionList(msg.Value.Payload);
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> CreateSessionAsync(string id, PtyOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(options);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteCreateSessionAsync(id, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        var msg = await _reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        if (msg is null)
        {
            throw new ProtocolException("Connection closed");
        }

        if (msg.Value.Type == MessageType.Error)
        {
            throw new InvalidOperationException(ProtocolReader.ParseError(msg.Value.Payload));
        }

        if (msg.Value.Type != MessageType.SessionCreated)
        {
            throw new ProtocolException($"Expected SessionCreated, got {msg.Value.Type}");
        }

        return ProtocolReader.ParseSessionCreated(msg.Value.Payload);
    }

    /// <inheritdoc/>
    public async Task<ISessionAttachment> AttachAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (_currentAttachment is not null)
        {
            throw new InvalidOperationException("Already attached to a session. Detach first.");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteAttachAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        var msg = await _reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        if (msg is null)
        {
            throw new ProtocolException("Connection closed");
        }

        if (msg.Value.Type == MessageType.Error)
        {
            throw new InvalidOperationException(ProtocolReader.ParseError(msg.Value.Payload));
        }

        if (msg.Value.Type != MessageType.Attached)
        {
            throw new ProtocolException($"Expected Attached, got {msg.Value.Type}");
        }

        var (session, bufferedOutput) = ProtocolReader.ParseAttached(msg.Value.Payload);
        var attachment = new SessionAttachment(this, session, bufferedOutput);
        _currentAttachment = attachment;
        return attachment;
    }

    /// <inheritdoc/>
    public async Task KillSessionAsync(string sessionId, bool force = false, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteKillSessionAsync(sessionId, force, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal async ValueTask SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteInputAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal async ValueTask SendResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteResizeAsync(columns, rows, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal async ValueTask SendDetachAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteDetachAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        _currentAttachment = null;
    }

    internal async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var msg = await _reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            if (msg is null)
            {
                yield break;
            }

            var (type, payload) = msg.Value;

            switch (type)
            {
                case MessageType.Output:
                    yield return payload;
                    break;

                case MessageType.SessionExited:
                    yield break;

                case MessageType.Error:
                    throw new InvalidOperationException(ProtocolReader.ParseError(payload));

                default:
                    // Ignore unexpected messages
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
        _stream.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
