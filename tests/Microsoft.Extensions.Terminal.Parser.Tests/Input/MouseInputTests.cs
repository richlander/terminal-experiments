// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from:
// - Windows Terminal src/terminal/adapter/ut_adapter/MouseInputTest.cpp
// - libvterm t/17state_mouse.test
// - xterm.js src/browser/input/Mouse.test.ts

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for mouse input escape sequence parsing.
/// Covers X10, Normal (1000), Button (1002), Any (1003), SGR (1006), and UTF-8 (1005) modes.
/// </summary>
public class MouseInputTests : ParserTestBase
{
    #region Mouse Tracking Mode DECSET/DECRST

    [Fact]
    public void MouseTracking_X10Mode_Enable()
    {
        // CSI ? 9 h - Enable X10 mouse reporting (press only)
        Parse("\u001b[?9h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(9, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_X10Mode_Disable()
    {
        // CSI ? 9 l - Disable X10 mouse reporting
        Parse("\u001b[?9l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(9, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_NormalMode_Enable()
    {
        // CSI ? 1000 h - Enable normal mouse tracking (press/release)
        // Source: libvterm t/17state_mouse.test line 17
        Parse("\u001b[?1000h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1000, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_NormalMode_Disable()
    {
        // CSI ? 1000 l - Disable normal mouse tracking
        Parse("\u001b[?1000l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1000, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_ButtonEventMode_Enable()
    {
        // CSI ? 1002 h - Enable button-event tracking (press/release/motion while pressed)
        // Source: libvterm t/17state_mouse.test line 76
        Parse("\u001b[?1002h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1002, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_ButtonEventMode_Disable()
    {
        // CSI ? 1002 l - Disable button-event tracking
        Parse("\u001b[?1002l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1002, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_AnyEventMode_Enable()
    {
        // CSI ? 1003 h - Enable any-event tracking (all motion)
        // Source: libvterm t/17state_mouse.test line 102
        Parse("\u001b[?1003h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1003, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_AnyEventMode_Disable()
    {
        // CSI ? 1003 l - Disable any-event tracking
        Parse("\u001b[?1003l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1003, evt.Params[0]);
    }

    #endregion

    #region Mouse Encoding Mode DECSET/DECRST

    [Fact]
    public void MouseEncoding_Utf8Mode_Enable()
    {
        // CSI ? 1005 h - Enable UTF-8 extended coordinate encoding
        // Source: libvterm t/17state_mouse.test line 134
        Parse("\u001b[?1005h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1005, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_Utf8Mode_Disable()
    {
        // CSI ? 1005 l - Disable UTF-8 extended encoding
        Parse("\u001b[?1005l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1005, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_SgrMode_Enable()
    {
        // CSI ? 1006 h - Enable SGR extended mouse mode
        // Source: libvterm t/17state_mouse.test line 149
        Parse("\u001b[?1006h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1006, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_SgrMode_Disable()
    {
        // CSI ? 1006 l - Disable SGR extended mouse mode
        Parse("\u001b[?1006l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1006, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_UrxvtMode_Enable()
    {
        // CSI ? 1015 h - Enable urxvt extended mouse mode
        // Source: libvterm t/17state_mouse.test line 164
        Parse("\u001b[?1015h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1015, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_UrxvtMode_Disable()
    {
        // CSI ? 1015 l - Disable urxvt extended mouse mode
        Parse("\u001b[?1015l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1015, evt.Params[0]);
    }

    #endregion

    #region Default Encoding Mouse Reports (CSI M Cb Cx Cy)

    [Fact]
    public void DefaultEncoding_LeftButtonPress_Origin()
    {
        // ESC [ M <button> <x+33> <y+33> - Left button press at (0,0)
        // Source: libvterm t/17state_mouse.test line 22-23 - output "\e[M\x20\x21\x21"
        // Button byte: 0x20 = 32 = ' ' = left button down
        // X,Y bytes: 0x21 = 33 = '!' = coordinate 0 (0+1+32=33)
        Parse("\u001b[M !!");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Empty(evt.Params);  // Raw mouse report - params are in the trailing bytes
    }

    [Fact]
    public void DefaultEncoding_LeftButtonRelease_Origin()
    {
        // ESC [ M <button> <x+33> <y+33> - Button release at (0,0)
        // Source: libvterm t/17state_mouse.test line 26-27 - output "\e[M\x23\x21\x21"
        // Button byte: 0x23 = 35 = '#' = button release (3)
        Parse("\u001b[M#!!");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_MiddleButtonPress()
    {
        // Source: libvterm t/17state_mouse.test line 36-37 - output "\e[M\x21\x21\x21"
        // Button byte: 0x21 = 33 = '!' = middle button (1)
        Parse("\u001b[M!!!");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_RightButtonPress()
    {
        // Button byte: 0x22 = 34 = '"' = right button (2)
        Parse("\u001b[M\"!!");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_PositionAt10_20()
    {
        // Mouse at position (10, 20) - coords encoded as x+33, y+33
        // Source: libvterm t/17state_mouse.test line 43-44 - MOUSEMOVE 10,20 output "\e[M\x20\x35\x2b"
        // X: 10+1+32 = 43 = 0x2B = '+'
        // Y: 20+1+32 = 53 = 0x35 = '5'
        Parse("\u001b[M 5+");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_WheelUp()
    {
        // Source: libvterm t/17state_mouse.test line 52-53 - output "\e[M\x60\x36\x2b"
        // Button byte: 0x60 = 96 = '`' = wheel up (64)
        Parse("\u001b[M`6+");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_WheelDown()
    {
        // Source: libvterm t/17state_mouse.test line 56-57 - output "\e[M\x61\x36\x2b"
        // Button byte: 0x61 = 97 = 'a' = wheel down (65)
        Parse("\u001b[Ma6+");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_WheelLeft()
    {
        // Source: libvterm t/17state_mouse.test line 58-59 - output "\e[M\x62\x36\x2b"
        // Button byte: 0x62 = 98 = 'b' = wheel left (66)
        Parse("\u001b[Mb6+");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_WheelRight()
    {
        // Source: libvterm t/17state_mouse.test line 60-61 - output "\e[M\x63\x36\x2b"
        // Button byte: 0x63 = 99 = 'c' = wheel right (67)
        Parse("\u001b[Mc6+");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_CtrlLeftButtonPress()
    {
        // Source: libvterm t/17state_mouse.test line 29-30 - output "\e[M\x30\x21\x21"
        // Button byte: 0x30 = 48 = '0' = left button + ctrl (0 + 16 = 16 + 32 = 48)
        Parse("\u001b[M0!!");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_DragEvent()
    {
        // Source: libvterm t/17state_mouse.test line 82-83 - output "\e[M\x40\x27\x26"
        // Button byte: 0x40 = 64 = '@' = drag (32 + left button)
        Parse("\u001b[M@'&");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_MotionWithNoButton()
    {
        // Source: libvterm t/17state_mouse.test line 105-106 - output "\e[M\x43\x29\x27"
        // Button byte: 0x43 = 67 = 'C' = motion with no button (32 + 3)
        Parse("\u001b[MC)'");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    [Fact]
    public void DefaultEncoding_MaxCoordinate()
    {
        // Source: libvterm t/17state_mouse.test line 117-118 - output "\e[M\x43\xff\xff"
        // Max coordinates (255 - 33 = 222 for x and y)
        Parse("\u001b[MC\xff\xff");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
    }

    #endregion

    #region SGR Extended Encoding (CSI < Pb ; Px ; Py M/m)

    [Fact]
    public void SgrEncoding_LeftButtonPress()
    {
        // CSI < 0 ; x ; y M - Left button press
        // Source: libvterm t/17state_mouse.test line 150-151 - output "\e[<0;301;301M"
        Parse("\u001b[<0;1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal('<', (char)evt.PrivateMarker);
        Assert.Equal(3, evt.Params.Length);
        Assert.Equal(0, evt.Params[0]);  // button 0 = left
        Assert.Equal(1, evt.Params[1]);  // x coordinate
        Assert.Equal(1, evt.Params[2]);  // y coordinate
    }

    [Fact]
    public void SgrEncoding_LeftButtonRelease()
    {
        // CSI < 0 ; x ; y m - Left button release (lowercase m)
        // Source: libvterm t/17state_mouse.test line 152-153 - output "\e[<0;301;301m"
        Parse("\u001b[<0;1;1m");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('m', evt.Final);
        Assert.Equal('<', (char)evt.PrivateMarker);
        Assert.Equal(3, evt.Params.Length);
        Assert.Equal(0, evt.Params[0]);
    }

    [Fact]
    public void SgrEncoding_MiddleButtonPress()
    {
        // CSI < 1 ; x ; y M - Middle button press
        Parse("\u001b[<1;5;10M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal('<', (char)evt.PrivateMarker);
        Assert.Equal(1, evt.Params[0]);  // button 1 = middle
        Assert.Equal(5, evt.Params[1]);
        Assert.Equal(10, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_RightButtonPress()
    {
        // CSI < 2 ; x ; y M - Right button press
        Parse("\u001b[<2;80;24M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(2, evt.Params[0]);  // button 2 = right
        Assert.Equal(80, evt.Params[1]);
        Assert.Equal(24, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_LargeCoordinates()
    {
        // SGR mode supports coordinates > 223 (beyond default encoding limit)
        // Source: libvterm t/17state_mouse.test uses 301 as coordinate
        Parse("\u001b[<0;301;301M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(301, evt.Params[1]);
        Assert.Equal(301, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_WheelUp()
    {
        // CSI < 64 ; x ; y M - Wheel up
        // Source: Windows Terminal MouseInputTest.cpp - wheel up is 64
        Parse("\u001b[<64;10;10M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(64, evt.Params[0]);
    }

    [Fact]
    public void SgrEncoding_WheelDown()
    {
        // CSI < 65 ; x ; y M - Wheel down
        // Source: Windows Terminal MouseInputTest.cpp - wheel down is 65
        Parse("\u001b[<65;10;10M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(65, evt.Params[0]);
    }

    [Fact]
    public void SgrEncoding_ShiftModifier()
    {
        // CSI < 4 ; x ; y M - Left button + Shift (0 + 4)
        // Source: Windows Terminal MouseInputTest.cpp - Shift adds 4
        Parse("\u001b[<4;1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(4, evt.Params[0]);  // Shift flag
    }

    [Fact]
    public void SgrEncoding_AltModifier()
    {
        // CSI < 8 ; x ; y M - Left button + Alt (0 + 8)
        // Source: Windows Terminal MouseInputTest.cpp - Alt/Meta adds 8
        Parse("\u001b[<8;1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(8, evt.Params[0]);  // Alt flag
    }

    [Fact]
    public void SgrEncoding_CtrlModifier()
    {
        // CSI < 16 ; x ; y M - Left button + Ctrl (0 + 16)
        // Source: Windows Terminal MouseInputTest.cpp - Ctrl adds 16
        Parse("\u001b[<16;1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(16, evt.Params[0]);  // Ctrl flag
    }

    [Fact]
    public void SgrEncoding_MotionEvent()
    {
        // CSI < 35 ; x ; y M - Motion with no button (32 + 3)
        // Source: Windows Terminal MouseInputTest.cpp - motion events add 32
        Parse("\u001b[<35;50;25M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(35, evt.Params[0]);  // 32 (motion) + 3 (no button)
    }

    [Fact]
    public void SgrEncoding_DragEvent()
    {
        // CSI < 32 ; x ; y M - Drag with left button (32 + 0)
        Parse("\u001b[<32;10;20M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(32, evt.Params[0]);  // 32 (motion) + 0 (left button)
    }

    #endregion

    #region Urxvt Extended Encoding (CSI Pb ; Px ; Py M)

    [Fact]
    public void UrxvtEncoding_LeftButtonPress()
    {
        // CSI 0 ; x ; y M - Left button press
        // Source: libvterm t/17state_mouse.test line 165-166 - output "\e[0;301;301M"
        Parse("\u001b[0;301;301M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.False(evt.Private);
        Assert.Equal(3, evt.Params.Length);
        Assert.Equal(0, evt.Params[0]);
        Assert.Equal(301, evt.Params[1]);
        Assert.Equal(301, evt.Params[2]);
    }

    [Fact]
    public void UrxvtEncoding_ButtonRelease()
    {
        // CSI 3 ; x ; y M - Button release
        // Source: libvterm t/17state_mouse.test line 167-168 - output "\e[3;301;301M"
        Parse("\u001b[3;301;301M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(3, evt.Params[0]);
    }

    #endregion

    #region Multiple Modes in Single Sequence

    [Fact]
    public void MultipleModes_TrackingAndEncoding()
    {
        // CSI ? 1002 ; 1006 h - Enable button tracking AND SGR encoding at once
        // Source: libvterm t/17state_mouse.test line 188 - PUSH "\e[?1002;1006h"
        Parse("\u001b[?1002;1006h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(1002, evt.Params[0]);
        Assert.Equal(1006, evt.Params[1]);
    }

    #endregion

    #region DECRQM Mouse Mode Queries

    [Fact]
    public void Decrqm_QueryMouseButtonMode()
    {
        // CSI ? 1000 $ p - Query mouse button mode
        // Source: libvterm t/17state_mouse.test line 5-6
        Parse("\u001b[?1000$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal('$', (char)evt.Intermediates);
        Assert.Equal(1000, evt.Params[0]);
    }

    [Fact]
    public void Decrqm_QueryMouseDragMode()
    {
        // CSI ? 1002 $ p - Query mouse drag mode
        // Source: libvterm t/17state_mouse.test line 7-8
        Parse("\u001b[?1002$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1002, evt.Params[0]);
    }

    [Fact]
    public void Decrqm_QueryMouseMotionMode()
    {
        // CSI ? 1003 $ p - Query mouse motion mode
        // Source: libvterm t/17state_mouse.test line 9-10
        Parse("\u001b[?1003$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1003, evt.Params[0]);
    }

    [Fact]
    public void Decrqm_QueryUtf8EncodingMode()
    {
        // CSI ? 1005 $ p - Query UTF-8 mouse encoding mode
        // Source: libvterm t/17state_mouse.test line 124-125
        Parse("\u001b[?1005$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1005, evt.Params[0]);
    }

    [Fact]
    public void Decrqm_QuerySgrEncodingMode()
    {
        // CSI ? 1006 $ p - Query SGR mouse encoding mode
        // Source: libvterm t/17state_mouse.test line 126-127
        Parse("\u001b[?1006$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1006, evt.Params[0]);
    }

    [Fact]
    public void Decrqm_QueryUrxvtEncodingMode()
    {
        // CSI ? 1015 $ p - Query urxvt mouse encoding mode
        // Source: libvterm t/17state_mouse.test line 128-129
        Parse("\u001b[?1015$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1015, evt.Params[0]);
    }

    #endregion

    #region Button Encoding Values

    [Theory]
    [InlineData(0, "left button")]
    [InlineData(1, "middle button")]
    [InlineData(2, "right button")]
    [InlineData(3, "no button/release")]
    public void SgrEncoding_ButtonValues(int button, string description)
    {
        // Test that button encoding values are parsed correctly
        // Source: Windows Terminal MouseInputTest.cpp GetSgrCharFromButton
        _ = description;  // Used for test naming
        Parse($"\u001b[<{button};1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(button, evt.Params[0]);
    }

    [Theory]
    [InlineData(64, "wheel up")]
    [InlineData(65, "wheel down")]
    [InlineData(66, "wheel left")]
    [InlineData(67, "wheel right")]
    public void SgrEncoding_WheelValues(int button, string description)
    {
        // Test wheel event encoding values
        // Source: Windows Terminal MouseInputTest.cpp GetSgrCharFromButton, libvterm
        _ = description;
        Parse($"\u001b[<{button};1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(button, evt.Params[0]);
    }

    [Theory]
    [InlineData(4, "shift")]
    [InlineData(8, "alt/meta")]
    [InlineData(16, "ctrl")]
    [InlineData(12, "shift+alt")]
    [InlineData(20, "shift+ctrl")]
    [InlineData(24, "alt+ctrl")]
    [InlineData(28, "shift+alt+ctrl")]
    public void SgrEncoding_ModifierFlags(int modifierValue, string description)
    {
        // Test modifier key encoding
        // Source: Windows Terminal MouseInputTest.cpp - shift=4, alt=8, ctrl=16
        _ = description;
        Parse($"\u001b[<{modifierValue};1;1M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(modifierValue, evt.Params[0]);
    }

    #endregion

    #region Coordinate Encoding Tests

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 20)]
    [InlineData(80, 24)]
    [InlineData(132, 43)]  // 132-column mode
    [InlineData(200, 60)]  // Large terminal
    public void SgrEncoding_VariousCoordinates(int x, int y)
    {
        // Test various coordinate values are parsed correctly
        // Source: Windows Terminal MouseInputTest.cpp s_rgTestCoords
        Parse($"\u001b[<0;{x};{y}M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('M', evt.Final);
        Assert.Equal(x, evt.Params[1]);
        Assert.Equal(y, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_Coordinate_94_94()
    {
        // 94+1+32 = 127 - near ASCII boundary for default encoding
        // Source: Windows Terminal MouseInputTest.cpp s_rgTestCoords
        Parse("\u001b[<0;95;95M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal(95, evt.Params[1]);
        Assert.Equal(95, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_Coordinate_95_95()
    {
        // 95+1+32 = 128 - at ASCII boundary
        // Source: Windows Terminal MouseInputTest.cpp s_rgTestCoords
        Parse("\u001b[<0;96;96M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal(96, evt.Params[1]);
        Assert.Equal(96, evt.Params[2]);
    }

    [Fact]
    public void SgrEncoding_LargeCoordinate_32735()
    {
        // Near SHORT_MAX - 33 boundary
        // Source: Windows Terminal MouseInputTest.cpp s_rgTestCoords
        Parse("\u001b[<0;32735;32735M");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal(32735, evt.Params[1]);
        Assert.Equal(32735, evt.Params[2]);
    }

    #endregion

    #region Highlight Mouse Tracking (Mode 1001)

    [Fact]
    public void MouseTracking_HighlightMode_Enable()
    {
        // CSI ? 1001 h - Enable highlight mouse tracking
        Parse("\u001b[?1001h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1001, evt.Params[0]);
    }

    [Fact]
    public void MouseTracking_HighlightMode_Disable()
    {
        // CSI ? 1001 l - Disable highlight mouse tracking
        Parse("\u001b[?1001l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1001, evt.Params[0]);
    }

    #endregion

    #region Pixel Mouse Mode (Mode 1016)

    [Fact]
    public void MouseEncoding_PixelMode_Enable()
    {
        // CSI ? 1016 h - Enable SGR pixel mouse mode
        Parse("\u001b[?1016h");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1016, evt.Params[0]);
    }

    [Fact]
    public void MouseEncoding_PixelMode_Disable()
    {
        // CSI ? 1016 l - Disable SGR pixel mouse mode
        Parse("\u001b[?1016l");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('l', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(1016, evt.Params[0]);
    }

    #endregion
}
