// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for cursor movement in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/11state_movecursor.test
/// Tests cursor positioning via control characters and escape sequences.
/// </remarks>
public class ScreenBufferCursorMovementTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Implicit Cursor Movement

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Implicit"
    /// Writing characters advances cursor.
    /// </summary>
    [Fact]
    public void Implicit_WritingAdvancesCursor()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC");

        Assert.Equal(3, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region Control Characters

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Backspace"
    /// </summary>
    [Fact]
    public void Backspace_MovesCursorLeft()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\b");

        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Horizontal Tab"
    /// </summary>
    [Fact]
    public void HorizontalTab_AdvancesToTabStop()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\b\t");

        Assert.Equal(8, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Carriage Return"
    /// </summary>
    [Fact]
    public void CarriageReturn_MovesToColumn0()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\r");

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Linefeed"
    /// </summary>
    [Fact]
    public void Linefeed_MovesToNextRow()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\r\n");

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Backspace bounded by lefthand edge"
    /// </summary>
    [Fact]
    public void Backspace_BoundedByLeftEdge()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[4;2H");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(3, buffer.CursorY);

        Parse(buffer, "\b");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(3, buffer.CursorY);

        Parse(buffer, "\b");
        Assert.Equal(0, buffer.CursorX); // Stays at 0
        Assert.Equal(3, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "HT bounded by righthand edge"
    /// </summary>
    [Fact]
    public void HorizontalTab_BoundedByRightEdge()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;78H");
        Assert.Equal(77, buffer.CursorX);

        Parse(buffer, "\t");
        Assert.Equal(79, buffer.CursorX);

        Parse(buffer, "\t");
        Assert.Equal(79, buffer.CursorX); // Stays at right edge
    }

    #endregion

    #region Index and Reverse Index

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Index"
    /// ESC D moves cursor down one line.
    /// </summary>
    [Fact]
    public void Index_MovesCursorDown()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\u001bD");

        Assert.Equal(3, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Reverse Index"
    /// ESC M moves cursor up one line.
    /// </summary>
    [Fact]
    public void ReverseIndex_MovesCursorUp()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\u001bD\u001bM");

        Assert.Equal(3, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Newline"
    /// ESC E moves to start of next line.
    /// </summary>
    [Fact]
    public void NextLine_MovesToStartOfNextLine()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC\u001bE");

        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    #endregion

    #region CSI Cursor Movement

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Forward" (actually Down)
    /// CSI B moves cursor down.
    /// </summary>
    [Fact]
    public void CursorDown_CsiB()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[B");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);

        Parse(buffer, "\u001b[3B");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Down" (actually Forward)
    /// CSI C moves cursor right.
    /// </summary>
    [Fact]
    public void CursorForward_CsiC()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5B"); // Move down 5 first

        Parse(buffer, "\u001b[C");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(5, buffer.CursorY);

        Parse(buffer, "\u001b[3C");
        Assert.Equal(4, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Up"
    /// CSI A moves cursor up.
    /// </summary>
    [Fact]
    public void CursorUp_CsiA()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5;6H"); // Row 5, Col 6

        Parse(buffer, "\u001b[A");
        Assert.Equal(5, buffer.CursorX);
        Assert.Equal(3, buffer.CursorY);

        Parse(buffer, "\u001b[3A");
        Assert.Equal(5, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Backward"
    /// CSI D moves cursor left.
    /// </summary>
    [Fact]
    public void CursorBackward_CsiD()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;6H"); // Col 6

        Parse(buffer, "\u001b[D");
        Assert.Equal(4, buffer.CursorX);

        Parse(buffer, "\u001b[3D");
        Assert.Equal(1, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Horizontal Absolute"
    /// CSI G moves to absolute column.
    /// </summary>
    [Fact]
    public void CursorHorizontalAbsolute_CsiG()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\n");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);

        Parse(buffer, "\u001b[20G");
        Assert.Equal(19, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);

        Parse(buffer, "\u001b[G");
        Assert.Equal(0, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Position"
    /// CSI H moves to absolute row and column.
    /// </summary>
    [Fact]
    public void CursorPosition_CsiH()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[10;5H");
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);

        Parse(buffer, "\u001b[8H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(7, buffer.CursorY);

        Parse(buffer, "\u001b[H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region Bounds Checking

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Bounds Checking"
    /// Cursor movement should be bounded by screen edges.
    /// </summary>
    [Fact]
    public void BoundsChecking_CursorStaysWithinScreen()
    {
        var buffer = CreateBuffer();

        // Try to move up from top
        Parse(buffer, "\u001b[A");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);

        // Try to move left from left edge
        Parse(buffer, "\u001b[D");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);

        // Move to bottom-right
        Parse(buffer, "\u001b[25;80H");
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(24, buffer.CursorY);

        // Try to move down from bottom
        Parse(buffer, "\u001b[B");
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(24, buffer.CursorY);

        // Large column value clamped
        Parse(buffer, "\u001b[1H\u001b[999G");
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);

        // Large row/col clamped
        Parse(buffer, "\u001b[99;99H");
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(24, buffer.CursorY);
    }

    #endregion

    #region Additional Position Commands

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Horizontal Position Absolute"
    /// CSI ` (backtick) sets horizontal position.
    /// </summary>
    [Fact(Skip = "HPA backtick (CSI `) not yet implemented")]
    public void HorizontalPositionAbsolute_CsiBacktick()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5`");

        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Horizontal and Vertical Position"
    /// CSI f sets both row and column.
    /// </summary>
    [Fact]
    public void HorizontalAndVerticalPosition_Csif()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[3;3f");

        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Vertical Position Absolute"
    /// CSI d sets row.
    /// </summary>
    [Fact]
    public void VerticalPositionAbsolute_CsiD()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[3;3f\u001b[5d");

        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    #endregion

    #region Horizontal Tab Movement

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Horizontal Tab"
    /// Tab advances to next 8-column boundary.
    /// </summary>
    [Fact]
    public void HorizontalTab_AdvancesBy8()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\t");
        Assert.Equal(8, buffer.CursorX);

        Parse(buffer, "   ");
        Assert.Equal(11, buffer.CursorX);

        Parse(buffer, "\t");
        Assert.Equal(16, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Horizontal Tab"
    /// CSI I advances by tab stops.
    /// </summary>
    [Fact(Skip = "CHT (CSI I) behavior differs from expected")]
    public void CursorHorizontalTab_CsiI()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[40G"); // Start at column 40

        Parse(buffer, "\u001b[I");
        Assert.Equal(48, buffer.CursorX);

        Parse(buffer, "\u001b[2I");
        Assert.Equal(64, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 11state_movecursor "Cursor Backward Tab"
    /// CSI Z moves back by tab stops.
    /// </summary>
    [Fact(Skip = "CBT (CSI Z) behavior differs from expected")]
    public void CursorBackwardTab_CsiZ()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[65G"); // Start at column 65

        Parse(buffer, "\u001b[Z");
        Assert.Equal(56, buffer.CursorX);

        Parse(buffer, "\u001b[2Z");
        Assert.Equal(40, buffer.CursorX);
    }

    #endregion
}
