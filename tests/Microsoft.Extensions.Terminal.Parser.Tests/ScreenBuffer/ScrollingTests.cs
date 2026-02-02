// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for scrolling in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/12state_scroll.test
/// Tests scroll region behavior (DECSTBM) and scroll commands (IND, RI, SU, SD).
/// </remarks>
public class ScrollingTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Linefeed Scrolling

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed"
    /// 24 linefeeds position cursor at bottom, one more scrolls.
    /// </summary>
    [Fact]
    public void Linefeed_24Times_PositionsAtBottom()
    {
        var buffer = CreateBuffer();

        // 24 linefeeds
        Parse(buffer, new string('\n', 24));

        Assert.Equal(24, buffer.CursorY);
        Assert.Equal(0, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed"
    /// One more linefeed from bottom should scroll.
    /// </summary>
    [Fact]
    public void Linefeed_AtBottom_Scrolls()
    {
        var buffer = CreateBuffer();

        // Write content at top
        Parse(buffer, "TopLine");

        // Move to bottom
        Parse(buffer, new string('\n', 24));
        Assert.Equal(24, buffer.CursorY);

        // One more linefeed should scroll
        Parse(buffer, "\n");
        Assert.Equal(24, buffer.CursorY); // Cursor stays at bottom

        // TopLine should have scrolled off
        Assert.NotEqual("TopLine", buffer.GetRowText(0));
    }

    #endregion

    #region Index (IND)

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Index"
    /// ESC D at bottom of screen should scroll.
    /// </summary>
    [Fact]
    public void Index_AtBottom_Scrolls()
    {
        var buffer = CreateBuffer();

        // Move to last row
        Parse(buffer, "\u001b[25H");
        Assert.Equal(24, buffer.CursorY);

        // Index should scroll
        Parse(buffer, "\u001bD");
        Assert.Equal(24, buffer.CursorY);
    }

    /// <summary>
    /// ESC D not at bottom moves cursor down.
    /// </summary>
    [Fact]
    public void Index_NotAtBottom_MovesCursorDown()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\u001bD");

        Assert.Equal(3, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    #endregion

    #region Reverse Index (RI)

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Reverse Index"
    /// ESC M at top of screen should scroll down.
    /// </summary>
    [Fact]
    public void ReverseIndex_AtTop_ScrollsDown()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "Line1");

        // Cursor at top
        Parse(buffer, "\u001b[H");
        Assert.Equal(0, buffer.CursorY);

        // Reverse index at top should scroll down
        Parse(buffer, "\u001bM");
        Assert.Equal(0, buffer.CursorY);

        // Line1 should have moved down
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("Line1", buffer.GetRowText(1));
    }

    #endregion

    #region DECSTBM - Scroll Regions

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed in DECSTBM"
    /// Setting scroll region homes cursor.
    /// </summary>
    [Fact]
    public void Decstbm_SetRegion_HomesCursor()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5;5H"); // Move away from origin
        Parse(buffer, "\u001b[1;10r"); // Set scroll region

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Linefeed in DECSTBM"
    /// Linefeed within scroll region scrolls only that region.
    /// </summary>
    [Fact]
    public void Decstbm_LinefeedInRegion_ScrollsRegionOnly()
    {
        var buffer = CreateBuffer();

        // Fill screen with identifiable content
        for (int i = 0; i < 25; i++)
        {
            Parse(buffer, $"\u001b[{i + 1};1HLine{i:D2}");
        }

        // Set scroll region to rows 1-10
        Parse(buffer, "\u001b[1;10r");
        Assert.Equal(0, buffer.CursorY);

        // Move to bottom of scroll region
        Parse(buffer, new string('\n', 9));
        Assert.Equal(9, buffer.CursorY);

        // One more linefeed should scroll within region
        Parse(buffer, "\n");
        Assert.Equal(9, buffer.CursorY);

        // Line00 should have scrolled off, Line01 now at top
        Assert.Equal("Line01", buffer.GetRowText(0).Substring(0, 6));

        // Line10 (outside region) should be unchanged
        Assert.Equal("Line10", buffer.GetRowText(10).Substring(0, 6));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll "DECSTBM resets cursor position"
    /// Setting scroll region homes the cursor.
    /// </summary>
    [Fact]
    public void Decstbm_ResetsCursorToHome()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5;5H");
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);

        Parse(buffer, "\u001b[r"); // Reset scroll region

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region SU/SD - Scroll Up/Down Commands

    /// <summary>
    /// Ported from: libvterm 12state_scroll "Scroll Down"
    /// CSI S scrolls up (content moves up, blank at bottom).
    /// </summary>
    [Fact]
    public void ScrollUp_CsiS_ScrollsContentUp()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "Line0\r\nLine1\r\nLine2");

        // Scroll up 1 line
        Parse(buffer, "\u001b[S");

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
        var buffer = CreateBuffer();

        Parse(buffer, "Line0\r\nLine1\r\nLine2\r\nLine3");

        // Scroll up 2 lines
        Parse(buffer, "\u001b[2S");

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
        var buffer = CreateBuffer();

        Parse(buffer, "Line0\r\nLine1\r\nLine2");

        // Scroll down 1 line
        Parse(buffer, "\u001b[T");

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("Line0", buffer.GetRowText(1));
        Assert.Equal("Line1", buffer.GetRowText(2));
    }

    /// <summary>
    /// Ported from: libvterm 12state_scroll
    /// SU/SD should not move cursor.
    /// </summary>
    [Fact]
    public void ScrollUp_CursorPositionUnchanged()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[10;15H");
        Assert.Equal(14, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);

        Parse(buffer, "\u001b[S");

        Assert.Equal(14, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    #endregion

    #region Scroll with Content

    /// <summary>
    /// Scrolling preserves content correctly.
    /// </summary>
    [Fact]
    public void Scroll_PreservesContentCorrectly()
    {
        var buffer = CreateBuffer(80, 5);

        Parse(buffer, "Row1\r\nRow2\r\nRow3\r\nRow4\r\nRow5");

        Assert.Equal("Row1", buffer.GetRowText(0));
        Assert.Equal("Row2", buffer.GetRowText(1));
        Assert.Equal("Row3", buffer.GetRowText(2));
        Assert.Equal("Row4", buffer.GetRowText(3));
        Assert.Equal("Row5", buffer.GetRowText(4));

        // Scroll up 1
        Parse(buffer, "\u001b[S");

        Assert.Equal("Row2", buffer.GetRowText(0));
        Assert.Equal("Row3", buffer.GetRowText(1));
        Assert.Equal("Row4", buffer.GetRowText(2));
        Assert.Equal("Row5", buffer.GetRowText(3));
        Assert.Equal("", buffer.GetRowText(4));
    }

    #endregion
}
