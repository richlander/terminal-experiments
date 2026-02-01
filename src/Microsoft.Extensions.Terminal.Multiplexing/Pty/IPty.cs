// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Represents a pseudo-terminal that can spawn and communicate with a child process.
/// </summary>
public interface IPty : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the process ID of the child process.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Gets a value indicating whether the child process has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Gets the exit code of the child process, or null if it hasn't exited.
    /// </summary>
    int? ExitCode { get; }

    /// <summary>
    /// Gets a task that completes when the process exits.
    /// </summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data from the PTY output.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of bytes read, or 0 if the PTY has closed.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to the PTY input.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes the PTY window.
    /// </summary>
    /// <param name="columns">The new width in columns.</param>
    /// <param name="rows">The new height in rows.</param>
    void Resize(int columns, int rows);

    /// <summary>
    /// Terminates the child process.
    /// </summary>
    /// <param name="force">If true, forcefully kills the process. If false, sends a termination signal.</param>
    void Kill(bool force = false);
}
