// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Interface for connecting to a session host.
/// </summary>
public interface ISessionClient : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Lists available sessions.
    /// </summary>
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new session.
    /// </summary>
    Task<SessionInfo> CreateSessionAsync(string id, PtyOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches to an existing session.
    /// </summary>
    Task<ISessionAttachment> AttachAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kills a session.
    /// </summary>
    Task KillSessionAsync(string sessionId, bool force = false, CancellationToken cancellationToken = default);
}
