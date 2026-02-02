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

    #region State Resize Tests - From libvterm 16state_resize

    /// <summary>
    /// Ported from: libvterm 16state_resize "Placement"
    /// Glyph placement at boundaries before resize.
    /// </summary>
    [Fact]
    public void Placement_GlyphsAtBoundary()
    {
        var buffer = CreateBuffer(80, 25);

        // Write at column 79 (0-indexed 78) - should wrap
        Parse(buffer, "AB\u001b[79GCD");

        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(78, 0).Character);
        Assert.Equal('D', buffer.GetCell(79, 0).Character);
    }

    /// <summary>
    /// Ported from: libvterm 16state_resize "Resize"
    /// After resize to wider, content placement changes.
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void Resize_WiderAllowsMoreColumns()
    {
        var buffer = CreateBuffer(80, 25);

        // Resize to 85 columns, then write at column 79
        // buffer.Resize(85, 27);
        Parse(buffer, "AB\u001b[79GCDE");

        // After resize, column 80 and 81 should be accessible
        Assert.Equal('E', buffer.GetCell(80, 0).Character);
        Assert.Equal(81, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 16state_resize "Resize without reset"
    /// Resize preserves cursor position if within bounds.
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void Resize_PreservesCursorPosition()
    {
        var buffer = CreateBuffer(80, 25);

        Parse(buffer, "\u001b[1;81H"); // Position at column 81 (would be clamped)

        // Resize to wider
        // buffer.Resize(90, 28);
        // Cursor should remain at valid position

        Parse(buffer, "FGHI");
        // Characters should appear at new positions
    }

    /// <summary>
    /// Ported from: libvterm 16state_resize "Resize shrink moves cursor"
    /// When shrinking, cursor is clamped to new bounds.
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeShrink_ClampsCursor()
    {
        var buffer = CreateBuffer(100, 30);

        Parse(buffer, "\u001b[1;90H"); // Position at column 90

        // Shrink to 80 columns
        // buffer.Resize(80, 25);

        // Cursor should be clamped to column 79 (0-indexed)
        Assert.True(buffer.CursorX < 80);
    }

    /// <summary>
    /// Ported from: libvterm 16state_resize "Resize grow doesn't cancel phantom"
    /// Phantom character (pending wrap) state is preserved on grow.
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeGrow_PreservesPhantomCharacter()
    {
        var buffer = CreateBuffer(80, 25);

        // Write to last column, putting cursor in phantom state
        Parse(buffer, "\u001b[79GAB");
        Assert.Equal(79, buffer.CursorX);

        // Resize wider
        // buffer.Resize(100, 30);

        // Cursor should now be at column 80 (no longer clamped)
        // and next character should go to column 81
        Parse(buffer, "C");
        Assert.Equal('C', buffer.GetCell(80, 0).Character);
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
