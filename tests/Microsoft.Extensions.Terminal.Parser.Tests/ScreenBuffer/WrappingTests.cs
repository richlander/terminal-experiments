// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer line wrapping tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/20state_wrapping.test
/// Tests auto-wrap mode (DECAWM) behavior.
/// </remarks>
public class ScreenBufferWrappingTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region DECAWM - Auto Wrap Mode

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// Writing at right margin with DECAWM on wraps to next line.
    /// </summary>
    [Fact]
    public void Decawm_AtRightMargin_WrapsToNextLine()
    {
        var buffer = CreateBuffer(10, 5);
        
        // DECAWM is on by default
        // Write exactly 10 chars to fill first line
        Parse(buffer, "0123456789");
        Assert.Equal(10, buffer.CursorX); // At margin (one past last column)
        Assert.Equal(0, buffer.CursorY);
        
        // Write one more char - should wrap
        Parse(buffer, "A");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
        
        Assert.Equal("0123456789", buffer.GetRowText(0));
        Assert.Equal("A", buffer.GetRowText(1));
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// With DECAWM off, cursor stays at right margin.
    /// </summary>
    [Fact]
    public void Decawm_Off_CursorStaysAtMargin()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Turn off DECAWM
        Parse(buffer, "\x1b[?7l");
        
        // Write more than 10 chars
        Parse(buffer, "0123456789ABC");
        
        // Current impl: cursor ends up at position 10 (past right edge)
        // because each Print() clamps to width-1 then writes, then increments
        // Last chars overwrite at position 9
        Assert.Equal("012345678C", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorY); // Still on row 0
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// DECAWM can be toggled on/off.
    /// </summary>
    [Fact]
    public void Decawm_Toggle_WorksCorrectly()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Turn off DECAWM
        Parse(buffer, "\x1b[?7l");
        Parse(buffer, "0123456789X");
        Assert.Equal(0, buffer.CursorY); // Stays on row 0
        
        // Turn on DECAWM
        Parse(buffer, "\x1b[?7h");
        
        // Go back to start of new line
        Parse(buffer, "\r\n");
        
        // Now should wrap
        Parse(buffer, "ABCDEFGHIJK");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// Wrapping at bottom of screen should scroll.
    /// </summary>
    [Fact]
    public void Decawm_AtBottomRight_ScrollsAndWraps()
    {
        var buffer = CreateBuffer(10, 3);
        
        // Fill screen
        Parse(buffer, "Line0Line0");  // Row 0
        Parse(buffer, "Line1Line1");  // Row 1
        Parse(buffer, "Line2Line2");  // Row 2
        
        // Cursor is now at position 10, row 2 (wrap pending)
        Assert.Equal(10, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
        
        // Write one more char - should scroll and wrap
        Parse(buffer, "X");
        
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(2, buffer.CursorY);
        
        // Line0 should have scrolled off
        Assert.Equal("Line1Line1", buffer.GetRowText(0));
        Assert.Equal("Line2Line2", buffer.GetRowText(1));
        Assert.Equal("X", buffer.GetRowText(2));
    }

    #endregion

    #region Pending Wrap State

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// Cursor at right margin is in "pending wrap" state.
    /// </summary>
    [Fact]
    public void PendingWrap_CursorAtMargin()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write exactly to fill line
        Parse(buffer, "0123456789");
        
        // Cursor should be at position 10 (pending wrap)
        Assert.Equal(10, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// Backspace from pending wrap moves cursor back.
    /// </summary>
    [Fact]
    public void PendingWrap_BackspaceMovesCursorBack()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "0123456789");  // Pending wrap
        Assert.Equal(10, buffer.CursorX);
        
        Parse(buffer, "\b");  // Backspace
        Assert.Equal(9, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        Parse(buffer, "X");  // Overwrite last char
        Assert.Equal("012345678X", buffer.GetRowText(0));
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// Cursor movement from pending wrap clears pending state.
    /// </summary>
    [Fact]
    public void PendingWrap_CursorMovementClearsPending()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "0123456789");  // Pending wrap
        Assert.Equal(10, buffer.CursorX);
        
        // Move cursor - clears pending wrap
        Parse(buffer, "\x1b[5G");  // Move to column 5
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        
        // Next char should not wrap
        Parse(buffer, "X");
        Assert.Equal(5, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    /// <summary>
    /// Ported from: libvterm 20state_wrapping
    /// CR from pending wrap should return to column 0 on same line.
    /// </summary>
    [Fact]
    public void PendingWrap_CarriageReturnStaysOnLine()
    {
        var buffer = CreateBuffer(10, 5);
        
        Parse(buffer, "0123456789");  // Pending wrap
        Parse(buffer, "\r");  // Carriage return
        
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);  // Still on line 0
    }

    #endregion

    #region Long Lines

    /// <summary>
    /// Writing a very long line wraps multiple times.
    /// </summary>
    [Fact]
    public void LongLine_WrapsMultipleTimes()
    {
        var buffer = CreateBuffer(10, 5);
        
        // Write 35 chars (should wrap 3 times)
        Parse(buffer, "ABCDEFGHIJ" + "KLMNOPQRST" + "UVWXYZ0123" + "45678");
        
        Assert.Equal("ABCDEFGHIJ", buffer.GetRowText(0));
        Assert.Equal("KLMNOPQRST", buffer.GetRowText(1));
        Assert.Equal("UVWXYZ0123", buffer.GetRowText(2));
        Assert.Equal("45678", buffer.GetRowText(3));
        
        Assert.Equal(5, buffer.CursorX);
        Assert.Equal(3, buffer.CursorY);
    }

    #endregion
}
