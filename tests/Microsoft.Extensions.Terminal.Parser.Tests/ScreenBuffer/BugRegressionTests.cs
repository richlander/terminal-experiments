// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Bug regression tests ported from VtNetCore.
/// These tests catch real-world bugs found in production terminal implementations.
/// </summary>
/// <remarks>
/// Ported from:
/// - VtNetCore: BackgroundColorBug.cs, NanoBug.cs, OCSBug.cs, ScreenBoundsBug.cs
/// </remarks>
public class BugRegressionTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 24)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region DECAWM Line Wrapping Bug

    /// <summary>
    /// Ported from: VtNetCore BackgroundColorBug - DECAWM wrapping behavior
    /// With DECAWM on, writing past the line should wrap correctly.
    /// With DECAWM off, writing should stop at the margin.
    /// </summary>
    [Fact]
    public void Decawm_WrapVsNoWrap()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Clear screen, set colors, home cursor, enable autowrap
        Parse(buffer, "\u001b[2J\u001b[0;33;44m\u001b[1;1H\u001b[?7h");
        
        // Write more than 80 asterisks - should wrap with DECAWM on
        Parse(buffer, new string('*', 160));
        
        // Should have wrapped to second line
        Assert.Equal(new string('*', 80), buffer.GetRowText(0));
        Assert.Equal(new string('*', 80), buffer.GetRowText(1));
        
        // Now disable autowrap and test again
        Parse(buffer, "\u001b[2J\u001b[1;1H\u001b[?7l");
        Parse(buffer, new string('*', 160));
        
        // Should stay on first line (last char repeatedly overwritten)
        Assert.Equal(new string('*', 80), buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
    }

    #endregion

    #region Nano Scrolling Bug

    /// <summary>
    /// Ported from: VtNetCore NanoBug - scroll region preserved after cursor restore
    /// Tests that DECSTBM margins are maintained after DECSC/DECRC.
    /// This was a real bug in nano text editor rendering.
    /// </summary>
    [Fact]
    public void NanoScrollingBug_DecstbmAfterDecrc()
    {
        var buffer = CreateBuffer(64, 18);
        
        // Move to row 15
        Parse(buffer, "\u001b[15;1H");
        Assert.Equal(14, buffer.CursorY);
        Assert.Equal(0, buffer.CursorX);
        
        // Save cursor (DECSC)
        Parse(buffer, "\u001b7");
        
        // Set scroll region rows 3-16
        Parse(buffer, "\u001b[3;16r");
        
        // Restore cursor (DECRC) - should go back to row 15
        Parse(buffer, "\u001b8");
        Assert.Equal(14, buffer.CursorY);
        Assert.Equal(0, buffer.CursorX);
        
        // Move to row 16 (VPA)
        Parse(buffer, "\u001b[16d");
        Assert.Equal(15, buffer.CursorY);
        
        // Scroll up 7 lines within region
        Parse(buffer, "\u001b[7S");
        
        // If margins were lost, this would scroll the whole screen incorrectly
    }

    #endregion

    #region OSC Parsing Bug

    /// <summary>
    /// Ported from: VtNetCore OCSBug - OSC sequences should not leave parser in bad state
    /// After parsing an OSC sequence, subsequent input should parse normally.
    /// </summary>
    [Fact]
    public void OscSequence_DoesNotCorruptParserState()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Write some normal text
        for (int i = 0; i < 100; i++)
        {
            parser.Parse(Encoding.UTF8.GetBytes("TheQuickBrownFoxJumpedOverTheLazyDog."));
        }
        
        // Parse OSC 112 (reset cursor color)
        parser.Parse(Encoding.UTF8.GetBytes("\u001b]112\u0007"));
        
        // Write more text - this should work normally
        parser.Parse(Encoding.UTF8.GetBytes("\u001b[H\u001b[2J")); // Clear
        for (int i = 0; i < 100; i++)
        {
            parser.Parse(Encoding.UTF8.GetBytes("TheQuickBrownFoxJumpedOverTheLazyDog."));
        }
        
        // If parser was corrupted, we might crash or have wrong output
        // Just verify we got here successfully
    }

    /// <summary>
    /// Ported from: VtNetCore OCSBug - tmux startup sequence
    /// Real tmux session data that triggered a bug.
    /// </summary>
    [Fact]
    public void TmuxStartupSequence_ParsesCorrectly()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Simplified tmux startup sequences
        var sequences = new[]
        {
            "\u001b[?1049h",           // Alternate screen
            "\u001b(B\u001b[m",         // Reset
            "\u001b[?1l\u001b>",        // Normal cursor keys
            "\u001b[H",                 // Home
            "\u001b[2J",                // Clear
            "\u001b[?12l\u001b[?25h",   // Cursor visible
            "\u001b[?1000l\u001b[?1006l\u001b[?1005l",  // Disable mouse
            "\u001b[c",                 // Device attributes
            "\u001b[>4;1m",             // Modifiers
            "\u001b[?1004h",            // Focus events
            "\u001b]112\u0007",         // Reset cursor color (OSC 112)
            "\u001b[?25l",              // Hide cursor
            "\u001b[1;1H",              // Position
        };
        
        foreach (var seq in sequences)
        {
            parser.Parse(Encoding.UTF8.GetBytes(seq));
        }
        
        // If any sequence corrupted the parser, subsequent parsing would fail
        parser.Parse(Encoding.UTF8.GetBytes("Session Started"));
        Assert.Contains("Session Started", buffer.GetRowText(0));
    }

    #endregion

    #region Screen Bounds Bug

    /// <summary>
    /// Cursor movement should be clamped to screen bounds.
    /// </summary>
    [Fact]
    public void CursorMovement_ClampedToBounds()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Try to move cursor way outside bounds
        Parse(buffer, "\u001b[9999;9999H");
        
        // Should be clamped to max
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(23, buffer.CursorY);
        
        // Try to move to negative (via large backward movement)
        Parse(buffer, "\u001b[1;1H");
        Parse(buffer, "\u001b[9999D");  // Move left a lot
        Assert.Equal(0, buffer.CursorX);
        
        Parse(buffer, "\u001b[9999A");  // Move up a lot
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region SGR Attribute Preservation Bug

    /// <summary>
    /// SGR attributes should persist correctly across operations.
    /// </summary>
    [Fact]
    public void SgrAttributes_PersistAcrossOperations()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Set bold + red
        Parse(buffer, "\u001b[1;31m");
        Parse(buffer, "Bold Red Text");
        
        // Verify first char has attributes
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(1u, cell.Foreground); // Red
        
        // Carriage return should not reset attributes
        Parse(buffer, "\r\nStill Bold Red");
        
        // Check second line
        cell = buffer.GetCell(0, 1);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(1u, cell.Foreground);
        
        // Reset then verify default
        Parse(buffer, "\u001b[m");
        Parse(buffer, "Normal");
        
        // "Normal" started at position 14 after "Still Bold Red"
        cell = buffer.GetCell(14, 1);  // 'N' in "Normal"
        Assert.Equal(CellAttributes.None, cell.Attributes);
    }

    #endregion

    #region Escape Sequence Split Across Buffers

    /// <summary>
    /// Escape sequences split across Parse calls should work correctly.
    /// This is critical for streaming data.
    /// </summary>
    [Fact]
    public void EscapeSequence_SplitAcrossParsesCalls()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Split CSI sequence across multiple calls
        parser.Parse(Encoding.UTF8.GetBytes("\u001b"));
        parser.Parse(Encoding.UTF8.GetBytes("["));
        parser.Parse(Encoding.UTF8.GetBytes("1"));
        parser.Parse(Encoding.UTF8.GetBytes(";"));
        parser.Parse(Encoding.UTF8.GetBytes("3"));
        parser.Parse(Encoding.UTF8.GetBytes("1"));
        parser.Parse(Encoding.UTF8.GetBytes("m"));
        parser.Parse(Encoding.UTF8.GetBytes("Red"));
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(1u, cell.Foreground);
        Assert.Equal('R', cell.Character);
    }

    /// <summary>
    /// OSC sequence split across Parse calls.
    /// </summary>
    [Fact]
    public void OscSequence_SplitAcrossParsesCalls()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Split OSC title sequence
        parser.Parse(Encoding.UTF8.GetBytes("\u001b]0;My "));
        parser.Parse(Encoding.UTF8.GetBytes("Window "));
        parser.Parse(Encoding.UTF8.GetBytes("Title\u0007"));
        
        Assert.Equal("My Window Title", buffer.Title);
    }

    #endregion

    #region UTF-8 Split Across Buffers

    /// <summary>
    /// UTF-8 multi-byte sequences split across Parse calls.
    /// </summary>
    [Fact]
    public void Utf8_SplitAcrossParsesCalls()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // é = U+00E9 = 0xC3 0xA9 in UTF-8
        parser.Parse(new byte[] { 0xC3 });
        parser.Parse(new byte[] { 0xA9 });
        
        Assert.Equal('é', buffer.GetCell(0, 0).Character);
    }

    /// <summary>
    /// 4-byte UTF-8 (emoji) split across Parse calls.
    /// </summary>
    [Fact]
    public void Utf8_4Byte_SplitAcrossParsesCalls()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // 😀 = U+1F600 = 0xF0 0x9F 0x98 0x80 in UTF-8
        parser.Parse(new byte[] { 0xF0 });
        parser.Parse(new byte[] { 0x9F });
        parser.Parse(new byte[] { 0x98 });
        parser.Parse(new byte[] { 0x80 });
        
        // Should produce surrogate pair or high surrogate in first cell
        var cell = buffer.GetCell(0, 0);
        Assert.True(char.IsHighSurrogate(cell.Character));
    }

    #endregion

    #region Erase Operations with Attributes

    /// <summary>
    /// Erase operations should use current background color.
    /// </summary>
    [Fact]
    public void EraseOperations_UseCurrentBackground()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Set blue background
        Parse(buffer, "\u001b[44m");
        
        // Write text
        Parse(buffer, "Hello");
        
        // Erase to end of line
        Parse(buffer, "\u001b[K");
        
        // Erased cells should have blue background
        var erasedCell = buffer.GetCell(10, 0);
        Assert.Equal(4u, erasedCell.Background); // Blue
    }

    #endregion

    #region Cursor Past Screen Bounds Bug

    /// <summary>
    /// Ported from: VtNetCore ScreenBoundsBug - MoveCursorPastEndOfScreen
    /// Tests that CUP to row beyond screen height clamps to last row.
    /// </summary>
    /// <remarks>
    /// Source: VtNetCore.Unit.Tests/ScreenBoundsBug.cs
    /// The original bug caused cursor movement past screen bounds to crash or wrap incorrectly.
    /// </remarks>
    [Fact]
    public void CursorMovement_PastScreenHeight_ClampsToLastRow()
    {
        var buffer = CreateBuffer(41, 18);
        
        // DECRST 1 - Normal Cursor Keys
        Parse(buffer, "\u001b[?1l");
        // DECKPNM - Normal keypad
        Parse(buffer, "\u001b>");
        // CUP to row 19 (which is beyond 18-row screen)
        Parse(buffer, "\u001b[19;1H");
        
        // Should be clamped to row 17 (0-indexed last row of 18-row screen)
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(17, buffer.CursorY);
    }

    #endregion

    #region Tmux Session Parsing Bug

    /// <summary>
    /// Ported from: VtNetCore OCSBug - TmuxStartingUpTest
    /// Real tmux session data that triggered a parser bug.
    /// </summary>
    /// <remarks>
    /// Source: VtNetCore.Unit.Tests/OCSBug.cs (TmuxStartingUpTest)
    /// This test uses actual base64-encoded tmux output to verify parser robustness.
    /// The original bug caused the parser to leave data in buffer after processing.
    /// </remarks>
    [Fact]
    public void TmuxSession_RealData_ParsesWithoutCorruption()
    {
        var buffer = CreateBuffer(80, 60);
        var parser = new VtParser(buffer);
        
        // Base64-encoded tmux session data from real tmux startup
        string[] tmuxSession = new[]
        {
            "dA==", // t
            "bQ==", // m
            "dQ==", // u
            "eA==", // x
            "DQo=", // CR LF
            // Complex sequence: alternate screen, reset, clear, cursor visibility, mouse modes, OSC 112, etc.
            "G1s/MTA0OWgbKEIbW20bWz8xbBs+G1tIG1syShtbPzEybBtbPzI1aBtbPzEwMDBsG1s/MTAwNmwbWz8xMDA1bBtbYxtbPjQ7MW0bWz8xMDA0aBtdMTEyBxtbPzI1bBtbMTsxSBtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobW0sNChtbSw0KG1tLDQobWzMwbRtbNDJtWzBdIDA6YmFzaCogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICJpcC0xNzItMzEtODEtODcuZWMyLmluIiAyMjowNSAwNS1PY3QtMjIbKEIbW20bWzYwOzFIG1sxOzYwchtbSBtbPzEybBtbPzI1aA==",
            // Tmux status bar updates
            "G1s/MjVsG1s2MGQbWzMwbRtbNDJtWzBdIDA6c3NtLXVzZXJAaXAtMTcyLTMxLTgxLTg3On4qICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICJpcC0xNzItMzEtODEtODcuZWMyLmluIiAyMjowNSAwNS1PY3QtMjIbKEIbW20bWzE7MUgbWz8xMmwbWz8yNWg=",
            // Scroll region and prompt
            "G1sxOzU5cltzc20tdXNlckBpcC0xNzItMzEtODEtODcgfl0kIBtbMTs2MHIbW0gbWzMwQw==",
            // Character input
            "G1sxOzU5chtbSBtbMzBDdBtbMTs2MHIbW0gbWzMxQw==",
            "G1sxOzU5chtbSBtbMzFDbxtbMTs2MHIbW0gbWzMyQw==",
            "G1sxOzU5chtbSBtbMzJDcBtbMTs2MHIbW0gbWzMzQw==",
            "DQo=",
            // Application cursor keys mode
            "G1s/MjVsG1s/MWgbPQ==",
        };
        
        foreach (var encoded in tmuxSession)
        {
            var data = Convert.FromBase64String(encoded);
            parser.Parse(data);
        }
        
        // If parser was corrupted, subsequent parsing would fail or produce wrong output
        // Write test text and verify it appears correctly
        parser.Parse(Encoding.UTF8.GetBytes("\u001b[1;1HComplete"));
        Assert.Contains("Complete", buffer.GetRowText(0));
    }

    #endregion

    #region SGR Graphic Rendition Bug

    /// <summary>
    /// Ported from: VtNetCore BackgroundColorBug - Graphic rendition test pattern
    /// Tests complex combinations of SGR attributes (bold, underline, blink, inverse).
    /// </summary>
    /// <remarks>
    /// Source: VtNetCore.Unit.Tests/BackgroundColorBug.cs (TestDarkBackgroundPage)
    /// This was part of a VT100 compatibility test that verified rendering with various
    /// combinations of bold, underline, blink, and negative (inverse) attributes.
    /// </remarks>
    [Fact]
    public void SgrGraphicRendition_ComplexCombinations()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Set scroll region and clear
        Parse(buffer, "\u001b[1;24r");
        Parse(buffer, "\u001b[2J");
        
        // Test title
        Parse(buffer, "\u001b[1;20H");
        Parse(buffer, "Graphic rendition test pattern:");
        
        // vanilla (reset all)
        Parse(buffer, "\u001b[4;1H");
        Parse(buffer, "\u001b[0m");
        Parse(buffer, "vanilla");
        var vanillaCell = buffer.GetCell(0, 3);
        Assert.Equal('v', vanillaCell.Character);
        Assert.Equal(CellAttributes.None, vanillaCell.Attributes);
        
        // bold
        Parse(buffer, "\u001b[4;40H");
        Parse(buffer, "\u001b[0;1m");
        Parse(buffer, "bold");
        var boldCell = buffer.GetCell(39, 3);
        Assert.Equal('b', boldCell.Character);
        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));
        
        // underline
        Parse(buffer, "\u001b[6;6H");
        Parse(buffer, "\u001b[;4m");
        Parse(buffer, "underline");
        var underlineCell = buffer.GetCell(5, 5);
        Assert.Equal('u', underlineCell.Character);
        Assert.True(underlineCell.Attributes.HasFlag(CellAttributes.Underline));
        
        // bold underline
        Parse(buffer, "\u001b[6;45H");
        Parse(buffer, "\u001b[;1m\u001b[4m");
        Parse(buffer, "bold underline");
        var boldUnderlineCell = buffer.GetCell(44, 5);
        Assert.Equal('b', boldUnderlineCell.Character);
        Assert.True(boldUnderlineCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(boldUnderlineCell.Attributes.HasFlag(CellAttributes.Underline));
        
        // blink
        Parse(buffer, "\u001b[8;1H");
        Parse(buffer, "\u001b[0;5m");
        Parse(buffer, "blink");
        var blinkCell = buffer.GetCell(0, 7);
        Assert.Equal('b', blinkCell.Character);
        Assert.True(blinkCell.Attributes.HasFlag(CellAttributes.Blink));
        
        // negative (inverse)
        Parse(buffer, "\u001b[12;1H");
        Parse(buffer, "\u001b[1;4;5;0;7m");
        Parse(buffer, "negative");
        var negativeCell = buffer.GetCell(0, 11);
        Assert.Equal('n', negativeCell.Character);
        Assert.True(negativeCell.Attributes.HasFlag(CellAttributes.Inverse));
    }

    /// <summary>
    /// Ported from: VtNetCore BackgroundColorBug - SGR with empty parameters
    /// Tests that SGR sequences with empty parameters (e.g., \e[;4m) are handled correctly.
    /// </summary>
    /// <remarks>
    /// The original VtNetCore test included sequences like "\u001b[;4m" which use an
    /// empty parameter (treated as 0, meaning reset) followed by underline.
    /// </remarks>
    [Fact]
    public void Sgr_EmptyParameters_TreatedAsZero()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Set bold first
        Parse(buffer, "\u001b[1mA");
        var cellA = buffer.GetCell(0, 0);
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        
        // \e[;4m should reset (empty = 0) then set underline
        Parse(buffer, "\u001b[;4mB");
        var cellB = buffer.GetCell(1, 0);
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Underline));
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region DECSM Idempotency Bug (LP#1640917)

    /// <summary>
    /// Ported from: libvterm t/92lp1640917.test
    /// Launchpad bug #1640917: Mode setting should be idempotent.
    /// </summary>
    /// <remarks>
    /// Source: libvterm/t/92lp1640917.test
    /// The original bug was with DECSM 1002 (mouse tracking), but the principle 
    /// applies to all modes: setting a mode twice should not break or toggle state.
    /// We test with DECTCEM (cursor visibility) which is exposed in our API.
    /// </remarks>
    [Fact]
    public void Decsm_Idempotent_CursorVisibility()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Hide cursor
        Parse(buffer, "\u001b[?25l");
        Assert.False(buffer.CursorVisible);
        
        // Hide cursor again (idempotent operation)
        Parse(buffer, "\u001b[?25l");
        Assert.False(buffer.CursorVisible, "Mode should still be off after idempotent reset");
        
        // Show cursor
        Parse(buffer, "\u001b[?25h");
        Assert.True(buffer.CursorVisible);
        
        // Show cursor again (idempotent)
        Parse(buffer, "\u001b[?25h");
        Assert.True(buffer.CursorVisible, "Mode should still be on after idempotent set");
    }

    /// <summary>
    /// Ported from: libvterm t/92lp1640917.test
    /// Tests that mouse mode sequences (even if not exposed) don't corrupt parser state.
    /// </summary>
    /// <remarks>
    /// The original libvterm test verified idempotent DECSM 1002. Even if we don't expose
    /// mouse mode state, we verify the parser handles these sequences correctly and that
    /// subsequent rendering still works.
    /// </remarks>
    [Fact]
    public void Decsm1002_ParsesWithoutCorruption()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Enable button event mouse tracking (DECSM 1002)
        Parse(buffer, "\u001b[?1002h");
        
        // Write text - should work normally
        Parse(buffer, "Before");
        Assert.Equal("Before", buffer.GetRowText(0));
        
        // Set mode again (idempotent) - this was the bug trigger
        Parse(buffer, "\u001b[?1002h");
        
        // Parser should not be corrupted
        Parse(buffer, " After");
        Assert.Equal("Before After", buffer.GetRowText(0));
        
        // Disable mouse tracking
        Parse(buffer, "\u001b[?1002l");
        
        // More text should still work
        Parse(buffer, " Done");
        Assert.Equal("Before After Done", buffer.GetRowText(0));
    }

    /// <summary>
    /// Tests DECAWM mode (auto-wrap) is idempotent - related to LP#1640917 pattern.
    /// </summary>
    [Fact]
    public void Decawm_Idempotent_AutoWrap()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Disable auto-wrap
        Parse(buffer, "\u001b[?7l");
        Parse(buffer, "0123456789XYZ");
        Assert.Equal("012345678Z", buffer.GetRowText(0));
        
        // Clear and reset position
        Parse(buffer, "\u001b[2J\u001b[H");
        
        // Disable auto-wrap again (idempotent)
        Parse(buffer, "\u001b[?7l");
        Parse(buffer, "ABCDEFGHIJKLM");
        Assert.Equal("ABCDEFGHIM", buffer.GetRowText(0));
        
        // Enable auto-wrap
        Parse(buffer, "\u001b[?7h");
        Parse(buffer, "\u001b[2J\u001b[H");
        Parse(buffer, "0123456789XY");
        Assert.Equal("0123456789", buffer.GetRowText(0));
        Assert.Equal("XY", buffer.GetRowText(1));
        
        // Enable auto-wrap again (idempotent)
        Parse(buffer, "\u001b[2J\u001b[H");
        Parse(buffer, "\u001b[?7h");
        Parse(buffer, "0123456789AB");
        Assert.Equal("0123456789", buffer.GetRowText(0));
        Assert.Equal("AB", buffer.GetRowText(1));
    }

    #endregion

    #region Rapid Reset Bug

    /// <summary>
    /// Rapid resets should not corrupt state.
    /// </summary>
    [Fact]
    public void RapidResets_DoNotCorruptState()
    {
        var buffer = CreateBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        for (int i = 0; i < 100; i++)
        {
            parser.Parse(Encoding.UTF8.GetBytes("\u001bc")); // RIS
            parser.Parse(Encoding.UTF8.GetBytes("Test"));
            parser.Reset();
        }
        
        // Should still be functional
        Parse(buffer, "\u001b[2J\u001b[H"); // Clear screen and home cursor
        parser.Parse(Encoding.UTF8.GetBytes("Final"));
        Assert.Equal("Final", buffer.GetRowText(0));
    }

    #endregion
}
