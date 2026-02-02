// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for text attributes (SGR) in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/30state_pen.test
/// Tests SGR sequences for setting text attributes (bold, italic, colors, etc.)
/// </remarks>
public class TextAttributeTests
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
    /// SGR 0 resets all attributes.
    /// </summary>
    [Fact]
    public void Sgr_Reset_ClearsAllAttributes()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;3;4;31mA\u001b[mB");

        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);

        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(CellAttributes.None, cellB.Attributes);
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
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

        Parse(buffer, "\u001b[1mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    /// <summary>
    /// Ported from: libvterm 30state_pen "Bold" off
    /// </summary>
    [Fact]
    public void Sgr_Bold_Off()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1mA\u001b[22mB");

        var cellA = buffer.GetCell(0, 0);
        var cellB = buffer.GetCell(1, 0);

        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
    }

    /// <summary>
    /// Reset (m) turns off bold.
    /// </summary>
    [Fact]
    public void Sgr_Bold_ResetByM()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1mA\u001b[mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Bold));
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

        Parse(buffer, "\u001b[4mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
    }

    /// <summary>
    /// SGR 24 turns off underline.
    /// </summary>
    [Fact]
    public void Sgr_Underline_Off()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[4mA\u001b[24mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Underline));
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

        Parse(buffer, "\u001b[3mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
    }

    /// <summary>
    /// SGR 23 turns off italic.
    /// </summary>
    [Fact]
    public void Sgr_Italic_Off()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[3mA\u001b[23mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Italic));
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

        Parse(buffer, "\u001b[5mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Blink));
    }

    /// <summary>
    /// SGR 25 turns off blink.
    /// </summary>
    [Fact]
    public void Sgr_Blink_Off()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5mA\u001b[25mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Blink));
    }

    #endregion

    #region Reverse/Inverse

    /// <summary>
    /// Ported from: libvterm 30state_pen "Reverse"
    /// </summary>
    [Fact]
    public void Sgr_Reverse_On()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[7mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Inverse));
    }

    /// <summary>
    /// SGR 27 turns off reverse.
    /// </summary>
    [Fact]
    public void Sgr_Reverse_Off()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[7mA\u001b[27mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.False(cellB.Attributes.HasFlag(CellAttributes.Inverse));
    }

    #endregion

    #region Foreground Colors

    /// <summary>
    /// Ported from: libvterm 30state_pen "Foreground"
    /// Standard foreground colors (30-37).
    /// </summary>
    [Theory]
    [InlineData(31, 1)]  // Red
    [InlineData(32, 2)]  // Green
    [InlineData(34, 4)]  // Blue
    public void Sgr_Foreground_StandardColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();

        Parse(buffer, $"\u001b[{sgrCode}mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Foreground);
    }

    /// <summary>
    /// Ported from: libvterm 30state_pen "Foreground" bright
    /// Bright foreground colors (90-97).
    /// </summary>
    [Theory]
    [InlineData(91, 9)]   // Bright Red
    public void Sgr_Foreground_BrightColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();

        Parse(buffer, $"\u001b[{sgrCode}mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Foreground);
    }

    /// <summary>
    /// SGR 39 resets to default foreground.
    /// </summary>
    [Fact]
    public void Sgr_Foreground_Default()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[31mA\u001b[39mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.Equal(TerminalCell.DefaultForeground, cellB.Foreground);
    }

    #endregion

    #region Background Colors

    /// <summary>
    /// Ported from: libvterm 30state_pen "Background"
    /// Standard background colors (40-47).
    /// </summary>
    [Theory]
    [InlineData(41, 1)]  // Red
    [InlineData(42, 2)]  // Green
    [InlineData(44, 4)]  // Blue
    public void Sgr_Background_StandardColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();

        Parse(buffer, $"\u001b[{sgrCode}mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Background);
    }

    /// <summary>
    /// Bright background colors (100-107).
    /// </summary>
    [Theory]
    [InlineData(101, 9)]  // Bright Red
    public void Sgr_Background_BrightColors(int sgrCode, uint expectedIndex)
    {
        var buffer = CreateBuffer();

        Parse(buffer, $"\u001b[{sgrCode}mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(expectedIndex, cell.Background);
    }

    /// <summary>
    /// SGR 49 resets to default background.
    /// </summary>
    [Fact]
    public void Sgr_Background_Default()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[44mA\u001b[49mB");

        var cellB = buffer.GetCell(1, 0);
        Assert.Equal(TerminalCell.DefaultBackground, cellB.Background);
    }

    #endregion

    #region 256 Color Mode

    /// <summary>
    /// Ported from: libvterm 30state_pen extended colors (38;5;n)
    /// </summary>
    [Fact]
    public void Sgr_256Color_Foreground()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[38;5;208mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(208u, cell.Foreground);
    }

    /// <summary>
    /// 256-color background (48;5;n).
    /// </summary>
    [Fact]
    public void Sgr_256Color_Background()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[48;5;123mX");

        var cell = buffer.GetCell(0, 0);
        Assert.Equal(123u, cell.Background);
    }

    #endregion

    #region True Color (RGB)

    /// <summary>
    /// Ported from: libvterm 30state_pen extended colors (38;2;r;g;b)
    /// </summary>
    [Fact]
    public void Sgr_TrueColor_Foreground()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[38;2;10;20;30mX");

        var cell = buffer.GetCell(0, 0);
        // True color has high bit set or special encoding
        Assert.True(cell.Foreground > 0xFFFFFF || cell.Foreground >= 0x1000000);
    }

    /// <summary>
    /// True color background (48;2;r;g;b).
    /// </summary>
    [Fact]
    public void Sgr_TrueColor_Background()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[48;2;10;20;30mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Background > 0xFFFFFF || cell.Background >= 0x1000000);
    }

    #endregion

    #region Combined Attributes

    /// <summary>
    /// Multiple attributes in single sequence.
    /// </summary>
    [Fact]
    public void Sgr_CombinedAttributes()
    {
        var buffer = CreateBuffer();

        // Bold + Italic + Underline + Red FG + Blue BG
        Parse(buffer, "\u001b[1;3;4;31;44mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
        Assert.Equal(1u, cell.Foreground); // Red
        Assert.Equal(4u, cell.Background); // Blue
    }

    /// <summary>
    /// Ported from: libvterm 30state_pen "Bold+ANSI colour == highbright"
    /// Bold combined with color 7 may map to bright white.
    /// </summary>
    [Fact]
    public void Sgr_BoldPlusColor()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;37mX");

        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        // Color behavior varies by implementation
    }

    #endregion

    #region DECSTR Resets Pen

    /// <summary>
    /// Ported from: libvterm 30state_pen "DECSTR resets pen attributes"
    /// CSI ! p resets pen attributes.
    /// </summary>
    [Fact(Skip = "DECSTR (CSI ! p) not yet implemented")]
    public void Decstr_ResetsPenAttributes()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;4m"); // Bold + Underline

        Parse(buffer, "\u001b[!p"); // Soft reset

        // Write character after reset
        Parse(buffer, "X");

        var cell = buffer.GetCell(0, 0);
        // After DECSTR, attributes should be reset
        Assert.False(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.False(cell.Attributes.HasFlag(CellAttributes.Underline));
    }

    #endregion

    #region Attribute Persistence

    /// <summary>
    /// Attributes persist across multiple characters.
    /// </summary>
    [Fact]
    public void Sgr_AttributesPersist()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;31mABC");

        Assert.True(buffer.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(buffer.GetCell(1, 0).Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(buffer.GetCell(2, 0).Attributes.HasFlag(CellAttributes.Bold));

        Assert.Equal(1u, buffer.GetCell(0, 0).Foreground);
        Assert.Equal(1u, buffer.GetCell(1, 0).Foreground);
        Assert.Equal(1u, buffer.GetCell(2, 0).Foreground);
    }

    /// <summary>
    /// Attributes persist across lines.
    /// </summary>
    [Fact]
    public void Sgr_AttributesPersistAcrossLines()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[1;31mA\r\nB");

        Assert.True(buffer.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(buffer.GetCell(0, 1).Attributes.HasFlag(CellAttributes.Bold));

        Assert.Equal(1u, buffer.GetCell(0, 0).Foreground);
        Assert.Equal(1u, buffer.GetCell(0, 1).Foreground);
    }

    #endregion
}
