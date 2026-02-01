// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// The state of a managed session.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// The session is starting up.
    /// </summary>
    Starting,

    /// <summary>
    /// The session is running.
    /// </summary>
    Running,

    /// <summary>
    /// The session has exited normally.
    /// </summary>
    Exited,

    /// <summary>
    /// The session failed to start or crashed.
    /// </summary>
    Failed
}
