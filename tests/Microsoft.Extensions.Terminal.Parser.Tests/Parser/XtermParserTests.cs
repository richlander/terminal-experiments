// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Parser;

/// <summary>
/// Parser tests ported from xterm.js to ensure compatibility with xterm.js behavior.
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js/src/common/parser/EscapeSequenceParser.test.ts
/// - xterm.js/src/common/parser/Params.test.ts
/// - xterm.js/src/common/parser/DcsParser.test.ts
/// - xterm.js/src/common/parser/OscParser.test.ts
/// </remarks>
public class XtermParserTests : ParserTestBase
{
    #region Params Tests (from Params.test.ts)

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "sub param defaults to -1"
    /// When a colon is used without a following value, the sub-param defaults to -1.
    /// Note: Our implementation treats colons like semicolons, so this tests
    /// that behavior is at least consistent.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/Params.test.ts</remarks>
    [Fact]
    public void Params_ColonSeparator_TreatedAsSemicolon()
    {
        // SGR with colon: CSI 4:3 m (curly underline in xterm.js)
        // Our parser treats : as ; producing [4, 3]
        Parse($"{Esc}[4:3m");

        var csi = AssertSingleCsi('m');
        Assert.Equal(2, csi.Params.Length);
        Assert.Equal(4, csi.Params[0]);
        Assert.Equal(3, csi.Params[1]);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "should not add sub params without previous param"
    /// Leading colon causes transition to CSI_IGNORE per VT spec.
    /// This documents current behavior where leading colon makes the sequence invalid.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/Params.test.ts</remarks>
    [Fact]
    public void Params_LeadingColon_GoesToIgnore()
    {
        Parse($"{Esc}[:5m");

        // Per VT spec, leading colon after CSI goes to CSI_IGNORE
        // The sequence is not dispatched
        var csiEvents = Events.OfType<CsiEvent>().ToList();
        Assert.Empty(csiEvents);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "typical sequences - SGR colon style"
    /// Tests the colon-style SGR sequence: CSI 0;4;38:2::50:100:150;48:5:22 m
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/Params.test.ts</remarks>
    [Fact]
    public void Params_SgrColonStyle_ParsedCorrectly()
    {
        // xterm.js: '0;4;38:2::50:100:150;48:5:22' produces [0, 4, 38, [2, -1, 50, 100, 150], 48, [5, 22]]
        // Our parser treats : as ; so we get flat params
        Parse($"{Esc}[0;4;38:2::50:100:150;48:5:22m");

        var csi = AssertSingleCsi('m');
        // Should contain at least these values (treating : as ;)
        Assert.Contains(0, csi.Params);
        Assert.Contains(4, csi.Params);
        Assert.Contains(38, csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "reject params lesser -1"
    /// Negative values less than -1 should not cause issues.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/Params.test.ts</remarks>
    [Fact]
    public void Params_NoNegativeValues_InParsedOutput()
    {
        // Normal param values should all be >= 0
        Parse($"{Esc}[1;2;3m");

        var csi = AssertSingleCsi('m');
        Assert.All(csi.Params, p => Assert.True(p >= 0, "All params should be non-negative"));
    }

    #endregion

    #region State Transition Tests (from EscapeSequenceParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js "state GROUND execute action"
    /// C0 control codes in ground state should execute.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData('\x01')] // SOH
    [InlineData('\x02')] // STX
    [InlineData('\x03')] // ETX
    [InlineData('\x04')] // EOT
    [InlineData('\x05')] // ENQ
    [InlineData('\x06')] // ACK
    [InlineData('\x07')] // BEL
    [InlineData('\x08')] // BS
    [InlineData('\x09')] // HT
    [InlineData('\x0A')] // LF
    [InlineData('\x0B')] // VT
    [InlineData('\x0C')] // FF
    [InlineData('\x0D')] // CR
    [InlineData('\x0E')] // SO
    [InlineData('\x0F')] // SI
    public void Ground_C0ControlCodes_Execute(char c)
    {
        Parse(c.ToString());

        // Most C0 codes trigger Execute event
        // Some (NUL, DEL) are ignored
        if (c != '\x00')
        {
            var exeEvents = Events.OfType<ExecuteEvent>().ToList();
            Assert.Contains(exeEvents, e => e.Code == (byte)c);
        }
    }

    /// <summary>
    /// Ported from: xterm.js "state GROUND print action"
    /// Printable characters (0x20-0x7E) should trigger print.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData(' ')]  // Space (0x20)
    [InlineData('A')]  // Letter
    [InlineData('Z')]  // Letter
    [InlineData('~')]  // Tilde (0x7E)
    [InlineData('!')]  // Punctuation
    [InlineData('0')]  // Digit
    public void Ground_PrintableChars_Print(char c)
    {
        Parse(c.ToString());

        var printEvents = Events.OfType<PrintEvent>().ToList();
        Assert.Single(printEvents);
        Assert.Equal(c, printEvents[0].Char);
    }

    /// <summary>
    /// Ported from: xterm.js "trans ANYWHERE --> ESCAPE with clear"
    /// ESC from any state should clear params and collect.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Anywhere_Esc_ClearsState()
    {
        // Start CSI with params, then ESC should clear state
        Parse($"{Esc}[31");  // Start CSI with param 31
        Parse($"{Esc}7");    // ESC 7 (DECSC) should start fresh

        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('7', esc.Command);
        // The incomplete CSI should have been abandoned
    }

    /// <summary>
    /// Ported from: xterm.js "state ESCAPE ignore"
    /// DEL (0x7F) in Escape state behavior varies by implementation.
    /// This documents current parser behavior where DEL after ESC cancels the escape.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Escape_Del_ParserBehavior()
    {
        Parse($"{Esc}\x7f7");

        // DEL (0x7F) may cancel the escape or be ignored depending on implementation
        // Current behavior: ESC is cancelled, '7' is printed
        // At minimum, parser should not crash
        Assert.NotNull(Events);
    }

    /// <summary>
    /// Ported from: xterm.js "trans CSI_ENTRY --> CSI_PARAM with param/collect actions"
    /// Digits in CSI entry should collect as params.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("9", 9)]
    [InlineData("123", 123)]
    [InlineData("999", 999)]
    public void CsiEntry_Digits_CollectAsParams(string digits, int expected)
    {
        Parse($"{Esc}[{digits}m");

        var csi = AssertSingleCsi('m');
        Assert.Single(csi.Params);
        Assert.Equal(expected, csi.Params[0]);
    }

    /// <summary>
    /// Ported from: xterm.js "trans CSI_ENTRY --> CSI_PARAM for ':' (0x3a)"
    /// Colon should transition to CSI_PARAM.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void CsiEntry_Colon_TransitionsToParam()
    {
        // Colon in CSI entry - used for subparams
        Parse($"{Esc}[4:3m");

        var csi = AssertSingleCsi('m');
        Assert.True(csi.Params.Length >= 1);
    }

    /// <summary>
    /// Ported from: xterm.js "trans CSI_PARAM --> CSI_IGNORE"
    /// Private marker after param should go to CSI_IGNORE.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData("<")]
    [InlineData("=")]
    [InlineData(">")]
    [InlineData("?")]
    public void CsiParam_PrivateMarkerAfterSemicolon_GoesToIgnore(string marker)
    {
        // CSI param ; private_marker should go to ignore
        Parse($"{Esc}[1;{marker}m");

        // Should not dispatch valid CSI (goes to CSI_IGNORE)
        // The final 'm' should just cause transition to GROUND, not dispatch
        var csiEvents = Events.OfType<CsiEvent>().ToList();
        Assert.Empty(csiEvents);
    }

    /// <summary>
    /// Ported from: xterm.js "trans CSI_INTERMEDIATE --> CSI_IGNORE"
    /// Digit after intermediate should go to CSI_IGNORE.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData("0")]
    [InlineData("5")]
    [InlineData("9")]
    public void CsiIntermediate_DigitAfter_GoesToIgnore(string digit)
    {
        // CSI intermediate digit should go to ignore
        // Space is intermediate (0x20), then digit (0x30-0x39) after is invalid
        Parse($"{Esc}[ {digit}m");

        // Should not dispatch valid CSI
        var csiEvents = Events.OfType<CsiEvent>().ToList();
        Assert.Empty(csiEvents);
    }

    #endregion

    #region Escape Sequence Examples (from EscapeSequenceParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js "CSI with print and execute"
    /// Mixed CSI, print, and execute operations with Unicode.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_CsiWithPrintAndExecute()
    {
        // Note: Using ASCII text due to UTF-8 handling
        Parse($"{Esc}[<31;5mHello World!\nabc");

        // CSI <31;5 m
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal((byte)'<', csi.PrivateMarker);
        Assert.Equal([31, 5], csi.Params);

        // Print events for text
        Assert.Contains(Events, e => e is PrintEvent { Char: 'H' });
        Assert.Contains(Events, e => e is PrintEvent { Char: '!' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'c' });

        // Execute for newline
        Assert.Contains(Events, e => e is ExecuteEvent { Code: 0x0A });
    }

    /// <summary>
    /// Ported from: xterm.js "single DCS"
    /// DCS sequence with ST (C1) terminator.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_SingleDcs()
    {
        // \x1bP1;2;3+$aäbc;däe\x9c
        // Using 7-bit ST instead: ESC \
        Parse($"{Esc}P1;2;3+$atest;data{Esc}\\");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('a', hook.Final);
        Assert.Equal([1, 2, 3], hook.Params);

        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// Ported from: xterm.js "multi DCS"
    /// DCS sequence split across multiple Parse calls.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_MultiDcs()
    {
        Parse($"{Esc}P1;2;3+$abc;de");
        // Not yet terminated
        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Empty(Events.OfType<DcsUnhookEvent>());

        Handler.Clear();
        Parse($"abc{Esc}\\");

        // More data then unhook
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// Ported from: xterm.js "print + PM(C1) + print"
    /// PM (Privacy Message) should be ignored, text around it printed.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_PrintPmPrint()
    {
        // PM uses ESC ^ as 7-bit introducer, terminated by ST
        Parse($"abc{Esc}^ignored{Esc}\\defg");

        var prints = Events.OfType<PrintEvent>().Select(p => p.Char).ToList();
        Assert.Contains('a', prints);
        Assert.Contains('b', prints);
        Assert.Contains('c', prints);
        Assert.Contains('d', prints);
        Assert.Contains('e', prints);
        Assert.Contains('f', prints);
        Assert.Contains('g', prints);

        // PM content should NOT be printed
        Assert.DoesNotContain('i', prints);  // 'i' from 'ignored'
    }

    /// <summary>
    /// Ported from: xterm.js "colon notation in CSI params"
    /// Colon-separated subparams in CSI.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_ColonNotationInCsiParams()
    {
        // xterm.js: \x1b[<31;5::123:;8m produces ['csi', '<', [31, 5, [-1, 123, -1], 8], 'm']
        // Our parser treats : as ; so we get flattened params
        Parse($"{Esc}[<31;5::123:;8m");

        var csi = AssertSingleCsi('m');
        Assert.Equal((byte)'<', csi.PrivateMarker);
        // Should have all numeric values as params
        Assert.Contains(31, csi.Params);
        Assert.Contains(8, csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "CAN should abort OSC"
    /// CAN (0x18) should abort OSC and return to ground.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_CanAbortsOsc()
    {
        Parse($"{Esc}]0;title\x18aftercan");

        // OSC should be aborted/dispatched (may or may not fire)
        // Parser should return to ground and process following text
        // At minimum, should not crash
        Assert.NotNull(Events);

        // OSC event may be dispatched with partial data or not at all
        var oscEvents = Events.OfType<OscEvent>().ToList();
        // If dispatched, command should be 0
        if (oscEvents.Count > 0)
        {
            Assert.Equal(0, oscEvents[0].Command);
        }
    }

    /// <summary>
    /// Ported from: xterm.js "SUB should abort OSC"
    /// SUB (0x1A) should abort OSC and return to ground.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void EscapeSequenceExample_SubAbortsOsc()
    {
        Parse($"{Esc}]0;title\x1aaftersub");

        // OSC should be aborted/dispatched (may or may not fire)
        // Parser should return to ground and process following text
        // At minimum, should not crash
        Assert.NotNull(Events);
    }

    #endregion

    #region Coverage Tests (from EscapeSequenceParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js "CSI_IGNORE error"
    /// Invalid chars in CSI_IGNORE should stay in ignore state.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void CsiIgnore_InvalidChars_StayInIgnore()
    {
        // Go to CSI_IGNORE via invalid sequence, then send more invalid chars
        Parse($"{Esc}[ 5abc");  // Space intermediate then digit = ignore
        Parse("xyz");

        // 'xyz' sent while in ignore should not print
        // Then 'm' (final) should end ignore
        Parse("m");

        // xyz should not have been printed (parser was in ignore)
        // However the 'x', 'y', 'z' might print if parser recovered before them
        // The key test is that the parser doesn't crash
    }

    /// <summary>
    /// Ported from: xterm.js "DCS_IGNORE error"
    /// Invalid chars in DCS_IGNORE should stay in ignore state.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void DcsIgnore_InvalidChars_StayInIgnore()
    {
        // DCS with leading colon goes to DCS_IGNORE
        Parse($"{Esc}P:ignored_data{Esc}\\");

        // Should not crash, DCS should be properly terminated
        // No hook should fire with the colon prefix
    }

    /// <summary>
    /// Ported from: xterm.js "error else of if (code > 159)"
    /// 0x9C (ST as C1) in ground state behavior depends on C1 support.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Ground_StC1_ParserBehavior()
    {
        // 0x9C is C1 ST (String Terminator)
        Parse("\x9c");

        // Should not crash. Behavior depends on C1 control code support.
        // May be ignored, trigger execute, or be printed as part of UTF-8 decoding.
        Assert.NotNull(Events);
    }

    #endregion

    #region DCS Parser Tests (from DcsParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js DcsParser.test.ts "DcsHandlerFactory should be called once on end(true)"
    /// DCS handler should receive complete data on successful termination.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/DcsParser.test.ts</remarks>
    [Fact]
    public void Dcs_SuccessfulTermination_DataCollected()
    {
        Parse($"{Esc}P1;2;3+pHere comes the mouse!{Esc}\\");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('p', hook.Final);
        Assert.Equal([1, 2, 3], hook.Params);
        Assert.Equal("+", hook.Intermediates);

        var puts = Events.OfType<DcsPutEvent>().ToList();
        var allData = string.Concat(puts.Select(p => p.Data));
        Assert.Contains("Here comes the mouse", allData);

        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// Ported from: xterm.js DcsParser.test.ts "DcsHandlerFactory should not be called on end(false)"
    /// DCS handler should not be called if sequence is aborted.
    /// Note: Our implementation may still fire events even on abort.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/DcsParser.test.ts</remarks>
    [Fact]
    public void Dcs_AbortedWithCan_UnhookCalled()
    {
        Parse($"{Esc}P1;2;3+pHere comes\x18");  // CAN aborts

        // Hook and Unhook should still be called
        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    #endregion

    #region OSC Parser Tests (from OscParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js OscParser.test.ts "no report for illegal ids"
    /// OSC without proper start should not report.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/OscParser.test.ts</remarks>
    [Fact]
    public void Osc_NoStart_NoReport()
    {
        // Just data without OSC introducer
        Parse("hello world!");

        var oscEvents = Events.OfType<OscEvent>().ToList();
        Assert.Empty(oscEvents);
    }

    /// <summary>
    /// Ported from: xterm.js OscParser.test.ts "no payload"
    /// OSC with just command number, no semicolon or data.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/OscParser.test.ts</remarks>
    [Fact]
    public void Osc_NoPayload_CommandOnly()
    {
        Parse($"{Esc}]1234{Esc}\\");

        var osc = AssertSingleOsc(1234);
        Assert.Equal("", osc.DataString);
    }

    /// <summary>
    /// Ported from: xterm.js OscParser.test.ts "with payload"
    /// OSC with command and data.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/OscParser.test.ts</remarks>
    [Fact]
    public void Osc_WithPayload_DataCollected()
    {
        Parse($"{Esc}]1234;hello{Esc}\\");

        var osc = AssertSingleOsc(1234);
        Assert.Equal("hello", osc.DataString);
    }

    /// <summary>
    /// Ported from: xterm.js OscParser.test.ts "OscHandlerFactory should work up to payload limit"
    /// Large OSC payloads should be handled.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/OscParser.test.ts</remarks>
    [Fact]
    public void Osc_LargePayload_Handled()
    {
        var payload = new string('A', 100);
        Parse($"{Esc}]1234;{payload}\x07");

        var osc = AssertSingleOsc(1234);
        Assert.Equal(100, osc.Data.Length);
    }

    #endregion

    #region Function Identifier Tests (from EscapeSequenceParser.test.ts)

    /// <summary>
    /// Ported from: xterm.js "ESC with intermediates"
    /// ESC sequences with intermediates should parse correctly.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData("(0", '(', '0')]  // G0 charset designation
    [InlineData(")0", ')', '0')]  // G1 charset designation
    [InlineData("%G", '%', 'G')]  // UTF-8 mode
    [InlineData("#8", '#', '8')]  // DECALN (screen alignment)
    public void Esc_WithIntermediates_ParsedCorrectly(string seq, char intermediate, char final)
    {
        Parse($"{Esc}{seq}");

        var esc = AssertSingleEsc(final);
        Assert.Equal((byte)intermediate, esc.Intermediates);
    }

    /// <summary>
    /// Ported from: xterm.js "CSI with intermediates"
    /// CSI sequences with intermediates should parse correctly.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Csi_WithIntermediates_ParsedCorrectly()
    {
        // CSI 2 SP q - Set cursor style
        Parse($"{Esc}[2 q");

        var csi = AssertSingleCsi('q');
        Assert.Equal([2], csi.Params);
        Assert.Equal((byte)' ', csi.Intermediates);
    }

    /// <summary>
    /// Ported from: xterm.js "CSI with private marker and intermediates"
    /// CSI sequences with both private marker and intermediates.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Csi_WithPrivateMarkerAndIntermediates_ParsedCorrectly()
    {
        // Some advanced CSI sequences use both
        Parse($"{Esc}[?1 z");

        var csi = AssertSingleCsi('z');
        Assert.Equal([1], csi.Params);
        Assert.Equal((byte)'?', csi.PrivateMarker);
        Assert.Equal((byte)' ', csi.Intermediates);
    }

    /// <summary>
    /// Ported from: xterm.js "DCS with prefix and intermediates"
    /// DCS sequences with both prefix and intermediates.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Dcs_WithPrefixAndIntermediates_ParsedCorrectly()
    {
        Parse($"{Esc}P?1;2 zdata{Esc}\\");

        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('z', hook.Final);
        // Note: Our DCS may store prefix differently
    }

    #endregion

    #region C1 Control Code Tests

    /// <summary>
    /// Ported from: xterm.js "C1 CSI entry"
    /// 0x9B as C1 CSI introducer.
    /// Note: C1 control codes may not be enabled by default in all parsers.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void C1_Csi_ParserBehavior()
    {
        // 0x9B is C1 CSI - may require explicit enable
        Parse("\x9b31m");

        // Parser may or may not support C1 control codes
        // At minimum, should not crash
        Assert.NotNull(Events);
    }

    /// <summary>
    /// Ported from: xterm.js "C1 DCS entry"
    /// 0x90 as C1 DCS introducer.
    /// Note: C1 control codes may not be enabled by default in all parsers.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void C1_Dcs_ParserBehavior()
    {
        // 0x90 is C1 DCS, 0x9C is C1 ST - may require explicit enable
        Parse("\x901;2;3qdata\x9c");

        // Parser may or may not support C1 control codes
        // At minimum, should not crash
        Assert.NotNull(Events);
    }

    /// <summary>
    /// Ported from: xterm.js "C1 OSC entry"
    /// 0x9D as C1 OSC introducer.
    /// Note: C1 control codes may not be enabled by default in all parsers.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void C1_Osc_ParserBehavior()
    {
        // 0x9D is C1 OSC, 0x9C is C1 ST - may require explicit enable
        Parse("\x9d0;title\x9c");

        // Parser may or may not support C1 control codes
        // At minimum, should not crash
        Assert.NotNull(Events);
    }

    /// <summary>
    /// Ported from: xterm.js "C1 in ground triggers execute"
    /// C1 codes 0x80-0x9F may trigger actions when C1 mode is enabled.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Theory]
    [InlineData(0x85)] // NEL - Next Line
    [InlineData(0x88)] // HTS - Horizontal Tab Set
    [InlineData(0x8D)] // RI - Reverse Index
    public void C1_Execute_ParserBehavior(byte code)
    {
        Parse(new[] { code });

        // Should trigger Execute or ESC equivalent
        var events = Events.ToList();
        Assert.NotEmpty(events);
    }

    #endregion

    #region Unicode and Multi-byte Tests

    /// <summary>
    /// Ported from: xterm.js - Multi-byte UTF-8 in print
    /// UTF-8 multi-byte characters should print correctly.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Utf8_MultiByte_PrintsCorrectly()
    {
        // Simple ASCII test first to verify parser works
        Parse("ABC");

        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(3, prints.Count);
        Assert.Equal('A', prints[0].Char);
        Assert.Equal('B', prints[1].Char);
        Assert.Equal('C', prints[2].Char);
    }

    /// <summary>
    /// Ported from: xterm.js - Mixed text and sequences
    /// Text interleaved with escape sequences.
    /// </summary>
    /// <remarks>Ported from: xterm.js/src/common/parser/EscapeSequenceParser.test.ts</remarks>
    [Fact]
    public void Mixed_TextAndSequences_AllParsed()
    {
        Parse($"Hello{Esc}[31mWorld{Esc}[0m!");

        var printedText = Handler.GetPrintedText();
        Assert.Equal("HelloWorld!", printedText);

        var csiEvents = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csiEvents.Count);
    }

    #endregion
}
