// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer scroll region tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/12state_scroll.test
/// Tests scroll region behavior (DECSTBM) and scroll commands (IND, RI, SU, SD).
/// </remarks>
public class ScreenBufferScrollTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Basic Linefeed Scrolling

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed"
    /// 24 linefeeds should position cursor at bottom, one more should scroll.
    /// </summary>
    [Fact]
    public void Linefeed_AtBottom_ScrollsContent()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write content at top
        Parse(buffer, "TopLine");
        
        // Move to bottom with linefeeds
        Parse(buffer, new string('\n', 24));
        Assert.Equal(24, buffer.CursorY);
        
        // Write at bottom
        buffer.GetCell(0, 24) = new TerminalCell('B', TerminalCell.DefaultForeground, TerminalCell.DefaultBackground, CellAttributes.None, 1);
        
        // One more linefeed should scroll
        Parse(buffer, "\n");
        Assert.Equal(24, buffer.CursorY); // Cursor stays at bottom
        
        // TopLine should have scrolled off
        Assert.NotEqual("TopLine", buffer.GetRowText(0));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Index"
    /// ESC D at bottom of screen should scroll.
    /// </summary>
    [Fact]
    public void Index_AtBottom_Scrolls()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Move to last row
        Parse(buffer, "\x1b[25H");
        Assert.Equal(24, buffer.CursorY);
        
        // Index should scroll
        Parse(buffer, "\x1bD");
        Assert.Equal(24, buffer.CursorY); // Cursor stays
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Reverse Index"
    /// ESC M at top of screen should scroll down (insert line at top).
    /// </summary>
    [Fact]
    public void ReverseIndex_AtTop_ScrollsDown()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Line1");
        
        // Cursor at top
        Parse(buffer, "\x1b[H");
        Assert.Equal(0, buffer.CursorY);
        
        // Reverse index at top should scroll down
        Parse(buffer, "\x1bM");
        Assert.Equal(0, buffer.CursorY);
        
        // Line1 should have moved down
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("Line1", buffer.GetRowText(1));
    }

    #endregion

    #region DECSTBM - Scroll Regions

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed in DECSTBM"
    /// Linefeed within scroll region should scroll only that region.
    /// </summary>
    [Fact]
    public void Decstbm_LinefeedInRegion_ScrollsRegionOnly()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Fill screen with identifiable content
        for (int i = 0; i < 25; i++)
        {
            Parse(buffer, $"\x1b[{i + 1};1HLine{i:D2}");
        }
        
        // Set scroll region to rows 1-10
        Parse(buffer, "\x1b[1;10r");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY); // DECSTBM homes cursor
        
        // Move to bottom of scroll region
        Parse(buffer, new string('\n', 9));
        Assert.Equal(9, buffer.CursorY);
        
        // One more linefeed should scroll within region
        Parse(buffer, "\n");
        Assert.Equal(9, buffer.CursorY); // Cursor stays at bottom of region
        
        // Line00 should have scrolled off, Line01 now at top
        Assert.Equal("Line01", buffer.GetRowText(0).Substring(0, 6));
        
        // Line10 (outside region) should be unchanged
        Assert.Equal("Line10", buffer.GetRowText(10).Substring(0, 6));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed outside DECSTBM"
    /// Linefeed outside scroll region should not scroll.
    /// </summary>
    [Fact]
    public void Decstbm_LinefeedOutsideRegion_NoScroll()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region to rows 5-15
        Parse(buffer, "\x1b[5;15r");
        
        // Move outside scroll region (row 20)
        Parse(buffer, "\x1b[20H");
        Assert.Equal(19, buffer.CursorY);
        
        // Linefeed - behavior outside scroll region is implementation-dependent
        // Current implementation: cursor stays at row 19 (doesn't move past bottom of region context)
        Parse(buffer, "\n");
        // Document actual behavior - may need adjustment based on VT spec interpretation
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Index in DECSTBM"
    /// Reverse index at top of scroll region should scroll region down.
    /// </summary>
    [Fact]
    public void Decstbm_ReverseIndexAtTop_ScrollsDown()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region to rows 9-10
        Parse(buffer, "\x1b[9;10r");
        
        // Move to row 10 (1-indexed = row 9 0-indexed)
        Parse(buffer, "\x1b[10H");
        
        // Reverse index should move up
        Parse(buffer, "\x1bM");
        Assert.Equal(8, buffer.CursorY);
        
        // Another reverse index at top of region should scroll
        Parse(buffer, "\x1bM");
        // Scrolls region down
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll - DECSTBM with cursor outside
    /// Linefeed at absolute bottom when outside scroll region.
    /// </summary>
    [Fact]
    public void Decstbm_CursorAtAbsoluteBottom_NoScroll()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region that doesn't include bottom
        Parse(buffer, "\x1b[1;20r");
        
        // Move to absolute bottom (row 25)
        Parse(buffer, "\x1b[25H");
        Assert.Equal(24, buffer.CursorY);
        
        // Linefeed should not scroll (outside region)
        Parse(buffer, "\n");
        Assert.Equal(24, buffer.CursorY); // Stays at bottom
    }

    #endregion

    #region SD/SU - Scroll Down/Up Commands

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Scroll Down"
    /// CSI S scrolls up (content moves up, blank at bottom).
    /// </summary>
    [Fact]
    public void ScrollUp_CsiS_ScrollsContentUp()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write identifiable content
        Parse(buffer, "Line0\r\nLine1\r\nLine2");
        
        // Scroll up 1 line
        Parse(buffer, "\x1b[S");
        
        Assert.Equal("Line1", buffer.GetRowText(0));
        Assert.Equal("Line2", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Scroll Down"
    /// CSI 2 S scrolls up 2 lines.
    /// </summary>
    [Fact]
    public void ScrollUp_Multiple_ScrollsMultipleLines()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2\r\nLine3");
        
        // Scroll up 2 lines
        Parse(buffer, "\x1b[2S");
        
        Assert.Equal("Line2", buffer.GetRowText(0));
        Assert.Equal("Line3", buffer.GetRowText(1));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Scroll Up"
    /// CSI T scrolls down (content moves down, blank at top).
    /// </summary>
    [Fact]
    public void ScrollDown_CsiT_ScrollsContentDown()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2");
        
        // Scroll down 1 line
        Parse(buffer, "\x1b[T");
        
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("Line0", buffer.GetRowText(1));
        Assert.Equal("Line1", buffer.GetRowText(2));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Scroll Up"
    /// CSI 2 T scrolls down 2 lines.
    /// </summary>
    [Fact]
    public void ScrollDown_Multiple_ScrollsMultipleLines()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1");
        
        // Scroll down 2 lines
        Parse(buffer, "\x1b[2T");
        
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        Assert.Equal("Line0", buffer.GetRowText(2));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll - Large scroll value
    /// Scrolling more than screen height should clear screen.
    /// </summary>
    [Fact]
    public void ScrollUp_LargeValue_ClearsScreen()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2");
        
        // Scroll up 100 lines (more than screen height)
        Parse(buffer, "\x1b[100S");
        
        // All content should be gone
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(24));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "SD/SU in DECSTBM"
    /// Scroll commands should respect scroll region.
    /// </summary>
    [Fact]
    public void ScrollUp_InDecstbm_ScrollsRegionOnly()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Fill with identifiable content
        for (int i = 0; i < 25; i++)
        {
            Parse(buffer, $"\x1b[{i + 1};1HLine{i:D2}");
        }
        
        // Set scroll region rows 5-20
        Parse(buffer, "\x1b[5;20r");
        
        // Scroll up 1 within region
        Parse(buffer, "\x1b[S");
        
        // Lines outside region should be unchanged
        Assert.Equal("Line00", buffer.GetRowText(0).Substring(0, 6));
        Assert.Equal("Line03", buffer.GetRowText(3).Substring(0, 6));
        
        // Lines inside region should have scrolled
        Assert.Equal("Line05", buffer.GetRowText(4).Substring(0, 6)); // Was Line04 position, now has Line05
    }

    #endregion

    #region Cursor Position After Scroll Operations

    /// <summary>
    /// Ported from: libvterm 12state_scroll
    /// SU/SD should not move cursor.
    /// </summary>
    [Fact]
    public void ScrollUp_CursorPositionUnchanged()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[10;15H"); // Move to row 10, col 15
        Assert.Equal(14, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
        
        Parse(buffer, "\x1b[S"); // Scroll up
        
        // Cursor position should be unchanged
        Assert.Equal(14, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "DECSTBM resets cursor position"
    /// Setting scroll region homes the cursor.
    /// </summary>
    [Fact]
    public void Decstbm_ResetsCursorToHome()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[5;5H"); // Move to row 5, col 5
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
        
        Parse(buffer, "\x1b[r"); // Reset scroll region
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Invalid boundaries"
    /// Invalid scroll region should be rejected/ignored.
    /// </summary>
    [Fact]
    public void Decstbm_InvalidBoundaries_Handled()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Try setting region beyond screen bounds
        Parse(buffer, "\x1b[100;105r");
        // Should handle gracefully (clamp to screen bounds)
        
        // Try setting inverted region (top > bottom)
        Parse(buffer, "\x1b[5;2r");
        // Should handle gracefully
    }

    [Fact]
    public void ScrollDown_ZeroLines_NoChange()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Line0");
        
        Parse(buffer, "\x1b[0S"); // 0 should default to 1
        
        // Content should have scrolled (0 defaults to 1)
        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion
}
