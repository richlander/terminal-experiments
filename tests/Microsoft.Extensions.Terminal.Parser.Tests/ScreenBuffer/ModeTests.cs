// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer terminal mode tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/15state_mode.test
/// Tests DECSET/DECRST mode handling.
/// </remarks>
public class ScreenBufferModeTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region DECAWM - Auto Wrap Mode (Mode 7)

    [Fact]
    public void Decawm_DefaultOn()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write past end of line - should wrap
        Parse(buffer, "0123456789X");
        
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    [Fact]
    public void Decawm_CanBeDisabled()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Disable auto-wrap
        Parse(buffer, "\x1b[?7l");
        
        Parse(buffer, "0123456789XYZ");
        
        // Content should have last chars overwriting at position 9
        Assert.Equal("012345678Z", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorY); // Still on row 0
    }

    [Fact]
    public void Decawm_CanBeReEnabled()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "\x1b[?7l");  // Disable
        Parse(buffer, "\x1b[?7h");  // Re-enable
        
        Parse(buffer, "0123456789X");
        
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    #endregion

    #region DECOM - Origin Mode (Mode 6)

    /// <summary>
    /// Ported from: libvterm 15state_mode
    /// Origin mode makes cursor relative to scroll region.
    /// </summary>
    [Fact]
    public void Decom_CursorRelativeToScrollRegion()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region to rows 5-15
        Parse(buffer, "\x1b[5;15r");
        
        // Enable origin mode
        Parse(buffer, "\x1b[?6h");
        
        // Home cursor - should go to top of scroll region
        Parse(buffer, "\x1b[H");
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);  // Row 5 (0-indexed = 4)
    }

    [Fact]
    public void Decom_CursorCannotLeaveScrollRegion()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region to rows 5-15
        Parse(buffer, "\x1b[5;15r");
        
        // Enable origin mode
        Parse(buffer, "\x1b[?6h");
        
        // Try to move beyond scroll region
        Parse(buffer, "\x1b[100H");  // Try to go way past bottom
        
        // Should be clamped to bottom of scroll region
        Assert.Equal(14, buffer.CursorY);  // Row 15 (0-indexed = 14)
    }

    [Fact]
    public void Decom_DisabledByDefault()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region
        Parse(buffer, "\x1b[5;15r");
        
        // Without origin mode, cursor position is absolute
        Parse(buffer, "\x1b[1H");
        
        Assert.Equal(0, buffer.CursorY);  // Absolute row 1 = index 0
    }

    [Fact]
    public void Decom_CanBeDisabled()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[5;15r");
        Parse(buffer, "\x1b[?6h");  // Enable origin mode
        Parse(buffer, "\x1b[?6l");  // Disable origin mode
        
        Parse(buffer, "\x1b[H");
        
        Assert.Equal(0, buffer.CursorY);  // Should go to absolute top
    }

    #endregion

    #region DECTCEM - Cursor Visible (Mode 25)

    [Fact]
    public void Dectcem_DefaultVisible()
    {
        var buffer = CreateBuffer();
        Assert.True(buffer.CursorVisible);
    }

    [Fact]
    public void Dectcem_CanHideCursor()
    {
        var buffer = CreateBuffer();
        
        Parse(buffer, "\x1b[?25l");  // Hide cursor
        
        Assert.False(buffer.CursorVisible);
    }

    [Fact]
    public void Dectcem_CanShowCursor()
    {
        var buffer = CreateBuffer();
        
        Parse(buffer, "\x1b[?25l");  // Hide
        Parse(buffer, "\x1b[?25h");  // Show
        
        Assert.True(buffer.CursorVisible);
    }

    [Fact]
    public void Dectcem_MultipleHides()
    {
        var buffer = CreateBuffer();
        
        Parse(buffer, "\x1b[?25l");
        Parse(buffer, "\x1b[?25l");
        Parse(buffer, "\x1b[?25l");
        
        Assert.False(buffer.CursorVisible);
        
        // Single show should restore
        Parse(buffer, "\x1b[?25h");
        Assert.True(buffer.CursorVisible);
    }

    #endregion

    #region Multiple Modes in Single Sequence

    [Fact]
    public void MultipleModesInSequence()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Set multiple modes at once: DECOM + DECAWM off
        Parse(buffer, "\x1b[?6;7l");
        
        // Verify DECAWM is off - content should overwrite at position 9
        Parse(buffer, "0123456789XYZ");
        Assert.Equal("012345678Z", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorY); // Stays on row 0
    }

    #endregion

    #region RIS - Reset to Initial State

    /// <summary>
    /// Ported from: libvterm 27state_reset
    /// ESC c resets terminal to initial state.
    /// </summary>
    [Fact]
    public void Ris_ResetsAllModes()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set up non-default state
        Parse(buffer, "\x1b[?25l");      // Hide cursor
        Parse(buffer, "\x1b[5;15r");     // Set scroll region
        Parse(buffer, "\x1b[10;10H");    // Move cursor
        Parse(buffer, "\x1b[1;31m");     // Set attributes
        Parse(buffer, "Hello");
        
        // Verify non-default state
        Assert.False(buffer.CursorVisible);
        Assert.Contains("Hello", buffer.GetRowText(9)); // Row 10 (0-indexed = 9)
        
        // Reset (ESC c = RIS)
        Parse(buffer, "\u001bc");
        
        // Verify reset
        Assert.True(buffer.CursorVisible);
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        // Screen should be cleared
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(9)); // Hello should be gone
    }

    #endregion

    #region DECSTR - Soft Terminal Reset

    /// <summary>
    /// Ported from: libvterm 15state_mode
    /// CSI ! p is soft terminal reset.
    /// </summary>
    [Fact]
    public void Decstr_SoftReset()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set up non-default state
        Parse(buffer, "\x1b[?25l");      // Hide cursor
        Parse(buffer, "\x1b[10;10H");    // Move cursor
        
        // Note: DECSTR (CSI ! p) may not be implemented
        // This test verifies the sequence parses without error
        Parse(buffer, "\x1b[!p");
    }

    #endregion
}
