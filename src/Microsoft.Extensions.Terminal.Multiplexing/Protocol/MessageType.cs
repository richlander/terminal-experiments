// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Types of messages in the session multiplexing protocol.
/// </summary>
internal enum MessageType : byte
{
    /// <summary>
    /// Initial handshake message with protocol version.
    /// </summary>
    Hello = 0,

    /// <summary>
    /// List available sessions.
    /// </summary>
    ListSessions = 1,

    /// <summary>
    /// Response with session list.
    /// </summary>
    SessionList = 2,

    /// <summary>
    /// Create a new session.
    /// </summary>
    CreateSession = 3,

    /// <summary>
    /// Response to session creation.
    /// </summary>
    SessionCreated = 4,

    /// <summary>
    /// Attach to an existing session.
    /// </summary>
    Attach = 5,

    /// <summary>
    /// Confirmation of attachment.
    /// </summary>
    Attached = 6,

    /// <summary>
    /// Detach from the current session.
    /// </summary>
    Detach = 7,

    /// <summary>
    /// Send input to the attached session.
    /// </summary>
    Input = 8,

    /// <summary>
    /// Output from the attached session.
    /// </summary>
    Output = 9,

    /// <summary>
    /// Resize the terminal window.
    /// </summary>
    Resize = 10,

    /// <summary>
    /// Kill a session.
    /// </summary>
    KillSession = 11,

    /// <summary>
    /// Session has exited.
    /// </summary>
    SessionExited = 12,

    /// <summary>
    /// Error message.
    /// </summary>
    Error = 255
}
