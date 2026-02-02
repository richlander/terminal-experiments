// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Round-trip tests for ScreenBuffer: parse input, verify output can be re-parsed.
/// </summary>
/// <remarks>
/// These tests verify that:
/// 1. Parsing input produces expected buffer state
/// 2. The buffer state could (in theory) be rendered back to ANSI and re-parsed
/// 
/// This is important for terminal multiplexing scenarios.
/// </remarks>
public class ScreenBufferRoundTripTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Basic Text Round Trip

    [Fact]
    public void RoundTrip_SimpleText()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Hello, World!");
        
        Assert.Equal("Hello, World!", buffer.GetRowText(0));
    }

    [Fact]
    public void RoundTrip_MultilineText()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Line 1\r\nLine 2\r\nLine 3");
        
        Assert.Equal("Line 1", buffer.GetRowText(0));
        Assert.Equal("Line 2", buffer.GetRowText(1));
        Assert.Equal("Line 3", buffer.GetRowText(2));
    }

    [Fact]
    public void RoundTrip_TextWithColors()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Red "Hello", reset, Blue "World"
        Parse(buffer, "\x1b[31mHello\x1b[0m \x1b[34mWorld\x1b[0m");
        
        Assert.Equal("Hello World", buffer.GetRowText(0));
        
        // Verify colors were applied
        var cellH = buffer.GetCell(0, 0);
        Assert.Equal(1u, cellH.Foreground); // Red
        
        var cellW = buffer.GetCell(6, 0);
        Assert.Equal(4u, cellW.Foreground); // Blue
    }

    #endregion

    #region Cursor State Round Trip

    [Fact]
    public void RoundTrip_CursorPosition()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[10;20H");
        
        Assert.Equal(19, buffer.CursorX); // 0-indexed
        Assert.Equal(9, buffer.CursorY);
    }

    [Fact]
    public void RoundTrip_CursorVisibility()
    {
        var buffer = CreateBuffer(80, 25);
        
        Assert.True(buffer.CursorVisible);
        
        Parse(buffer, "\x1b[?25l");
        Assert.False(buffer.CursorVisible);
        
        Parse(buffer, "\x1b[?25h");
        Assert.True(buffer.CursorVisible);
    }

    [Fact]
    public void RoundTrip_SaveRestoreCursor()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[5;10H");  // Move
        Parse(buffer, "\u001b7");        // Save
        Parse(buffer, "\x1b[1;1H");   // Move elsewhere
        Parse(buffer, "\u001b8");        // Restore
        
        Assert.Equal(9, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    #endregion

    #region Screen Content Round Trip

    [Fact]
    public void RoundTrip_ClearScreen()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "Some content");
        Assert.NotEqual("", buffer.GetRowText(0));
        
        Parse(buffer, "\x1b[2J");
        Assert.Equal("", buffer.GetRowText(0));
    }

    [Fact]
    public void RoundTrip_ScrollRegion()
    {
        var buffer = CreateBuffer(80, 10);
        
        // Fill with content
        for (int i = 0; i < 10; i++)
        {
            Parse(buffer, $"\x1b[{i + 1};1HRow{i}");
        }
        
        // Set scroll region and trigger scroll
        Parse(buffer, "\x1b[3;7r");   // Rows 3-7
        Parse(buffer, "\x1b[7;1H");   // Move to row 7
        Parse(buffer, "\nNewLine");   // Scroll within region
        
        // Rows outside region should be unchanged
        Assert.StartsWith("Row0", buffer.GetRowText(0));
        Assert.StartsWith("Row1", buffer.GetRowText(1));
    }

    #endregion

    #region Attributes Round Trip

    [Fact]
    public void RoundTrip_BoldAttribute()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[1mBold\x1b[0m Normal");
        
        var boldCell = buffer.GetCell(0, 0);
        var normalCell = buffer.GetCell(5, 0);
        
        Assert.True(boldCell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(normalCell.Attributes.HasFlag(CellAttributes.Bold));
    }

    [Fact]
    public void RoundTrip_MultipleAttributes()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Bold + Italic + Underline
        Parse(buffer, "\x1b[1;3;4mStyled\x1b[0m");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
    }

    [Fact]
    public void RoundTrip_256Colors()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[38;5;196mRed\x1b[0m");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(196u, cell.Foreground);
    }

    [Fact]
    public void RoundTrip_TrueColor()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[38;2;255;128;64mOrange\x1b[0m");
        
        var cell = buffer.GetCell(0, 0);
        // True color is stored with RGB values
        Assert.True(cell.Foreground > 0xFFFFFF);
    }

    #endregion

    #region Title Round Trip

    [Fact]
    public void RoundTrip_WindowTitle()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b]0;My Terminal\x07");
        
        Assert.Equal("My Terminal", buffer.Title);
    }

    [Fact]
    public void RoundTrip_TitleWithStTerminator()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b]2;Another Title\x1b\\");
        
        Assert.Equal("Another Title", buffer.Title);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void RoundTrip_TypicalShellSession()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Simulate a typical shell session
        Parse(buffer, "\x1b]0;user@host:~\x07");  // Set title
        Parse(buffer, "\x1b[32muser@host\x1b[0m:\x1b[34m~\x1b[0m$ ");  // Colored prompt
        Parse(buffer, "ls -la");  // Command
        Parse(buffer, "\r\n");
        Parse(buffer, "total 42\r\n");
        Parse(buffer, "drwxr-xr-x  2 user user 4096 Jan  1 00:00 .\r\n");
        
        Assert.Equal("user@host:~", buffer.Title);
        Assert.Contains("user@host", buffer.GetRowText(0));
        Assert.Contains("ls -la", buffer.GetRowText(0));
        Assert.Contains("total 42", buffer.GetRowText(1));
    }

    [Fact]
    public void RoundTrip_EditorLikeApplication()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Simulate entering a full-screen editor
        Parse(buffer, "\x1b[?1049h");  // Alternate screen
        Parse(buffer, "\x1b[2J");       // Clear
        Parse(buffer, "\x1b[H");        // Home
        
        // Status bar at top
        Parse(buffer, "\x1b[7m File: test.txt  Ln 1, Col 1 \x1b[0m");
        
        // Content
        Parse(buffer, "\x1b[2;1H");
        Parse(buffer, "Hello, this is line 1");
        Parse(buffer, "\x1b[3;1H");
        Parse(buffer, "And this is line 2");
        
        // Cursor position
        Parse(buffer, "\x1b[2;5H");
        
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
        Assert.Contains("test.txt", buffer.GetRowText(0));
    }

    [Fact]
    public void RoundTrip_ProgressBar()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Simulate a progress bar being updated
        Parse(buffer, "Downloading: [");
        
        for (int i = 0; i < 10; i++)
        {
            Parse(buffer, "#");
        }
        
        Parse(buffer, "          ] 50%");
        Parse(buffer, "\r");  // Carriage return
        Parse(buffer, "Downloading: [");
        
        for (int i = 0; i < 20; i++)
        {
            Parse(buffer, "#");
        }
        
        Parse(buffer, "] 100%");
        
        Assert.Contains("####################", buffer.GetRowText(0));
        Assert.Contains("100%", buffer.GetRowText(0));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RoundTrip_EmptyInput()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "");
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        Assert.Equal("", buffer.GetRowText(0));
    }

    [Fact]
    public void RoundTrip_OnlyControlCodes()
    {
        var buffer = CreateBuffer(80, 25);
        
        Parse(buffer, "\x1b[H\x1b[2J\x1b[?25l\x1b[?25h");
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        Assert.True(buffer.CursorVisible);
    }

    [Fact]
    public void RoundTrip_RapidUpdates()
    {
        var buffer = CreateBuffer(80, 25);
        
        // Simulate rapid counter updates
        for (int i = 0; i < 100; i++)
        {
            Parse(buffer, $"\rCount: {i,5}");
        }
        
        Assert.Contains("Count:    99", buffer.GetRowText(0));
    }

    #endregion
}
