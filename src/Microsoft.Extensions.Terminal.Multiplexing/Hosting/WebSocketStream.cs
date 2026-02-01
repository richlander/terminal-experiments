// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Wraps a WebSocket as a Stream for use with protocol reader/writer.
/// </summary>
internal sealed class WebSocketStream : Stream
{
    private readonly WebSocket _webSocket;
    private readonly byte[] _receiveBuffer = new byte[8192];
    private int _receiveBufferOffset;
    private int _receiveBufferCount;

    public WebSocketStream(WebSocket webSocket)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Return buffered data first
        if (_receiveBufferCount > 0)
        {
            int toCopy = Math.Min(_receiveBufferCount, buffer.Length);
            _receiveBuffer.AsSpan(_receiveBufferOffset, toCopy).CopyTo(buffer.Span);
            _receiveBufferOffset += toCopy;
            _receiveBufferCount -= toCopy;
            return toCopy;
        }

        // Read from WebSocket
        var result = await _webSocket.ReceiveAsync(_receiveBuffer, cancellationToken).ConfigureAwait(false);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return 0;
        }

        int bytesToReturn = Math.Min(result.Count, buffer.Length);
        _receiveBuffer.AsSpan(0, bytesToReturn).CopyTo(buffer.Span);

        // Buffer any remaining data
        if (result.Count > bytesToReturn)
        {
            _receiveBufferOffset = bytesToReturn;
            _receiveBufferCount = result.Count - bytesToReturn;
        }

        return bytesToReturn;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    public override void Flush()
    {
        // WebSocket flushes on each send
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Use ReadAsync");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Use WriteAsync");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webSocket.Dispose();
        }
        base.Dispose(disposing);
    }
}
