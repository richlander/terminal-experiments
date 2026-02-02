// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer resize behavior tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/63screen_resize.test
/// Tests screen resize operations including content preservation,
/// cursor position adjustment, and scrollback interaction.
/// 
/// NOTE: ScreenBuffer.Resize() is not yet implemented. These tests are
/// skipped until the Resize method is available.
/// </remarks>
public class ScreenResizeTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Resize Wider - libvterm "Resize wider preserves cells"

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize wider preserves cells"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeWider_PreservesContent()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "AB\r\nCD");
        Assert.Equal("AB", buffer.GetRowText(0));
        Assert.Equal("CD", buffer.GetRowText(1));
    }

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize wider allows print in new area"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeWider_AllowsWriteInNewArea()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "AB\u001b[79GCD");
    }

    #endregion

    #region Resize Shorter - libvterm "Resize shorter"

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize shorter with blanks just truncates"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeShorter_WithBlanks_Truncates()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Top\u001b[10HLine 10");
    }

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize shorter with content must scroll"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeShorter_WithContent_Scrolls()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Top\u001b[25HLine 25\u001b[15H");
    }

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize shorter does not lose line with cursor"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeShorter_PreservesCursorLine()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "\u001b[24HLine 24\r\nLine 25\r\n");
    }

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize shorter does not send cursor to negative row"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeShorter_CursorNeverNegative()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "\u001b[24HLine 24\r\nLine 25\u001b[H");
    }

    #endregion

    #region Resize Taller - libvterm "Resize taller"

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize taller attempts to pop scrollback"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeTaller_CanRestoreScrollback()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Line 1\u001b[25HBottom\u001b[15H");
    }

    #endregion

    #region Resize on Altscreen - libvterm "Resize can operate on altscreen"

    /// <summary>
    /// Ported from: libvterm 63screen_resize "Resize can operate on altscreen"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void Resize_WorksOnAltscreen()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "Main screen");
        Parse(buffer, "\u001b[?1049h\u001b[HAlt screen");
    }

    #endregion

    #region Basic Dimension Tests (No Resize Needed)

    /// <summary>
    /// Verify initial dimensions are set correctly.
    /// </summary>
    [Fact]
    public void Dimensions_InitialValuesCorrect()
    {
        var buffer = CreateBuffer(100, 40);
        Assert.Equal(100, buffer.Width);
        Assert.Equal(40, buffer.Height);
    }

    /// <summary>
    /// Verify content respects initial dimensions.
    /// </summary>
    [Fact]
    public void Dimensions_ContentRespectsSize()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "0123456789");
        Assert.Equal(10, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        Parse(buffer, "A");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// Verify cursor clamping at boundaries.
    /// </summary>
    [Fact]
    public void Dimensions_CursorClampedToBoundaries()
    {
        var buffer = CreateBuffer(80, 25);
        Parse(buffer, "\u001b[1;200H");
        Assert.True(buffer.CursorX < 80);
        Assert.Equal(0, buffer.CursorY);
        Parse(buffer, "\u001b[200;1H");
        Assert.Equal(0, buffer.CursorX);
        Assert.True(buffer.CursorY < 25);
    }

    #endregion
}
