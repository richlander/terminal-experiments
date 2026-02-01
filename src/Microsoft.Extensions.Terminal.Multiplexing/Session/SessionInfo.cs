// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Information about a managed session.
/// </summary>
/// <param name="Id">The unique session identifier.</param>
/// <param name="Command">The command being executed.</param>
/// <param name="WorkingDirectory">The working directory of the session.</param>
/// <param name="State">The current state of the session.</param>
/// <param name="Created">When the session was created.</param>
/// <param name="ExitCode">The exit code if the session has exited.</param>
/// <param name="Columns">The current terminal width.</param>
/// <param name="Rows">The current terminal height.</param>
public sealed record SessionInfo(
    string Id,
    string Command,
    string? WorkingDirectory,
    SessionState State,
    DateTimeOffset Created,
    int? ExitCode,
    int Columns,
    int Rows);
