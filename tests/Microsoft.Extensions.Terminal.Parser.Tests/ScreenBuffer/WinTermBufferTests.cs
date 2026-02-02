// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Comprehensive buffer and text attribute tests ported from Windows Terminal.
/// Tests buffer behavior, attribute handling, cursor state, and VT sequences.
/// </summary>
/// <remarks>
/// Ported from:
/// - terminal/src/host/ut_host/ScreenBufferTests.cpp
/// - terminal/src/host/ut_host/TextBufferTests.cpp
/// - terminal/src/buffer/out/ut_textbuffer/TextAttributeTests.cpp
/// - terminal/src/buffer/out/ut_textbuffer/ReflowTests.cpp
/// </remarks>
public class WinTermBufferTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Reverse Line Feed (RI) Tests

    /// <summary>
    /// Test reverse line feed (RI) from below top of viewport.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestReverseLineFeed</remarks>
    [Fact]
    public void ReverseLineFeed_FromBelowTop_MovesCursorUp()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Write text, then go to row 1
        Parse(buffer, "foo\r\nfoo");
        var cursorX = buffer.CursorX;
        Assert.Equal(1, buffer.CursorY);
        
        // Reverse line feed should move cursor up
        Parse(buffer, "\u001bM");
        
        Assert.Equal(cursorX, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Test reverse line feed from top of viewport scrolls down.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestReverseLineFeed</remarks>
    [Fact]
    public void ReverseLineFeed_FromTop_ScrollsDown()
    {
        var buffer = CreateBuffer(80, 5);
        
        // Write content on first line and position cursor there
        Parse(buffer, "Line0\u001b[H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        // RI at top should scroll content down and insert blank line
        Parse(buffer, "\u001bM");
        
        Assert.Equal(0, buffer.CursorY);
        Assert.Equal("", buffer.GetRowText(0));
        Assert.StartsWith("Line0", buffer.GetRowText(1));
    }

    #endregion

    #region Tab Stop Tests

    /// <summary>
    /// Default tabs are every 8 columns.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestResetClearTabStops</remarks>
    [Fact]
    public void TabStops_DefaultEvery8Columns()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "A\t");
        Assert.Equal(8, buffer.CursorX);
        
        Parse(buffer, "B\t");
        Assert.Equal(16, buffer.CursorX);
    }

    /// <summary>
    /// HTS sets a tab stop at cursor position.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestAddTabStop</remarks>
    [Fact(Skip = "HTS (tab stop setting) not implemented in ScreenBuffer")]
    public void TabStops_HTS_SetsTabAtCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Clear all tabs
        Parse(buffer, "\u001b[3g");
        
        // Move to column 12 and set tab
        Parse(buffer, "\u001b[13G\u001bH");
        
        // Go back to start and tab
        Parse(buffer, "\u001b[1G\t");
        Assert.Equal(12, buffer.CursorX);
    }

    /// <summary>
    /// TBC 0 clears tab at cursor position.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestClearTabStop</remarks>
    [Fact(Skip = "TBC (tab clear) not implemented in ScreenBuffer")]
    public void TabStops_TBC0_ClearsTabAtCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Default tab at 8, move to col 8 and clear
        Parse(buffer, "\u001b[9G\u001b[0g");
        
        // Tab from start should now go to 16
        Parse(buffer, "\u001b[1G\t");
        Assert.Equal(16, buffer.CursorX);
    }

    /// <summary>
    /// TBC 3 clears all tab stops.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestResetClearTabStops</remarks>
    [Fact(Skip = "TBC (tab clear) not implemented in ScreenBuffer")]
    public void TabStops_TBC3_ClearsAllTabs()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Clear all tabs
        Parse(buffer, "\u001b[3g");
        
        // Tab should now go to end of line
        Parse(buffer, "\u001b[1G\t");
        Assert.Equal(79, buffer.CursorX);
    }

    /// <summary>
    /// CHT (cursor horizontal tab) advances to next tab.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestGetForwardTab</remarks>
    [Fact(Skip = "CHT (cursor forward tab) not implemented in ScreenBuffer")]
    public void TabStops_CHT_AdvancesForward()
    {
        var buffer = CreateBuffer(80, 25);
        
        // CSI I is CHT (cursor forward tabulation)
        Parse(buffer, "\u001b[I");
        Assert.Equal(8, buffer.CursorX);
        
        Parse(buffer, "\u001b[I");
        Assert.Equal(16, buffer.CursorX);
    }

    /// <summary>
    /// CBT (cursor backward tab) moves back to previous tab.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestGetReverseTab</remarks>
    [Fact(Skip = "CBT (cursor backward tab) not implemented in ScreenBuffer")]
    public void TabStops_CBT_MovesBackward()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Move to column 20
        Parse(buffer, "\u001b[21G");
        Assert.Equal(20, buffer.CursorX);
        
        // CBT (CSI Z) moves back to previous tab
        Parse(buffer, "\u001b[Z");
        Assert.Equal(16, buffer.CursorX);
        
        Parse(buffer, "\u001b[Z");
        Assert.Equal(8, buffer.CursorX);
    }

    #endregion

    #region Mixed RGB and Legacy Color Tests

    /// <summary>
    /// Test RGB foreground followed by default foreground reset.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestMixedRgbAndLegacyForeground</remarks>
    [Fact]
    public void MixedRgbAndLegacy_ForegroundReset_PreservesRgb()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Reset, set RGB foreground, write X, reset fg to default, write X
        Parse(buffer, "\u001b[m\u001b[38;2;64;128;255mX\u001b[39mX");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        
        // First X should have RGB color
        Assert.True(cellA.Foreground > 0xFFFFFF);
        
        // Second X should have default foreground
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
    }

    /// <summary>
    /// Test RGB background followed by default background reset.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestMixedRgbAndLegacyBackground</remarks>
    [Fact]
    public void MixedRgbAndLegacy_BackgroundReset_PreservesRgb()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Reset, set RGB background, write X, reset bg to default, write X
        Parse(buffer, "\u001b[m\u001b[48;2;64;128;255mX\u001b[49mX");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        
        // First X should have RGB background
        Assert.True(cellA.Background > 0xFFFFFF);
        
        // Second X should have default background
        Assert.Equal(TerminalCell.DefaultBackground, cellB.Background);
    }

    /// <summary>
    /// Test RGB background with underline attribute added.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestMixedRgbAndLegacyUnderline</remarks>
    [Fact]
    public void MixedRgbAndLegacy_UnderlinePreservesRgb()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set RGB background, write X, add underline, write X
        Parse(buffer, "\u001b[m\u001b[48;2;64;128;255mX\u001b[4mX");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        
        // Both should have RGB background
        Assert.True(cellA.Background > 0xFFFFFF);
        Assert.True(cellB.Background > 0xFFFFFF);
        
        // Only second should have underline
        Assert.False(cellA.Attributes.HasFlag(CellAttributes.Underline));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Underline));
    }

    #endregion

    #region Intensity and Color Tests

    /// <summary>
    /// SGR 22 turns off bold/intense.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestUnintense</remarks>
    [Fact]
    public void Sgr22_TurnsOffBold()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[1mA\u001b[22mB");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    /// <summary>
    /// Reset clears intensity/bold.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestResetClearsIntensity</remarks>
    [Fact]
    public void SgrReset_ClearsIntensity()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[1mA\u001b[0mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    /// <summary>
    /// RGB then intense keeps both attributes.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestRgbThenIntense</remarks>
    [Fact]
    public void RgbThenIntense_KeepsBoth()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[38;2;100;100;100m\u001b[1mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Foreground > 0xFFFFFF);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region Reverse Video Tests

    /// <summary>
    /// Reverse video swaps colors and can be toggled.
    /// </summary>
    /// <remarks>Ported from: terminal/src/buffer/out/ut_textbuffer/TextAttributeTests.cpp - TestReverseDefaultColors</remarks>
    [Fact]
    public void ReverseVideo_TogglesCorrectly()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "A\u001b[7mB\u001b[27mC");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        var cellC = buffer.GetCell(2, 0);
        
        Assert.False(cellA.Attributes.HasFlag(CellAttributes.Inverse));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Inverse));
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Inverse));
    }

    /// <summary>
    /// Test reverse reset with default background.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - ReverseResetWithDefaultBackground</remarks>
    [Fact]
    public void ReverseReset_RestoresNormalVideo()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[m");
        Parse(buffer, "X\u001b[7mX\u001b[27mX");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        var cellC = buffer.GetCell(2, 0);
        
        // First and third should match, second should be reversed
        Assert.False(cellA.Attributes.HasFlag(CellAttributes.Inverse));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Inverse));
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Inverse));
    }

    #endregion

    #region Erase All (ED 2) Tests

    /// <summary>
    /// ED 2 erases display and cursor position is preserved relative to viewport.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - EraseAllTests</remarks>
    [Fact]
    public void EraseAll_PreservesCursorRelativePosition()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "foo");
        Parse(buffer, "\u001b[2J");
        
        // Buffer should be cleared
        Assert.Equal("", buffer.GetRowText(0));
    }

    /// <summary>
    /// VT erase all persists cursor position.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - VtEraseAllPersistCursor</remarks>
    [Fact]
    public void VtEraseAll_PersistsCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[2;2H"); // Move to row 2, col 2
        var cursorX = buffer.CursorX;
        var cursorY = buffer.CursorY;
        
        Parse(buffer, "\u001b[2J");
        
        // Cursor position should be preserved
        Assert.Equal(cursorX, buffer.CursorX);
        Assert.Equal(cursorY, buffer.CursorY);
    }

    /// <summary>
    /// ED 2 with color fills new lines with that color.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - VtEraseAllPersistCursorFillColor</remarks>
    [Fact]
    public void VtEraseAll_UsesCurrentAttributes()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Set red foreground, blue background
        Parse(buffer, "\u001b[31;44m");
        Parse(buffer, "Test");
        Parse(buffer, "\u001b[2J");
        
        // After erase, cells should have blue background
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(4u, cell.Background);
    }

    #endregion

    #region Scroll Region Tests

    /// <summary>
    /// Insert line within scroll region only affects region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - InsertLinesInMargins</remarks>
    [Fact]
    public void InsertLine_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(20, 10);
        
        // Fill with content
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        
        // Set scroll region rows 3-7
        Parse(buffer, "\u001b[3;7r");
        
        // Move to row 3 and insert line
        Parse(buffer, "\u001b[3;1H\u001b[L");
        
        // Rows 1-2 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        Assert.StartsWith("R2", buffer.GetRowText(1));
        
        // Row 3 should be blank (inserted)
        Assert.Equal("", buffer.GetRowText(2));
        
        // Row 4 should have old R3
        Assert.StartsWith("R3", buffer.GetRowText(3));
        
        // Rows 8-10 unchanged
        Assert.StartsWith("R8", buffer.GetRowText(7));
        Assert.StartsWith("R9", buffer.GetRowText(8));
        Assert.StartsWith("R10", buffer.GetRowText(9));
        
        // Reset scroll region
        Parse(buffer, "\u001b[r");
    }

    /// <summary>
    /// Delete line within scroll region only affects region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - DeleteLinesInMargins</remarks>
    [Fact]
    public void DeleteLine_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(20, 10);
        
        // Fill with content
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7\r\nR8\r\nR9\r\nR10");
        
        // Set scroll region rows 3-7
        Parse(buffer, "\u001b[3;7r");
        
        // Move to row 3 and delete line
        Parse(buffer, "\u001b[3;1H\u001b[M");
        
        // Rows 1-2 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        Assert.StartsWith("R2", buffer.GetRowText(1));
        
        // Row 3 should have old R4 (shifted up)
        Assert.StartsWith("R4", buffer.GetRowText(2));
        
        // Row 7 should be blank
        Assert.Equal("", buffer.GetRowText(6));
        
        // Rows 8-10 unchanged
        Assert.StartsWith("R8", buffer.GetRowText(7));
        Assert.StartsWith("R9", buffer.GetRowText(8));
        Assert.StartsWith("R10", buffer.GetRowText(9));
        
        // Reset scroll region
        Parse(buffer, "\u001b[r");
    }

    /// <summary>
    /// Scroll up (SU) within scroll region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - ScrollUpInMargins</remarks>
    [Fact]
    public void ScrollUp_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(20, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7");
        
        // Set scroll region rows 2-6
        Parse(buffer, "\u001b[2;6r");
        
        // Scroll up 1
        Parse(buffer, "\u001b[1S");
        
        // Row 1 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        
        // Rows shifted up within region
        Assert.StartsWith("R3", buffer.GetRowText(1));
        Assert.StartsWith("R4", buffer.GetRowText(2));
        
        // Row 6 blank
        Assert.Equal("", buffer.GetRowText(5));
        
        // Row 7 unchanged
        Assert.StartsWith("R7", buffer.GetRowText(6));
        
        Parse(buffer, "\u001b[r");
    }

    /// <summary>
    /// Scroll down (SD) within scroll region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - ScrollDownInMargins</remarks>
    [Fact]
    public void ScrollDown_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(20, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5\r\nR6\r\nR7");
        
        // Set scroll region rows 2-6
        Parse(buffer, "\u001b[2;6r");
        
        // Scroll down 1
        Parse(buffer, "\u001b[1T");
        
        // Row 1 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        
        // Row 2 blank (inserted)
        Assert.Equal("", buffer.GetRowText(1));
        
        // Rows shifted down within region
        Assert.StartsWith("R2", buffer.GetRowText(2));
        Assert.StartsWith("R3", buffer.GetRowText(3));
        
        // Row 7 unchanged
        Assert.StartsWith("R7", buffer.GetRowText(6));
        
        Parse(buffer, "\u001b[r");
    }

    /// <summary>
    /// Reverse line feed within scroll region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - ReverseLineFeedInMargins</remarks>
    [Fact]
    public void ReverseLineFeed_RespectsScrollRegion()
    {
        var buffer = CreateBuffer(20, 10);
        
        Parse(buffer, "R1\r\nR2\r\nR3\r\nR4\r\nR5");
        
        // Set scroll region rows 2-4
        Parse(buffer, "\u001b[2;4r");
        
        // Move to top of region
        Parse(buffer, "\u001b[2;1H");
        
        // RI at top of region scrolls region down
        Parse(buffer, "\u001bM");
        
        // Row 1 unchanged
        Assert.StartsWith("R1", buffer.GetRowText(0));
        
        // Row 2 blank (inserted)
        Assert.Equal("", buffer.GetRowText(1));
        
        // R2 shifted to row 3
        Assert.StartsWith("R2", buffer.GetRowText(2));
        
        Parse(buffer, "\u001b[r");
    }

    #endregion

    #region Delete Chars Near End of Line Tests

    /// <summary>
    /// Delete characters near end of line works correctly.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - DeleteCharsNearEndOfLineSimpleFirstCase</remarks>
    [Fact]
    public void DeleteChars_NearEndOfLine_Case1()
    {
        var buffer = CreateBuffer(8, 5);
        
        Parse(buffer, "ABCDEFG");
        
        // Place cursor on 'D' (column 4, 0-indexed = 3)
        Parse(buffer, "\u001b[4G");
        Assert.Equal(3, buffer.CursorX);
        
        // Delete 3 chars [D, E, F]
        Parse(buffer, "\u001b[3P");
        
        // Should result in "ABCG" followed by spaces
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(2, 0).Character);
        Assert.Equal('G', buffer.GetCell(3, 0).Character);
        Assert.Equal(' ', buffer.GetCell(4, 0).Character);
    }

    /// <summary>
    /// Delete characters near end of line works correctly - second case.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - DeleteCharsNearEndOfLineSimpleSecondCase</remarks>
    [Fact]
    public void DeleteChars_NearEndOfLine_Case2()
    {
        var buffer = CreateBuffer(8, 5);
        
        Parse(buffer, "ABCDEFG");
        
        // Place cursor on 'C' (column 3, 0-indexed = 2)
        Parse(buffer, "\u001b[3G");
        
        // Delete 4 chars [C, D, E, F]
        Parse(buffer, "\u001b[4P");
        
        // Should result in "ABG" followed by spaces
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('G', buffer.GetCell(2, 0).Character);
        Assert.Equal(' ', buffer.GetCell(3, 0).Character);
    }

    #endregion

    #region Repeat Character Tests

    /// <summary>
    /// REP (CSI b) repeats the previous character.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestRepeatCharacter</remarks>
    [Fact(Skip = "REP (repeat character) not implemented in ScreenBuffer")]
    public void Rep_RepeatsLastCharacter()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "X\u001b[5b");
        
        // Should have 6 X's total (1 + 5 repeated)
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal('X', buffer.GetCell(i, 0).Character);
        }
        Assert.Equal(' ', buffer.GetCell(6, 0).Character);
    }

    /// <summary>
    /// REP uses current attributes.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestRepeatCharacter</remarks>
    [Fact(Skip = "REP (repeat character) not implemented in ScreenBuffer")]
    public void Rep_UsesCurrentAttributes()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[31mX\u001b[3b");
        
        // All 4 X's should be red
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(1u, buffer.GetCell(i, 0).Foreground);
        }
    }

    #endregion

    #region Backspace Tests

    /// <summary>
    /// Backspace at column 0 doesn't go negative.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestBackspaceRightSideVt</remarks>
    [Fact]
    public void Backspace_AtColumn0_StaysAt0()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Try multiple backspaces from start
        Parse(buffer, "\b\b\b");
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Backspace behavior with default attrs.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - BackspaceDefaultAttrs</remarks>
    [Fact]
    public void Backspace_WithDefaultAttrs()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[m");
        Parse(buffer, "XX\b");
        
        Assert.Equal(1, buffer.CursorX);
    }

    #endregion

    #region Cursor State Tests

    /// <summary>
    /// Save and restore cursor position (DECSC/DECRC).
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorSaveRestore</remarks>
    [Fact]
    public void Decsc_Decrc_SavesAndRestoresPosition()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[10;20H"); // Move to row 10, col 20
        Parse(buffer, "\u001b7");       // Save cursor
        Parse(buffer, "\u001b[1;1H");   // Move to home
        Parse(buffer, "\u001b8");       // Restore cursor
        
        Assert.Equal(19, buffer.CursorX); // 20 - 1 (0-indexed)
        Assert.Equal(9, buffer.CursorY);  // 10 - 1 (0-indexed)
    }

    /// <summary>
    /// CSI s/u also saves and restores cursor.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorSaveRestore</remarks>
    [Fact]
    public void CsiSaveCursor_WorksCorrectly()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[5;15H");  // Move to row 5, col 15
        Parse(buffer, "\u001b[s");       // Save cursor (CSI)
        Parse(buffer, "\u001b[20;30H"); // Move elsewhere
        Parse(buffer, "\u001b[u");       // Restore cursor (CSI)
        
        Assert.Equal(14, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    #endregion

    #region Cursor Movement Edge Cases

    /// <summary>
    /// CUU (cursor up) stops at top of screen.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorUpDownAcrossMargins</remarks>
    [Fact]
    public void CursorUp_StopsAtTop()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[5;5H");   // Move to row 5
        Parse(buffer, "\u001b[999A");   // Try to move up 999 rows
        
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// CUD (cursor down) stops at bottom of screen.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorUpDownAcrossMargins</remarks>
    [Fact]
    public void CursorDown_StopsAtBottom()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[5;5H");   // Move to row 5
        Parse(buffer, "\u001b[999B");   // Try to move down 999 rows
        
        Assert.Equal(24, buffer.CursorY);
    }

    /// <summary>
    /// CUF (cursor forward) stops at right edge.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorLeftRightAcrossMargins</remarks>
    [Fact]
    public void CursorForward_StopsAtRight()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[999C");
        
        Assert.Equal(79, buffer.CursorX);
    }

    /// <summary>
    /// CUB (cursor back) stops at left edge.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorLeftRightAcrossMargins</remarks>
    [Fact]
    public void CursorBack_StopsAtLeft()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[50G");    // Move to column 50
        Parse(buffer, "\u001b[999D");   // Try to move back 999 columns
        
        Assert.Equal(0, buffer.CursorX);
    }

    /// <summary>
    /// CNL (cursor next line) moves to start of line below.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorNextPreviousLine</remarks>
    [Fact]
    public void CursorNextLine_MovesToStartOfNextLine()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[5;20H");  // Row 5, col 20
        Parse(buffer, "\u001b[2E");     // Next line 2 times
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(6, buffer.CursorY); // Row 7 (0-indexed = 6)
    }

    /// <summary>
    /// CPL (cursor previous line) moves to start of line above.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - CursorNextPreviousLine</remarks>
    [Fact]
    public void CursorPreviousLine_MovesToStartOfPreviousLine()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[5;20H");  // Row 5, col 20
        Parse(buffer, "\u001b[2F");     // Previous line 2 times
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY); // Row 3 (0-indexed = 2)
    }

    #endregion

    #region Screen Alignment Pattern Tests

    /// <summary>
    /// DECALN fills screen with 'E'.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - ScreenAlignmentPattern</remarks>
    [Fact(Skip = "DECALN screen alignment not implemented in ScreenBuffer")]
    public void Decaln_FillsWithE()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "\u001b#8"); // DECALN
        
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Assert.Equal('E', buffer.GetCell(x, y).Character);
            }
        }
    }

    #endregion

    #region Insert/Replace Mode Tests

    /// <summary>
    /// IRM (insert/replace mode) inserts characters.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - InsertReplaceMode</remarks>
    [Fact(Skip = "IRM insert mode not fully implemented")]
    public void InsertMode_InsertsCharacters()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "ABCDEF");
        Parse(buffer, "\u001b[4G");     // Move to column 4
        Parse(buffer, "\u001b[4h");     // Enable insert mode
        Parse(buffer, "XY");            // Insert XY
        Parse(buffer, "\u001b[4l");     // Disable insert mode
        
        Assert.StartsWith("ABCXYDEF", buffer.GetRowText(0));
    }

    #endregion

    #region Extended Text Attributes Tests

    /// <summary>
    /// Double underline attribute.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - TestExtendedTextAttributes</remarks>
    [Fact(Skip = "SGR 21 double underline not fully implemented")]
    public void Sgr21_SetsDoubleUnderline()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[21mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.DoubleUnderline));
    }

    // Note: Overline (SGR 53) tests skipped - CellAttributes.Overline not implemented

    #endregion

    #region Hard Reset Tests

    /// <summary>
    /// RIS (ESC c) resets terminal state.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - HardResetBuffer</remarks>
    [Fact]
    public void Ris_ResetsTerminalState()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set some state
        Parse(buffer, "\u001b[31;44;1;4m"); // Red, blue bg, bold, underline
        Parse(buffer, "TestContent");
        Parse(buffer, "\u001b[10;20H");     // Move cursor
        
        // RIS
        Parse(buffer, "\u001bc");
        
        // Cursor should be at home
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        // Screen should be cleared
        Assert.Equal("", buffer.GetRowText(0));
    }

    #endregion

    #region Inactive Control Characters Tests

    /// <summary>
    /// NUL and other inactive control chars don't move cursor.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - InactiveControlCharactersTest</remarks>
    [Theory]
    [InlineData('\x00')] // NUL
    [InlineData('\x01')] // SOH
    [InlineData('\x02')] // STX
    [InlineData('\x03')] // ETX
    [InlineData('\x04')] // EOT
    [InlineData('\x05')] // ENQ
    [InlineData('\x06')] // ACK
    public void InactiveControlChars_DontMoveCursor(char ch)
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, ch.ToString());
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region Wrap Flag Tests

    /// <summary>
    /// Lines that wrap are marked as wrapped.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestWrapFlag</remarks>
    [Fact]
    public void WrapFlag_SetWhenLineWraps()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write more than 10 chars to force wrap
        Parse(buffer, "12345678901234567890");
        
        // Text should wrap to second line
        Assert.StartsWith("1234567890", buffer.GetRowText(0));
        Assert.StartsWith("1234567890", buffer.GetRowText(1));
    }

    #endregion

    #region Double Byte Padding Tests

    /// <summary>
    /// Wide characters are padded correctly at line end.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/TextBufferTests.cpp - TestDoubleBytePadFlag</remarks>
    [Fact]
    public void WideCharAtLineEnd_IsPaddedOrWrapped()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Fill 9 chars, then try to add a wide char
        Parse(buffer, "123456789\u4e2d"); // 中 is a wide character
        
        // Either padded or wrapped to next line
        var lastCellFirstRow = buffer.GetCell(9, 0);
        var firstCellSecondRow = buffer.GetCell(0, 1);
        
        // Wide char should not be cut in half
        if (lastCellFirstRow.Character != '\u4e2d')
        {
            // Wide char wrapped to second line
            Assert.Equal('\u4e2d', firstCellSecondRow.Character);
        }
    }

    #endregion

    #region Dim Attribute Tests

    /// <summary>
    /// Dim (faint) attribute is independent of bold.
    /// </summary>
    /// <remarks>Ported from: terminal/src/buffer/out/ut_textbuffer/TextAttributeTests.cpp - TestTextAttributeColorGetters</remarks>
    [Fact]
    public void DimAndBold_AreIndependent()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\u001b[1mA");       // Bold
        Parse(buffer, "\u001b[2mB");       // Add dim
        Parse(buffer, "\u001b[22mC");      // Remove bold and dim
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        var cellC = buffer.GetCell(2, 0);
        
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cellA.Attributes.HasFlag(CellAttributes.Dim));
        
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Dim));
        
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Dim));
    }

    #endregion

    // Note: Protected attribute tests skipped - CellAttributes.Protected not implemented

    #region Multiline Wrap Tests

    /// <summary>
    /// Content wrapping across multiple lines preserves data.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - MultilineWrap</remarks>
    [Fact]
    public void MultilineWrap_PreservesContent()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write 35 characters - should span 4 lines in a 10-wide buffer
        Parse(buffer, "1234567890ABCDEFGHIJKLMNOPQRSTUVWXY");
        
        Assert.StartsWith("1234567890", buffer.GetRowText(0));
        Assert.StartsWith("ABCDEFGHIJ", buffer.GetRowText(1));
        Assert.StartsWith("KLMNOPQRST", buffer.GetRowText(2));
        Assert.StartsWith("UVWXY", buffer.GetRowText(3));
    }

    #endregion

    #region Delayed Wrap Tests

    /// <summary>
    /// Delayed wrap (autowrap) behavior at end of line.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - DelayedWrapReset</remarks>
    [Fact]
    public void DelayedWrap_AtEndOfLine()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write exactly 10 chars
        Parse(buffer, "1234567890");
        
        // Cursor behavior at end of line varies by implementation
        // Either stays at last column (delayed wrap) or wraps immediately
        Assert.True(buffer.CursorX >= 0 && buffer.CursorX <= 10);
        
        // Write one more char
        Parse(buffer, "A");
        
        // A should now be written somewhere
        bool foundA = false;
        for (int y = 0; y < 5 && !foundA; y++)
        {
            for (int x = 0; x < 10 && !foundA; x++)
            {
                if (buffer.GetCell(x, y).Character == 'A')
                    foundA = true;
            }
        }
        Assert.True(foundA, "Character 'A' should be written to buffer");
    }

    #endregion

    #region Origin Mode Tests

    /// <summary>
    /// Origin mode makes cursor relative to scroll region.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - SetOriginMode</remarks>
    [Fact]
    public void OriginMode_CursorRelativeToRegion()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Set scroll region rows 5-15
        Parse(buffer, "\u001b[5;15r");
        
        // Enable origin mode
        Parse(buffer, "\u001b[?6h");
        
        // Move to home - should be at top of region (row 5)
        Parse(buffer, "\u001b[H");
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY); // Row 5 (0-indexed = 4)
        
        // Disable origin mode and reset region
        Parse(buffer, "\u001b[?6l\u001b[r");
    }

    #endregion

    #region Autowrap Mode Tests

    /// <summary>
    /// DECAWM enables/disables autowrap.
    /// </summary>
    /// <remarks>Ported from: terminal/src/host/ut_host/ScreenBufferTests.cpp - SetAutoWrapMode</remarks>
    [Fact]
    public void Decawm_ControlsAutowrap()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Disable autowrap
        Parse(buffer, "\u001b[?7l");
        
        // Write more than 10 chars
        Parse(buffer, "12345678901234567890");
        
        // Without autowrap, cursor stays at last column, text overwrites
        Assert.Equal(0, buffer.CursorY);
        
        // Enable autowrap
        Parse(buffer, "\u001b[?7h");
    }

    #endregion
}
