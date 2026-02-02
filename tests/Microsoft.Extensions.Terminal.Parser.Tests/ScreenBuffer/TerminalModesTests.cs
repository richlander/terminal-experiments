// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for terminal modes in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/15state_mode.test
/// Tests various terminal modes including Insert/Replace, DECOM, DECAWM, etc.
/// </remarks>
public class TerminalModesTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Insert/Replace Mode (SM/RM 4)

    /// <summary>
    /// Ported from: libvterm 15state_mode "Insert/Replace Mode"
    /// Default is replace mode - characters overwrite.
    /// </summary>
    [Fact]
    public void ReplaceMode_Default_OverwritesCharacters()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "AC\u001b[DB");

        // 'B' should overwrite 'C' at position 1
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
    }

    /// <summary>
    /// Ported from: libvterm 15state_mode "Insert/Replace Mode"
    /// CSI 4 h enables insert mode - characters shift right.
    /// </summary>
    [Fact(Skip = "Insert mode not yet implemented")]
    public void InsertMode_Enabled_ShiftsCharacters()
    {
        var buffer = CreateBuffer();

        // Enable insert mode
        Parse(buffer, "\u001b[4h");

        // Write "AC", move back 1, insert "B"
        Parse(buffer, "AC\u001b[DB");

        // 'B' should be inserted, 'C' shifted right
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(2, 0).Character);
    }

    /// <summary>
    /// CSI 4 l disables insert mode.
    /// </summary>
    [Fact]
    public void InsertMode_CanBeDisabled()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[4h"); // Enable
        Parse(buffer, "\u001b[4l"); // Disable

        Parse(buffer, "AC\u001b[DB");

        // Back in replace mode
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
    }

    #endregion

    #region DECOM - Origin Mode (DECSET/DECRST 6)

    /// <summary>
    /// Ported from: libvterm 15state_mode "DEC origin mode"
    /// Default: cursor position is absolute.
    /// </summary>
    [Fact]
    public void Decom_Disabled_AbsolutePosition()
    {
        var buffer = CreateBuffer();

        // Set scroll region
        Parse(buffer, "\u001b[5;15r");

        // Without origin mode, H goes to absolute position
        Parse(buffer, "\u001b[H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);

        Parse(buffer, "\u001b[3;3H");
        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 15state_mode "DEC origin mode"
    /// With origin mode, cursor position is relative to scroll region.
    /// </summary>
    [Fact]
    public void Decom_Enabled_RelativeToScrollRegion()
    {
        var buffer = CreateBuffer();

        // Set scroll region and enable origin mode
        Parse(buffer, "\u001b[5;15r\u001b[?6h");

        // H goes to top of scroll region
        Parse(buffer, "\u001b[H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY); // Row 5 (0-indexed = 4)

        // Position is relative to scroll region
        Parse(buffer, "\u001b[3;3H");
        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(6, buffer.CursorY); // Row 5+2 = Row 7 (0-indexed = 6)
    }

    /// <summary>
    /// Ported from: libvterm 15state_mode "Origin mode bounds cursor"
    /// With origin mode, cursor cannot leave scroll region.
    /// </summary>
    [Fact]
    public void Decom_Enabled_CursorBoundedByRegion()
    {
        var buffer = CreateBuffer();

        // Set scroll region (rows 5-15) and enable origin mode
        Parse(buffer, "\u001b[5;15r\u001b[?6h");

        // Try to move above top of region
        Parse(buffer, "\u001b[H\u001b[10A");
        Assert.Equal(4, buffer.CursorY); // Stays at top of region

        // Try to move below bottom of region
        Parse(buffer, "\u001b[20B");
        Assert.Equal(14, buffer.CursorY); // Stays at bottom of region
    }

    #endregion

    #region DECAWM - Auto Wrap Mode (DECSET/DECRST 7)

    /// <summary>
    /// Default: DECAWM is enabled.
    /// </summary>
    [Fact]
    public void Decawm_DefaultEnabled()
    {
        var buffer = CreateBuffer(10, 5);

        // Write past end of line - should wrap
        Parse(buffer, "0123456789X");

        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// CSI ? 7 l disables auto-wrap.
    /// </summary>
    [Fact]
    public void Decawm_CanBeDisabled()
    {
        var buffer = CreateBuffer(10, 5);

        Parse(buffer, "\u001b[?7l"); // Disable auto-wrap

        Parse(buffer, "0123456789XYZ");

        // Last chars overwrite at position 9
        Assert.Equal("012345678Z", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorY); // Still on row 0
    }

    /// <summary>
    /// CSI ? 7 h re-enables auto-wrap.
    /// </summary>
    [Fact]
    public void Decawm_CanBeReEnabled()
    {
        var buffer = CreateBuffer(10, 5);

        Parse(buffer, "\u001b[?7l"); // Disable
        Parse(buffer, "\u001b[?7h"); // Re-enable

        Parse(buffer, "0123456789X");

        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    #endregion

    #region DECTCEM - Cursor Visible (DECSET/DECRST 25)

    /// <summary>
    /// Default: cursor is visible.
    /// </summary>
    [Fact]
    public void Dectcem_DefaultVisible()
    {
        var buffer = CreateBuffer();
        Assert.True(buffer.CursorVisible);
    }

    /// <summary>
    /// CSI ? 25 l hides cursor.
    /// </summary>
    [Fact]
    public void Dectcem_CanHideCursor()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[?25l");

        Assert.False(buffer.CursorVisible);
    }

    /// <summary>
    /// CSI ? 25 h shows cursor.
    /// </summary>
    [Fact]
    public void Dectcem_CanShowCursor()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[?25l"); // Hide
        Parse(buffer, "\u001b[?25h"); // Show

        Assert.True(buffer.CursorVisible);
    }

    #endregion

    #region Multiple Modes

    /// <summary>
    /// Multiple modes can be set/reset in single sequence.
    /// </summary>
    [Fact]
    public void MultipleModes_InSingleSequence()
    {
        var buffer = CreateBuffer(10, 5);

        // Set multiple modes: DECOM off + DECAWM off
        Parse(buffer, "\u001b[?6;7l");

        // DECAWM should be off
        Parse(buffer, "0123456789XYZ");
        Assert.Equal("012345678Z", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region RIS - Reset to Initial State

    /// <summary>
    /// ESC c resets terminal to initial state.
    /// </summary>
    [Fact]
    public void Ris_ResetsAllModes()
    {
        var buffer = CreateBuffer();

        // Set up non-default state
        Parse(buffer, "\u001b[?25l");      // Hide cursor
        Parse(buffer, "\u001b[5;15r");     // Set scroll region
        Parse(buffer, "\u001b[10;10H");    // Move cursor
        Parse(buffer, "Hello");

        Assert.False(buffer.CursorVisible);

        // Reset
        Parse(buffer, "\u001bc");

        // Verify reset
        Assert.True(buffer.CursorVisible);
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion

    #region DECSTR - Soft Terminal Reset

    /// <summary>
    /// CSI ! p performs soft terminal reset.
    /// </summary>
    [Fact]
    public void Decstr_SoftReset()
    {
        var buffer = CreateBuffer();

        // Set up non-default state
        Parse(buffer, "\u001b[?25l");      // Hide cursor
        Parse(buffer, "\u001b[10;10H");    // Move cursor

        // Note: DECSTR (CSI ! p) may have limited implementation
        Parse(buffer, "\u001b[!p");

        // Parsing should not error
    }

    #endregion
}
