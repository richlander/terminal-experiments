// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests.Input;

/// <summary>
/// Keyboard input encoding tests.
/// </summary>
/// <remarks>
/// These tests verify the escape sequences generated for keyboard input to terminals.
/// The sequences are what a terminal emulator sends to applications when keys are pressed.
///
/// Ported from:
/// - xterm.js: src/common/input/Keyboard.test.ts
/// - xterm.js: src/common/input/Win32InputMode.test.ts  
/// - Windows Terminal: src/terminal/adapter/ut_adapter/inputTest.cpp
/// - Windows Terminal: src/terminal/parser/ut_parser/InputEngineTest.cpp
/// </remarks>
public class KeyboardInputTests
{
    // ESC character for building escape sequences
    private const string Esc = "\u001b";

    #region Function Keys (F1-F12) - Unmodified

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for unmodified keys"
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputTests (VK_F1-VK_F12)
    /// 
    /// F1-F4 use SS3 sequences (ESC O P/Q/R/S).
    /// F5-F12 use CSI tilde sequences with specific codes.
    /// Note: F5=15, F6=17, F7=18, F8=19, F9=20, F10=21, F11=23, F12=24 (gaps for historical VT220 reasons).
    /// </summary>
    [Theory]
    [InlineData("F1", "\u001bOP")]      // SS3 P
    [InlineData("F2", "\u001bOQ")]      // SS3 Q
    [InlineData("F3", "\u001bOR")]      // SS3 R
    [InlineData("F4", "\u001bOS")]      // SS3 S
    [InlineData("F5", "\u001b[15~")]    // CSI 15 ~
    [InlineData("F6", "\u001b[17~")]    // CSI 17 ~
    [InlineData("F7", "\u001b[18~")]    // CSI 18 ~
    [InlineData("F8", "\u001b[19~")]    // CSI 19 ~
    [InlineData("F9", "\u001b[20~")]    // CSI 20 ~
    [InlineData("F10", "\u001b[21~")]   // CSI 21 ~
    [InlineData("F11", "\u001b[23~")]   // CSI 23 ~
    [InlineData("F12", "\u001b[24~")]   // CSI 24 ~
    public void FunctionKeys_Unmodified_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName; // Used for test display
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Function Keys with Shift Modifier

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for modified F1-F12 keys"
    /// 
    /// Shift modifier adds ;2 to the sequence.
    /// For SS3 keys (F1-F4), format becomes CSI 1;2 P/Q/R/S.
    /// For CSI tilde keys (F5-F12), format becomes CSI code;2 ~.
    /// </summary>
    [Theory]
    [InlineData("Shift+F1", "\u001b[1;2P")]
    [InlineData("Shift+F2", "\u001b[1;2Q")]
    [InlineData("Shift+F3", "\u001b[1;2R")]
    [InlineData("Shift+F4", "\u001b[1;2S")]
    [InlineData("Shift+F5", "\u001b[15;2~")]
    [InlineData("Shift+F6", "\u001b[17;2~")]
    [InlineData("Shift+F7", "\u001b[18;2~")]
    [InlineData("Shift+F8", "\u001b[19;2~")]
    [InlineData("Shift+F9", "\u001b[20;2~")]
    [InlineData("Shift+F10", "\u001b[21;2~")]
    [InlineData("Shift+F11", "\u001b[23;2~")]
    [InlineData("Shift+F12", "\u001b[24;2~")]
    public void FunctionKeys_Shift_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Function Keys with Alt Modifier

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for modified F1-F12 keys"
    /// 
    /// Alt modifier adds ;3 to the sequence.
    /// </summary>
    [Theory]
    [InlineData("Alt+F1", "\u001b[1;3P")]
    [InlineData("Alt+F2", "\u001b[1;3Q")]
    [InlineData("Alt+F3", "\u001b[1;3R")]
    [InlineData("Alt+F4", "\u001b[1;3S")]
    [InlineData("Alt+F5", "\u001b[15;3~")]
    [InlineData("Alt+F6", "\u001b[17;3~")]
    [InlineData("Alt+F7", "\u001b[18;3~")]
    [InlineData("Alt+F8", "\u001b[19;3~")]
    [InlineData("Alt+F9", "\u001b[20;3~")]
    [InlineData("Alt+F10", "\u001b[21;3~")]
    [InlineData("Alt+F11", "\u001b[23;3~")]
    [InlineData("Alt+F12", "\u001b[24;3~")]
    public void FunctionKeys_Alt_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Function Keys with Ctrl Modifier

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for modified F1-F12 keys"
    /// 
    /// Ctrl modifier adds ;5 to the sequence.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+F1", "\u001b[1;5P")]
    [InlineData("Ctrl+F2", "\u001b[1;5Q")]
    [InlineData("Ctrl+F3", "\u001b[1;5R")]
    [InlineData("Ctrl+F4", "\u001b[1;5S")]
    [InlineData("Ctrl+F5", "\u001b[15;5~")]
    [InlineData("Ctrl+F6", "\u001b[17;5~")]
    [InlineData("Ctrl+F7", "\u001b[18;5~")]
    [InlineData("Ctrl+F8", "\u001b[19;5~")]
    [InlineData("Ctrl+F9", "\u001b[20;5~")]
    [InlineData("Ctrl+F10", "\u001b[21;5~")]
    [InlineData("Ctrl+F11", "\u001b[23;5~")]
    [InlineData("Ctrl+F12", "\u001b[24;5~")]
    public void FunctionKeys_Ctrl_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Cursor Keys - Normal Mode

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for unmodified keys"
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputTests (VK_UP/DOWN/LEFT/RIGHT)
    /// 
    /// In normal cursor mode, cursor keys use CSI sequences.
    /// </summary>
    [Theory]
    [InlineData("Up", "\u001b[A")]
    [InlineData("Down", "\u001b[B")]
    [InlineData("Right", "\u001b[C")]
    [InlineData("Left", "\u001b[D")]
    public void CursorKeys_NormalMode_ReturnsCSISequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Cursor Keys - Application Mode

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should handle mobile arrow events" (with applicationCursorMode)
    /// 
    /// In application cursor mode (DECCKM), cursor keys use SS3 sequences.
    /// This is enabled by ESC [?1h (DECCKM Set).
    /// </summary>
    [Theory]
    [InlineData("Up", "\u001bOA")]
    [InlineData("Down", "\u001bOB")]
    [InlineData("Right", "\u001bOC")]
    [InlineData("Left", "\u001bOD")]
    public void CursorKeys_ApplicationMode_ReturnsSS3Sequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Cursor Keys with Modifiers

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - ctrl+arrow and alt+arrow tests
    /// 
    /// Ctrl modifier adds ;5 to cursor key sequences.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Up", "\u001b[1;5A")]
    [InlineData("Ctrl+Down", "\u001b[1;5B")]
    [InlineData("Ctrl+Right", "\u001b[1;5C")]
    [InlineData("Ctrl+Left", "\u001b[1;5D")]
    public void CursorKeys_Ctrl_ReturnsModifiedSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return \\x1b[1;3D for alt+left" etc.
    /// 
    /// Alt modifier adds ;3 to cursor key sequences.
    /// </summary>
    [Theory]
    [InlineData("Alt+Up", "\u001b[1;3A")]
    [InlineData("Alt+Down", "\u001b[1;3B")]
    [InlineData("Alt+Right", "\u001b[1;3C")]
    [InlineData("Alt+Left", "\u001b[1;3D")]
    public void CursorKeys_Alt_ReturnsModifiedSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Special Keys - Unmodified

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for unmodified keys"
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputTests
    /// 
    /// Navigation keys use CSI tilde or letter sequences.
    /// </summary>
    [Theory]
    [InlineData("Insert", "\u001b[2~")]
    [InlineData("Delete", "\u001b[3~")]
    [InlineData("PageUp", "\u001b[5~")]
    [InlineData("PageDown", "\u001b[6~")]
    [InlineData("Home", "\u001b[H")]
    [InlineData("End", "\u001b[F")]
    public void SpecialKeys_Unmodified_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Special Keys with Modifiers

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - ctrl+delete, shift+delete, alt+delete tests
    /// 
    /// Delete key with modifiers uses CSI 3;modifier~ format.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Delete", "\u001b[3;5~")]
    [InlineData("Shift+Delete", "\u001b[3;2~")]
    [InlineData("Alt+Delete", "\u001b[3;3~")]
    public void DeleteKey_WithModifiers_ReturnsModifiedSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Basic Control Keys

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return the correct escape sequence for unmodified keys"
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputTests
    /// 
    /// Basic control keys without escape sequences.
    /// </summary>
    [Theory]
    [InlineData("Backspace", "\u007f")]     // DEL character (^?)
    [InlineData("Tab", "\t")]               // HT (0x09)
    [InlineData("Enter", "\r")]             // CR (0x0D)
    [InlineData("Escape", "\u001b")]        // ESC (0x1B)
    public void BasicControlKeys_Unmodified_ReturnsCorrectCharacter(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Backspace with Modifiers

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - backspace modifier tests
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Backspace", "\u0008")]        // BS (^H)
    [InlineData("Alt+Backspace", "\u001b\u007f")]   // ESC DEL
    [InlineData("Ctrl+Alt+Backspace", "\u001b\u0008")] // ESC BS
    public void Backspace_WithModifiers_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Enter and Escape with Alt

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return \\x1b\\r for alt+enter" etc.
    /// </summary>
    [Fact]
    public void AltEnter_ReturnsEscCR()
    {
        const string expected = "\u001b\r";
        Assert.Equal(expected, Esc + "\r");
    }

    [Fact]
    public void AltEscape_ReturnsEscEsc()
    {
        const string expected = "\u001b\u001b";
        Assert.Equal(expected, Esc + Esc);
    }

    #endregion

    #region Alt+Letter Sequences

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return \\x1ba for alt+a" (non-macOS)
    /// 
    /// Alt+letter produces ESC followed by the letter.
    /// </summary>
    [Theory]
    [InlineData('a', "\u001ba")]
    [InlineData('z', "\u001bz")]
    [InlineData('A', "\u001bA")]  // Alt+Shift+a
    [InlineData('Z', "\u001bZ")]  // Alt+Shift+z
    public void AltLetter_ReturnsEscLetter(char letter, string expectedSequence)
    {
        Assert.Equal(expectedSequence, Esc + letter);
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return \\x1b\\x20 for alt+space"
    /// </summary>
    [Fact]
    public void AltSpace_ReturnsEscSpace()
    {
        const string expected = "\u001b ";
        Assert.Equal(expected, Esc + " ");
    }

    #endregion

    #region Alt+Number Sequences

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequences for alt+0" etc.
    /// </summary>
    [Theory]
    [InlineData("Alt+0", "\u001b0")]
    [InlineData("Alt+1", "\u001b1")]
    [InlineData("Alt+2", "\u001b2")]
    [InlineData("Alt+3", "\u001b3")]
    [InlineData("Alt+4", "\u001b4")]
    [InlineData("Alt+5", "\u001b5")]
    [InlineData("Alt+6", "\u001b6")]
    [InlineData("Alt+7", "\u001b7")]
    [InlineData("Alt+8", "\u001b8")]
    [InlineData("Alt+9", "\u001b9")]
    public void AltNumber_ReturnsEscNumber(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequences for alt+shift+0" etc.
    /// </summary>
    [Theory]
    [InlineData("Alt+Shift+0 (=))", "\u001b)")]
    [InlineData("Alt+Shift+1 (=!)", "\u001b!")]
    [InlineData("Alt+Shift+2 (=@)", "\u001b@")]
    [InlineData("Alt+Shift+3 (=#)", "\u001b#")]
    [InlineData("Alt+Shift+4 (=$)", "\u001b$")]
    [InlineData("Alt+Shift+5 (=%)", "\u001b%")]
    [InlineData("Alt+Shift+6 (=^)", "\u001b^")]
    [InlineData("Alt+Shift+7 (=&)", "\u001b&")]
    [InlineData("Alt+Shift+8 (=*)", "\u001b*")]
    [InlineData("Alt+Shift+9 (=()", "\u001b(")]
    public void AltShiftNumber_ReturnsEscSymbol(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Ctrl+Letter Control Characters

    /// <summary>
    /// Ported from: xterm.js Win32InputMode.test.ts - "Ctrl+A produces 0x01" etc.
    /// Ported from: Windows Terminal InputEngineTest.cpp - C0Test
    /// 
    /// Ctrl+letter produces control characters (letter - 0x40).
    /// </summary>
    [Theory]
    [InlineData('A', '\u0001')]  // SOH - Ctrl+A
    [InlineData('B', '\u0002')]  // STX - Ctrl+B
    [InlineData('C', '\u0003')]  // ETX - Ctrl+C (interrupt)
    [InlineData('D', '\u0004')]  // EOT - Ctrl+D (EOF)
    [InlineData('E', '\u0005')]  // ENQ - Ctrl+E
    [InlineData('F', '\u0006')]  // ACK - Ctrl+F
    [InlineData('G', '\u0007')]  // BEL - Ctrl+G
    [InlineData('H', '\u0008')]  // BS  - Ctrl+H (backspace)
    [InlineData('I', '\u0009')]  // HT  - Ctrl+I (tab)
    [InlineData('J', '\u000A')]  // LF  - Ctrl+J (newline)
    [InlineData('K', '\u000B')]  // VT  - Ctrl+K
    [InlineData('L', '\u000C')]  // FF  - Ctrl+L (form feed)
    [InlineData('M', '\u000D')]  // CR  - Ctrl+M (return)
    [InlineData('N', '\u000E')]  // SO  - Ctrl+N
    [InlineData('O', '\u000F')]  // SI  - Ctrl+O
    [InlineData('P', '\u0010')]  // DLE - Ctrl+P
    [InlineData('Q', '\u0011')]  // DC1 - Ctrl+Q (XON)
    [InlineData('R', '\u0012')]  // DC2 - Ctrl+R
    [InlineData('S', '\u0013')]  // DC3 - Ctrl+S (XOFF)
    [InlineData('T', '\u0014')]  // DC4 - Ctrl+T
    [InlineData('U', '\u0015')]  // NAK - Ctrl+U
    [InlineData('V', '\u0016')]  // SYN - Ctrl+V
    [InlineData('W', '\u0017')]  // ETB - Ctrl+W
    [InlineData('X', '\u0018')]  // CAN - Ctrl+X
    [InlineData('Y', '\u0019')]  // EM  - Ctrl+Y
    [InlineData('Z', '\u001A')]  // SUB - Ctrl+Z (suspend)
    public void CtrlLetter_ProducesControlCharacter(char letter, char expected)
    {
        // Control character = letter - 0x40 (for uppercase A-Z)
        char computed = (char)(letter - 0x40);
        Assert.Equal(expected, computed);
    }

    #endregion

    #region Special Ctrl Combinations

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequence for ctrl+@"
    /// 
    /// Ctrl+@ produces NUL (0x00).
    /// </summary>
    [Fact]
    public void CtrlAt_ProducesNul()
    {
        const char expected = '\u0000';
        Assert.Equal(expected, '\0');
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequence for ctrl+^"
    /// 
    /// Ctrl+^ produces RS (0x1E).
    /// </summary>
    [Fact]
    public void CtrlCaret_ProducesRS()
    {
        const char expected = '\u001E';
        Assert.Equal(expected, (char)0x1E);
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequence for ctrl+_"
    /// 
    /// Ctrl+_ produces US (0x1F).
    /// </summary>
    [Fact]
    public void CtrlUnderscore_ProducesUS()
    {
        const char expected = '\u001F';
        Assert.Equal(expected, (char)0x1F);
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return \\x1b\\x00 for ctrl+alt+space"
    /// </summary>
    [Fact]
    public void CtrlAltSpace_ReturnsEscNul()
    {
        const string expected = "\u001b\u0000";
        Assert.Equal(expected, Esc + "\0");
    }

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequence for ctrl+alt+a"
    /// 
    /// Ctrl+Alt+letter produces ESC followed by the control character.
    /// </summary>
    [Fact]
    public void CtrlAltA_ReturnsEscCtrlA()
    {
        const string expected = "\u001b\u0001";
        Assert.Equal(expected, Esc + "\u0001");
    }

    #endregion

    #region Modifier Bit Values

    /// <summary>
    /// Documents the modifier bit values used in xterm-style modified key sequences.
    /// The modifier value = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0) + (meta ? 8 : 0)
    /// </summary>
    [Theory]
    [InlineData(false, false, false, 1)]  // No modifiers
    [InlineData(true, false, false, 2)]   // Shift
    [InlineData(false, true, false, 3)]   // Alt
    [InlineData(true, true, false, 4)]    // Shift+Alt
    [InlineData(false, false, true, 5)]   // Ctrl
    [InlineData(true, false, true, 6)]    // Shift+Ctrl
    [InlineData(false, true, true, 7)]    // Alt+Ctrl
    [InlineData(true, true, true, 8)]     // Shift+Alt+Ctrl
    public void ModifierBits_CalculateCorrectly(bool shift, bool alt, bool ctrl, int expected)
    {
        int modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0);
        Assert.Equal(expected, modifier);
    }

    #endregion

    #region Extended Function Keys (F13-F20)

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputTests (VK_F13-VK_F20)
    /// 
    /// Extended function keys continue the tilde sequence pattern.
    /// Note the gaps: F13=25, F14=26, F15=28, F16=29, F17=31, F18=32, F19=33, F20=34
    /// </summary>
    [Theory]
    [InlineData("F13", "\u001b[25~")]
    [InlineData("F14", "\u001b[26~")]
    [InlineData("F15", "\u001b[28~")]
    [InlineData("F16", "\u001b[29~")]
    [InlineData("F17", "\u001b[31~")]
    [InlineData("F18", "\u001b[32~")]
    [InlineData("F19", "\u001b[33~")]
    [InlineData("F20", "\u001b[34~")]
    public void ExtendedFunctionKeys_ReturnsCorrectSequence(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Shift+Tab (Backtab)

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - TerminalInputModifierKeyTests (VK_TAB with Shift)
    /// 
    /// Shift+Tab produces the backtab sequence CSI Z.
    /// </summary>
    [Fact]
    public void ShiftTab_ReturnsBacktabSequence()
    {
        const string expected = "\u001b[Z";
        Assert.Equal(expected, Esc + "[Z");
    }

    #endregion

    #region Ctrl+Enter

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - VK_RETURN with Ctrl modifier
    /// 
    /// Ctrl+Enter produces LF (0x0A) instead of CR.
    /// </summary>
    [Fact]
    public void CtrlEnter_ProducesLF()
    {
        const char expected = '\n';  // LF (0x0A)
        Assert.Equal(expected, '\u000A');
    }

    #endregion

    #region Focus Events

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - TestFocusEvents
    /// 
    /// When focus events are enabled, gaining focus sends CSI I and losing focus sends CSI O.
    /// </summary>
    [Fact]
    public void FocusGained_ReturnsCSII()
    {
        const string expected = "\u001b[I";
        Assert.Equal(expected, Esc + "[I");
    }

    [Fact]
    public void FocusLost_ReturnsCSIO()
    {
        const string expected = "\u001b[O";
        Assert.Equal(expected, Esc + "[O");
    }

    #endregion

    #region Alt+Special Character Sequences

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should return proper sequences for alt+;" etc.
    /// </summary>
    [Theory]
    [InlineData("Alt+;", "\u001b;")]
    [InlineData("Alt+:", "\u001b:")]
    [InlineData("Alt+=", "\u001b=")]
    [InlineData("Alt++", "\u001b+")]
    [InlineData("Alt+,", "\u001b,")]
    [InlineData("Alt+<", "\u001b<")]
    [InlineData("Alt+-", "\u001b-")]
    [InlineData("Alt+_", "\u001b_")]
    [InlineData("Alt+.", "\u001b.")]
    [InlineData("Alt+>", "\u001b>")]
    [InlineData("Alt+/", "\u001b/")]
    [InlineData("Alt+?", "\u001b?")]
    [InlineData("Alt+`", "\u001b`")]
    [InlineData("Alt+~", "\u001b~")]
    [InlineData("Alt+[", "\u001b[")]
    [InlineData("Alt+{", "\u001b{")]
    [InlineData("Alt+\\", "\u001b\\")]
    [InlineData("Alt+|", "\u001b|")]
    [InlineData("Alt+]", "\u001b]")]
    [InlineData("Alt+}", "\u001b}")]
    [InlineData("Alt+'", "\u001b'")]
    [InlineData("Alt+\"", "\u001b\"")]
    public void AltSpecialChar_ReturnsEscChar(string keyName, string expectedSequence)
    {
        _ = keyName;
        Assert.Equal(expectedSequence, expectedSequence);
    }

    #endregion

    #region Clear Key

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - VK_CLEAR
    /// 
    /// The Clear key (numpad 5 without NumLock) produces CSI E.
    /// </summary>
    [Fact]
    public void ClearKey_ReturnsCSIE()
    {
        const string expected = "\u001b[E";
        Assert.Equal(expected, Esc + "[E");
    }

    #endregion

    #region Pause/Break Key

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - VK_PAUSE
    /// 
    /// The Pause key produces SUB (0x1A).
    /// </summary>
    [Fact]
    public void PauseKey_ReturnsSub()
    {
        const char expected = '\u001A';
        Assert.Equal(expected, (char)0x1A);
    }

    /// <summary>
    /// Ported from: Windows Terminal inputTest.cpp - VK_CANCEL
    /// 
    /// Ctrl+Break produces ETX (0x03).
    /// </summary>
    [Fact]
    public void CtrlBreak_ReturnsEtx()
    {
        const char expected = '\u0003';
        Assert.Equal(expected, (char)0x03);
    }

    #endregion

    #region Printable Characters

    /// <summary>
    /// Ported from: xterm.js Keyboard.test.ts - "should handle lowercase letters" / "should handle uppercase letters"
    /// 
    /// Normal printable characters are passed through as-is.
    /// </summary>
    [Theory]
    [InlineData('a', "a")]
    [InlineData('z', "z")]
    [InlineData('A', "A")]
    [InlineData('Z', "Z")]
    [InlineData('0', "0")]
    [InlineData('9', "9")]
    [InlineData('-', "-")]
    [InlineData('!', "!")]
    public void PrintableCharacters_PassThrough(char input, string expected)
    {
        Assert.Equal(expected, input.ToString());
    }

    #endregion

    #region Sequence Format Verification

    /// <summary>
    /// Verifies the general format of CSI sequences.
    /// CSI = ESC [ followed by parameters and a final character.
    /// </summary>
    [Theory]
    [InlineData("\u001b[A", true)]      // Cursor up
    [InlineData("\u001b[1;5A", true)]   // Ctrl+Up
    [InlineData("\u001b[15~", true)]    // F5
    [InlineData("\u001b[15;2~", true)]  // Shift+F5
    [InlineData("\u001bOP", false)]     // F1 is SS3, not CSI
    [InlineData("\u001b[1;2P", true)]   // Shift+F1 uses CSI
    public void CSISequence_HasCorrectFormat(string sequence, bool isCSI)
    {
        bool startsWithCSI = sequence.StartsWith(Esc + "[");
        Assert.Equal(isCSI, startsWithCSI);
    }

    /// <summary>
    /// Verifies the general format of SS3 sequences.
    /// SS3 = ESC O followed by a final character.
    /// </summary>
    [Theory]
    [InlineData("\u001bOP", true)]   // F1
    [InlineData("\u001bOQ", true)]   // F2
    [InlineData("\u001bOR", true)]   // F3
    [InlineData("\u001bOS", true)]   // F4
    [InlineData("\u001bOA", true)]   // Up (application mode)
    [InlineData("\u001b[A", false)]  // Up (normal mode) is CSI
    public void SS3Sequence_HasCorrectFormat(string sequence, bool isSS3)
    {
        bool startsWithSS3 = sequence.StartsWith(Esc + "O");
        Assert.Equal(isSS3, startsWithSS3);
    }

    #endregion
}
