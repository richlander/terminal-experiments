// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for ScreenBuffer - terminal state tracking.
/// </summary>
public class ScreenBufferTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 24)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Basic Text

    [Fact]
    public void Print_SimpleText_AppearsInBuffer()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "Hello");

        Assert.Equal("Hello", buffer.GetRowText(0));
        Assert.Equal(5, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    [Fact]
    public void Print_MultipleLines_NewlineAdvancesCursor()
    {
        var buffer = CreateBuffer();
        // LF only moves down, CR+LF moves down and to start
        Parse(buffer, "Line1\r\nLine2\r\nLine3");

        Assert.Equal("Line1", buffer.GetRowText(0));
        Assert.Equal("Line2", buffer.GetRowText(1));
        Assert.Equal("Line3", buffer.GetRowText(2));
    }

    [Fact]
    public void Print_CarriageReturn_ResetsCursorX()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "Hello\rWorld");

        Assert.Equal("World", buffer.GetRowText(0));
    }

    [Fact]
    public void Print_Backspace_MovesCursorBack()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABC\bX");

        Assert.Equal("ABX", buffer.GetRowText(0));
    }

    [Fact]
    public void Print_Tab_AdvancesToNextTabStop()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "A\tB");

        Assert.Equal(8, buffer.CursorX - 1); // B is at position 8
    }

    #endregion

    #region Cursor Movement (CSI)

    [Fact]
    public void CursorUp_MovesCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[5;5H"); // Move to row 5, col 5
        Parse(buffer, "\u001b[2A");   // Up 2

        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
    }

    [Fact]
    public void CursorDown_MovesCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[5;5H"); // Move to row 5, col 5
        Parse(buffer, "\u001b[3B");   // Down 3

        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(7, buffer.CursorY);
    }

    [Fact]
    public void CursorForward_MovesCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[5;5H"); // Move to row 5, col 5
        Parse(buffer, "\u001b[3C");   // Forward 3

        Assert.Equal(7, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    [Fact]
    public void CursorBack_MovesCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[5;5H"); // Move to row 5, col 5
        Parse(buffer, "\u001b[2D");   // Back 2

        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    [Fact]
    public void CursorPosition_MovesToAbsolute()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[10;20H");

        Assert.Equal(19, buffer.CursorX); // 0-based
        Assert.Equal(9, buffer.CursorY);
    }

    [Fact]
    public void CursorPosition_DefaultsToHome()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "test\u001b[H");

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    [Fact]
    public void CursorPosition_ClampsToScreenBounds()
    {
        var buffer = CreateBuffer(80, 24);
        Parse(buffer, "\u001b[999;999H");

        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(23, buffer.CursorY);
    }

    #endregion

    #region Erase Operations

    [Fact]
    public void EraseInDisplay_FromCursorToEnd()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC");
        Parse(buffer, "\u001b[2;5H"); // Row 2, Col 5
        Parse(buffer, "\u001b[0J");   // Erase from cursor to end

        Assert.Equal("AAAAAAAAAA", buffer.GetRowText(0));
        Assert.Equal("BBBB", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
    }

    [Fact]
    public void EraseInDisplay_FromStartToCursor()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC");
        Parse(buffer, "\u001b[2;5H"); // Row 2, Col 5
        Parse(buffer, "\u001b[1J");   // Erase from start to cursor

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("BBBBB", buffer.GetRowText(1).TrimStart());
        Assert.Equal("CCCCCCCCCC", buffer.GetRowText(2));
    }

    [Fact]
    public void EraseInDisplay_EntireScreen()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC");
        Parse(buffer, "\u001b[2J");

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
    }

    [Fact]
    public void EraseInLine_FromCursorToEnd()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABCDEFGHIJ");
        Parse(buffer, "\u001b[5G");   // Col 5
        Parse(buffer, "\u001b[0K");   // Erase to end of line

        Assert.Equal("ABCD", buffer.GetRowText(0));
    }

    [Fact]
    public void EraseInLine_FromStartToCursor()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABCDEFGHIJ");
        Parse(buffer, "\u001b[5G");   // Col 5
        Parse(buffer, "\u001b[1K");   // Erase from start

        Assert.Equal("FGHIJ", buffer.GetRowText(0).TrimStart());
    }

    [Fact]
    public void EraseInLine_EntireLine()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABCDEFGHIJ");
        Parse(buffer, "\u001b[2K");

        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion

    #region SGR (Colors and Attributes)

    [Fact]
    public void Sgr_Bold_SetsAttribute()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[1mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    [Fact]
    public void Sgr_ForegroundColor_SetsColor()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[31mX"); // Red

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(1u, cell.Foreground); // Red = 1
    }

    [Fact]
    public void Sgr_BackgroundColor_SetsColor()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[44mX"); // Blue background

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(4u, cell.Background); // Blue = 4
    }

    [Fact]
    public void Sgr_256Color_SetsColor()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[38;5;208mX"); // Orange (256 color)

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(208u, cell.Foreground);
    }

    [Fact]
    public void Sgr_TrueColor_SetsRgb()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[38;2;255;128;64mX"); // RGB

        var cell = buffer.GetCell(0, 0);
        // RGB is stored with high bit set
        Assert.True(cell.Foreground > 0xFFFFFF);
    }

    [Fact]
    public void Sgr_Reset_ClearsAttributes()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[1;31mA\u001b[0mB");

        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);

        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(1u, cellA.Foreground);

        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
    }

    #endregion

    #region Scrolling

    [Fact]
    public void Scroll_AtBottom_ScrollsContent()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "Line1\r\nLine2\r\nLine3\r\nLine4");

        // Line1 should be scrolled off, Line4 should be at bottom
        Assert.Equal("Line2", buffer.GetRowText(0));
        Assert.Equal("Line3", buffer.GetRowText(1));
        Assert.Equal("Line4", buffer.GetRowText(2));
    }

    [Fact]
    public void ScrollUp_ExplicitCommand_Scrolls()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "Line1\r\nLine2\r\nLine3");
        Parse(buffer, "\u001b[1S"); // Scroll up 1

        Assert.Equal("Line2", buffer.GetRowText(0));
        Assert.Equal("Line3", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
    }

    [Fact]
    public void ScrollDown_ExplicitCommand_Scrolls()
    {
        var buffer = CreateBuffer(10, 3);
        Parse(buffer, "Line1\r\nLine2\r\nLine3");
        Parse(buffer, "\u001b[1T"); // Scroll down 1

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("Line1", buffer.GetRowText(1));
        Assert.Equal("Line2", buffer.GetRowText(2));
    }

    [Fact]
    public void SetScrollRegion_LimitsScrolling()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "Line1\r\nLine2\r\nLine3\r\nLine4\r\nLine5");
        Parse(buffer, "\u001b[2;4r");  // Scroll region: rows 2-4
        Parse(buffer, "\u001b[4;1H");  // Move to row 4
        Parse(buffer, "\r\nNewLine"); // Should scroll within region

        Assert.Equal("Line1", buffer.GetRowText(0));    // Outside region
        Assert.Equal("Line3", buffer.GetRowText(1));    // Scrolled up
        Assert.Equal("Line4", buffer.GetRowText(2));    // Scrolled up
        Assert.Equal("NewLine", buffer.GetRowText(3));  // New content
        Assert.Equal("Line5", buffer.GetRowText(4));    // Outside region
    }

    #endregion

    #region Save/Restore Cursor

    [Fact]
    public void SaveRestoreCursor_Esc_WorksCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[10;20H");  // Move cursor
        Parse(buffer, "\u001b7");         // Save (ESC 7)
        Parse(buffer, "\u001b[1;1H");    // Move elsewhere
        Parse(buffer, "\u001b8");         // Restore (ESC 8)

        Assert.Equal(19, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    [Fact]
    public void SaveRestoreCursor_Csi_WorksCorrectly()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[10;20H");  // Move cursor
        Parse(buffer, "\u001b[s");        // Save (CSI s)
        Parse(buffer, "\u001b[1;1H");    // Move elsewhere
        Parse(buffer, "\u001b[u");        // Restore (CSI u)

        Assert.Equal(19, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    #endregion

    #region OSC

    [Fact]
    public void Osc_SetTitle_UpdatesTitle()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b]0;My Window Title\x07");

        Assert.Equal("My Window Title", buffer.Title);
    }

    [Fact]
    public void Osc_SetTitle_Osc2_UpdatesTitle()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b]2;Another Title\u001b\\");

        Assert.Equal("Another Title", buffer.Title);
    }

    #endregion

    #region Modes

    [Fact]
    public void CursorVisible_DefaultTrue()
    {
        var buffer = CreateBuffer();
        Assert.True(buffer.CursorVisible);
    }

    [Fact]
    public void CursorVisible_CanBeHidden()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[?25l"); // Hide cursor

        Assert.False(buffer.CursorVisible);
    }

    [Fact]
    public void CursorVisible_CanBeShown()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[?25l"); // Hide
        Parse(buffer, "\u001b[?25h"); // Show

        Assert.True(buffer.CursorVisible);
    }

    #endregion

    #region Insert/Delete

    [Fact]
    public void InsertCharacters_ShiftsRight()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABCDEF");
        Parse(buffer, "\u001b[3G");   // Col 3
        Parse(buffer, "\u001b[2@");   // Insert 2 chars

        Assert.Equal("AB  CDEF", buffer.GetRowText(0));
    }

    [Fact]
    public void DeleteCharacters_ShiftsLeft()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "ABCDEFGH");
        Parse(buffer, "\u001b[3G");   // Col 3
        Parse(buffer, "\u001b[2P");   // Delete 2 chars

        Assert.Equal("ABEFGH", buffer.GetRowText(0));
    }

    [Fact]
    public void InsertLines_ShiftsDown()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "Line1\r\nLine2\r\nLine3\r\nLine4\r\nLine5");
        Parse(buffer, "\u001b[2;1H"); // Row 2
        Parse(buffer, "\u001b[1L");   // Insert 1 line

        Assert.Equal("Line1", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        Assert.Equal("Line2", buffer.GetRowText(2));
    }

    [Fact]
    public void DeleteLines_ShiftsUp()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "Line1\r\nLine2\r\nLine3\r\nLine4\r\nLine5");
        Parse(buffer, "\u001b[2;1H"); // Row 2
        Parse(buffer, "\u001b[1M");   // Delete 1 line

        Assert.Equal("Line1", buffer.GetRowText(0));
        Assert.Equal("Line3", buffer.GetRowText(1));
        Assert.Equal("Line4", buffer.GetRowText(2));
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsEverything()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\u001b[31mTest\u001b[10;10H");
        
        buffer.Reset();

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    [Fact]
    public void EscC_Reset_ClearsTerminal()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "Test\u001bc");

        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion
}
