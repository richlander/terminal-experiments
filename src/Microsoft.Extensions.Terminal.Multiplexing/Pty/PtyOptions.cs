// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Options for creating a pseudo-terminal.
/// </summary>
public sealed class PtyOptions
{
    /// <summary>
    /// Gets or sets the command to execute.
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Gets or sets the arguments to pass to the command.
    /// </summary>
    public string[]? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the process.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets additional environment variables for the process.
    /// </summary>
    public IDictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Gets or sets the initial number of columns. Default is 80.
    /// </summary>
    public int Columns { get; set; } = 80;

    /// <summary>
    /// Gets or sets the initial number of rows. Default is 24.
    /// </summary>
    public int Rows { get; set; } = 24;
}
