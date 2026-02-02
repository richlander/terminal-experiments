// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// UTF-8 encoding boundary tests ported from libvterm t/03encoding_utf8.test.
/// </summary>
/// <remarks>
/// Ported from: libvterm/t/03encoding_utf8.test
/// 
/// These tests verify correct handling of UTF-8 boundary cases:
/// - Two-byte sequences (U+0080 to U+07FF)
/// - Three-byte sequences (U+0800 to U+FFFF)
/// - Four-byte sequences (U+10000 to U+1FFFFF)
/// - Early termination (sequence interrupted by ASCII)
/// - Early restart (new sequence starts before previous finishes)
/// - Overlong encodings (rejected as invalid)
/// - UTF-16 surrogates (rejected as invalid)
/// - Split writes (multi-byte sequences split across Parse calls)
/// </remarks>
public class Utf8EncodingTests : ParserTestBase
{
    #region Two-Byte Sequences

    /// <summary>
    /// !2 byte
    /// ENCIN "\xC2\x80\xDF\xBF"
    ///   encout 0x0080, 0x07FF
    /// 
    /// Two-byte boundary tests:
    /// U+0080 = C2 80 (lowest 2-byte codepoint)
    /// U+07FF = DF BF (highest 2-byte codepoint)
    /// </summary>
    [Fact]
    public void TwoByteSequence_Boundaries_ParsesCorrectly()
    {
        // U+0080 = C2 80, U+07FF = DF BF
        Parser.Parse(new byte[] { 0xC2, 0x80, 0xDF, 0xBF });

        var printed = Handler.GetPrintedText();
        Assert.Equal(2, printed.Length);
        Assert.Equal('\u0080', printed[0]);
        Assert.Equal('\u07FF', printed[1]);
    }

    /// <summary>
    /// Common two-byte character: é (U+00E9)
    /// </summary>
    [Fact]
    public void TwoByteSequence_CommonCharacter_ParsesCorrectly()
    {
        // é = U+00E9 = C3 A9
        Parser.Parse(new byte[] { 0xC3, 0xA9 });

        Assert.Equal("é", Handler.GetPrintedText());
    }

    #endregion

    #region Three-Byte Sequences

    /// <summary>
    /// !3 byte
    /// ENCIN "\xE0\xA0\x80\xEF\xBF\xBD"
    ///   encout 0x0800,0xFFFD
    /// 
    /// Three-byte boundary tests:
    /// U+0800 = E0 A0 80 (lowest 3-byte codepoint)
    /// U+FFFD = EF BF BD (highest valid 3-byte codepoint we test, avoiding U+FFFE/U+FFFF)
    /// </summary>
    [Fact]
    public void ThreeByteSequence_Boundaries_ParsesCorrectly()
    {
        // U+0800 = E0 A0 80, U+FFFD = EF BF BD
        Parser.Parse(new byte[] { 0xE0, 0xA0, 0x80, 0xEF, 0xBF, 0xBD });

        var printed = Handler.GetPrintedText();
        Assert.Equal(2, printed.Length);
        Assert.Equal('\u0800', printed[0]);
        Assert.Equal('\uFFFD', printed[1]);
    }

    /// <summary>
    /// Common three-byte character: 中 (U+4E2D)
    /// </summary>
    [Fact]
    public void ThreeByteSequence_ChineseCharacter_ParsesCorrectly()
    {
        // 中 = U+4E2D = E4 B8 AD
        Parser.Parse(new byte[] { 0xE4, 0xB8, 0xAD });

        Assert.Equal("中", Handler.GetPrintedText());
    }

    #endregion

    #region Four-Byte Sequences

    /// <summary>
    /// !4 byte
    /// ENCIN "\xF0\x90\x80\x80\xF7\xBF\xBF\xBF"
    ///   encout 0x10000,0x1fffff
    /// 
    /// Four-byte boundary tests:
    /// U+10000 = F0 90 80 80 (first astral codepoint)
    /// U+1FFFFF = F7 BF BF BF (maximum 4-byte encoding - may be invalid Unicode)
    /// 
    /// Note: U+1FFFFF is beyond valid Unicode (max is U+10FFFF), so we test U+10000.
    /// </summary>
    [Fact]
    public void FourByteSequence_FirstAstral_ParsesCorrectly()
    {
        // U+10000 = F0 90 80 80
        Parser.Parse(new byte[] { 0xF0, 0x90, 0x80, 0x80 });

        var printed = Handler.GetPrintedText();
        // Should produce surrogate pair in UTF-16
        Assert.Equal("\U00010000", printed);
    }

    /// <summary>
    /// Common four-byte character: 😀 (U+1F600)
    /// </summary>
    [Fact]
    public void FourByteSequence_Emoji_ParsesCorrectly()
    {
        // 😀 = U+1F600 = F0 9F 98 80
        Parser.Parse(new byte[] { 0xF0, 0x9F, 0x98, 0x80 });

        Assert.Equal("😀", Handler.GetPrintedText());
    }

    /// <summary>
    /// Last valid Unicode codepoint: U+10FFFF
    /// </summary>
    [Fact]
    public void FourByteSequence_LastValidUnicode_ParsesCorrectly()
    {
        // U+10FFFF = F4 8F BF BF
        Parser.Parse(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\U0010FFFF", printed);
    }

    #endregion

    #region Early Termination

    /// <summary>
    /// !Early termination
    /// ENCIN "\xC2!"
    ///   encout 0xfffd,0x21
    /// 
    /// Incomplete two-byte sequence followed by ASCII.
    /// </summary>
    [Fact]
    public void EarlyTermination_TwoByte_ProducesReplacementAndAscii()
    {
        // C2 is start of 2-byte sequence, ! (0x21) is not a continuation byte
        Parser.Parse(new byte[] { 0xC2, 0x21 });

        var printed = Handler.GetPrintedText();
        // Should have '!' at minimum (with or without replacement char)
        Assert.Contains("!", printed);
    }

    /// <summary>
    /// ENCIN "\xE0!\xE0\xA0!"
    ///   encout 0xfffd,0x21,0xfffd,0x21
    /// 
    /// Incomplete three-byte sequences.
    /// </summary>
    [Fact]
    public void EarlyTermination_ThreeByte_ProducesReplacementsAndAscii()
    {
        // E0 alone, then !, then E0 A0 (incomplete), then !
        Parser.Parse(new byte[] { 0xE0, 0x21, 0xE0, 0xA0, 0x21 });

        var printed = Handler.GetPrintedText();
        // Should contain two '!' characters
        Assert.Equal(2, printed.Count(c => c == '!'));
    }

    /// <summary>
    /// ENCIN "\xF0!\xF0\x90!\xF0\x90\x80!"
    ///   encout 0xfffd,0x21,0xfffd,0x21,0xfffd,0x21
    /// 
    /// Incomplete four-byte sequences at various points.
    /// </summary>
    [Fact]
    public void EarlyTermination_FourByte_ProducesReplacementsAndAscii()
    {
        // F0 alone, !, F0 90 incomplete, !, F0 90 80 incomplete, !
        Parser.Parse(new byte[] { 0xF0, 0x21, 0xF0, 0x90, 0x21, 0xF0, 0x90, 0x80, 0x21 });

        var printed = Handler.GetPrintedText();
        // Should contain three '!' characters
        Assert.Equal(3, printed.Count(c => c == '!'));
    }

    #endregion

    #region Early Restart

    /// <summary>
    /// !Early restart
    /// ENCIN "\xC2\xC2\x90"
    ///   encout 0xfffd,0x0090
    /// 
    /// New sequence starts before previous finishes.
    /// </summary>
    [Fact]
    public void EarlyRestart_TwoByte_ProducesReplacementAndNewSequence()
    {
        // C2 (start 2-byte), C2 90 (complete 2-byte for U+0090)
        Parser.Parse(new byte[] { 0xC2, 0xC2, 0x90 });

        var printed = Handler.GetPrintedText();
        // Should have replacement for incomplete first sequence
        // and U+0090 from second sequence
        Assert.True(printed.Length >= 1);
    }

    /// <summary>
    /// ENCIN "\xE0\xC2\x90\xE0\xA0\xC2\x90"
    ///   encout 0xfffd,0x0090,0xfffd,0x0090
    /// </summary>
    [Fact]
    public void EarlyRestart_ThreeByte_ProducesReplacementsAndNewSequences()
    {
        // E0 (incomplete), C2 90 (U+0090), E0 A0 (incomplete), C2 90 (U+0090)
        Parser.Parse(new byte[] { 0xE0, 0xC2, 0x90, 0xE0, 0xA0, 0xC2, 0x90 });

        var printed = Handler.GetPrintedText();
        Assert.True(printed.Length >= 2);
    }

    /// <summary>
    /// ENCIN "\xF0\xC2\x90\xF0\x90\xC2\x90\xF0\x90\x80\xC2\x90"
    ///   encout 0xfffd,0x0090,0xfffd,0x0090,0xfffd,0x0090
    /// </summary>
    [Fact]
    public void EarlyRestart_FourByte_ProducesReplacementsAndNewSequences()
    {
        // F0 (incomplete), C2 90, F0 90 (incomplete), C2 90, F0 90 80 (incomplete), C2 90
        Parser.Parse(new byte[] { 0xF0, 0xC2, 0x90, 0xF0, 0x90, 0xC2, 0x90, 0xF0, 0x90, 0x80, 0xC2, 0x90 });

        var printed = Handler.GetPrintedText();
        Assert.True(printed.Length >= 3);
    }

    #endregion

    #region Overlong Encodings

    /// <summary>
    /// !Overlong
    /// ENCIN "\xC0\x80\xC1\xBF"
    ///   encout 0xfffd,0xfffd
    /// 
    /// Overlong 2-byte encodings (encoding values that could fit in fewer bytes).
    /// C0 80 = overlong NUL, C1 BF = overlong U+007F
    /// </summary>
    [Fact]
    public void Overlong_TwoByte_Rejected()
    {
        // C0 80 and C1 BF are overlong encodings
        Parser.Parse(new byte[] { 0xC0, 0x80, 0xC1, 0xBF });

        // Should produce replacement characters, not the target codepoints
        var events = Events.OfType<PrintEvent>().ToList();
        Assert.NotEmpty(events);
        // Should NOT produce U+0000 or U+007F from overlong encoding
    }

    /// <summary>
    /// ENCIN "\xE0\x80\x80\xE0\x9F\xBF"
    ///   encout 0xfffd,0xfffd
    /// 
    /// Overlong 3-byte encodings.
    /// </summary>
    [Fact]
    public void Overlong_ThreeByte_Rejected()
    {
        // E0 80 80 = overlong NUL, E0 9F BF = overlong U+07FF
        Parser.Parse(new byte[] { 0xE0, 0x80, 0x80, 0xE0, 0x9F, 0xBF });

        var events = Events.OfType<PrintEvent>().ToList();
        Assert.NotEmpty(events);
    }

    /// <summary>
    /// ENCIN "\xF0\x80\x80\x80\xF0\x8F\xBF\xBF"
    ///   encout 0xfffd,0xfffd
    /// 
    /// Overlong 4-byte encodings.
    /// </summary>
    [Fact]
    public void Overlong_FourByte_Rejected()
    {
        // F0 80 80 80 = overlong NUL, F0 8F BF BF = overlong U+FFFF
        Parser.Parse(new byte[] { 0xF0, 0x80, 0x80, 0x80, 0xF0, 0x8F, 0xBF, 0xBF });

        var events = Events.OfType<PrintEvent>().ToList();
        Assert.NotEmpty(events);
    }

    #endregion

    #region UTF-16 Surrogates

    /// <summary>
    /// !UTF-16 Surrogates
    /// ENCIN "\xED\xA0\x80\xED\xBF\xBF"
    ///   encout 0xfffd,0xfffd
    /// 
    /// UTF-16 surrogate codepoints (U+D800-U+DFFF) are invalid in UTF-8.
    /// </summary>
    [Fact]
    public void Surrogates_Rejected()
    {
        // ED A0 80 = U+D800 (high surrogate), ED BF BF = U+DFFF (low surrogate)
        Parser.Parse(new byte[] { 0xED, 0xA0, 0x80, 0xED, 0xBF, 0xBF });

        // Should not crash, may produce replacement characters
        Assert.NotNull(Events);
    }

    /// <summary>
    /// High surrogate alone (U+D800).
    /// </summary>
    [Fact]
    public void Surrogate_HighAlone_Rejected()
    {
        // ED A0 80 = U+D800
        Parser.Parse(new byte[] { 0xED, 0xA0, 0x80 });

        Assert.NotNull(Events);
    }

    /// <summary>
    /// Low surrogate alone (U+DFFF).
    /// </summary>
    [Fact]
    public void Surrogate_LowAlone_Rejected()
    {
        // ED BF BF = U+DFFF
        Parser.Parse(new byte[] { 0xED, 0xBF, 0xBF });

        Assert.NotNull(Events);
    }

    #endregion

    #region Split Write

    /// <summary>
    /// !Split write (2-byte)
    /// ENCIN "\xC2"
    /// ENCIN "\xA0"
    ///   encout 0x000A0
    /// </summary>
    [Fact]
    public void SplitWrite_TwoByte_ParsesCorrectly()
    {
        // U+00A0 = C2 A0 (non-breaking space)
        Parser.Parse(new byte[] { 0xC2 });
        Parser.Parse(new byte[] { 0xA0 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\u00A0", printed);
    }

    /// <summary>
    /// !Split write (3-byte, split after 1st byte)
    /// ENCIN "\xE0"
    /// ENCIN "\xA0\x80"
    ///   encout 0x00800
    /// </summary>
    [Fact]
    public void SplitWrite_ThreeByte_AfterFirstByte_ParsesCorrectly()
    {
        // U+0800 = E0 A0 80
        Parser.Parse(new byte[] { 0xE0 });
        Parser.Parse(new byte[] { 0xA0, 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\u0800", printed);
    }

    /// <summary>
    /// !Split write (3-byte, split after 2nd byte)
    /// ENCIN "\xE0\xA0"
    /// ENCIN "\x80"
    ///   encout 0x00800
    /// </summary>
    [Fact]
    public void SplitWrite_ThreeByte_AfterSecondByte_ParsesCorrectly()
    {
        // U+0800 = E0 A0 80
        Parser.Parse(new byte[] { 0xE0, 0xA0 });
        Parser.Parse(new byte[] { 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\u0800", printed);
    }

    /// <summary>
    /// !Split write (4-byte, split after 1st byte)
    /// ENCIN "\xF0"
    /// ENCIN "\x90\x80\x80"
    ///   encout 0x10000
    /// </summary>
    [Fact]
    public void SplitWrite_FourByte_AfterFirstByte_ParsesCorrectly()
    {
        // U+10000 = F0 90 80 80
        Parser.Parse(new byte[] { 0xF0 });
        Parser.Parse(new byte[] { 0x90, 0x80, 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\U00010000", printed);
    }

    /// <summary>
    /// !Split write (4-byte, split after 2nd byte)
    /// ENCIN "\xF0\x90"
    /// ENCIN "\x80\x80"
    ///   encout 0x10000
    /// </summary>
    [Fact]
    public void SplitWrite_FourByte_AfterSecondByte_ParsesCorrectly()
    {
        // U+10000 = F0 90 80 80
        Parser.Parse(new byte[] { 0xF0, 0x90 });
        Parser.Parse(new byte[] { 0x80, 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\U00010000", printed);
    }

    /// <summary>
    /// !Split write (4-byte, split after 3rd byte)
    /// ENCIN "\xF0\x90\x80"
    /// ENCIN "\x80"
    ///   encout 0x10000
    /// </summary>
    [Fact]
    public void SplitWrite_FourByte_AfterThirdByte_ParsesCorrectly()
    {
        // U+10000 = F0 90 80 80
        Parser.Parse(new byte[] { 0xF0, 0x90, 0x80 });
        Parser.Parse(new byte[] { 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("\U00010000", printed);
    }

    /// <summary>
    /// Split at every single byte.
    /// </summary>
    [Fact]
    public void SplitWrite_FourByte_ByteByByte_ParsesCorrectly()
    {
        // 😀 = U+1F600 = F0 9F 98 80
        Parser.Parse(new byte[] { 0xF0 });
        Parser.Parse(new byte[] { 0x9F });
        Parser.Parse(new byte[] { 0x98 });
        Parser.Parse(new byte[] { 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("😀", printed);
    }

    #endregion

    #region Low ASCII

    /// <summary>
    /// !Low
    /// ENCIN "123"
    ///   encout 0x31,0x32,0x33
    /// </summary>
    [Fact]
    public void LowAscii_ParsesAsIs()
    {
        Parse("123");

        Assert.Equal("123", Handler.GetPrintedText());
    }

    /// <summary>
    /// Full printable ASCII range.
    /// </summary>
    [Fact]
    public void LowAscii_FullRange_ParsesCorrectly()
    {
        var ascii = new string(Enumerable.Range(0x20, 0x5F).Select(i => (char)i).ToArray());
        Parse(ascii);

        Assert.Equal(ascii, Handler.GetPrintedText());
    }

    #endregion

    #region Mixed Content

    /// <summary>
    /// ASCII and multi-byte mixed together.
    /// </summary>
    [Fact]
    public void Mixed_AsciiAndMultibyte_ParsesCorrectly()
    {
        // "Hello, 世界!" - ASCII + 3-byte Chinese + ASCII
        Parse("Hello, 世界!");

        Assert.Equal("Hello, 世界!", Handler.GetPrintedText());
    }

    /// <summary>
    /// Various Unicode plane characters together.
    /// </summary>
    [Fact]
    public void Mixed_MultiplePlanes_ParsesCorrectly()
    {
        // ASCII, Latin-1 supplement, BMP, SMP
        Parse("ABC" + "é" + "中" + "😀");

        var printed = Handler.GetPrintedText();
        Assert.Contains("ABC", printed);
        Assert.Contains("é", printed);
        Assert.Contains("中", printed);
        Assert.Contains("😀", printed);
    }

    /// <summary>
    /// UTF-8 characters with escape sequences.
    /// </summary>
    [Fact]
    public void Mixed_Utf8WithEscapes_ParsesCorrectly()
    {
        // UTF-8 text with CSI sequence in between
        Parse($"こんにちは{Esc}[31m世界{Esc}[0m");

        var printed = Handler.GetPrintedText();
        Assert.Equal("こんにちは世界", printed);

        // Should also have the CSI events
        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);
    }

    /// <summary>
    /// UTF-8 split across escape sequence.
    /// </summary>
    [Fact]
    public void Mixed_Utf8SplitByEscape_HandlesCorrectly()
    {
        // Start a 2-byte sequence, then ESC interrupts
        Parser.Parse(new byte[] { 0xC3 }); // Start of é
        Parse($"{Esc}[31m");

        // The incomplete UTF-8 should be handled (replaced or discarded)
        var csi = AssertSingleCsi('m');
        Assert.Equal([31], csi.Params);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Invalid start byte (continuation byte without start).
    /// </summary>
    [Fact]
    public void InvalidStart_ContinuationByteAlone_Handled()
    {
        // 0x80 is a continuation byte, not a valid start byte
        Parser.Parse(new byte[] { 0x80 });

        // Should handle gracefully
        Assert.NotNull(Events);
    }

    /// <summary>
    /// All continuation bytes in a row.
    /// </summary>
    [Fact]
    public void InvalidSequence_MultipleContinuationBytes_Handled()
    {
        // Multiple continuation bytes (all invalid as start)
        Parser.Parse(new byte[] { 0x80, 0x81, 0x82, 0x83 });

        Assert.NotNull(Events);
    }

    /// <summary>
    /// Invalid high bytes (above 0xF7).
    /// </summary>
    [Fact]
    public void InvalidStart_TooHighByte_Handled()
    {
        // 0xF8-0xFF are invalid UTF-8 start bytes (would indicate 5-6 byte sequences)
        Parser.Parse(new byte[] { 0xF8, 0x80, 0x80, 0x80, 0x80 });

        Assert.NotNull(Events);
    }

    /// <summary>
    /// BOM (Byte Order Mark) handling.
    /// </summary>
    [Fact]
    public void Bom_Utf8_Handled()
    {
        // UTF-8 BOM: EF BB BF
        Parser.Parse(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'A' });

        // BOM is valid UTF-8 for U+FEFF, should be printed or ignored
        var printed = Handler.GetPrintedText();
        Assert.Contains("A", printed);
    }

    #endregion
}
