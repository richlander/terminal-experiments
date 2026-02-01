// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Represents an attachment to a session for sending input and receiving output.
/// </summary>
public interface ISessionAttachment : IAsyncDisposable
{
    /// <summary>
    /// Gets the session information.
    /// </summary>
    SessionInfo SessionInfo { get; }

    /// <summary>
    /// Gets the buffered output received on attachment.
    /// </summary>
    ReadOnlyMemory<byte> BufferedOutput { get; }

    /// <summary>
    /// Sends input to the session.
    /// </summary>
    ValueTask SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes the terminal.
    /// </summary>
    ValueTask ResizeAsync(int columns, int rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the output stream from the session.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches from the session.
    /// </summary>
    ValueTask DetachAsync(CancellationToken cancellationToken = default);
}
