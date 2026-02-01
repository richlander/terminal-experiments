// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Represents an active attachment to a session.
/// </summary>
internal sealed class SessionAttachment : ISessionAttachment
{
    private readonly SessionClient _client;
    private bool _detached;

    public SessionAttachment(SessionClient client, SessionInfo sessionInfo, byte[] bufferedOutput)
    {
        _client = client;
        SessionInfo = sessionInfo;
        BufferedOutput = bufferedOutput;
    }

    /// <inheritdoc/>
    public SessionInfo SessionInfo { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> BufferedOutput { get; }

    /// <inheritdoc/>
    public async ValueTask SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_detached, this);
        await _client.SendInputAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ResizeAsync(int columns, int rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_detached, this);
        await _client.SendResizeAsync(columns, rows, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var data in _client.ReadOutputAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return data;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DetachAsync(CancellationToken cancellationToken = default)
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        await _client.SendDetachAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_detached)
        {
            await DetachAsync().ConfigureAwait(false);
        }
    }
}
