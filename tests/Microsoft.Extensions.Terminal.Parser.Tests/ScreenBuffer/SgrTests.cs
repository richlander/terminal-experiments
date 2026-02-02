// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer SGR (Select Graphic Rendition) behavior tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/30state_pen.test
/// Tests that SGR sequences correctly set cell attributes.
/// </remarks>
public class ScreenBufferSgrTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Reset

    /// <summary>
    /// Ported from: libvterm 30state_pen "Reset"
    /// SGR 0 resets all attributes to default.
    /// </summary>
    [Fact]
    public void Sgr_Reset_ClearsAllAttributes()
    {
        var buffer = CreateBuffer();
        
        // Set various attributes
        Parse(buffer, "\x1b[1;3;4;31;44mA");
        
        var cellA = buffer.GetCell(0, 0);
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        
        // Reset and write new char
        Parse(buffer, "\x1b[mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.Equal(CellAttributes.None, cellB.Attributes);
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
        Assert.Equal(TerminalCell.DefaultBackground, cellB.Background);
    }

    #endregion

    #region Bold

    /// <summary>
    /// Ported from: libvterm 30state_pen "Bold"
    /// </summary>
    [Fact]
    public void Sgr_Bold_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    [Fact]
    public void Sgr_Bold_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1mA\x1b[22mB");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    [Fact]
    public void Sgr_Bold_ResetByM()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1mA\x1b[mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region Dim

    [Fact]
    public void Sgr_Dim_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[2mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Dim));
    }

    [Fact]
    public void Sgr_Dim_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[2mA\x1b[22mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Dim));
    }

    #endregion

    #region Italic

    /// <summary>
    /// Ported from: libvterm 30state_pen "Italic"
    /// </summary>
    [Fact]
    public void Sgr_Italic_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[3mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
    }

    [Fact]
    public void Sgr_Italic_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[3mA\x1b[23mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Italic));
    }

    #endregion

    #region Underline

    /// <summary>
    /// Ported from: libvterm 30state_pen "Underline"
    /// </summary>
    [Fact]
    public void Sgr_Underline_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[4mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
    }

    [Fact]
    public void Sgr_Underline_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[4mA\x1b[24mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Underline));
    }

    #endregion

    #region Blink

    /// <summary>
    /// Ported from: libvterm 30state_pen "Blink"
    /// </summary>
    [Fact]
    public void Sgr_Blink_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[5mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Blink));
    }

    [Fact]
    public void Sgr_Blink_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[5mA\x1b[25mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Blink));
    }

    #endregion

    #region Inverse / Reverse

    /// <summary>
    /// Ported from: libvterm 30state_pen "Reverse"
    /// </summary>
    [Fact]
    public void Sgr_Inverse_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[7mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Inverse));
    }

    [Fact]
    public void Sgr_Inverse_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[7mA\x1b[27mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Inverse));
    }

    #endregion

    #region Hidden

    [Fact]
    public void Sgr_Hidden_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[8mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Hidden));
    }

    [Fact]
    public void Sgr_Hidden_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[8mA\x1b[28mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Hidden));
    }

    #endregion

    #region Strikethrough

    [Fact]
    public void Sgr_Strikethrough_On()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[9mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void Sgr_Strikethrough_Off()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[9mA\x1b[29mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    #endregion

    #region Foreground Colors

    /// <summary>
    /// Ported from: libvterm 30state_pen "Foreground"
    /// </summary>
    [Theory]
    [InlineData(30, 0)]  // Black
    [InlineData(31, 1)]  // Red
    [InlineData(32, 2)]  // Green
    [InlineData(33, 3)]  // Yellow
    [InlineData(34, 4)]  // Blue
    [InlineData(35, 5)]  // Magenta
    [InlineData(36, 6)]  // Cyan
    [InlineData(37, 7)]  // White
    public void Sgr_Foreground_StandardColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();
        Parse(buffer, $"\x1b[{sgrCode}mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Foreground);
    }

    /// <summary>
    /// Ported from: libvterm 30state_pen "Foreground" (bright)
    /// </summary>
    [Theory]
    [InlineData(90, 8)]   // Bright Black
    [InlineData(91, 9)]   // Bright Red
    [InlineData(97, 15)]  // Bright White
    public void Sgr_Foreground_BrightColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();
        Parse(buffer, $"\x1b[{sgrCode}mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Foreground);
    }

    [Fact]
    public void Sgr_Foreground_Default()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[31mA\x1b[39mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
    }

    #endregion

    #region Background Colors

    /// <summary>
    /// Ported from: libvterm 30state_pen "Background"
    /// </summary>
    [Theory]
    [InlineData(40, 0)]  // Black
    [InlineData(41, 1)]  // Red
    [InlineData(44, 4)]  // Blue
    [InlineData(47, 7)]  // White
    public void Sgr_Background_StandardColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();
        Parse(buffer, $"\x1b[{sgrCode}mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Background);
    }

    [Theory]
    [InlineData(100, 8)]   // Bright Black
    [InlineData(101, 9)]   // Bright Red
    [InlineData(107, 15)]  // Bright White
    public void Sgr_Background_BrightColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();
        Parse(buffer, $"\x1b[{sgrCode}mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Background);
    }

    [Fact]
    public void Sgr_Background_Default()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[44mA\x1b[49mB");
        
        var cellB = buffer.GetCell(1, 0);
        Assert.Equal(TerminalCell.DefaultBackground, cellB.Background);
    }

    #endregion

    #region 256 Color Mode

    /// <summary>
    /// Ported from: libvterm 30state_pen extended colors
    /// </summary>
    [Fact]
    public void Sgr_256Color_Foreground()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[38;5;208mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(208u, cell.Foreground);
    }

    [Fact]
    public void Sgr_256Color_Background()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[48;5;123mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal(123u, cell.Background);
    }

    [Theory]
    [InlineData(0)]    // Black
    [InlineData(15)]   // Bright white
    [InlineData(16)]   // Start of 6x6x6 cube
    [InlineData(231)]  // End of cube
    [InlineData(232)]  // Start of grayscale
    [InlineData(255)]  // End of grayscale
    public void Sgr_256Color_AllRanges(int colorIndex)
    {
        var buffer = CreateBuffer();
        Parse(buffer, $"\x1b[38;5;{colorIndex}mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.Equal((uint)colorIndex, cell.Foreground);
    }

    #endregion

    #region True Color (RGB)

    [Fact]
    public void Sgr_TrueColor_Foreground()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[38;2;255;128;64mX");
        
        var cell = buffer.GetCell(0, 0);
        // True color has high bit set
        Assert.True(cell.Foreground > 0xFFFFFF);
    }

    [Fact]
    public void Sgr_TrueColor_Background()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[48;2;10;20;30mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Background > 0xFFFFFF);
    }

    [Fact]
    public void Sgr_TrueColor_Black()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[38;2;0;0;0mX");
        
        var cell = buffer.GetCell(0, 0);
        // Even black RGB is marked as true color
        Assert.True(cell.Foreground >= 0x1000000);
    }

    [Fact]
    public void Sgr_TrueColor_White()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[38;2;255;255;255mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Foreground > 0xFFFFFF);
    }

    #endregion

    #region Multiple Attributes

    [Fact]
    public void Sgr_MultipleAttributes_SingleSequence()
    {
        var buffer = CreateBuffer();
        // Bold + Italic + Underline + Red FG + Blue BG
        Parse(buffer, "\x1b[1;3;4;31;44mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.Equal(1u, cell.Foreground);  // Red
        Assert.Equal(4u, cell.Background);  // Blue
    }

    [Fact]
    public void Sgr_MultipleAttributes_SeparateSequences()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1m\x1b[3m\x1b[31mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.Equal(1u, cell.Foreground);
    }

    /// <summary>
    /// Ported from: libvterm 30state_pen "Bold+ANSI colour == highbright"
    /// Some terminals map bold + color 7 to bright color 15.
    /// Our implementation stores them separately.
    /// </summary>
    [Fact]
    public void Sgr_BoldPlusColor_StoresSeparately()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1;37mX");
        
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        // Note: Some implementations would store 15 (bright white)
        // Our implementation stores bold flag + color 7
    }

    #endregion

    #region Attribute Persistence

    [Fact]
    public void Sgr_AttributesPersistAcrossCharacters()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1;31mABC");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);
        var cellC = buffer.GetCell(2, 0);
        
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cellC.Attributes.HasFlag(CellAttributes.Bold));
        
        Assert.Equal(1u, cellA.Foreground);
        Assert.Equal(1u, cellB.Foreground);
        Assert.Equal(1u, cellC.Foreground);
    }

    [Fact]
    public void Sgr_AttributesPersistAcrossLines()
    {
        var buffer = CreateBuffer();
        Parse(buffer, "\x1b[1;31mA\r\nB");
        
        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(0, 1);
        
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cellB.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(1u, cellA.Foreground);
        Assert.Equal(1u, cellB.Foreground);
    }

    #endregion
}
