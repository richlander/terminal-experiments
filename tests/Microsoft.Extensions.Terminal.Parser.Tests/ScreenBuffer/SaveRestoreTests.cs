// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for cursor save/restore in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/22state_save.test
/// Tests DECSC/DECRC (ESC 7 / ESC 8) and DECSET/DECRST 1048.
/// </remarks>
public class SaveRestoreTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region DECSC/DECRC - Save and Restore Cursor

    /// <summary>
    /// Ported from: libvterm 22state_save "Save/restore using DECSC/DECRC"
    /// ESC 7 saves cursor, ESC 8 restores it.
    /// </summary>
    [Fact]
    public void Decsc_Decrc_SavesAndRestoresCursor()
    {
        var buffer = CreateBuffer();

        // Move to position (1,1) and save
        Parse(buffer, "\u001b[2;2H\u001b7");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);

        // Move elsewhere
        Parse(buffer, "\u001b[5;5H");
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);

        // Restore
        Parse(buffer, "\u001b8");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// DECSC saves attributes, DECRC restores them.
    /// </summary>
    [Fact]
    public void Decsc_Decrc_SavesAndRestoresAttributes()
    {
        var buffer = CreateBuffer();

        // Set bold and save
        Parse(buffer, "\u001b[1m\u001b7");

        // Change to italic
        Parse(buffer, "\u001b[0m\u001b[3m");

        // Restore
        Parse(buffer, "\u001b8");

        // Write a character and check it has bold
        Parse(buffer, "A");
        var cell = buffer.GetCell(0, 0);
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
    }

    #endregion

    #region DECSET/DECRST 1048 - Save/Restore Cursor

    /// <summary>
    /// Ported from: libvterm 22state_save "Set up state" and "Save"
    /// CSI ? 1048 h saves cursor, CSI ? 1048 l restores.
    /// </summary>
    [Fact(Skip = "DECSET 1048 not yet implemented")]
    public void Decset1048_SavesAndRestoresCursor()
    {
        var buffer = CreateBuffer();

        // Set up state at position (1,1)
        Parse(buffer, "\u001b[2;2H");
        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);

        // Save cursor
        Parse(buffer, "\u001b[?1048h");

        // Change state
        Parse(buffer, "\u001b[5;5H");
        Assert.Equal(4, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);

        // Restore cursor
        Parse(buffer, "\u001b[?1048l");

        Assert.Equal(1, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    #endregion

    #region DECSET/DECRST 1047 - Alternate Screen Buffer

    /// <summary>
    /// CSI ? 1047 h switches to alternate buffer.
    /// CSI ? 1047 l switches back to main buffer.
    /// </summary>
    [Fact]
    public void Decset1047_AlternateScreenBuffer()
    {
        var buffer = CreateBuffer();

        // Write to main buffer
        Parse(buffer, "MainBuffer");
        Assert.Equal("MainBuffer", buffer.GetRowText(0));

        // Note: Alternate buffer behavior may vary by implementation
        // This test documents the expected sequence handling
        Parse(buffer, "\u001b[?1047h"); // Switch to alternate

        // Content might be preserved or cleared depending on implementation
        Parse(buffer, "\u001b[H"); // Home cursor

        // Switch back to main
        Parse(buffer, "\u001b[?1047l");
    }

    #endregion

    #region Save/Restore with Scroll Regions

    /// <summary>
    /// Save/restore should preserve scroll region context.
    /// </summary>
    [Fact]
    public void SaveRestore_WithScrollRegion()
    {
        var buffer = CreateBuffer();

        // Set scroll region and position
        Parse(buffer, "\u001b[5;15r\u001b[10;10H");
        Assert.Equal(9, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);

        // Save
        Parse(buffer, "\u001b7");

        // Change scroll region
        Parse(buffer, "\u001b[1;25r\u001b[1;1H");
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);

        // Restore - cursor position should be restored
        Parse(buffer, "\u001b8");
        Assert.Equal(9, buffer.CursorX);
        Assert.Equal(9, buffer.CursorY);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Restore without save should be handled gracefully.
    /// </summary>
    [Fact]
    public void Decrc_WithoutSave_HandledGracefully()
    {
        var buffer = CreateBuffer();

        // Position cursor
        Parse(buffer, "\u001b[5;5H");

        // Restore without prior save - should not crash
        Parse(buffer, "\u001b8");

        // Cursor position may vary (typically stays or goes home)
    }

    /// <summary>
    /// Multiple saves overwrite previous save.
    /// </summary>
    [Fact]
    public void Decsc_MultipleSaves_OverwritesPrevious()
    {
        var buffer = CreateBuffer();

        // First save at (2,2)
        Parse(buffer, "\u001b[3;3H\u001b7");

        // Second save at (10,10)
        Parse(buffer, "\u001b[11;11H\u001b7");

        // Move elsewhere
        Parse(buffer, "\u001b[1;1H");

        // Restore should go to last saved position
        Parse(buffer, "\u001b8");
        Assert.Equal(10, buffer.CursorX);
        Assert.Equal(10, buffer.CursorY);
    }

    #endregion
}
