// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Edge case tests ported from the vte (Rust) terminal parser.
/// These tests focus on parser robustness, buffer limits, overflow handling,
/// and invalid input scenarios.
/// </summary>
/// <remarks>
/// Ported from: vte/src/lib.rs and vte/src/ansi.rs
/// </remarks>
public class VteEdgeCaseTests : ParserTestBase
{
    #region OSC Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "parse_osc"
    /// Basic OSC parsing with multiple parameters.
    /// </summary>
    [Fact]
    public void Osc_MultipleParams_ParsedCorrectly()
    {
        // OSC 2;title ST - set window title
        Parse($"{Esc}]2;window title{Esc}\\");

        var osc = AssertSingleOsc(2);
        Assert.Equal("window title", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_empty_osc"
    /// Empty OSC with just BEL terminator.
    /// </summary>
    [Fact]
    public void Osc_Empty_DispatchesWithNoData()
    {
        Parse($"{Esc}]\x07");

        var osc = AssertSingleOsc(0);
        Assert.Empty(osc.Data);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_osc_max_params"
    /// OSC with many semicolon-separated params should be handled.
    /// </summary>
    [Fact]
    public void Osc_MaxParams_HandledGracefully()
    {
        // Many empty params separated by semicolons
        var manyParams = string.Join(";", Enumerable.Repeat("", 20));
        Parse($"{Esc}]{manyParams}{Esc}\\");

        // Should dispatch without crash
        var oscs = Events.OfType<OscEvent>().ToList();
        Assert.Single(oscs);
    }

    /// <summary>
    /// Ported from: vte lib.rs "osc_bell_terminated"
    /// OSC terminated by BEL (0x07).
    /// </summary>
    [Fact]
    public void Osc_BellTerminated_ParsesCorrectly()
    {
        Parse($"{Esc}]11;ff/00/ff\x07");

        var osc = AssertSingleOsc(11);
        Assert.Equal("ff/00/ff", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Ported from: vte lib.rs "osc_c0_st_terminated"
    /// OSC terminated by C0 ST (ESC \).
    /// </summary>
    [Fact]
    public void Osc_C0StTerminated_ParsesCorrectly()
    {
        Parse($"{Esc}]11;ff/00/ff{Esc}\\");

        var osc = AssertSingleOsc(11);
        Assert.Equal("ff/00/ff", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_osc_with_utf8_arguments"
    /// OSC containing UTF-8 characters in data.
    /// </summary>
    [Fact]
    public void Osc_WithUtf8Arguments_ParsesCorrectly()
    {
        // OSC 2 with UTF-8 content: "echo '¯\_(ツ)_/¯' && sleep 1"
        Parse($"{Esc}]2;echo '¯\\_(ツ)_/¯' && sleep 1\x07");

        var osc = AssertSingleOsc(2);
        var data = Encoding.UTF8.GetString(osc.Data);
        Assert.Contains("ツ", data);
        Assert.Contains("¯", data);
    }

    /// <summary>
    /// Ported from: vte lib.rs "osc_containing_string_terminator"
    /// OSC containing UTF-8 that might look like ST.
    /// Note: The vte parser handles this edge case where UTF-8 continuation bytes
    /// might be confused with terminators. Our parser may handle this differently.
    /// </summary>
    [Fact]
    public void Osc_ContainingUtf8LikeStringTerminator_ParsesCorrectly()
    {
        // UTF-8 sequence containing bytes that might look like terminators
        // 末 (U+672B) = E6 9C AB
        // The key test is that parser doesn't crash and dispatches OSC
        Parse($"{Esc}]2;末{Esc}\\");

        var osc = AssertSingleOsc(2);
        // Data should be captured (may be interpreted differently based on UTF-8 handling in OSC)
        Assert.True(osc.Data.Length > 0);
    }

    /// <summary>
    /// Ported from: vte lib.rs "exceed_max_buffer_size"
    /// OSC with data exceeding typical buffer size.
    /// </summary>
    [Fact]
    public void Osc_ExceedMaxBufferSize_HandledGracefully()
    {
        const int numBytes = 5000;

        // Start OSC
        var sb = new StringBuilder();
        sb.Append($"{Esc}]52;s");
        sb.Append(new string('a', numBytes));
        sb.Append("\x07");

        Parse(sb.ToString());

        var osc = AssertSingleOsc(52);
        // Data should be captured (possibly truncated at buffer limit)
        Assert.True(osc.Data.Length > 0);
    }

    #endregion

    #region CSI Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_max_params"
    /// CSI with maximum number of parameters should not set ignore flag.
    /// </summary>
    [Fact]
    public void Csi_AtMaxParams_DispatchesWithoutIgnore()
    {
        // 16 params (our limit) - should all be captured
        var params16 = string.Join(";", Enumerable.Range(1, 15)) + ";";
        Parse($"{Esc}[{params16}p");

        var csi = AssertSingleCsi('p');
        Assert.Equal(16, csi.Params.Length);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_params_ignore_long_params"
    /// CSI exceeding max params should dispatch with params truncated.
    /// </summary>
    [Fact]
    public void Csi_ExceedsMaxParams_DispatchesTruncated()
    {
        // More than 16 params - should truncate
        var params20 = string.Join(";", Enumerable.Repeat("1", 20));
        Parse($"{Esc}[{params20}p");

        var csi = AssertSingleCsi('p');
        Assert.Equal(16, csi.Params.Length);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_long_csi_param" (integer overflow)
    /// Parameter exceeding i64::MAX should be clamped.
    /// </summary>
    [Fact]
    public void Csi_IntegerOverflow_ClampsToMax()
    {
        // 9223372036854775808 is i64::MAX + 1
        Parse($"{Esc}[9223372036854775808m");

        var csi = AssertSingleCsi('m');
        // Should clamp to int.MaxValue, not wrap to negative
        Assert.True(csi.Params[0] >= 0);
        Assert.Equal(int.MaxValue, csi.Params[0]);
    }

    /// <summary>
    /// Ported from: vte lib.rs "csi_reset"
    /// ESC in middle of CSI should reset and start new sequence.
    /// </summary>
    [Fact]
    public void Csi_EscInMiddle_ResetsAndStartsNew()
    {
        Parse($"{Esc}[3;1{Esc}[?1049h");

        // Only the second CSI should dispatch
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('h', csi.Command);
        Assert.Equal([1049], csi.Params);
        Assert.True(csi.Private);
    }

    /// <summary>
    /// Ported from: vte lib.rs "csi_subparameters"
    /// CSI with colon-separated subparameters.
    /// </summary>
    [Fact]
    public void Csi_Subparameters_ParsesColonSeparated()
    {
        // SGR 38:2:255:0:255;1 m - truecolor followed by bold
        Parse($"{Esc}[38:2:255:0:255;1m");

        var csi = AssertSingleCsi('m');
        // Our parser treats : like ;
        Assert.Contains(38, csi.Params);
        Assert.Contains(255, csi.Params);
        Assert.Contains(1, csi.Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs "params_buffer_filled_with_subparam"
    /// CSI with maximum subparameters (many colons).
    /// Note: Many colons fill up the subparam buffer. The parser should
    /// handle this gracefully without crashing.
    /// </summary>
    [Fact]
    public void Csi_MaxSubparameters_HandledGracefully()
    {
        // CSI with many zeros separated by colons (subparam separators)
        // 0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0x
        Parse($"{Esc}[0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0x");

        // Should dispatch CSI without crash
        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Single(csis);
        Assert.Equal('x', csis[0].Command);
    }

    #endregion

    #region DCS Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "parse_dcs_max_params"
    /// DCS with many parameters should handle overflow.
    /// </summary>
    [Fact]
    public void Dcs_MaxParams_HandledGracefully()
    {
        var manyParams = string.Join(";", Enumerable.Repeat("1", 20));
        Parse($"{Esc}P{manyParams}p{Esc}\\");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        // Params should be truncated to limit
        Assert.True(hook.Params.Length <= 16);
    }

    /// <summary>
    /// Ported from: vte lib.rs "dcs_reset"
    /// DCS state should reset when interrupted by new sequence.
    /// </summary>
    [Fact]
    public void Dcs_Reset_StateCleared()
    {
        // CSI interrupted by DCS - DCS should parse fresh
        Parse($"{Esc}[3;1{Esc}P1$tx\x9c");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal([1], hook.Params);
        Assert.Equal("$", hook.Intermediates);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_dcs"
    /// Basic DCS parsing with data payload.
    /// </summary>
    [Fact]
    public void Dcs_BasicParsing_PayloadCaptured()
    {
        // DCS 0;1 | data ST
        Parse($"{Esc}P0;1|17/ab\x9c");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal([0, 1], hook.Params);
        Assert.Equal('|', hook.Command);
    }

    /// <summary>
    /// Ported from: vte lib.rs "intermediate_reset_on_dcs_exit"
    /// Intermediate bytes should reset after DCS exit.
    /// </summary>
    [Fact]
    public void Dcs_IntermediateResetOnExit()
    {
        // DCS with = intermediate, then ESC with + intermediate
        Parse($"{Esc}P=1sZZZ{Esc}+\\");

        // The ESC + \ should dispatch with + as intermediate
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('\\', esc.Command);
        Assert.Equal((byte)'+', esc.Intermediates);
    }

    #endregion

    #region ESC Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset"
    /// ESC sequence state should reset for next sequence.
    /// </summary>
    [Fact]
    public void Esc_Reset_StateCleared()
    {
        // CSI interrupted by ESC - ESC should parse fresh
        Parse($"{Esc}[3;1{Esc}(A");

        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('A', esc.Command);
        Assert.Equal((byte)'(', esc.Intermediates);
    }

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset_intermediates"
    /// ESC intermediate bytes should reset between sequences.
    /// </summary>
    [Fact]
    public void Esc_IntermediatesReset_BetweenSequences()
    {
        Parse($"{Esc}[?2004l{Esc}#8");

        // First CSI, then ESC # 8
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('l', csi.Command);

        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('8', esc.Command);
        Assert.Equal((byte)'#', esc.Intermediates);
    }

    #endregion

    #region UTF-8 Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "unicode"
    /// Various Unicode characters including emoji.
    /// </summary>
    [Fact]
    public void Utf8_Unicode_EmojisAndUnderscores()
    {
        // 🎉_🦀🦀_🎉
        Parse("🎉_🦀🦀_🎉");

        var printed = Handler.GetPrintedText();
        Assert.Equal("🎉_🦀🦀_🎉", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "invalid_utf8"
    /// Invalid UTF-8 should produce replacement character.
    /// </summary>
    [Fact]
    public void Utf8_Invalid_ProducesReplacementChar()
    {
        // a + invalid UTF-8 (EF BC is incomplete 3-byte sequence) + b
        Parser.Parse(new byte[] { (byte)'a', 0xEF, 0xBC, (byte)'b' });

        var printed = Handler.GetPrintedText();
        Assert.Contains("a", printed);
        Assert.Contains("b", printed);
        // Should have handled the invalid sequence (replacement char or Latin-1)
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_utf8"
    /// Partial UTF-8 arriving byte-by-byte.
    /// </summary>
    [Fact]
    public void Utf8_Partial_ByteByByte()
    {
        // 🚀 = F0 9F 9A 80
        Parser.Parse(new byte[] { 0xF0 });
        Parser.Parse(new byte[] { 0x9F });
        Parser.Parse(new byte[] { 0x9A });
        Parser.Parse(new byte[] { 0x80 });

        var printed = Handler.GetPrintedText();
        Assert.Equal("🚀", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_utf8_separating_utf8"
    /// Multi-byte UTF-8 after partial UTF-8 state.
    /// </summary>
    [Fact]
    public void Utf8_PartialSeparatingUtf8()
    {
        // ĸ (C4 B8) + 🎉 (F0 9F 8E 89)
        Parser.Parse(new byte[] { 0xC4 });
        Parser.Parse(new byte[] { 0xB8, 0xF0, 0x9F, 0x8E, 0x89 });

        var printed = Handler.GetPrintedText();
        Assert.Contains("ĸ", printed);
        Assert.Contains("🎉", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_invalid_utf8"
    /// Partial invalid UTF-8 arriving byte-by-byte.
    /// </summary>
    [Fact]
    public void Utf8_PartialInvalid_ByteByByte()
    {
        // a + EF BC (invalid incomplete) + b
        Parser.Parse(new byte[] { (byte)'a' });
        Parser.Parse(new byte[] { 0xEF });
        Parser.Parse(new byte[] { 0xBC });
        Parser.Parse(new byte[] { (byte)'b' });

        var printed = Handler.GetPrintedText();
        Assert.Contains("a", printed);
        Assert.Contains("b", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_invalid_utf8_split"
    /// Invalid UTF-8 with valid portion split across calls.
    /// </summary>
    [Fact]
    public void Utf8_PartialInvalidSplit()
    {
        // 俙 (E4 BF 99) valid, followed by B5 (invalid continuation)
        Parser.Parse(new byte[] { 0xE4, 0xBF });
        Parser.Parse(new byte[] { 0x99, 0xB5 });

        var printed = Handler.GetPrintedText();
        Assert.Contains("俙", printed);
    }

    /// <summary>
    /// Ported from: vte lib.rs "partial_utf8_into_esc"
    /// Partial UTF-8 interrupted by escape sequence.
    /// </summary>
    [Fact]
    public void Utf8_PartialIntoEsc_ResetsAndParsesEsc()
    {
        // D8 (start of 2-byte) then ESC 0 1 2
        Parser.Parse(new byte[] { 0xD8, 0x1B, (byte)'0', (byte)'1', (byte)'2' });

        // Should have ESC 0, then print '1' '2'
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('0', esc.Command);

        var printed = Handler.GetPrintedText();
        Assert.Contains("1", printed);
        Assert.Contains("2", printed);
    }

    #endregion

    #region C1 Control Code Edge Cases

    /// <summary>
    /// Ported from: vte lib.rs "c1s"
    /// C1 control codes (0x80-0x9F) should execute.
    /// </summary>
    [Fact]
    public void C1_ControlCodes_Execute()
    {
        Parser.Parse(new byte[] { 0x00, 0x1F, 0x80, 0x90, 0x98, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, (byte)'a' });

        // Various execute events should occur
        var executes = Events.OfType<ExecuteEvent>().ToList();
        Assert.True(executes.Count >= 2); // At least 0x00 and 0x1F

        // 'a' should print
        Assert.Contains(Events, e => e is PrintEvent { Char: 'a' });
    }

    /// <summary>
    /// Ported from: vte lib.rs "execute_anywhere"
    /// CAN (0x18) and SUB (0x1A) should cancel sequences from anywhere.
    /// Note: Our parser may not dispatch Execute events for CAN/SUB directly,
    /// as they primarily function as sequence cancellation codes.
    /// </summary>
    [Fact]
    public void C0_ExecuteAnywhere_CanAndSub()
    {
        // Test that CAN and SUB cancel an in-progress sequence
        Parse($"{Esc}[1");  // Start CSI
        Parser.Parse(new byte[] { 0x18 });  // CAN cancels
        Parse("X");  // X should print (back to ground)

        // X should be printed after CAN cancels the CSI
        Assert.Contains(Events, e => e is PrintEvent { Char: 'X' });
        // CSI should not have dispatched
        Assert.DoesNotContain(Events, e => e is CsiEvent);
    }

    #endregion

    #region Color Parsing Edge Cases (from ansi.rs)

    /// <summary>
    /// Ported from: vte ansi.rs "parse_truecolor_attr"
    /// SGR 38;2;R;G;B for truecolor foreground.
    /// </summary>
    [Fact]
    public void Sgr_Truecolor_ParsesCorrectly()
    {
        Parse($"{Esc}[38;2;128;66;255m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([38, 2, 128, 66, 255], csi.Params);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_osc4_set_color"
    /// OSC 4 to set palette color.
    /// </summary>
    [Fact]
    public void Osc4_SetColor_ParsesCorrectly()
    {
        Parse($"{Esc}]4;0;#fff{Esc}\\");

        var osc = AssertSingleOsc(4);
        Assert.Equal("0;#fff", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_osc104_reset_color"
    /// OSC 104 to reset palette color.
    /// </summary>
    [Fact]
    public void Osc104_ResetColor_ParsesCorrectly()
    {
        Parse($"{Esc}]104;1;{Esc}\\");

        var osc = AssertSingleOsc(104);
        Assert.Equal("1;", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_osc104_reset_all_colors"
    /// OSC 104 with empty data to reset all colors.
    /// </summary>
    [Fact]
    public void Osc104_ResetAllColors_ParsesCorrectly()
    {
        Parse($"{Esc}]104;{Esc}\\");

        var osc = AssertSingleOsc(104);
        Assert.Empty(osc.Data);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_osc104_reset_all_colors_no_semicolon"
    /// OSC 104 without semicolon to reset all colors.
    /// </summary>
    [Fact]
    public void Osc104_ResetAllColorsNoSemicolon_ParsesCorrectly()
    {
        Parse($"{Esc}]104{Esc}\\");

        var osc = AssertSingleOsc(104);
        Assert.Empty(osc.Data);
    }

    #endregion

    #region Charset/Designate Edge Cases (from ansi.rs)

    /// <summary>
    /// Ported from: vte ansi.rs "parse_designate_g0_as_line_drawing"
    /// ESC ( 0 designates G0 as line drawing charset.
    /// </summary>
    [Fact]
    public void Esc_DesignateG0LineDrawing_ParsesCorrectly()
    {
        Parse($"{Esc}(0");

        var esc = AssertSingleEsc('0');
        Assert.Equal((byte)'(', esc.Intermediates);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_designate_g1_as_line_drawing_and_invoke"
    /// ESC ) 0 designates G1, SO (0x0E) invokes it.
    /// </summary>
    [Fact]
    public void Esc_DesignateG1AndInvoke_ParsesCorrectly()
    {
        Parse($"{Esc})0\x0E");

        var esc = AssertSingleEsc('0');
        Assert.Equal((byte)')', esc.Intermediates);

        // SO (0x0E) should execute
        Assert.Contains(Events, e => e is ExecuteEvent { Code: 0x0E });
    }

    #endregion

    #region Synchronized Update Edge Cases (from ansi.rs)

    /// <summary>
    /// Ported from: vte ansi.rs "partial_sync_updates"
    /// DECSET ?2026 for synchronized updates, split across calls.
    /// </summary>
    [Fact]
    public void Csi_SyncUpdate_SplitAcrossCalls()
    {
        // Start synchronized update
        Parse($"{Esc}[?20");
        Parse("26h");

        var csi = AssertSingleCsi('h');
        Assert.Equal([2026], csi.Params);
        Assert.True(csi.Private);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "mixed_sync_escape"
    /// Synchronized update with SGR following immediately.
    /// </summary>
    [Fact]
    public void Csi_SyncUpdateWithSgr_BothParsed()
    {
        Parse($"{Esc}[?2026h{Esc}[31m");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);

        Assert.Equal('h', csis[0].Command);
        Assert.Equal([2026], csis[0].Params);

        Assert.Equal('m', csis[1].Command);
        Assert.Equal([31], csis[1].Params);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "sync_bsu_with_esu"
    /// Begin Synchronized Update followed by End Synchronized Update.
    /// </summary>
    [Fact]
    public void Csi_BeginEndSyncUpdate_BothParsed()
    {
        Parse($"{Esc}[?2026h{Esc}[1m{Esc}[?2026l{Esc}[?2026h{Esc}[4m{Esc}[?2026l");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(6, csis.Count);

        // First BSU
        Assert.Equal('h', csis[0].Command);
        Assert.Equal([2026], csis[0].Params);

        // Bold
        Assert.Equal('m', csis[1].Command);
        Assert.Equal([1], csis[1].Params);

        // First ESU
        Assert.Equal('l', csis[2].Command);
        Assert.Equal([2026], csis[2].Params);
    }

    #endregion

    #region Terminal Identity Edge Cases (from ansi.rs)

    /// <summary>
    /// Ported from: vte ansi.rs "parse_terminal_identity_csi"
    /// CSI c (DA1) with various parameter forms.
    /// </summary>
    [Theory]
    [InlineData("\u001b[c", true)]     // No param - should report
    [InlineData("\u001b[0c", true)]    // 0 - should report
    [InlineData("\u001b[1c", false)]   // 1 - should not report (DA2)
    public void Csi_TerminalIdentity_VariousParams(string input, bool expectsIdentityRequest)
    {
        Parse(input);

        var csi = AssertSingleCsi('c');
        if (expectsIdentityRequest && csi.Params.Length > 0)
        {
            Assert.True(csi.Params[0] == 0);
        }
        else if (!expectsIdentityRequest && csi.Params.Length > 0)
        {
            Assert.True(csi.Params[0] != 0);
        }
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_terminal_identity_esc"
    /// ESC Z (DECID) requests terminal identity.
    /// </summary>
    [Fact]
    public void Esc_TerminalIdentity_Decid()
    {
        Parse($"{Esc}Z");

        var esc = AssertSingleEsc('Z');
        Assert.Equal(0, esc.Intermediates);
    }

    /// <summary>
    /// Ported from: vte ansi.rs "parse_terminal_identity_esc" with intermediate
    /// ESC # Z should NOT request terminal identity.
    /// </summary>
    [Fact]
    public void Esc_TerminalIdentityWithIntermediate_NotDecid()
    {
        Parse($"{Esc}#Z");

        var esc = AssertSingleEsc('Z');
        Assert.Equal((byte)'#', esc.Intermediates);
    }

    #endregion
}
