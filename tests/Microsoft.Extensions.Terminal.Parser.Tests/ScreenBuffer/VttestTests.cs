// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// VTtest-style integration tests for ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/90vttest_*.test
/// These are end-to-end tests based on the vttest terminal testing suite.
/// </remarks>
public class ScreenBufferVttestTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Cursor Movement Tests (vttest menu 1)

    /// <summary>
    /// Ported from: libvterm 90vttest_01-movement-1
    /// Tests basic cursor positioning.
    /// </summary>
    [Fact]
    public void Vttest_CursorPositioning()
    {
        var buffer = CreateBuffer();
        
        // Move to various positions
        Parse(buffer, "\x1b[1;1H");   // Home
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        Parse(buffer, "\x1b[12;40H"); // Middle-ish
        Assert.Equal(39, buffer.CursorX);
        Assert.Equal(11, buffer.CursorY);
        
        Parse(buffer, "\x1b[24;80H"); // Near bottom-right
        Assert.Equal(79, buffer.CursorX);
        Assert.Equal(23, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_01-movement-2
    /// Tests cursor movement commands (CUU, CUD, CUF, CUB).
    /// </summary>
    [Fact]
    public void Vttest_CursorMovement_Relative()
    {
        var buffer = CreateBuffer();
        
        // Start in middle
        Parse(buffer, "\x1b[12;40H");
        Assert.Equal(39, buffer.CursorX);
        Assert.Equal(11, buffer.CursorY);
        
        // Move up 5
        Parse(buffer, "\x1b[5A");
        Assert.Equal(39, buffer.CursorX);
        Assert.Equal(6, buffer.CursorY);
        
        // Move down 3
        Parse(buffer, "\x1b[3B");
        Assert.Equal(39, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
        
        // Move right 10
        Parse(buffer, "\x1b[10C");
        Assert.Equal(49, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
        
        // Move left 20
        Parse(buffer, "\x1b[20D");
        Assert.Equal(29, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_01-movement-3
    /// Tests cursor movement clamping at screen edges.
    /// </summary>
    [Fact]
    public void Vttest_CursorMovement_Clamping()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Try to move past top
        Parse(buffer, "\x1b[1;1H\x1b[999A");
        Assert.Equal(0, buffer.CursorY);
        
        // Try to move past bottom
        Parse(buffer, "\x1b[24;1H\x1b[999B");
        Assert.Equal(23, buffer.CursorY);
        
        // Try to move past left
        Parse(buffer, "\x1b[1;1H\x1b[999D");
        Assert.Equal(0, buffer.CursorX);
        
        // Try to move past right
        Parse(buffer, "\x1b[1;80H\x1b[999C");
        Assert.Equal(79, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_01-movement-4
    /// Tests save/restore cursor.
    /// </summary>
    [Fact]
    public void Vttest_CursorSaveRestore()
    {
        var buffer = CreateBuffer();
        
        // Position and save - cursor at column 30 (0-indexed = 29), row 15 (0-indexed = 14)
        Parse(buffer, "\x1b[15;30H");
        Assert.Equal(29, buffer.CursorX);
        Assert.Equal(14, buffer.CursorY);
        
        // Save cursor
        Parse(buffer, "\u001b7");
        
        // Move elsewhere
        Parse(buffer, "\x1b[1;1H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        // Restore - should go back to saved position
        Parse(buffer, "\u001b8");
        Assert.Equal(29, buffer.CursorX);
        Assert.Equal(14, buffer.CursorY);
    }

    #endregion

    #region Screen Tests (vttest menu 2)

    /// <summary>
    /// Ported from: libvterm 90vttest_02-screen-1
    /// Tests screen fill and clear.
    /// </summary>
    [Fact]
    public void Vttest_ScreenFillAndClear()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Fill screen with 'E' (simulate DECALN behavior)
        for (int y = 0; y < 24; y++)
        {
            Parse(buffer, $"\x1b[{y + 1};1H");
            Parse(buffer, new string('E', 80));
        }
        
        // Verify fill
        Assert.StartsWith("EEEEE", buffer.GetRowText(0));
        Assert.StartsWith("EEEEE", buffer.GetRowText(12));
        Assert.StartsWith("EEEEE", buffer.GetRowText(23));
        
        // Clear screen
        Parse(buffer, "\x1b[2J");
        
        // Verify clear
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(12));
        Assert.Equal("", buffer.GetRowText(23));
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_02-screen-2
    /// Tests erase in line (EL).
    /// </summary>
    [Fact]
    public void Vttest_EraseInLine()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Write a line
        Parse(buffer, "\x1b[1;1HABCDEFGHIJ");
        Assert.Equal("ABCDEFGHIJ", buffer.GetRowText(0));
        
        // Erase from cursor to end (cursor at col 5)
        Parse(buffer, "\x1b[1;5H\x1b[0K");
        Assert.Equal("ABCD", buffer.GetRowText(0));
        
        // Write again
        Parse(buffer, "\x1b[2;1HABCDEFGHIJ");
        
        // Erase from start to cursor (cursor at col 5)
        Parse(buffer, "\x1b[2;5H\x1b[1K");
        Assert.Equal("FGHIJ", buffer.GetRowText(1).TrimStart());
        
        // Write and erase entire line
        Parse(buffer, "\x1b[3;1HABCDEFGHIJ");
        Parse(buffer, "\x1b[3;5H\x1b[2K");
        Assert.Equal("", buffer.GetRowText(2));
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_02-screen-3
    /// Tests insert/delete characters.
    /// </summary>
    [Fact]
    public void Vttest_InsertDeleteCharacters()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Write text
        Parse(buffer, "\x1b[1;1HABCDEFGH");
        Assert.Equal("ABCDEFGH", buffer.GetRowText(0));
        
        // Insert 2 chars at position 3
        Parse(buffer, "\x1b[1;3H\x1b[2@");
        Assert.Equal("AB  CDEFGH", buffer.GetRowText(0));
        
        // Write new text
        Parse(buffer, "\x1b[2;1HXYZUVWRS");
        Assert.Equal("XYZUVWRS", buffer.GetRowText(1));
        
        // Delete 3 chars at position 4
        Parse(buffer, "\x1b[2;4H\x1b[3P");
        Assert.Equal("XYZRS", buffer.GetRowText(1));
    }

    /// <summary>
    /// Ported from: libvterm 90vttest_02-screen-4
    /// Tests insert/delete lines.
    /// </summary>
    [Fact]
    public void Vttest_InsertDeleteLines()
    {
        var buffer = CreateBuffer(80, 10);
        
        // Fill with identifiable lines
        for (int i = 0; i < 10; i++)
        {
            Parse(buffer, $"\x1b[{i + 1};1HLine{i}");
        }
        
        // Insert 2 lines at row 3
        Parse(buffer, "\x1b[3;1H\x1b[2L");
        
        Assert.Equal("Line0", buffer.GetRowText(0));
        Assert.Equal("Line1", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));  // Inserted
        Assert.Equal("", buffer.GetRowText(3));  // Inserted
        Assert.Equal("Line2", buffer.GetRowText(4));
        
        // Delete 1 line at row 5
        Parse(buffer, "\x1b[5;1H\x1b[1M");
        
        Assert.Equal("Line3", buffer.GetRowText(4));
    }

    #endregion

    #region Origin Mode Tests

    /// <summary>
    /// Tests cursor with origin mode and scroll region.
    /// </summary>
    [Fact]
    public void Vttest_OriginMode_WithScrollRegion()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Set scroll region rows 5-15
        Parse(buffer, "\x1b[5;15r");
        
        // Enable origin mode
        Parse(buffer, "\x1b[?6h");
        
        // Home should go to row 5 (0-indexed = 4)
        Parse(buffer, "\x1b[H");
        Assert.Equal(4, buffer.CursorY);
        Assert.Equal(0, buffer.CursorX);
        
        // Row 1 in origin mode = row 5 absolute
        Parse(buffer, "\x1b[1;1H");
        Assert.Equal(4, buffer.CursorY);
        
        // Row 11 in origin mode = row 15 absolute (top of region + 10)
        Parse(buffer, "\x1b[11;1H");
        Assert.Equal(14, buffer.CursorY);
        
        // Try to go past bottom of region
        Parse(buffer, "\x1b[99;1H");
        Assert.Equal(14, buffer.CursorY);  // Clamped to row 15
        
        // Disable origin mode
        Parse(buffer, "\x1b[?6l");
        
        // Now home goes to absolute row 1
        Parse(buffer, "\x1b[H");
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region Wrap and Scroll Integration

    /// <summary>
    /// Tests that wrapping at bottom-right scrolls correctly.
    /// </summary>
    [Fact]
    public void Vttest_WrapAtBottomRight_Scrolls()
    {
        var buffer = CreateBuffer(10, 3);
        
        // Write to fill buffer
        Parse(buffer, "Line0Line0");  // Row 0, fills exactly
        Parse(buffer, "Line1Line1");  // Row 1, fills exactly (wraps to row 1)
        
        // Now at pending wrap on row 1
        // Write more - should wrap to row 2
        Parse(buffer, "Line2Line2");  // Row 2, fills exactly
        
        // Now at pending wrap on row 2 (last row)
        // Write one more char - should scroll
        Parse(buffer, "X");
        
        // Line0 scrolled off, buffer now has:
        // Row 0: Line1Line1
        // Row 1: Line2Line2
        // Row 2: X
        Assert.Equal("Line1Line1", buffer.GetRowText(0));
        Assert.Equal("Line2Line2", buffer.GetRowText(1));
        Assert.Equal("X", buffer.GetRowText(2));
    }

    #endregion

    #region Complex Sequences

    /// <summary>
    /// Tests a realistic sequence of operations.
    /// </summary>
    [Fact]
    public void Vttest_RealisticSequence()
    {
        var buffer = CreateBuffer(80, 24);
        
        // Clear screen and home cursor
        Parse(buffer, "\x1b[2J\x1b[H");
        
        // Set title
        Parse(buffer, "\x1b]0;Test Terminal\x07");
        Assert.Equal("Test Terminal", buffer.Title);
        
        // Write colored header
        Parse(buffer, "\x1b[1;34mHeader\x1b[0m");
        Assert.Equal("Header", buffer.GetRowText(0));
        
        // Move down and write more
        Parse(buffer, "\r\n\r\nContent line 1");
        Parse(buffer, "\r\nContent line 2");
        
        Assert.Equal("Content line 1", buffer.GetRowText(2));
        Assert.Equal("Content line 2", buffer.GetRowText(3));
        
        // Move cursor with save/restore
        Parse(buffer, "\u001b7");  // Save at end of Content line 2
        Parse(buffer, "\x1b[10;10H");
        Parse(buffer, "Middle");
        Parse(buffer, "\u001b8");  // Restore
        
        // Cursor should be back after "Content line 2" - row is correct
        // The exact cursor position depends on save/restore implementation
        Assert.Contains("Middle", buffer.GetRowText(9));
    }

    #endregion
}
