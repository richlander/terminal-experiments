// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/21state_tabstops.test
// Focus: Horizontal tab stops, HTS, TBC

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for tab stop sequences.
/// </summary>
public class TabStopTests : ParserTestBase
{
    #region HTS - Horizontal Tab Set (ESC H)

    [Fact]
    public void HorizontalTabSet()
    {
        // ESC H - Set tab stop at current column
        // Source: libvterm t/21state_tabstops.test line 13
        Parse("\u001bH");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('H', evt.Final);
    }

    [Fact]
    public void HTS_AfterMovingCursor()
    {
        // Move to column 5 then set tab stop
        // Source: libvterm t/21state_tabstops.test line 13
        Parse("\u001b[5G\u001bH");

        var events = Handler.Events.ToList();
        
        // CUP to column 5
        var csi = events.OfType<CsiEvent>().First();
        Assert.Equal('G', csi.Final);
        Assert.Equal(5, csi.Params[0]);

        // HTS
        var esc = events.OfType<EscEvent>().First();
        Assert.Equal('H', esc.Final);
    }

    #endregion

    #region TBC - Tab Clear (CSI Ps g)

    [Fact]
    public void TabClear_AtCursor()
    {
        // CSI g or CSI 0 g - Clear tab stop at current position
        // Source: libvterm t/21state_tabstops.test line 19
        Parse("\u001b[g");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('g', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void TabClear_AtCursor_Explicit()
    {
        // CSI 0 g - Explicitly clear tab stop at current position
        Parse("\u001b[0g");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('g', evt.Final);
        Assert.Equal(0, evt.Params[0]);
    }

    [Fact]
    public void TabClear_AllStops()
    {
        // CSI 3 g - Clear all tab stops
        // Source: libvterm t/21state_tabstops.test line 25
        Parse("\u001b[3g");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('g', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region CHT - Cursor Horizontal Tab (CSI Ps I)

    [Fact]
    public void CursorHorizontalTab_Default()
    {
        // CSI I - Move cursor to next tab stop
        Parse("\u001b[I");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('I', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorHorizontalTab_Multiple()
    {
        // CSI 3 I - Move cursor forward 3 tab stops
        Parse("\u001b[3I");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('I', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region CBT - Cursor Backward Tab (CSI Ps Z)

    [Fact]
    public void CursorBackwardTab_Default()
    {
        // CSI Z - Move cursor to previous tab stop
        Parse("\u001b[Z");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('Z', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void CursorBackwardTab_Multiple()
    {
        // CSI 3 Z - Move cursor backward 3 tab stops
        Parse("\u001b[3Z");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('Z', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Horizontal Tab (HT - 0x09)

    [Fact]
    public void HorizontalTab_Control()
    {
        // Tab character moves cursor to next tab stop
        Parse("\t");

        var evt = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(0x09, evt.Code);
    }

    [Fact]
    public void HorizontalTab_WithText()
    {
        // Text followed by tab
        Parse("ABC\tD");

        var events = Handler.Events.ToList();
        
        // 4 print events for ABCD
        var prints = events.OfType<PrintEvent>().ToList();
        Assert.Equal(4, prints.Count);
        
        // 1 execute for tab
        var exec = Assert.Single(events.OfType<ExecuteEvent>());
        Assert.Equal(0x09, exec.Code);
        
        Assert.Equal('D', prints.Last().Char);
    }

    #endregion

    #region Tab Sequence Combinations

    [Fact]
    public void SetTabClearTab()
    {
        // Set a tab stop, then clear it
        // ESC H (set), then ESC [g (clear at cursor)
        Parse("\u001bH\u001b[g");

        var events = Handler.Events.ToList();
        
        var escEvents = events.OfType<EscEvent>().ToList();
        Assert.Single(escEvents);
        Assert.Equal('H', escEvents[0].Final);
        
        var csiEvents = events.OfType<CsiEvent>().ToList();
        Assert.Single(csiEvents);
        Assert.Equal('g', csiEvents[0].Final);
    }

    [Fact]
    public void ClearAllThenSetNew()
    {
        // Clear all tab stops, then set a new one
        // CSI 3 g (clear all), then ESC H (set)
        Parse("\u001b[3g\u001bH");

        var events = Handler.Events.ToList();
        
        var csiEvents = events.OfType<CsiEvent>().ToList();
        Assert.Single(csiEvents);
        Assert.Equal('g', csiEvents[0].Final);
        Assert.Equal(3, csiEvents[0].Params[0]);
        
        var escEvents = events.OfType<EscEvent>().ToList();
        Assert.Single(escEvents);
        Assert.Equal('H', escEvents[0].Final);
    }

    #endregion
}
