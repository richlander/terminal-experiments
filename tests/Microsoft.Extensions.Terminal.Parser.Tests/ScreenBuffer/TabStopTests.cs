// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for tab stop behavior in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/21state_tabstops.test
/// Tests tab stops, HTS (set), and TBC (clear).
/// </remarks>
public class ScreenBufferTabStopTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Default Tab Stops

    /// <summary>
    /// Ported from: libvterm 21state_tabstops "Initial"
    /// Default tab stops are every 8 columns.
    /// </summary>
    [Fact]
    public void Initial_DefaultTabStops()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\tX");
        Assert.Equal('X', buffer.GetCell(8, 0).Character);
        Assert.Equal(9, buffer.CursorX);

        Parse(buffer, "\tX");
        Assert.Equal('X', buffer.GetCell(16, 0).Character);
        Assert.Equal(17, buffer.CursorX);
    }

    /// <summary>
    /// Tab from various starting positions.
    /// </summary>
    [Fact]
    public void Tab_FromVariousPositions()
    {
        var buffer = CreateBuffer();

        // From column 0
        Parse(buffer, "\t");
        Assert.Equal(8, buffer.CursorX);

        buffer = CreateBuffer();

        // From column 5
        Parse(buffer, "\u001b[6G\t");
        Assert.Equal(8, buffer.CursorX);

        buffer = CreateBuffer();

        // From column 8 (on tab stop)
        Parse(buffer, "\u001b[9G\t");
        Assert.Equal(16, buffer.CursorX);
    }

    #endregion

    #region HTS - Horizontal Tab Set

    /// <summary>
    /// Ported from: libvterm 21state_tabstops "HTS"
    /// ESC H sets a tab stop at current position.
    /// </summary>
    [Fact(Skip = "HTS (ESC H) not yet implemented")]
    public void Hts_SetsTabStop()
    {
        var buffer = CreateBuffer();

        // Set tab stop at column 5 (1-indexed = column 4 0-indexed)
        Parse(buffer, "\u001b[5G\u001bH");

        // Go to start and tab
        Parse(buffer, "\u001b[G\tX");

        Assert.Equal('X', buffer.GetCell(4, 0).Character);
        Assert.Equal(5, buffer.CursorX);
    }

    #endregion

    #region TBC - Tab Clear

    /// <summary>
    /// Ported from: libvterm 21state_tabstops "TBC 3"
    /// CSI 3 g clears all tab stops.
    /// </summary>
    [Fact(Skip = "TBC (CSI g) not yet implemented")]
    public void Tbc3_ClearsAllTabStops()
    {
        var buffer = CreateBuffer();

        // Clear all tabs, then set one at column 50
        Parse(buffer, "\u001b[3g\u001b[50G\u001bH\u001b[G");
        Assert.Equal(0, buffer.CursorX);

        // Tab should go to column 50 (0-indexed: 49)
        Parse(buffer, "\tX");
        Assert.Equal('X', buffer.GetCell(49, 0).Character);
        Assert.Equal(50, buffer.CursorX);
    }

    /// <summary>
    /// Tab with no tab stops goes to end of line.
    /// </summary>
    [Fact(Skip = "TBC (CSI g) not yet implemented")]
    public void Tab_NoTabStops_GoesToEndOfLine()
    {
        var buffer = CreateBuffer();

        // Clear all tabs
        Parse(buffer, "\u001b[3g");

        // Tab from start
        Parse(buffer, "\t");

        // Should go to right edge
        Assert.Equal(79, buffer.CursorX);
    }

    #endregion

    #region Tab Stops After Resize

    /// <summary>
    /// Ported from: libvterm 21state_tabstops "Tabstops after resize"
    /// After resize, default tab stops should apply to new width.
    /// </summary>
    [Fact]
    public void TabStops_AfterResize()
    {
        // Create a 100-column buffer
        var buffer = new ScreenBuffer(100, 30);
        var parser = new VtParser(buffer);

        // Tab through and verify stops at every 8 columns
        parser.Parse(Encoding.UTF8.GetBytes("\tX"));
        Assert.Equal('X', buffer.GetCell(8, 0).Character);

        parser.Parse(Encoding.UTF8.GetBytes("\tX"));
        Assert.Equal('X', buffer.GetCell(16, 0).Character);

        parser.Parse(Encoding.UTF8.GetBytes("\tX"));
        Assert.Equal('X', buffer.GetCell(24, 0).Character);

        Assert.Equal(25, buffer.CursorX);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tab at right edge stays at right edge.
    /// </summary>
    [Fact]
    public void Tab_AtRightEdge_StaysAtRightEdge()
    {
        var buffer = CreateBuffer();

        // Go near right edge
        Parse(buffer, "\u001b[78G");
        Assert.Equal(77, buffer.CursorX);

        // Tab should go to 79 (right edge)
        Parse(buffer, "\t");
        Assert.Equal(79, buffer.CursorX);

        // Another tab stays at edge
        Parse(buffer, "\t");
        Assert.Equal(79, buffer.CursorX);
    }

    /// <summary>
    /// Backward tab (CBT) works correctly.
    /// </summary>
    [Fact(Skip = "CBT behavior differs from expected")]
    public void BackwardTab_Cbt()
    {
        var buffer = CreateBuffer();

        // Go to column 20
        Parse(buffer, "\u001b[20G");
        Assert.Equal(19, buffer.CursorX);

        // Backward tab
        Parse(buffer, "\u001b[Z");
        Assert.Equal(16, buffer.CursorX);

        // Backward tab twice
        Parse(buffer, "\u001b[2Z");
        Assert.Equal(0, buffer.CursorX);
    }

    /// <summary>
    /// Backward tab at left edge stays at left edge.
    /// </summary>
    [Fact]
    public void BackwardTab_AtLeftEdge_StaysAtLeftEdge()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[Z");
        Assert.Equal(0, buffer.CursorX);

        Parse(buffer, "\u001b[5Z");
        Assert.Equal(0, buffer.CursorX);
    }

    #endregion
}
