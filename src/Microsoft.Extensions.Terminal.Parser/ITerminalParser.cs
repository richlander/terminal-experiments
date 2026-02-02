// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// Stateless parser that processes bytes and dispatches to a handler.
/// </summary>
/// <remarks>
/// Design influenced by:
/// - vte (Rust): Clean state machine with Perform trait
/// - xterm.js: Handler-based dispatch pattern
/// </remarks>
public interface ITerminalParser
{
    /// <summary>
    /// Parse input bytes, dispatching recognized sequences to the handler.
    /// Must handle sequences split across multiple calls.
    /// </summary>
    void Parse(ReadOnlySpan<byte> data);

    /// <summary>
    /// Reset parser state (e.g., abandon incomplete sequence).
    /// </summary>
    void Reset();
}
