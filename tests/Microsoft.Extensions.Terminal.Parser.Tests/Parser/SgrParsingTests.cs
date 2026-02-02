// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// SGR (Select Graphic Rendition) parsing tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - libvterm: t/30state_pen.test (parsing aspects)
/// - Various terminal implementations
/// 
/// These tests verify the parser correctly handles SGR parameter formats.
/// Actual attribute behavior is tested in ScreenBufferTests.
/// </remarks>
public class SgrParsingTests : ParserTestBase
{
    #region Basic SGR

    [Fact]
    public void Sgr_Reset_NoParams()
    {
        Parse($"{Esc}[m");

        var csi = AssertSingleCsi('m');
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void Sgr_Reset_ExplicitZero()
    {
        Parse($"{Esc}[0m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([0], csi.Params);
    }

    [Fact]
    public void Sgr_Bold()
    {
        Parse($"{Esc}[1m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1], csi.Params);
    }

    [Fact]
    public void Sgr_MultipleAttributes()
    {
        // Bold + Italic + Underline
        Parse($"{Esc}[1;3;4m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1, 3, 4], csi.Params);
    }

    #endregion

    #region Standard Colors (30-37, 40-47)

    [Theory]
    [InlineData(30, "Black")]
    [InlineData(31, "Red")]
    [InlineData(32, "Green")]
    [InlineData(33, "Yellow")]
    [InlineData(34, "Blue")]
    [InlineData(35, "Magenta")]
    [InlineData(36, "Cyan")]
    [InlineData(37, "White")]
    public void Sgr_ForegroundColors(int colorCode, string colorName)
    {
        _ = colorName; // Used for test identification
        Parse($"{Esc}[{colorCode}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([colorCode], csi.Params);
    }

    [Theory]
    [InlineData(40, "Black")]
    [InlineData(41, "Red")]
    [InlineData(42, "Green")]
    [InlineData(43, "Yellow")]
    [InlineData(44, "Blue")]
    [InlineData(45, "Magenta")]
    [InlineData(46, "Cyan")]
    [InlineData(47, "White")]
    public void Sgr_BackgroundColors(int colorCode, string colorName)
    {
        _ = colorName;
        Parse($"{Esc}[{colorCode}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([colorCode], csi.Params);
    }

    #endregion

    #region Bright Colors (90-97, 100-107)

    [Theory]
    [InlineData(90)]
    [InlineData(91)]
    [InlineData(97)]
    public void Sgr_BrightForegroundColors(int colorCode)
    {
        Parse($"{Esc}[{colorCode}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([colorCode], csi.Params);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(107)]
    public void Sgr_BrightBackgroundColors(int colorCode)
    {
        Parse($"{Esc}[{colorCode}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([colorCode], csi.Params);
    }

    #endregion

    #region 256 Color Mode (38;5;N, 48;5;N)

    [Fact]
    public void Sgr_256Color_Foreground_Semicolon()
    {
        // Standard 256-color: ESC [ 38 ; 5 ; 208 m
        Parse($"{Esc}[38;5;208m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 5, 208], csi.Params);
    }

    [Fact]
    public void Sgr_256Color_Background_Semicolon()
    {
        Parse($"{Esc}[48;5;123m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([48, 5, 123], csi.Params);
    }

    [Fact]
    public void Sgr_256Color_BothColors()
    {
        // Foreground and background in same sequence
        Parse($"{Esc}[38;5;196;48;5;21m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 5, 196, 48, 5, 21], csi.Params);
    }

    [Theory]
    [InlineData(0)]    // Black
    [InlineData(15)]   // Bright White
    [InlineData(16)]   // Start of 216-color cube
    [InlineData(231)]  // End of 216-color cube
    [InlineData(232)]  // Start of grayscale
    [InlineData(255)]  // End of grayscale
    public void Sgr_256Color_AllRanges(int colorIndex)
    {
        Parse($"{Esc}[38;5;{colorIndex}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 5, colorIndex], csi.Params);
    }

    #endregion

    #region True Color / RGB (38;2;R;G;B, 48;2;R;G;B)

    [Fact]
    public void Sgr_TrueColor_Foreground_Semicolon()
    {
        // Standard RGB: ESC [ 38 ; 2 ; R ; G ; B m
        Parse($"{Esc}[38;2;255;128;64m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 2, 255, 128, 64], csi.Params);
    }

    [Fact]
    public void Sgr_TrueColor_Background_Semicolon()
    {
        Parse($"{Esc}[48;2;0;255;0m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([48, 2, 0, 255, 0], csi.Params);
    }

    [Fact]
    public void Sgr_TrueColor_Black()
    {
        Parse($"{Esc}[38;2;0;0;0m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 2, 0, 0, 0], csi.Params);
    }

    [Fact]
    public void Sgr_TrueColor_White()
    {
        Parse($"{Esc}[38;2;255;255;255m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 2, 255, 255, 255], csi.Params);
    }

    [Fact]
    public void Sgr_TrueColor_WithAttributes()
    {
        // Bold + RGB foreground + underline
        Parse($"{Esc}[1;38;2;100;150;200;4m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1, 38, 2, 100, 150, 200, 4], csi.Params);
    }

    #endregion

    #region Colon-Separated Subparameters (ITU T.416)

    /// <summary>
    /// Modern terminals support colon as subparameter separator.
    /// ESC [ 38 : 2 : R : G : B m
    /// </summary>
    [Fact]
    public void Sgr_TrueColor_Colon_Format()
    {
        // Colon-separated (ITU T.416 format)
        Parse($"{Esc}[38:2:255:128:64m");

        var csi = AssertSingleCsi('m');
        // Colons are treated as subparameter separators, each becomes a param
        Assert.Equal([38, 2, 255, 128, 64], csi.Params);
    }

    [Fact]
    public void Sgr_256Color_Colon_Format()
    {
        Parse($"{Esc}[38:5:208m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 5, 208], csi.Params);
    }

    [Fact]
    public void Sgr_TrueColor_ColonWithColorspace()
    {
        // Full ITU format: 38:2:colorspace:R:G:B (colorspace often empty or 0)
        Parse($"{Esc}[38:2::255:128:64m");

        var csi = AssertSingleCsi('m');
        // Empty param becomes 0 in ZDM
        Assert.Equal([38, 2, 0, 255, 128, 64], csi.Params);
    }

    #endregion

    #region Underline Styles (CSI 4:N m)

    [Theory]
    [InlineData(0, "None")]
    [InlineData(1, "Single")]
    [InlineData(2, "Double")]
    [InlineData(3, "Curly")]
    [InlineData(4, "Dotted")]
    [InlineData(5, "Dashed")]
    public void Sgr_UnderlineStyles_Colon(int style, string styleName)
    {
        _ = styleName;
        Parse($"{Esc}[4:{style}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([4, style], csi.Params);
    }

    #endregion

    #region Default Colors (39, 49)

    [Fact]
    public void Sgr_DefaultForeground()
    {
        Parse($"{Esc}[39m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([39], csi.Params);
    }

    [Fact]
    public void Sgr_DefaultBackground()
    {
        Parse($"{Esc}[49m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([49], csi.Params);
    }

    #endregion

    #region Attribute Reset Codes

    [Theory]
    [InlineData(22, "Normal intensity")]
    [InlineData(23, "Not italic")]
    [InlineData(24, "Not underline")]
    [InlineData(25, "Not blink")]
    [InlineData(27, "Not reverse")]
    [InlineData(28, "Not hidden")]
    [InlineData(29, "Not strikethrough")]
    public void Sgr_AttributeReset(int code, string description)
    {
        _ = description;
        Parse($"{Esc}[{code}m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([code], csi.Params);
    }

    #endregion

    #region Complex Combinations

    [Fact]
    public void Sgr_ComplexCombination()
    {
        // Bold, red fg, white bg, then reset, then green
        Parse($"{Esc}[1;31;47m");
        Parse($"{Esc}[0m");
        Parse($"{Esc}[32m");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(3, csis.Count);
        Assert.Equal([1, 31, 47], csis[0].Params);
        Assert.Equal([0], csis[1].Params);
        Assert.Equal([32], csis[2].Params);
    }

    [Fact]
    public void Sgr_BoldPlusAnsiColor()
    {
        // Bold + white should potentially map to bright white (implementation detail)
        Parse($"{Esc}[1;37m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1, 37], csi.Params);
    }

    #endregion
}
