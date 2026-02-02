// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/11state_movecursor.test
// Focus: Cursor movement sequences

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Additional cursor movement tests from libvterm.
/// </summary>
public class CursorMovementTests : ParserTestBase
{
    #region Index / Reverse Index

    [Fact]
    public void Index_MoveDown()
    {
        // ESC D - Index (move cursor down one line, scroll if at bottom)
        // Source: libvterm t/11state_movecursor.test line 48
        Parse("\u001bD");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('D', evt.Final);
    }

    [Fact]
    public void ReverseIndex_MoveUp()
    {
        // ESC M - Reverse Index (move cursor up one line)
        // Source: libvterm t/11state_movecursor.test line 51
        Parse("\u001bM");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void NextLine()
    {
        // ESC E - Next Line (move to column 0 of next line)
        // Source: libvterm t/11state_movecursor.test line 54
        Parse("\u001bE");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('E', evt.Final);
    }

    #endregion

    #region Cursor Next/Previous Line (CNL/CPL)

    [Fact]
    public void CursorNextLine_Default()
    {
        // CSI E - Cursor Next Line (default 1)
        // Source: libvterm t/11state_movecursor.test line 94
        Parse("\u001b[E");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('E', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorNextLine_Multiple()
    {
        // CSI 2 E - Move down 2 lines to column 0
        // Source: libvterm t/11state_movecursor.test line 98
        Parse("\u001b[2E");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('E', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    [Fact]
    public void CursorPreviousLine_Default()
    {
        // CSI F - Cursor Previous Line (default 1)
        // Source: libvterm t/11state_movecursor.test line 106
        Parse("\u001b[F");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('F', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorPreviousLine_Multiple()
    {
        // CSI 2 F - Move up 2 lines to column 0
        // Source: libvterm t/11state_movecursor.test line 110
        Parse("\u001b[2F");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('F', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region Horizontal Position Absolute (HPA)

    [Fact]
    public void HorizontalPositionAbsolute()
    {
        // CSI Ps ` - Horizontal Position Absolute
        // Source: libvterm t/11state_movecursor.test line 169
        Parse("\u001b[5`");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('`', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    #endregion

    #region Horizontal Position Relative (HPR)

    [Fact]
    public void HorizontalPositionRelative()
    {
        // CSI Ps a - Horizontal Position Relative
        // Source: libvterm t/11state_movecursor.test line 173
        Parse("\u001b[3a");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('a', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Horizontal Position Backward (HPB)

    [Fact]
    public void HorizontalPositionBackward()
    {
        // CSI Ps j - Horizontal Position Backward
        // Source: libvterm t/11state_movecursor.test line 177
        Parse("\u001b[3j");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('j', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Horizontal and Vertical Position (HVP)

    [Fact]
    public void HorizontalVerticalPosition()
    {
        // CSI Ps ; Ps f - Horizontal and Vertical Position
        // Source: libvterm t/11state_movecursor.test line 181
        Parse("\u001b[3;3f");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('f', evt.Final);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(3, evt.Params[0]);
        Assert.Equal(3, evt.Params[1]);
    }

    #endregion

    #region Vertical Position Absolute (VPA)

    [Fact]
    public void VerticalPositionAbsolute()
    {
        // CSI Ps d - Vertical Position Absolute
        // Source: libvterm t/11state_movecursor.test line 185
        Parse("\u001b[5d");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('d', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    #endregion

    #region Vertical Position Relative (VPR)

    [Fact]
    public void VerticalPositionRelative()
    {
        // CSI Ps e - Vertical Position Relative
        // Source: libvterm t/11state_movecursor.test line 189
        Parse("\u001b[2e");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('e', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region Vertical Position Backward (VPB)

    [Fact]
    public void VerticalPositionBackward()
    {
        // CSI Ps k - Vertical Position Backward
        // Source: libvterm t/11state_movecursor.test line 193
        Parse("\u001b[2k");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('k', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region Cursor Horizontal Tab (CHT)

    [Fact]
    public void CursorHorizontalTab_Default()
    {
        // CSI I - Cursor forward to next tab stop
        // Source: libvterm t/11state_movecursor.test line 215
        Parse("\u001b[I");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('I', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorHorizontalTab_Multiple()
    {
        // CSI 2 I - Cursor forward 2 tab stops
        // Source: libvterm t/11state_movecursor.test line 217
        Parse("\u001b[2I");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('I', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region Cursor Backward Tab (CBT)

    [Fact]
    public void CursorBackwardTab_Default()
    {
        // CSI Z - Cursor backward to previous tab stop
        // Source: libvterm t/11state_movecursor.test line 221
        Parse("\u001b[Z");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('Z', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorBackwardTab_Multiple()
    {
        // CSI 2 Z - Cursor backward 2 tab stops
        // Source: libvterm t/11state_movecursor.test line 223
        Parse("\u001b[2Z");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('Z', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region Cursor Horizontal Absolute (CHA)

    [Fact]
    public void CursorHorizontalAbsolute()
    {
        // CSI Ps G - Cursor Horizontal Absolute
        // Source: libvterm t/11state_movecursor.test line 118
        Parse("\u001b[20G");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('G', evt.Final);
        Assert.Equal(20, evt.Params[0]);
    }

    [Fact]
    public void CursorHorizontalAbsolute_Default()
    {
        // CSI G - Default to column 1
        // Source: libvterm t/11state_movecursor.test line 120
        Parse("\u001b[G");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('G', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    #endregion

    #region Tab Clear (TBC)

    [Fact]
    public void TabClear_Current()
    {
        // CSI 0 g - Clear tab stop at current position
        Parse("\u001b[g");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('g', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void TabClear_All()
    {
        // CSI 3 g - Clear all tab stops
        Parse("\u001b[3g");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('g', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Tab Set (HTS)

    [Fact]
    public void HorizontalTabSet()
    {
        // ESC H - Set tab stop at current position
        Parse("\u001bH");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('H', evt.Final);
    }

    #endregion

    #region Save and Restore Cursor (DECSC/DECRC)

    [Fact]
    public void SaveCursor()
    {
        // ESC 7 - Save cursor position
        Parse("\u001b7");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('7', evt.Final);
    }

    [Fact]
    public void RestoreCursor()
    {
        // ESC 8 - Restore cursor position
        Parse("\u001b8");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('8', evt.Final);
    }

    [Fact]
    public void SaveCursor_ANSI()
    {
        // CSI s - ANSI save cursor (SCOSC)
        Parse("\u001b[s");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('s', evt.Final);
    }

    [Fact]
    public void RestoreCursor_ANSI()
    {
        // CSI u - ANSI restore cursor (SCORC)
        Parse("\u001b[u");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('u', evt.Final);
    }

    #endregion
}
