// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for ScreenBuffer editing operations.
/// Ported from libvterm 13state_edit and VtNetCore libvtermStateEdit.
/// </summary>
/// <remarks>
/// These tests verify ICH (Insert Character), DCH (Delete Character),
/// IL (Insert Line), DL (Delete Line), and ECH (Erase Character).
/// </remarks>
public class EditingOperationsTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    private void FillWithPattern(ScreenBuffer buffer)
    {
        // Fill buffer with alphabetic pattern for visibility
        for (int row = 0; row < buffer.Height; row++)
        {
            Parse(buffer, $"\u001b[{row + 1};1H");
            for (int col = 0; col < buffer.Width; col++)
            {
                char c = (char)('a' + ((row + col) % 26));
                Parse(buffer, c.ToString());
            }
        }
        Parse(buffer, "\u001b[1;1H"); // Home cursor
    }

    #region Insert Character (ICH) Tests

    /// <summary>
    /// ICH (CSI @) inserts blank characters, shifting content right.
    /// </summary>
    [Fact]
    public void Ich_InsertsBlankAtCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write "ACD", move back 2, insert space
        Parse(buffer, "ACD\u001b[2D\u001b[@");
        
        // Should now be "A CD" (space inserted before C)
        var text = buffer.GetRowText(0);
        Assert.StartsWith("A C", text);
        
        // Cursor should stay at position 1
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// ICH with count inserts multiple blanks.
    /// </summary>
    [Fact]
    public void Ich_InsertMultipleBlanks()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABC\u001b[2D\u001b[3@");
        
        // Should insert 3 blanks before B
        var text = buffer.GetRowText(0);
        Assert.StartsWith("A   ", text);
        Assert.Equal(1, buffer.CursorX);
    }

    /// <summary>
    /// ICH should not affect other lines.
    /// </summary>
    [Fact]
    public void Ich_OnlyAffectsCurrentLine()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line1\r\nLine2\r\nLine3");
        Parse(buffer, "\u001b[2;1H\u001b[3@"); // Go to line 2, insert 3
        
        Assert.StartsWith("Line1", buffer.GetRowText(0));
        Assert.StartsWith("   L", buffer.GetRowText(1));
        Assert.StartsWith("Line3", buffer.GetRowText(2));
    }

    #endregion

    #region Delete Character (DCH) Tests

    /// <summary>
    /// DCH (CSI P) deletes characters, shifting content left.
    /// </summary>
    [Fact]
    public void Dch_DeletesCharacterAtCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write "ABBC", move back 3, delete one
        Parse(buffer, "ABBC\u001b[3D\u001b[P");
        
        // Should be "ABC" (first B deleted)
        var text = buffer.GetRowText(0);
        Assert.StartsWith("ABC", text);
        Assert.Equal(1, buffer.CursorX);
    }

    /// <summary>
    /// DCH with count deletes multiple characters.
    /// </summary>
    [Fact]
    public void Dch_DeleteMultipleCharacters()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABCDEF\u001b[5D\u001b[3P");
        
        // Delete 3 chars at position 1: "ABCDEF" -> "AEF"
        var text = buffer.GetRowText(0);
        Assert.StartsWith("AEF", text);
    }

    /// <summary>
    /// DCH at end of line should only delete to end.
    /// </summary>
    [Fact]
    public void Dch_ClampsToEndOfLine()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "Hello\u001b[1G\u001b[100P"); // Delete 100 chars from start
        
        // Should result in blank line
        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion

    #region Erase Character (ECH) Tests

    /// <summary>
    /// ECH (CSI X) erases characters without shifting.
    /// </summary>
    [Fact]
    public void Ech_ErasesWithoutShifting()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write "ABC", move back 2, erase 1
        Parse(buffer, "ABC\u001b[2D\u001b[X");
        
        // Should be "A C" (B erased but C stays in place)
        var text = buffer.GetRowText(0);
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal(' ', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(2, 0).Character);
    }

    /// <summary>
    /// ECH with count erases multiple characters.
    /// </summary>
    [Fact]
    public void Ech_EraseMultipleCharacters()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABCDEF\u001b[5D\u001b[3X");
        
        // Erase 3 chars at position 1: "ABCDEF" -> "A   EF"
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal(' ', buffer.GetCell(1, 0).Character);
        Assert.Equal(' ', buffer.GetCell(2, 0).Character);
        Assert.Equal(' ', buffer.GetCell(3, 0).Character);
        Assert.Equal('E', buffer.GetCell(4, 0).Character);
        Assert.Equal('F', buffer.GetCell(5, 0).Character);
    }

    /// <summary>
    /// ECH beyond line boundary should be clamped.
    /// </summary>
    [Fact]
    public void Ech_ClampsToEndOfLine()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "Hello\u001b[2G\u001b[100X");
        
        // Should erase from position 1 to end, H stays
        Assert.Equal('H', buffer.GetCell(0, 0).Character);
        Assert.Equal(' ', buffer.GetCell(1, 0).Character);
    }

    /// <summary>
    /// ECH cursor should not move.
    /// </summary>
    [Fact]
    public void Ech_CursorStaysInPlace()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABC\u001b[2D");
        var x = buffer.CursorX;
        
        Parse(buffer, "\u001b[3X");
        
        Assert.Equal(x, buffer.CursorX);
    }

    #endregion

    #region Insert Line (IL) Tests

    /// <summary>
    /// IL (CSI L) inserts blank lines, pushing content down.
    /// </summary>
    [Fact]
    public void Il_InsertsBlankLine()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2");
        Parse(buffer, "\u001b[2;1H\u001b[L"); // Go to line 2, insert 1
        
        Assert.StartsWith("Line0", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1)); // Blank line inserted
        Assert.StartsWith("Line1", buffer.GetRowText(2));
        Assert.StartsWith("Line2", buffer.GetRowText(3));
    }

    /// <summary>
    /// IL with count inserts multiple blank lines.
    /// </summary>
    [Fact]
    public void Il_InsertsMultipleBlankLines()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "A\r\nB\r\nC\r\nD\r\nE");
        Parse(buffer, "\u001b[2;1H\u001b[3L"); // At line 2, insert 3 blanks
        
        Assert.StartsWith("A", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
        Assert.Equal("", buffer.GetRowText(3));
        Assert.StartsWith("B", buffer.GetRowText(4));
    }

    /// <summary>
    /// IL within scroll region only affects the region.
    /// </summary>
    [Fact]
    public void Il_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        Parse(buffer, "\u001b[3;7r"); // Scroll region rows 3-7
        Parse(buffer, "\u001b[3;1H\u001b[L"); // Insert at row 3
        
        // Rows 1-2 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        Assert.StartsWith("R2", buffer.GetRowText(1));
        // Row 3 now blank
        Assert.Equal("", buffer.GetRowText(2));
        // Row 4 has old row 3 content
        Assert.StartsWith("R3", buffer.GetRowText(3));
        // Rows 8-10 unchanged
        Assert.StartsWith("R8", buffer.GetRowText(7));
        Assert.StartsWith("R9", buffer.GetRowText(8));
        Assert.StartsWith("R10", buffer.GetRowText(9));
    }

    /// <summary>
    /// IL outside scroll region should do nothing.
    /// </summary>
    [Fact]
    public void Il_OutsideScrollRegion_NoOp()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        Parse(buffer, "\u001b[3;7r"); // Scroll region rows 3-7
        Parse(buffer, "\u001b[9;1H\u001b[L"); // Try insert at row 9 (outside region)
        
        // No change
        Assert.StartsWith("R9", buffer.GetRowText(8));
    }

    #endregion

    #region Delete Line (DL) Tests

    /// <summary>
    /// DL (CSI M) deletes lines, shifting content up.
    /// </summary>
    [Fact]
    public void Dl_DeletesLine()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2");
        Parse(buffer, "\u001b[2;1H\u001b[M"); // Go to line 2, delete 1
        
        Assert.StartsWith("Line0", buffer.GetRowText(0));
        Assert.StartsWith("Line2", buffer.GetRowText(1)); // Line2 shifted up
    }

    /// <summary>
    /// DL with count deletes multiple lines.
    /// </summary>
    [Fact]
    public void Dl_DeletesMultipleLines()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "A\r\nB\r\nC\r\nD\r\nE\r\nF");
        Parse(buffer, "\u001b[2;1H\u001b[3M"); // At line 2, delete 3
        
        Assert.StartsWith("A", buffer.GetRowText(0));
        Assert.StartsWith("E", buffer.GetRowText(1)); // E shifted up
        Assert.StartsWith("F", buffer.GetRowText(2));
    }

    /// <summary>
    /// DL within scroll region only affects the region.
    /// </summary>
    [Fact]
    public void Dl_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        Parse(buffer, "\u001b[3;7r"); // Scroll region rows 3-7
        Parse(buffer, "\u001b[3;1H\u001b[M"); // Delete at row 3
        
        // Rows 1-2 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        Assert.StartsWith("R2", buffer.GetRowText(1));
        // Row 3 now has old row 4 content
        Assert.StartsWith("R4", buffer.GetRowText(2));
        // Row 7 blank (bottom of region)
        Assert.Equal("", buffer.GetRowText(6));
        // Rows 8-10 unchanged
        Assert.StartsWith("R8", buffer.GetRowText(7));
    }

    /// <summary>
    /// DL outside scroll region should do nothing.
    /// </summary>
    [Fact]
    public void Dl_OutsideScrollRegion_NoOp()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        Parse(buffer, "\u001b[3;7r"); // Scroll region rows 3-7
        Parse(buffer, "\u001b[9;1H\u001b[M"); // Try delete at row 9 (outside region)
        
        // No change
        Assert.StartsWith("R9", buffer.GetRowText(8));
    }

    #endregion

    #region Erase in Display (ED) Tests

    /// <summary>
    /// ED 0 (CSI J) erases from cursor to end of display.
    /// </summary>
    [Fact]
    public void Ed0_ErasesFromCursorToEnd()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2\r\nLine3\r\nLine4");
        Parse(buffer, "\u001b[3;3H\u001b[J"); // Row 3, col 3, erase to end
        
        Assert.StartsWith("Line0", buffer.GetRowText(0));
        Assert.StartsWith("Line1", buffer.GetRowText(1));
        // Row 3 partial erase
        Assert.Equal("Li", buffer.GetRowText(2));
        // Rows 4-5 completely erased
        Assert.Equal("", buffer.GetRowText(3));
        Assert.Equal("", buffer.GetRowText(4));
    }

    /// <summary>
    /// ED 1 (CSI 1J) erases from start of display to cursor.
    /// </summary>
    [Fact]
    public void Ed1_ErasesFromStartToCursor()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2\r\nLine3\r\nLine4");
        Parse(buffer, "\u001b[3;3H\u001b[1J"); // Row 3, col 3, erase from start
        
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));
        // Row 3 partial (first 3 chars erased)
        Assert.StartsWith("  ", buffer.GetRowText(2));
        Assert.StartsWith("Line3", buffer.GetRowText(3));
        Assert.StartsWith("Line4", buffer.GetRowText(4));
    }

    /// <summary>
    /// ED 2 (CSI 2J) erases entire display.
    /// </summary>
    [Fact]
    public void Ed2_ErasesEntireDisplay()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Line0\r\nLine1\r\nLine2\r\nLine3\r\nLine4");
        Parse(buffer, "\u001b[3;3H\u001b[2J");
        
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("", buffer.GetRowText(i));
        }
    }

    #endregion

    #region Erase in Line (EL) Tests

    /// <summary>
    /// EL 0 (CSI K) erases from cursor to end of line.
    /// </summary>
    [Fact]
    public void El0_ErasesFromCursorToEndOfLine()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Hello World!");
        Parse(buffer, "\u001b[1;6H\u001b[K"); // Col 6, erase to end
        
        Assert.Equal("Hello", buffer.GetRowText(0));
    }

    /// <summary>
    /// EL 1 (CSI 1K) erases from start of line to cursor.
    /// </summary>
    [Fact]
    public void El1_ErasesFromStartOfLineToCursor()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Hello World!");
        Parse(buffer, "\u001b[1;6H\u001b[1K"); // Col 6, erase from start
        
        // First 6 chars erased, rest remains
        Assert.StartsWith("      ", buffer.GetRowText(0));
        Assert.EndsWith("World!", buffer.GetRowText(0).TrimEnd());
    }

    /// <summary>
    /// EL 2 (CSI 2K) erases entire line.
    /// </summary>
    [Fact]
    public void El2_ErasesEntireLine()
    {
        var buffer = CreateBuffer(20, 5);
        
        Parse(buffer, "Hello World!");
        Parse(buffer, "\u001b[1;6H\u001b[2K");
        
        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion

    #region Combined Operations Tests

    /// <summary>
    /// Multiple editing operations in sequence.
    /// </summary>
    [Fact]
    public void CombinedOperations_WorkCorrectly()
    {
        var buffer = CreateBuffer(80, 10);
        
        // Complex sequence of edits
        Parse(buffer, "ABCDEFGHIJ");           // Write 10 chars
        Parse(buffer, "\u001b[1;3H");           // Move to col 3
        Parse(buffer, "\u001b[2@");             // Insert 2 blanks
        Parse(buffer, "XY");                    // Write XY in the blanks
        Parse(buffer, "\u001b[1;8H");           // Move to col 8
        Parse(buffer, "\u001b[P");              // Delete 1 char
        
        var text = buffer.GetRowText(0);
        Assert.StartsWith("ABXYCD", text.TrimEnd());
    }

    /// <summary>
    /// Insert and delete line combination.
    /// </summary>
    [Fact]
    public void InsertDeleteLine_Combination()
    {
        var buffer = CreateBuffer(80, 10);
        
        Parse(buffer, "Row1\r\nRow2\r\nRow3\r\nRow4\r\nRow5");
        Parse(buffer, "\u001b[2;1H\u001b[L");   // Insert blank at row 2
        Parse(buffer, "\u001b[4;1H\u001b[M");   // Delete row 4
        
        Assert.StartsWith("Row1", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));  // Inserted blank
        Assert.StartsWith("Row2", buffer.GetRowText(2));
        Assert.StartsWith("Row4", buffer.GetRowText(3)); // Row3 was deleted
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Editing at buffer boundaries.
    /// </summary>
    [Fact]
    public void EditingAtBoundaries()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Fill first row completely
        Parse(buffer, "1234567890");
        
        // Insert at last column - should push last char off
        Parse(buffer, "\u001b[1;10H\u001b[@X");
        
        // Delete more chars than exist
        Parse(buffer, "\u001b[1;1H\u001b[100P");
        
        // Should have empty line
        Assert.Equal("", buffer.GetRowText(0));
    }

    /// <summary>
    /// ECH should use current attributes for erased cells.
    /// </summary>
    [Fact]
    public void Ech_UsesCurrentAttributes()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Hello");
        Parse(buffer, "\u001b[44m");      // Blue background
        Parse(buffer, "\u001b[1;2H");     // Move to col 2
        Parse(buffer, "\u001b[3X");       // Erase 3 chars
        
        // Erased cells should have blue background
        Assert.Equal(4u, buffer.GetCell(1, 0).Background);
        Assert.Equal(4u, buffer.GetCell(2, 0).Background);
        Assert.Equal(4u, buffer.GetCell(3, 0).Background);
    }

    /// <summary>
    /// Zero count should default to 1.
    /// </summary>
    [Fact]
    public void ZeroCount_DefaultsToOne()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABCD\u001b[3D\u001b[0P"); // Delete 0 (=1)
        
        Assert.StartsWith("ACD", buffer.GetRowText(0));
    }

    #endregion
}
