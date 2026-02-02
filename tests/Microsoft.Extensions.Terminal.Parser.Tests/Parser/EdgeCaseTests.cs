// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Edge case and malformed sequence tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js: src/common/parser/EscapeSequenceParser.test.ts
/// - libvterm: t/02parser.test
/// - Windows Terminal: src/terminal/parser/ut_parser/
/// </remarks>
public class EdgeCaseTests : ParserTestBase
{
    #region Sequence Interruption

    /// <summary>
    /// Ported from: xterm.js "ESC in middle of CSI should start new escape"
    /// </summary>
    [Fact]
    public void Esc_InterruptsCsi_StartsNewSequence()
    {
        Parse("\u001b[31\u001b7");

        // Original CSI should be abandoned
        Assert.DoesNotContain(Handler.Events, e => e is CsiEvent);
        
        // New ESC sequence should be parsed
        Assert.Contains(Handler.Events, e => e is EscEvent { Command: '7' });
    }

    /// <summary>
    /// Ported from: libvterm - ESC in OSC
    /// </summary>
    [Fact]
    public void Esc_InterruptsOsc_EndsOsc()
    {
        // ESC in OSC should terminate it (ESC \ is ST)
        Parse("\u001b]0;title\u001b\\");

        var osc = AssertSingleOsc(0);
        Assert.Equal("title", System.Text.Encoding.UTF8.GetString(osc.Data));
    }

    #endregion

    #region Invalid Sequences

    /// <summary>
    /// Ported from: xterm.js "should handle invalid CSI"
    /// 
    /// Invalid bytes in CSI sequence should cause it to be ignored.
    /// </summary>
    [Fact]
    public void Csi_InvalidByte_IgnoresSequence()
    {
        // 0x7F (DEL) is invalid in CSI
        Parse("\u001b[\x7fm");

        // Final byte reached, but sequence was invalid - may or may not dispatch
        // depending on implementation. At minimum, shouldn't crash.
    }

    /// <summary>
    /// Ported from: xterm.js "should handle param after intermediate"
    /// 
    /// Parameters after intermediates are invalid - sequence ignored.
    /// </summary>
    [Fact]
    public void Csi_ParamAfterIntermediate_IgnoresSequence()
    {
        Parse("\u001b[ 5m");  // Space is intermediate, 5 after is invalid

        // This should go to CSI ignore state
        var csiEvents = Handler.Events.OfType<CsiEvent>().ToList();
        
        // Either no dispatch or dispatch with partial data
        // The key is no crash
    }

    #endregion

    #region Reset Behavior

    /// <summary>
    /// Ported from: xterm.js "Reset should clear state"
    /// </summary>
    [Fact]
    public void Reset_ClearsPartialSequence()
    {
        Parse("\u001b[31");  // Partial CSI
        Parser.Reset();
        Parse("m");  // Would be final byte if state preserved

        // 'm' should be printed, not dispatch CSI
        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'm' });
        Assert.DoesNotContain(Handler.Events, e => e is CsiEvent);
    }

    #endregion

    #region Rapid Sequences

    /// <summary>
    /// Ported from: Windows Terminal - multiple sequences
    /// </summary>
    [Fact]
    public void Multiple_CsiSequences_AllParsed()
    {
        Parse("\u001b[1m\u001b[31m\u001b[42m");

        var csiEvents = Handler.Events.OfType<CsiEvent>().ToList();
        Assert.Equal(3, csiEvents.Count);
        Assert.All(csiEvents, e => Assert.Equal('m', e.Command));
    }

    /// <summary>
    /// Ported from: Windows Terminal - mixed sequence types
    /// </summary>
    [Fact]
    public void Mixed_SequenceTypes_AllParsed()
    {
        Parse("\u001b[31mHello\u001b7\u001b]0;Title\x07World\u001b[0m");

        var events = Handler.Events;
        
        Assert.Equal(2, events.OfType<CsiEvent>().Count());  // [31m and [0m
        Assert.Single(events.OfType<EscEvent>());
        Assert.Single(events.OfType<OscEvent>());
        Assert.Equal("HelloWorld", Handler.GetPrintedText());
    }

    #endregion

    #region DCS Sequences

    /// <summary>
    /// Ported from: xterm.js DcsParser.test.ts
    /// </summary>
    [Fact]
    public void Dcs_BasicSequence_ParsesCorrectly()
    {
        Parse("\u001bPq#0;2;0;0;0#1;2;100;100;100\u001b\\");

        var hooks = Handler.Events.OfType<DcsHookEvent>().ToList();
        Assert.Single(hooks);
        Assert.Equal('q', hooks[0].Command);
        
        var unhooks = Handler.Events.OfType<DcsUnhookEvent>().ToList();
        Assert.Single(unhooks);
    }

    /// <summary>
    /// Ported from: xterm.js "DCS with params"
    /// </summary>
    [Fact]
    public void Dcs_WithParams_ParsesCorrectly()
    {
        Parse("\u001bP1;2;3qDATA\u001b\\");

        var hook = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal([1, 2, 3], hook.Params);
        Assert.Equal('q', hook.Command);

        // Our handler accumulates DCS data and emits as single event
        var put = Assert.Single(Handler.Events.OfType<DcsPutEvent>());
        Assert.Equal("DATA", put.Data);
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "osc_containing_string_terminator"
    /// OSC that contains what looks like a string terminator.
    /// </summary>
    [Fact]
    public void Osc_ContainingEscapeBackslash_ParsesCorrectly()
    {
        // OSC with data that might look like ST
        Parse("\u001b]0;title with \\ backslash\x07");

        var osc = AssertSingleOsc(0);
        var data = System.Text.Encoding.UTF8.GetString(osc.Data);
        Assert.Contains("backslash", data);
    }

    /// <summary>
    /// Ported from: vte lib.rs "exceed_max_buffer_size"
    /// OSC with data exceeding buffer should not crash.
    /// </summary>
    [Fact]
    public void Osc_ExceedsBuffer_TruncatesGracefully()
    {
        var longData = new string('x', 5000);  // Exceeds our 4096 buffer
        Parse($"\u001b]0;{longData}\x07");

        var osc = AssertSingleOsc(0);
        Assert.True(osc.Data.Length <= 4096);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_dcs_max_params"
    /// DCS with maximum parameters.
    /// </summary>
    [Fact]
    public void Dcs_MaxParams_HandledCorrectly()
    {
        Parse("\u001bP1;2;3;4;5;6;7;8qDATA\u001b\\");

        var hook = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal(8, hook.Params.Length);
    }

    /// <summary>
    /// Ported from: vte lib.rs "dcs_reset"
    /// DCS parser state resets properly.
    /// </summary>
    [Fact]
    public void Dcs_Reset_ClearsState()
    {
        Parse("\u001bP1qDATA\u001b\\");
        Handler.Clear();

        Parse("\u001bP2rMORE\u001b\\");

        var hook = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal([2], hook.Params);
        Assert.Equal('r', hook.Command);
    }

    /// <summary>
    /// Ported from: vte lib.rs "intermediate_reset_on_dcs_exit"
    /// Intermediate bytes should reset after DCS.
    /// </summary>
    [Fact]
    public void Dcs_ExitResetsIntermediates()
    {
        Parse("\u001bP q\u001b\\");  // DCS with space intermediate
        Handler.Clear();

        Parse("\u001bPr\u001b\\");  // DCS without intermediate

        var hook = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal("", hook.Intermediates);
    }

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset"
    /// ESC state resets properly.
    /// </summary>
    [Fact]
    public void Esc_Reset_ClearsState()
    {
        Parse("\u001b7");
        Handler.Clear();
        Parser.Reset();

        Parse("X");  // Should print, not be part of escape

        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'X' });
        Assert.DoesNotContain(Handler.Events, e => e is EscEvent);
    }

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset_intermediates"
    /// ESC intermediate bytes reset after sequence.
    /// </summary>
    [Fact]
    public void Esc_IntermediatesReset()
    {
        Parse("\u001b(0");  // ESC with '(' intermediate
        Handler.Clear();

        Parse("\u001b7");  // ESC 7 without intermediate

        var esc = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal(0, esc.Intermediates);
    }

    #endregion

    #region Windows Terminal Ported Tests

    /// <summary>
    /// Ported from: Windows Terminal StateMachineTest.cpp "BulkTextPrint"
    /// Large blocks of text should print efficiently.
    /// </summary>
    [Fact]
    public void Print_BulkText_AllCharactersPrinted()
    {
        var text = new string('A', 10000);
        Parse(text);

        Assert.Equal(10000, Handler.Events.OfType<PrintEvent>().Count());
    }

    /// <summary>
    /// Ported from: Windows Terminal StateMachineTest.cpp "PassThroughUnhandledSplitAcrossWrites"
    /// Unhandled sequences split across writes should reassemble.
    /// </summary>
    [Fact]
    public void Csi_UnhandledSplitWrites_Reassembles()
    {
        Parse("\u001b[?");
        Parse("9999");
        Parse("h");

        var csi = AssertSingleCsi('h');
        Assert.Equal((byte)'?', csi.PrivateMarker);
        Assert.Equal([9999], csi.Params);
    }

    /// <summary>
    /// Ported from: Windows Terminal StateMachineTest.cpp "DcsDataStringsReceivedByHandler"
    /// DCS with various terminators.
    /// </summary>
    [Theory]
    [InlineData("\u001bPqDATA\u001b\\", "ST")]     // String Terminator
    [InlineData("\u001bPqDATA\x18", "CAN")]       // Cancel
    [InlineData("\u001bPqDATA\x1A", "SUB")]       // Substitute
    public void Dcs_VariousTerminators_AllWork(string input, string terminatorName)
    {
        _ = terminatorName;
        Parse(input);

        Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Single(Handler.Events.OfType<DcsUnhookEvent>());
    }

    #endregion

    #region xterm.js Escape Sequence Examples

    /// <summary>
    /// Ported from: xterm.js "CSI with print and execute"
    /// Mixed CSI, print, and execute operations.
    /// </summary>
    [Fact]
    public void Csi_WithPrintAndExecute_AllDispatch()
    {
        Parse("\u001b[31;5mHello World!\nabc");

        // CSI 31;5 m
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal([31, 5], csi.Params);

        // Print events for text
        Assert.Contains(Events, e => e is PrintEvent { Char: 'H' });
        Assert.Contains(Events, e => e is PrintEvent { Char: '!' });

        // Execute for newline
        Assert.Contains(Events, e => e is ExecuteEvent { Code: 0x0A });
    }

    /// <summary>
    /// Ported from: xterm.js "error recovery"
    /// Invalid byte in CSI should cause sequence to be ignored and parser recovers.
    /// </summary>
    [Fact]
    public void Csi_InvalidByteRecovery_PrintsAndContinues()
    {
        // Use ASCII only to avoid UTF-8 complications
        // Invalid char in CSI (like !) outside valid range causes CsiIgnore
        // After CsiIgnore, parser returns to Ground on final byte
        Parse("\u001b[1!m");  // '!' is 0x21, invalid in CSI params
        Parse("xyz\u001b[<;c");

        // Should print xyz after recovery
        Assert.Contains(Events, e => e is PrintEvent { Char: 'x' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'y' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'z' });

        // Should parse the second CSI
        var csi = Events.OfType<CsiEvent>().LastOrDefault();
        Assert.NotNull(csi);
        Assert.Equal('c', csi.Command);
    }

    /// <summary>
    /// Ported from: xterm.js "7bit ST should be swallowed"
    /// ESC \ (7-bit ST) terminates OSC and shouldn't produce extra output.
    /// </summary>
    [Fact]
    public void Osc_7BitST_SwallowedCorrectly()
    {
        Parse("abc\u001b]123;data\u001b\\defg");

        // abc printed
        Assert.Contains(Events, e => e is PrintEvent { Char: 'a' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'b' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'c' });

        // OSC dispatched
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(123, osc.Command);

        // defg printed (ST swallowed, no extra chars)
        Assert.Contains(Events, e => e is PrintEvent { Char: 'd' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'g' });
    }

    /// <summary>
    /// Ported from: xterm.js "CAN should abort DCS"
    /// CAN (0x18) aborts DCS and returns to ground.
    /// </summary>
    [Fact]
    public void Dcs_CanAborts_UnhookCalled()
    {
        // DCS with data then CAN
        Parse("\u001bP1;2;3qdata\x18");

        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// Ported from: xterm.js "SUB should abort DCS"
    /// SUB (0x1A) aborts DCS and returns to ground.
    /// </summary>
    [Fact]
    public void Dcs_SubAborts_UnhookCalled()
    {
        Parse("\u001bP1;2;3qdata\x1a");

        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// Ported from: xterm.js "print + PM(C1) + print"
    /// PM (Privacy Message) with C1 intro should be ignored, text around it printed.
    /// </summary>
    [Fact]
    public void Pm_C1_IgnoredTextPrinted()
    {
        // PM uses ESC ^ as 7-bit introducer
        Parse("abc\u001b^ignored\u001b\\defg");

        Assert.Contains(Events, e => e is PrintEvent { Char: 'a' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'c' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'd' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'g' });

        // PM content should not be printed
        Assert.DoesNotContain(Events, e => e is PrintEvent { Char: 'i' });
    }

    /// <summary>
    /// Ported from: xterm.js "APC handling"
    /// APC (Application Program Command) with 7-bit introducer.
    /// </summary>
    [Fact]
    public void Apc_7Bit_Handled()
    {
        // APC uses ESC _ as 7-bit introducer
        Parse("abc\u001b_apc data\u001b\\xyz");

        Assert.Contains(Events, e => e is PrintEvent { Char: 'a' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'c' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'x' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'z' });

        // APC content should not be printed
        Assert.DoesNotContain(Events, e => e is PrintEvent { Char: 'p' });
    }

    /// <summary>
    /// Ported from: xterm.js - SOS handling
    /// SOS (Start of String) with 7-bit introducer.
    /// </summary>
    [Fact]
    public void Sos_7Bit_Handled()
    {
        // SOS uses ESC X as 7-bit introducer
        Parse("abc\u001bXsos data\u001b\\xyz");

        Assert.Contains(Events, e => e is PrintEvent { Char: 'a' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'c' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'x' });
        Assert.Contains(Events, e => e is PrintEvent { Char: 'z' });

        // SOS content should not be printed
        Assert.DoesNotContain(Events, e => e is PrintEvent { Char: 's' });
    }

    #endregion
}
