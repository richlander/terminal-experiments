// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// Receives parsed terminal events. Implementations record calls for testing
/// or apply effects to a terminal emulator.
/// </summary>
/// <remarks>
/// Design influenced by:
/// - vte (Rust): Perform trait with print/execute/csi_dispatch/etc.
/// - xterm.js: Handler registration pattern
/// </remarks>
public interface IParserHandler
{
    /// <summary>
    /// Printable character to display at cursor.
    /// </summary>
    void Print(char c);

    /// <summary>
    /// C0/C1 control code (e.g., 0x07 bell, 0x08 backspace).
    /// </summary>
    void Execute(byte controlCode);

    /// <summary>
    /// CSI sequence complete.
    /// Example: \x1b[1;5H → params=[1,5], privateMarker=0, intermediates=0, command='H'
    /// </summary>
    /// <remarks>
    /// Empty/omitted params use Zero Default Mode (0) per xterm.js convention.
    /// PrivateMarker encodes characters like '?' or '>' that appear before params.
    /// Intermediates encodes characters like '$' or ' ' that appear after params.
    /// </remarks>
    void CsiDispatch(ReadOnlySpan<int> parameters, byte privateMarker, byte intermediates, char command);

    /// <summary>
    /// ESC sequence (non-CSI).
    /// Example: \x1b7 (save cursor) → intermediates=0, command='7'
    /// </summary>
    void EscDispatch(byte intermediates, char command);

    /// <summary>
    /// OSC (Operating System Command) sequence.
    /// Example: \x1b]0;title\x07 → command=0, data="title"
    /// </summary>
    void OscDispatch(int command, ReadOnlySpan<byte> data);

    /// <summary>
    /// DCS (Device Control String) hook start.
    /// </summary>
    void DcsHook(ReadOnlySpan<int> parameters, byte intermediates, char command);

    /// <summary>
    /// DCS data received.
    /// </summary>
    void DcsPut(byte data);

    /// <summary>
    /// DCS sequence complete.
    /// </summary>
    void DcsUnhook();
}
