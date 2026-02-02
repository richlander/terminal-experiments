// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/13state_edit.test
// Focus: Line editing operations (insert/delete character/line)

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for line and character editing CSI sequences.
/// </summary>
public class EditingTests : ParserTestBase
{
    #region ICH - Insert Character (CSI Ps @)

    [Fact]
    public void InsertCharacter_Default()
    {
        // CSI @ - Insert 1 character
        // Source: libvterm t/13state_edit.test line 12
        Parse("\u001b[@");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('@', evt.Final);
        // Zero Default Mode: empty params means default of 0
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void InsertCharacter_Multiple()
    {
        // CSI 3 @ - Insert 3 characters
        // Source: libvterm t/13state_edit.test line 17
        Parse("\u001b[3@");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('@', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region DCH - Delete Character (CSI Ps P)

    [Fact]
    public void DeleteCharacter_Default()
    {
        // CSI P - Delete 1 character
        // Source: libvterm t/13state_edit.test line 37
        Parse("\u001b[P");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('P', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void DeleteCharacter_Multiple()
    {
        // CSI 3 P - Delete 3 characters
        // Source: libvterm t/13state_edit.test line 40
        Parse("\u001b[3P");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('P', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region ECH - Erase Character (CSI Ps X)

    [Fact]
    public void EraseCharacter_Default()
    {
        // CSI X - Erase 1 character
        // Source: libvterm t/13state_edit.test line 61
        Parse("\u001b[X");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('X', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void EraseCharacter_Multiple()
    {
        // CSI 3 X - Erase 3 characters
        // Source: libvterm t/13state_edit.test line 64
        Parse("\u001b[3X");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('X', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    [Fact]
    public void EraseCharacter_LargeCount()
    {
        // CSI 100 X - Should be bounded to line width
        // Source: libvterm t/13state_edit.test line 68
        Parse("\u001b[100X");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('X', evt.Final);
        Assert.Equal(100, evt.Params[0]);
    }

    #endregion

    #region IL - Insert Line (CSI Ps L)

    [Fact]
    public void InsertLine_Default()
    {
        // CSI L - Insert 1 line
        // Source: libvterm t/13state_edit.test line 77
        Parse("\u001b[L");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('L', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void InsertLine_Multiple()
    {
        // CSI 3 L - Insert 3 lines
        // Source: libvterm t/13state_edit.test line 84
        Parse("\u001b[3L");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('L', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region DL - Delete Line (CSI Ps M)

    [Fact]
    public void DeleteLine_Default()
    {
        // CSI M - Delete 1 line
        // Source: libvterm t/13state_edit.test line 110
        Parse("\u001b[M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void DeleteLine_Multiple()
    {
        // CSI 3 M - Delete 3 lines
        // Source: libvterm t/13state_edit.test line 113
        Parse("\u001b[3M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region DECIC - Insert Column (CSI Ps ' })

    [Fact]
    public void InsertColumn()
    {
        // CSI 5 ' } - Insert 5 columns
        // Source: libvterm t/13state_edit.test line 135
        Parse("\u001b[5'}");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('}', evt.Final);
        Assert.Equal(5, evt.Params[0]);
        Assert.Equal('\'', (char)evt.Intermediates);
    }

    #endregion

    #region DECDC - Delete Column (CSI Ps ' ~)

    [Fact]
    public void DeleteColumn()
    {
        // CSI 5 ' ~ - Delete 5 columns
        // Source: libvterm t/13state_edit.test (around line 148)
        Parse("\u001b[5'~");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('~', evt.Final);
        Assert.Equal(5, evt.Params[0]);
        Assert.Equal('\'', (char)evt.Intermediates);
    }

    #endregion

    #region SD - Scroll Down (CSI Ps T)

    [Fact]
    public void ScrollDown_Default()
    {
        // CSI T - Scroll down 1 line
        Parse("\u001b[T");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('T', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void ScrollDown_Multiple()
    {
        // CSI 5 T - Scroll down 5 lines
        Parse("\u001b[5T");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('T', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    #endregion

    #region SU - Scroll Up (CSI Ps S)

    [Fact]
    public void ScrollUp_Default()
    {
        // CSI S - Scroll up 1 line
        Parse("\u001b[S");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('S', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void ScrollUp_Multiple()
    {
        // CSI 5 S - Scroll up 5 lines
        Parse("\u001b[5S");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('S', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    #endregion

    #region REP - Repeat (CSI Ps b)

    [Fact]
    public void RepeatCharacter()
    {
        // CSI 5 b - Repeat previous character 5 times
        Parse("\u001b[5b");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('b', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    #endregion

    #region EL - Erase in Line (CSI Ps K)

    [Fact]
    public void EraseInLine_ToEnd()
    {
        // CSI K or CSI 0 K - Erase from cursor to end of line
        Parse("\u001b[K");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('K', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void EraseInLine_ToBeginning()
    {
        // CSI 1 K - Erase from beginning of line to cursor
        Parse("\u001b[1K");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('K', evt.Final);
        Assert.Equal(1, evt.Params[0]);
    }

    [Fact]
    public void EraseInLine_Entire()
    {
        // CSI 2 K - Erase entire line
        Parse("\u001b[2K");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('K', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    #endregion

    #region ED - Erase in Display (CSI Ps J)

    [Fact]
    public void EraseInDisplay_Below()
    {
        // CSI J or CSI 0 J - Erase from cursor to end of display
        Parse("\u001b[J");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('J', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void EraseInDisplay_Above()
    {
        // CSI 1 J - Erase from beginning of display to cursor
        Parse("\u001b[1J");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('J', evt.Final);
        Assert.Equal(1, evt.Params[0]);
    }

    [Fact]
    public void EraseInDisplay_All()
    {
        // CSI 2 J - Erase entire display
        Parse("\u001b[2J");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('J', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    [Fact]
    public void EraseInDisplay_Scrollback()
    {
        // CSI 3 J - Erase scrollback buffer (xterm extension)
        Parse("\u001b[3J");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('J', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Selective Erase

    [Fact]
    public void SelectiveEraseInLine()
    {
        // CSI ? K - Selective erase in line
        Parse("\u001b[?K");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('K', evt.Final);
        Assert.True(evt.Private);
    }

    [Fact]
    public void SelectiveEraseInDisplay()
    {
        // CSI ? J - Selective erase in display
        Parse("\u001b[?J");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('J', evt.Final);
        Assert.True(evt.Private);
    }

    #endregion
}
