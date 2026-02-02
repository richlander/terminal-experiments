// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// Parser state machine states, following Paul Williams' VT500 model.
/// </summary>
/// <remarks>
/// State names and transitions based on:
/// - https://vt100.net/emu/dec_ansi_parser
/// - vte (Rust) and xterm.js implementations
/// </remarks>
internal enum ParserState
{
    /// <summary>
    /// Initial state. Printable chars are printed, C0 controls are executed.
    /// </summary>
    Ground,

    /// <summary>
    /// Entered after receiving ESC (0x1B).
    /// </summary>
    Escape,

    /// <summary>
    /// Collecting intermediate bytes (0x20-0x2F) after ESC.
    /// </summary>
    EscapeIntermediate,

    /// <summary>
    /// Entered after ESC [ (CSI). Ready to collect params or intermediates.
    /// </summary>
    CsiEntry,

    /// <summary>
    /// Collecting CSI parameters (digits and semicolons).
    /// </summary>
    CsiParam,

    /// <summary>
    /// Collecting CSI intermediate bytes (0x20-0x2F).
    /// </summary>
    CsiIntermediate,

    /// <summary>
    /// CSI sequence is invalid, ignore until final byte.
    /// </summary>
    CsiIgnore,

    /// <summary>
    /// Collecting OSC string data after ESC ].
    /// </summary>
    OscString,

    /// <summary>
    /// Entered after ESC P (DCS). Ready to collect params.
    /// </summary>
    DcsEntry,

    /// <summary>
    /// Collecting DCS parameters.
    /// </summary>
    DcsParam,

    /// <summary>
    /// Collecting DCS intermediate bytes.
    /// </summary>
    DcsIntermediate,

    /// <summary>
    /// Passing through DCS data to handler.
    /// </summary>
    DcsPassthrough,

    /// <summary>
    /// DCS sequence is invalid, ignore until ST.
    /// </summary>
    DcsIgnore,

    /// <summary>
    /// Collecting SOS, PM, or APC string (ignored in most implementations).
    /// </summary>
    SosPmApcString,
}
