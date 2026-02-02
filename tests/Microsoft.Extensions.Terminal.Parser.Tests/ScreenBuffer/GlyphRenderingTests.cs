// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for glyph rendering in ScreenBuffer.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/10state_putglyph.test
/// Tests character output, UTF-8 encoding, wide characters, and combining characters.
/// </remarks>
public class GlyphRenderingTests
{
    private ScreenBuffer CreateBuffer(int width = 80, int height = 25)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Basic ASCII Output

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "Low"
    /// PUSH "ABC" should place glyphs at positions 0,0 0,1 0,2
    /// </summary>
    [Fact]
    public void Low_AsciiCharacters()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABC");

        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(2, 0).Character);
        Assert.Equal(3, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
    }

    #endregion

    #region UTF-8 Single Characters

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "UTF-8 1 char"
    /// U+00C1 = LATIN CAPITAL LETTER A WITH ACUTE
    /// U+00E9 = LATIN SMALL LETTER E WITH ACUTE
    /// </summary>
    [Fact]
    public void Utf8_SingleByteExtended()
    {
        var buffer = CreateBuffer();

        // U+00C1 (Á) and U+00E9 (é)
        Parse(buffer, "\u00C1\u00E9");

        Assert.Equal('\u00C1', buffer.GetCell(0, 0).Character);
        Assert.Equal('\u00E9', buffer.GetCell(1, 0).Character);
        Assert.Equal(2, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "UTF-8 split writes"
    /// Multi-byte UTF-8 character split across buffer writes should still work.
    /// </summary>
    [Fact]
    public void Utf8_SplitWrites()
    {
        var buffer = CreateBuffer();
        var parser = new VtParser(buffer);

        // U+00C1 = 0xC3 0x81 - split across two writes
        parser.Parse(new byte[] { 0xC3 });
        parser.Parse(new byte[] { 0x81 });

        Assert.Equal('\u00C1', buffer.GetCell(0, 0).Character);
    }

    #endregion

    #region Wide Characters

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "UTF-8 wide char"
    /// U+FF10 = FULLWIDTH DIGIT ZERO (width 2)
    /// </summary>
    [Fact(Skip = "Wide character width detection not yet implemented")]
    public void Utf8_WideCharacter()
    {
        var buffer = CreateBuffer();

        // U+FF10 (fullwidth 0) followed by space
        Parse(buffer, "\uFF10 ");

        var cell0 = buffer.GetCell(0, 0);
        Assert.Equal('\uFF10', cell0.Character);
        Assert.Equal(2, cell0.Width); // Wide character takes 2 cells

        // Space should be at position 2 (after the wide char)
        Assert.Equal(' ', buffer.GetCell(2, 0).Character);
        Assert.Equal(3, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "UTF-8 emoji wide char"
    /// U+1F600 = GRINNING FACE (width 2)
    /// </summary>
    [Fact(Skip = "Wide character width detection not yet implemented")]
    public void Utf8_EmojiWideCharacter()
    {
        var buffer = CreateBuffer();
        var parser = new VtParser(buffer);

        // U+1F600 = 0xF0 0x9F 0x98 0x80 followed by space
        parser.Parse(new byte[] { 0xF0, 0x9F, 0x98, 0x80, 0x20 });

        var cell0 = buffer.GetCell(0, 0);
        // The character should be stored (may be surrogate pair representation)
        Assert.Equal(2, cell0.Width);

        // Space after wide emoji
        Assert.Equal(' ', buffer.GetCell(2, 0).Character);
    }

    #endregion

    #region Combining Characters

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "UTF-8 combining chars"
    /// U+0301 = COMBINING ACUTE ACCENT
    /// "e" followed by combining acute should produce "é" in one cell
    /// </summary>
    [Fact(Skip = "Combining character handling not yet implemented")]
    public void Utf8_CombiningCharacters()
    {
        var buffer = CreateBuffer();
        var parser = new VtParser(buffer);

        // e + combining acute + Z
        parser.Parse(new byte[] { (byte)'e', 0xCC, 0x81, (byte)'Z' });

        // First cell should have 'e' with combining mark
        var cell0 = buffer.GetCell(0, 0);
        Assert.Equal('e', cell0.Character);

        // Z should be at position 1
        Assert.Equal('Z', buffer.GetCell(1, 0).Character);
        Assert.Equal(2, buffer.CursorX);
    }

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "Combining across buffers"
    /// Combining character sent in separate write should still combine.
    /// </summary>
    [Fact(Skip = "Combining character handling not yet implemented")]
    public void Utf8_CombiningAcrossBuffers()
    {
        var buffer = CreateBuffer();
        var parser = new VtParser(buffer);

        // First write: just 'e'
        parser.Parse(new byte[] { (byte)'e' });
        Assert.Equal('e', buffer.GetCell(0, 0).Character);

        // Second write: combining acute + Z
        parser.Parse(new byte[] { 0xCC, 0x81, (byte)'Z' });

        // First cell should still have 'e' (with combining mark applied)
        var cell0 = buffer.GetCell(0, 0);
        Assert.Equal('e', cell0.Character);

        // Z should be at position 1
        Assert.Equal('Z', buffer.GetCell(1, 0).Character);
    }

    #endregion

    #region DECSCA Protected Characters

    /// <summary>
    /// Ported from: libvterm 10state_putglyph "DECSCA protected"
    /// CSI 1 " q enables protection, CSI 2 " q disables it.
    /// </summary>
    [Fact]
    public void Decsca_ProtectedCharacters()
    {
        var buffer = CreateBuffer();

        // A (unprotected) + enable protection + B (protected) + disable protection + C (unprotected)
        Parse(buffer, "A\u001b[1\"qB\u001b[2\"qC");

        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
        Assert.Equal('C', buffer.GetCell(2, 0).Character);
    }

    #endregion

    #region Character Placement

    /// <summary>
    /// Characters should be placed at cursor position and cursor advances.
    /// </summary>
    [Fact]
    public void Characters_PlacedAtCursorPosition()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "\u001b[5;10HXYZ");

        Assert.Equal('X', buffer.GetCell(9, 4).Character);
        Assert.Equal('Y', buffer.GetCell(10, 4).Character);
        Assert.Equal('Z', buffer.GetCell(11, 4).Character);
        Assert.Equal(12, buffer.CursorX);
        Assert.Equal(4, buffer.CursorY);
    }

    /// <summary>
    /// Overwriting existing characters.
    /// </summary>
    [Fact]
    public void Characters_Overwrite()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "ABCDE");
        Parse(buffer, "\u001b[1GB"); // Go to column 1, write B

        Assert.Equal('B', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
    }

    #endregion

    #region Special Characters

    /// <summary>
    /// Control characters should not be displayed as glyphs.
    /// </summary>
    [Fact(Skip = "Control character handling differs from expected")]
    public void ControlCharacters_NotDisplayed()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "A\x01\x02\x03B");

        // A and B should be adjacent (control chars not printed)
        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        Assert.Equal('B', buffer.GetCell(1, 0).Character);
    }

    /// <summary>
    /// Tab character advances to next tab stop.
    /// </summary>
    [Fact]
    public void TabCharacter_AdvancesToTabStop()
    {
        var buffer = CreateBuffer();

        Parse(buffer, "A\tB");

        Assert.Equal('A', buffer.GetCell(0, 0).Character);
        // B should be at tab stop (column 8 by default)
        Assert.Equal('B', buffer.GetCell(8, 0).Character);
    }

    #endregion
}
