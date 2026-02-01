// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Options for configuring a session host.
/// </summary>
public sealed class SessionHostOptions
{
    /// <summary>
    /// Gets or sets the port for the WebSocket server. Default is 7777.
    /// Set to 0 to disable the WebSocket server.
    /// </summary>
    public int WebSocketPort { get; set; } = 7777;

    /// <summary>
    /// Gets or sets the name of the named pipe for local IPC.
    /// Set to null to disable the named pipe server.
    /// </summary>
    public string? PipeName { get; set; } = "termhost";

    /// <summary>
    /// Gets or sets the maximum number of sessions. Default is 100.
    /// </summary>
    public int MaxSessions { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default output buffer size in bytes. Default is 64KB.
    /// </summary>
    public int DefaultBufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the protocol version. Default is 1.
    /// </summary>
    public byte ProtocolVersion { get; set; } = 1;
}
