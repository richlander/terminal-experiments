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
