// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for terminal query responses.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/26state_query.test
/// Tests for DA, DSR, CPR, and other query/response sequences.
/// 
/// NOTE: Query response functionality is not yet implemented in ScreenBuffer.
/// These tests are skipped until output/response mechanism is available.
/// </remarks>
public class QueryResponseTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region DA - Device Attributes

    /// <summary>
    /// Ported from: libvterm 26state_query "DA"
    /// CSI c requests primary device attributes.
    /// Expected response: CSI ? 1 ; 2 c
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Da_RequestDeviceAttributes()
    {
        var buffer = CreateBuffer();

        // CSI c = DA (primary device attributes request)
        Parse(buffer, "\u001b[c");

        // Expected output: \e[?1;2c
        // Cannot verify without output mechanism
    }

    #endregion

    #region DSR - Device Status Report

    /// <summary>
    /// Ported from: libvterm 26state_query "DSR"
    /// CSI 5 n requests terminal status.
    /// Expected response: CSI 0 n (terminal OK)
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Dsr_RequestTerminalStatus()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5n");

        // Expected output: \e[0n
    }

    #endregion

    #region CPR - Cursor Position Report

    /// <summary>
    /// Ported from: libvterm 26state_query "CPR"
    /// CSI 6 n requests cursor position.
    /// Expected response: CSI row ; col R
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Cpr_RequestCursorPosition_Initial()
    {
        var buffer = CreateBuffer();

        // Cursor at 1,1 (1-indexed)
        Parse(buffer, "\u001b[6n");

        // Expected output: \e[1;1R
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "CPR" (cursor moved)
    /// Cursor position after movement should be reported correctly.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Cpr_RequestCursorPosition_AfterMove()
    {
        var buffer = CreateBuffer();

        // Move cursor and request position
        Parse(buffer, "\u001b[10;10H\u001b[6n");

        // Expected output: \e[10;10R
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECCPR"
    /// CSI ? 6 n requests DEC-specific cursor position.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Deccpr_RequestCursorPosition()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[10;10H\u001b[?6n");

        // Expected output: \e[?10;10R
    }

    #endregion

    #region XTVERSION - Terminal Version

    /// <summary>
    /// Ported from: libvterm 26state_query "XTVERSION"
    /// CSI > q requests terminal version.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Xtversion_RequestTerminalVersion()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[>q");

        // Expected output: DCS > | version ST
    }

    #endregion

    #region DECRQSS - Request Selection or Setting

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on DECSCUSR"
    /// Request cursor style setting.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestCursorStyle()
    {
        var buffer = CreateBuffer();

        // Set cursor style, then request it
        Parse(buffer, "\u001b[3 q");
        Parse(buffer, "\u001bP$q q\u001b\\");

        // Expected output: DCS 1 $ r 3 SP q ST
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on SGR"
    /// Request SGR (graphic rendition) settings.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestSgr()
    {
        var buffer = CreateBuffer();

        // Set attributes, then request
        Parse(buffer, "\u001b[1;5;7m");
        Parse(buffer, "\u001bP$qm\u001b\\");

        // Expected output: DCS 1 $ r 1;5;7 m ST
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on SGR ANSI colours"
    /// Request SGR with ANSI colors.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestSgr_AnsiColors()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[0;31;42m");
        Parse(buffer, "\u001bP$qm\u001b\\");

        // Expected output: DCS 1 $ r 31;42 m ST
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on SGR ANSI hi-bright colours"
    /// Request SGR with bright ANSI colors.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestSgr_BrightAnsiColors()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[0;93;104m");
        Parse(buffer, "\u001bP$qm\u001b\\");

        // Expected output: DCS 1 $ r 93;104 m ST
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on SGR 256-palette colours"
    /// Request SGR with 256-color palette.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestSgr_256Colors()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[0;38:5:56;48:5:78m");
        Parse(buffer, "\u001bP$qm\u001b\\");

        // Expected output: DCS 1 $ r 38:5:56;48:5:78 m ST
    }

    /// <summary>
    /// Ported from: libvterm 26state_query "DECRQSS on SGR RGB8 colours"
    /// Request SGR with RGB true color.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void Decrqss_RequestSgr_RgbColors()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[0;38:2:24:68:112;48:2:13:57:101m");
        Parse(buffer, "\u001bP$qm\u001b\\");

        // Expected output: DCS 1 $ r 38:2:24:68:112;48:2:13:57:101 m ST
    }

    #endregion

    #region S8C1T - 8-bit C1 Control Characters

    /// <summary>
    /// Ported from: libvterm 26state_query "S8C1T on DSR"
    /// When S8C1T mode is enabled, responses use 8-bit C1 controls.
    /// </summary>
    [Fact(Skip = "Query response output not implemented")]
    public void S8c1t_DsrUsesEightBitControls()
    {
        var buffer = CreateBuffer();

        // ESC SP G enables S8C1T
        Parse(buffer, "\u001b G");
        Parse(buffer, "\u001b[5n");

        // Expected output: 0x9b 0x30 0x6e (CSI as 0x9b instead of ESC [)

        // ESC SP F disables S8C1T
        Parse(buffer, "\u001b F");
    }

    #endregion
}
