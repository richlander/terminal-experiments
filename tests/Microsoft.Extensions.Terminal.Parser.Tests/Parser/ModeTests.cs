// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/25state_input.test and t/15state_mode.test
// Focus: DECSET/DECRST mode handling for bracketed paste, focus events, etc.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for DECSET/DECRST mode sequences.
/// These are CSI ? Ps h (set) and CSI ? Ps l (reset) sequences.
/// </summary>
public class ModeTests : ParserTestBase
{
    #region Bracketed Paste Mode (DECSET 2004)

    [Fact]
    public void BracketedPasteMode_Enable()
    {
        // CSI ? 2004 h - Enable bracketed paste mode
        // Source: libvterm t/25state_input.test line 140
        Parse("\u001b[?2004h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(2004, evt.Params[0]);
    }

    [Fact]
    public void BracketedPasteMode_Disable()
    {
        // CSI ? 2004 l - Disable bracketed paste mode
        Parse("\u001b[?2004l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(2004, evt.Params[0]);
    }

    #endregion

    #region Focus Event Reporting (DECSET 1004)

    [Fact]
    public void FocusReporting_Enable()
    {
        // CSI ? 1004 h - Enable focus reporting
        // Source: libvterm t/25state_input.test line 152
        Parse("\u001b[?1004h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1004, evt.Params[0]);
    }

    [Fact]
    public void FocusReporting_Disable()
    {
        // CSI ? 1004 l - Disable focus reporting
        Parse("\u001b[?1004l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1004, evt.Params[0]);
    }

    #endregion

    #region Cursor Visibility (DECSET 25)

    [Fact]
    public void CursorVisibility_Show()
    {
        // CSI ? 25 h - Show cursor (DECTCEM)
        Parse("\u001b[?25h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(25, evt.Params[0]);
    }

    [Fact]
    public void CursorVisibility_Hide()
    {
        // CSI ? 25 l - Hide cursor
        Parse("\u001b[?25l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(25, evt.Params[0]);
    }

    #endregion

    #region Alternate Screen Buffer (DECSET 1049)

    [Fact]
    public void AlternateScreenBuffer_Enable()
    {
        // CSI ? 1049 h - Switch to alternate screen buffer
        Parse("\u001b[?1049h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1049, evt.Params[0]);
    }

    [Fact]
    public void AlternateScreenBuffer_Disable()
    {
        // CSI ? 1049 l - Switch back to main screen buffer
        Parse("\u001b[?1049l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1049, evt.Params[0]);
    }

    [Fact]
    public void AlternateScreenBuffer_1047_Enable()
    {
        // CSI ? 1047 h - Alternate screen buffer (no cursor save)
        Parse("\u001b[?1047h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1047, evt.Params[0]);
    }

    [Fact]
    public void AlternateScreenBuffer_1048_CursorSave()
    {
        // CSI ? 1048 h - Save cursor position
        Parse("\u001b[?1048h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1048, evt.Params[0]);
    }

    #endregion

    #region Auto-Wrap Mode (DECSET 7)

    [Fact]
    public void AutoWrapMode_Enable()
    {
        // CSI ? 7 h - Enable auto-wrap (DECAWM)
        Parse("\u001b[?7h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(7, evt.Params[0]);
    }

    [Fact]
    public void AutoWrapMode_Disable()
    {
        // CSI ? 7 l - Disable auto-wrap
        Parse("\u001b[?7l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(7, evt.Params[0]);
    }

    #endregion

    #region Origin Mode (DECSET 6)

    [Fact]
    public void OriginMode_Enable()
    {
        // CSI ? 6 h - Enable origin mode (DECOM)
        Parse("\u001b[?6h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(6, evt.Params[0]);
    }

    [Fact]
    public void OriginMode_Disable()
    {
        // CSI ? 6 l - Disable origin mode
        Parse("\u001b[?6l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(6, evt.Params[0]);
    }

    #endregion

    #region Application Cursor Keys (DECSET 1)

    [Fact]
    public void ApplicationCursorKeys_Enable()
    {
        // CSI ? 1 h - Enable application cursor keys (DECCKM)
        Parse("\u001b[?1h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1, evt.Params[0]);
    }

    [Fact]
    public void ApplicationCursorKeys_Disable()
    {
        // CSI ? 1 l - Disable application cursor keys
        Parse("\u001b[?1l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1, evt.Params[0]);
    }

    #endregion

    #region Mouse Tracking Modes

    [Fact]
    public void MouseTracking_1000_Enable()
    {
        // CSI ? 1000 h - Enable mouse click tracking
        Parse("\u001b[?1000h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1000, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_1002_ButtonEvent()
    {
        // CSI ? 1002 h - Enable button-event tracking
        Parse("\u001b[?1002h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1002, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_1003_AnyEvent()
    {
        // CSI ? 1003 h - Enable any-event tracking
        Parse("\u001b[?1003h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1003, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_1006_SGRMode()
    {
        // CSI ? 1006 h - Enable SGR extended mouse mode
        Parse("\u001b[?1006h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1006, evt.Params[0]);
    }

    #endregion

    #region Cursor Blink Mode (DECSET 12)

    [Fact]
    public void CursorBlink_Enable()
    {
        // CSI ? 12 h - Start cursor blinking
        Parse("\u001b[?12h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(12, evt.Params[0]);
    }

    [Fact]
    public void CursorBlink_Disable()
    {
        // CSI ? 12 l - Stop cursor blinking
        Parse("\u001b[?12l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(12, evt.Params[0]);
    }

    #endregion

    #region Multiple Modes in Single Sequence

    [Fact]
    public void MultipleModesInSingleSequence()
    {
        // CSI ? 25 ; 1049 h - Show cursor AND enable alternate screen
        Parse("\u001b[?25;1049h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(25, evt.Params[0]);
        Assert.Equal(1049, evt.Params[1]);
    }

    #endregion

    #region Cursor Shape (DECSCUSR)

    [Fact]
    public void CursorShape_BlinkingBlock()
    {
        // CSI 1 SP q - Blinking block cursor
        Parse("\u001b[1 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(1, evt.Params[0]);
        Assert.Equal(' ', (char)evt.Intermediates);
    }

    [Fact]
    public void CursorShape_SteadyBlock()
    {
        // CSI 2 SP q - Steady block cursor
        Parse("\u001b[2 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(2, evt.Params[0]);
    }

    [Fact]
    public void CursorShape_BlinkingUnderline()
    {
        // CSI 3 SP q - Blinking underline cursor
        Parse("\u001b[3 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    [Fact]
    public void CursorShape_SteadyUnderline()
    {
        // CSI 4 SP q - Steady underline cursor
        Parse("\u001b[4 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(4, evt.Params[0]);
    }

    [Fact]
    public void CursorShape_BlinkingBar()
    {
        // CSI 5 SP q - Blinking bar (beam) cursor
        Parse("\u001b[5 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    [Fact]
    public void CursorShape_SteadyBar()
    {
        // CSI 6 SP q - Steady bar (beam) cursor
        Parse("\u001b[6 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(6, evt.Params[0]);
    }

    [Fact]
    public void CursorShape_Default()
    {
        // CSI 0 SP q - Default cursor shape
        Parse("\u001b[0 q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal(0, evt.Params[0]);
    }

    #endregion

    #region DECSLRM - Left/Right Margin Mode

    [Fact]
    public void LeftRightMarginMode_Enable()
    {
        // CSI ? 69 h - Enable left/right margin mode
        // Source: libvterm t/13state_edit.test line 21
        Parse("\u001b[?69h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(69, evt.Params[0]);
    }

    [Fact]
    public void SetLeftRightMargins()
    {
        // CSI Pl ; Pr s - Set left and right margins (DECSLRM)
        // Note: This conflicts with SCOSC (save cursor) without params
        Parse("\u001b[10;50s");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('s', evt.Final);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(10, evt.Params[0]);
        Assert.Equal(50, evt.Params[1]);
    }

    #endregion
}
