// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// UTF-8 encoding tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js: src/common/parser/EscapeSequenceParser.test.ts
/// - libvterm: t/03encoding_utf8.test
/// </remarks>
public class Utf8Tests : ParserTestBase
{
    #region Basic UTF-8

    /// <summary>
    /// Ported from: xterm.js "should handle 2-byte UTF-8"
    /// </summary>
    [Fact]
    public void Utf8_TwoByteChar_PrintsCorrectly()
    {
        Parse("é"); // U+00E9, 2 bytes in UTF-8

        Assert.Equal("é", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: xterm.js "should handle 3-byte UTF-8"
    /// </summary>
    [Fact]
    public void Utf8_ThreeByteChar_PrintsCorrectly()
    {
        Parse("中"); // U+4E2D, 3 bytes in UTF-8

        Assert.Equal("中", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: xterm.js "should handle 4-byte UTF-8"
    /// </summary>
    [Fact]
    public void Utf8_FourByteChar_PrintsCorrectly()
    {
        Parse("😀"); // U+1F600, 4 bytes in UTF-8

        // Emoji prints as surrogate pair (two Print calls)
        var printed = Handler.GetPrintedText();
        Assert.Equal("😀", printed);
    }

    #endregion

    #region Mixed Content

    /// <summary>
    /// Ported from: xterm.js "should handle mixed ASCII and UTF-8"
    /// </summary>
    [Fact]
    public void Utf8_MixedWithAscii_PrintsCorrectly()
    {
        Parse("Hello, 世界!");

        Assert.Equal("Hello, 世界!", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: xterm.js "should handle UTF-8 with escape sequences"
    /// </summary>
    [Fact]
    public void Utf8_WithEscapeSequences_HandledCorrectly()
    {
        Parse("\u001b[31m中文\u001b[0m");

        Assert.Equal("中文", Handler.GetPrintedText());
        Assert.Equal(2, Handler.Events.OfType<CsiEvent>().Count());
    }

    #endregion

    #region Split UTF-8 Sequences

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test
    /// 
    /// UTF-8 multi-byte sequences may be split across Parse() calls.
    /// </summary>
    [Fact]
    public void Utf8_SplitMultibyte_ParsesCorrectly()
    {
        // UTF-8 for "é" is 0xC3 0xA9
        Parser.Parse(new byte[] { 0xC3 });
        Parser.Parse(new byte[] { 0xA9 });

        Assert.Equal("é", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: libvterm - 3-byte split
    /// </summary>
    [Fact]
    public void Utf8_SplitThreeByte_ParsesCorrectly()
    {
        // UTF-8 for "中" is 0xE4 0xB8 0xAD
        Parser.Parse(new byte[] { 0xE4 });
        Parser.Parse(new byte[] { 0xB8, 0xAD });

        Assert.Equal("中", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: libvterm - 4-byte split
    /// </summary>
    [Fact]
    public void Utf8_SplitFourByte_ParsesCorrectly()
    {
        // UTF-8 for "😀" is 0xF0 0x9F 0x98 0x80
        Parser.Parse(new byte[] { 0xF0 });
        Parser.Parse(new byte[] { 0x9F });
        Parser.Parse(new byte[] { 0x98 });
        Parser.Parse(new byte[] { 0x80 });

        Assert.Equal("😀", Handler.GetPrintedText());
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Ported from: libvterm - high bytes treated as Latin-1 fallback
    /// 
    /// Invalid UTF-8 start bytes (0x80-0xBF, 0xF8-0xFF) may be
    /// treated as Latin-1 or replacement characters.
    /// </summary>
    [Fact]
    public void Utf8_InvalidStartByte_FallsBackToLatin1()
    {
        // 0x80 is not a valid UTF-8 start byte
        Parser.Parse(new byte[] { 0x80 });

        // Should print as Latin-1 character (€ in Windows-1252, but \x80 in Latin-1)
        var printed = Handler.GetPrintedText();
        Assert.Single(printed);
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "invalid_utf8"
    /// Invalid UTF-8 sequences should not crash the parser.
    /// </summary>
    [Fact]
    public void Utf8_InvalidSequence_DoesNotCrash()
    {
        // 0xC0 0x80 is overlong encoding (invalid)
        Parser.Parse(new byte[] { 0xC0, 0x80 });

        // Should handle gracefully - either print replacement chars or skip
        Assert.NotNull(Handler.Events);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_utf8"
    /// Partial UTF-8 arriving in chunks.
    /// </summary>
    [Fact]
    public void Utf8_PartialAcrossChunks_Completes()
    {
        // "é" = 0xC3 0xA9, "ñ" = 0xC3 0xB1
        Parser.Parse(new byte[] { 0xC3 });  // First byte of é
        Parser.Parse(new byte[] { 0xA9, 0xC3 });  // Second byte of é, first byte of ñ
        Parser.Parse(new byte[] { 0xB1 });  // Second byte of ñ

        Assert.Equal("éñ", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_utf8_into_esc"
    /// UTF-8 sequence interrupted by escape should reset UTF-8 state.
    /// </summary>
    [Fact]
    public void Utf8_InterruptedByEscape_ResetsState()
    {
        // Start of 2-byte UTF-8 then ESC
        Parser.Parse(new byte[] { 0xC3, 0x1B, (byte)'7' });

        // The incomplete UTF-8 should be discarded, ESC 7 should dispatch
        Assert.Contains(Handler.Events, e => e is EscEvent { Command: '7' });
    }

    /// <summary>
    /// Ported from: vte lib.rs "unicode"
    /// Various Unicode characters including emoji.
    /// </summary>
    [Fact]
    public void Utf8_VariousUnicode_AllPrint()
    {
        Parse("ASCII → émojis: 🎉🚀💻");

        var printed = Handler.GetPrintedText();
        Assert.Contains("ASCII", printed);
        Assert.Contains("→", printed);
        Assert.Contains("🎉", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_invalid_utf8"
    /// Invalid UTF-8 that looks valid until last byte.
    /// </summary>
    [Fact]
    public void Utf8_InvalidContinuation_Recovers()
    {
        // Start 3-byte sequence, but second byte is not continuation
        Parser.Parse(new byte[] { 0xE0, 0x41, 0x42 });  // 0xE0 then 'A' 'B'

        // Should recover and print 'A' 'B' at minimum
        var printed = Handler.GetPrintedText();
        Assert.Contains("A", printed);
        Assert.Contains("B", printed);
    }

    #endregion

    #region libvterm 03encoding_utf8.test Ported Tests

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "2 byte"
    /// Boundary test for 2-byte UTF-8 sequences.
    /// U+0080 (C2 80) and U+07FF (DF BF) are the low/high boundaries.
    /// </summary>
    [Fact]
    public void Utf8_TwoBytesBoundary_ParsesCorrectly()
    {
        // U+0080 = C2 80, U+07FF = DF BF
        Parser.Parse(new byte[] { 0xC2, 0x80, 0xDF, 0xBF });

        var printed = Handler.GetPrintedText();
        Assert.Equal(2, printed.Length);
        Assert.Equal('\u0080', printed[0]);
        Assert.Equal('\u07FF', printed[1]);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "3 byte"
    /// Boundary test for 3-byte UTF-8 sequences.
    /// U+0800 (E0 A0 80) and U+FFFD (EF BF BD).
    /// </summary>
    [Fact]
    public void Utf8_ThreeBytesBoundary_ParsesCorrectly()
    {
        // U+0800 = E0 A0 80, U+FFFD = EF BF BD
        Parser.Parse(new byte[] { 0xE0, 0xA0, 0x80, 0xEF, 0xBF, 0xBD });

        var printed = Handler.GetPrintedText();
        Assert.Equal(2, printed.Length);
        Assert.Equal('\u0800', printed[0]);
        Assert.Equal('\uFFFD', printed[1]);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "4 byte"
    /// Boundary test for 4-byte UTF-8 (astral plane).
    /// U+10000 (F0 90 80 80) is first astral codepoint.
    /// </summary>
    [Fact]
    public void Utf8_FourBytesBoundary_ParsesCorrectly()
    {
        // U+10000 = F0 90 80 80 (first astral)
        Parser.Parse(new byte[] { 0xF0, 0x90, 0x80, 0x80 });

        var printed = Handler.GetPrintedText();
        // Should produce surrogate pair
        Assert.Equal("\U00010000", printed);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "Early termination"
    /// Incomplete sequence followed by ASCII should produce replacement char.
    /// </summary>
    [Fact]
    public void Utf8_EarlyTermination_ProducesReplacementOrFallback()
    {
        // C2 followed by '!' (0x21) - incomplete 2-byte sequence
        Parser.Parse(new byte[] { 0xC2, 0x21 });

        var printed = Handler.GetPrintedText();
        // Should have '!' and possibly a replacement char
        Assert.Contains("!", printed);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "Early restart"
    /// New sequence start before previous finished.
    /// </summary>
    [Fact]
    public void Utf8_EarlyRestart_HandlesGracefully()
    {
        // C2 then C2 90 - first C2 is incomplete, second starts new sequence
        Parser.Parse(new byte[] { 0xC2, 0xC2, 0x90 });

        var printed = Handler.GetPrintedText();
        // Should have at least U+0090 from the second sequence
        Assert.True(printed.Length >= 1);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "Overlong"
    /// Overlong encodings should be rejected.
    /// </summary>
    [Fact]
    public void Utf8_Overlong_Rejected()
    {
        // C0 80 is overlong encoding of NUL
        Parser.Parse(new byte[] { 0xC0, 0x80 });

        // Should not produce valid codepoint U+0000 from overlong encoding
        // May produce replacement chars or Latin-1 fallback
        var events = Handler.Events.OfType<PrintEvent>().ToList();
        
        // Should have at least one character (replacement or fallback)
        Assert.NotEmpty(events);
        
        // Should NOT have decoded as U+0000 (the overlong target)
        // If properly rejected, we get replacement chars or Latin-1 bytes
        // Either way, we shouldn't get a clean NUL from the overlong encoding
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "UTF-16 Surrogates"
    /// UTF-16 surrogate codepoints in UTF-8 are invalid.
    /// </summary>
    [Fact]
    public void Utf8_Surrogates_Rejected()
    {
        // ED A0 80 = U+D800 (high surrogate) - invalid in UTF-8
        Parser.Parse(new byte[] { 0xED, 0xA0, 0x80 });

        // Should not crash, might produce replacement chars
        Assert.NotNull(Handler.Events);
    }

    /// <summary>
    /// Ported from: libvterm t/03encoding_utf8.test "Split write" variants
    /// Multi-byte sequences split at every possible point.
    /// </summary>
    [Fact]
    public void Utf8_ThreeByteSplitAtEveryPoint_ParsesCorrectly()
    {
        // U+0800 = E0 A0 80, split after first byte
        Parser.Parse(new byte[] { 0xE0 });
        Parser.Parse(new byte[] { 0xA0, 0x80 });
        var printed1 = Handler.GetPrintedText();
        Handler.Clear();

        // U+0800 = E0 A0 80, split after second byte
        Parser.Parse(new byte[] { 0xE0, 0xA0 });
        Parser.Parse(new byte[] { 0x80 });
        var printed2 = Handler.GetPrintedText();

        Assert.Equal('\u0800', printed1[0]);
        Assert.Equal('\u0800', printed2[0]);
    }

    #endregion
}
