// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// CSI (Control Sequence Introducer) parsing tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js: src/common/parser/EscapeSequenceParser.test.ts
/// - libvterm: t/02parser.test
/// </remarks>
public class CsiParsingTests : ParserTestBase
{
    #region Basic CSI Sequences

    /// <summary>
    /// Ported from: xterm.js "should parse CSI with final byte"
    /// Basic CSI sequence with no parameters.
    /// </summary>
    [Fact]
    public void Csi_NoParams_DispatchesCommand()
    {
        Parse("\u001b[m");

        var csi = AssertSingleCsi('m');
        Assert.Empty(csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should parse CSI with single param"
    /// </summary>
    [Fact]
    public void Csi_SingleParam_ParsesCorrectly()
    {
        Parse("\u001b[1m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should parse CSI with multiple params"
    /// </summary>
    [Fact]
    public void Csi_MultipleParams_ParsesCorrectly()
    {
        Parse("\u001b[1;2;3m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1, 2, 3], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle CSI with empty params"
    /// 
    /// Some applications omit parameters, expecting defaults.
    /// \u001b[;H should produce params [0, 0] per Zero Default Mode.
    /// </summary>
    [Fact]
    public void Csi_EmptyParams_UsesZeroDefault()
    {
        Parse("\u001b[;H");

        var csi = AssertSingleCsi('H');
        Assert.Equal([0, 0], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle leading semicolon"
    /// </summary>
    [Fact]
    public void Csi_LeadingSemicolon_TreatsAsZero()
    {
        Parse("\u001b[;5H");

        var csi = AssertSingleCsi('H');
        Assert.Equal([0, 5], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle trailing semicolon"
    /// </summary>
    [Fact]
    public void Csi_TrailingSemicolon_AddsZeroParam()
    {
        Parse("\u001b[5;H");

        var csi = AssertSingleCsi('H');
        Assert.Equal([5, 0], csi.Params);
    }

    #endregion

    #region Cursor Movement (CUP, CUU, CUD, CUF, CUB)

    /// <summary>
    /// Ported from: libvterm t/02parser.test "CSI H"
    /// Cursor position with parameters.
    /// </summary>
    [Fact]
    public void Csi_CursorPosition_ParsesRowAndColumn()
    {
        Parse("\u001b[5;10H");

        var csi = AssertSingleCsi('H');
        Assert.Equal([5, 10], csi.Params);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test
    /// Cursor movement commands.
    /// </summary>
    [Theory]
    [InlineData("\u001b[5A", 'A', new[] { 5 })]  // Cursor up
    [InlineData("\u001b[5B", 'B', new[] { 5 })]  // Cursor down
    [InlineData("\u001b[5C", 'C', new[] { 5 })]  // Cursor forward
    [InlineData("\u001b[5D", 'D', new[] { 5 })]  // Cursor back
    public void Csi_CursorMovement_ParsesCorrectly(string input, char command, int[] expectedParams)
    {
        Parse(input);

        var csi = AssertSingleCsi(command);
        Assert.Equal(expectedParams, csi.Params);
    }

    #endregion

    #region Private Mode Sequences (DECSET/DECRST)

    /// <summary>
    /// Ported from: xterm.js "should handle private marker ?"
    /// 
    /// Private mode sequences like \u001b[?1049h use '?' as private marker.
    /// </summary>
    [Fact]
    public void Csi_PrivateMode_ParsesQuestionMark()
    {
        Parse("\u001b[?1049h");

        var csi = AssertSingleCsi('h');
        Assert.Equal([1049], csi.Params);
        Assert.Equal((byte)'?', csi.PrivateMarker);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle private marker >"
    /// </summary>
    [Fact]
    public void Csi_PrivateMode_ParsesGreaterThan()
    {
        Parse("\u001b[>0c");

        var csi = AssertSingleCsi('c');
        Assert.Equal([0], csi.Params);
        Assert.Equal((byte)'>', csi.PrivateMarker);
    }

    #endregion

    #region SGR (Select Graphic Rendition)

    /// <summary>
    /// Ported from: xterm.js - SGR sequences for colors
    /// </summary>
    [Theory]
    [InlineData("\u001b[0m", new[] { 0 })]        // Reset
    [InlineData("\u001b[1m", new[] { 1 })]        // Bold
    [InlineData("\u001b[31m", new[] { 31 })]      // Red foreground
    [InlineData("\u001b[38;5;196m", new[] { 38, 5, 196 })]  // 256-color
    public void Csi_Sgr_ParsesColorCodes(string input, int[] expectedParams)
    {
        Parse(input);

        var csi = AssertSingleCsi('m');
        Assert.Equal(expectedParams, csi.Params);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Ported from: xterm.js "should handle large parameter values"
    /// </summary>
    [Fact]
    public void Csi_LargeParamValue_ParsesCorrectly()
    {
        Parse("\u001b[99999m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([99999], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle many parameters"
    /// </summary>
    [Fact]
    public void Csi_ManyParams_ParsesUpToLimit()
    {
        // 16 params should all be captured (our limit)
        Parse("\u001b[1;2;3;4;5;6;7;8;9;10;11;12;13;14;15;16m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16], csi.Params);
    }

    /// <summary>
    /// Ported from: libvterm "CSI with leading zeros"
    /// Leading zeros should be ignored in parameter parsing.
    /// </summary>
    [Fact]
    public void Csi_LeadingZeros_ParsesCorrectly()
    {
        Parse("\u001b[007m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([7], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle intermediate bytes"
    /// </summary>
    [Fact]
    public void Csi_IntermediateBytes_ParsesCorrectly()
    {
        Parse("\u001b[1 q");  // DECSCUSR - set cursor style

        var csi = AssertSingleCsi('q');
        Assert.Equal([1], csi.Params);
        Assert.Equal((byte)' ', csi.Intermediates);
    }

    #endregion

    #region Split Sequences

    /// <summary>
    /// Ported from: xterm.js "should handle split sequences"
    /// 
    /// Escape sequences may arrive across multiple Parse() calls.
    /// Parser must maintain state between calls.
    /// </summary>
    [Fact]
    public void Csi_SplitAcrossCalls_ParsesCorrectly()
    {
        Parse("\u001b[");
        Parse("1;2");
        Parse("H");

        var csi = AssertSingleCsi('H');
        Assert.Equal([1, 2], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js "should handle byte-by-byte input"
    /// Extreme case: every byte arrives separately.
    /// </summary>
    [Fact]
    public void Csi_ByteByByte_ParsesCorrectly()
    {
        foreach (byte b in "\u001b[31m"u8)
        {
            Parser.Parse(new[] { b });
        }

        var csi = AssertSingleCsi('m');
        Assert.Equal([31], csi.Params);
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_max_params"
    /// Tests CSI with maximum number of parameters.
    /// </summary>
    [Fact]
    public void Csi_MaxParams_HandlesCorrectly()
    {
        // Our limit is 16, vte's is 32. Test at our limit.
        Parse("\u001b[1;2;3;4;5;6;7;8;9;10;11;12;13;14;15;16m");

        var csi = AssertSingleCsi('m');
        Assert.Equal(16, csi.Params.Length);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_params_ignore_long_params"
    /// Parameters beyond the limit should be ignored, not crash.
    /// </summary>
    [Fact]
    public void Csi_ExceedsMaxParams_IgnoresExtras()
    {
        // 20 params, but we only store 16
        Parse("\u001b[1;2;3;4;5;6;7;8;9;10;11;12;13;14;15;16;17;18;19;20m");

        var csi = AssertSingleCsi('m');
        Assert.Equal(16, csi.Params.Length);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16], csi.Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_long_csi_param"
    /// Very large parameter values should not overflow or crash.
    /// </summary>
    [Fact]
    public void Csi_VeryLargeParam_HandlesWithoutOverflow()
    {
        // Large but valid int value
        Parse("\u001b[2147483647m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([2147483647], csi.Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs "csi_subparameters"
    /// Colon-separated subparameters (used in SGR for underline styles).
    /// </summary>
    [Fact]
    public void Csi_Subparameters_ParsesColonSeparator()
    {
        // SGR with underline style: CSI 4:3 m (curly underline)
        Parse("\u001b[4:3m");

        var csi = AssertSingleCsi('m');
        // Our implementation treats : like ; for now
        Assert.Equal(2, csi.Params.Length);
    }

    /// <summary>
    /// Ported from: vte lib.rs "csi_reset"
    /// Parser state should reset properly between sequences.
    /// </summary>
    [Fact]
    public void Csi_Reset_ClearsState()
    {
        Parse("\u001b[1;2;3m");
        Handler.Clear();

        Parse("\u001b[4m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([4], csi.Params);
    }

    #endregion

    #region Windows Terminal Ported Tests

    /// <summary>
    /// Ported from: Windows Terminal StateMachineTest.cpp "TwoStateMachinesDoNotInterfereWithEachOther"
    /// Two independent parser instances should not affect each other.
    /// </summary>
    [Fact]
    public void Csi_TwoIndependentParsers_DoNotInterfere()
    {
        var handler1 = new RecordingHandler();
        var handler2 = new RecordingHandler();
        var parser1 = new VtParser(handler1);
        var parser2 = new VtParser(handler2);

        parser1.Parse("\u001b[1m"u8);
        parser2.Parse("\u001b[2m"u8);

        var csi1 = Assert.Single(handler1.Events.OfType<CsiEvent>());
        var csi2 = Assert.Single(handler2.Events.OfType<CsiEvent>());

        Assert.Equal([1], csi1.Params);
        Assert.Equal([2], csi2.Params);
    }

    /// <summary>
    /// Ported from: Windows Terminal OutputEngineTest.cpp "TestCsiMaxParamCount"
    /// Windows Terminal limits to 32 parameters.
    /// </summary>
    [Fact]
    public void Csi_WindowsTerminalMaxParams_Respected()
    {
        // Build a CSI with 32 params
        var seq = "\u001b[" + string.Join(";", Enumerable.Range(1, 32)) + "m";
        Parse(seq);

        var csi = AssertSingleCsi('m');
        // We capture up to our limit (16)
        Assert.True(csi.Params.Length <= 16);
    }

    #endregion

    #region xterm.js Params.test.ts Ported Tests

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "param defaults to 0 (ZDM)"
    /// Zero Default Mode - empty params become 0.
    /// </summary>
    [Fact]
    public void Params_EmptyDefaultsToZero()
    {
        Parse("\u001b[m");  // No params at all

        var csi = AssertSingleCsi('m');
        // With no params, the array should be empty (no implicit 0)
        // OR some implementations add a single 0
        Assert.True(csi.Params.Length == 0 || (csi.Params.Length == 1 && csi.Params[0] == 0));
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "clamp parsed params"
    /// Integer overflow should clamp to max value, not wrap.
    /// </summary>
    [Fact]
    public void Params_OverflowClamps()
    {
        // 2147483648 is int.MaxValue + 1
        // Should clamp to int.MaxValue or some reasonable max
        Parse("\u001b[2147483648m");

        var csi = AssertSingleCsi('m');
        // Should not be negative (overflow wrap)
        Assert.True(csi.Params[0] >= 0);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "should correctly reset on new sequence"
    /// Parser state should reset between sequences.
    /// </summary>
    [Fact]
    public void Params_ResetBetweenSequences()
    {
        Parse("\u001b[1;2;3m");
        Parse("\u001b[4m");

        var csiEvents = Handler.Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csiEvents.Count);
        Assert.Equal([1, 2, 3], csiEvents[0].Params);
        Assert.Equal([4], csiEvents[1].Params);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "typical sequences - SGR color codes"
    /// Real-world SGR sequences with various param styles.
    /// </summary>
    [Theory]
    [InlineData("\u001b[38;5;196m", new[] { 38, 5, 196 })]        // 256-color foreground
    [InlineData("\u001b[48;5;21m", new[] { 48, 5, 21 })]          // 256-color background
    [InlineData("\u001b[38;2;255;128;0m", new[] { 38, 2, 255, 128, 0 })]  // RGB foreground
    [InlineData("\u001b[0;1;4;31m", new[] { 0, 1, 4, 31 })]       // Reset, bold, underline, red
    public void Params_TypicalSgrSequences(string input, int[] expectedParams)
    {
        Parse(input);

        var csi = AssertSingleCsi('m');
        Assert.Equal(expectedParams, csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "should carry forward isSub state"
    /// Colon-separated subparams across chunked input.
    /// </summary>
    [Fact]
    public void Params_SubparamsAcrossChunks()
    {
        // SGR underline with style: CSI 4:3 m (curly underline)
        // Split across chunks
        Parse("\u001b[4:");
        Parse("3m");

        var csi = AssertSingleCsi('m');
        // We treat : like ; so we get [4, 3]
        Assert.Equal([4, 3], csi.Params);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "should handle length restrictions correctly"
    /// Excess parameters should be truncated gracefully.
    /// </summary>
    [Fact]
    public void Params_ExcessTruncated()
    {
        // Way more params than our limit
        var manyParams = string.Join(";", Enumerable.Range(1, 100));
        Parse($"\u001b[{manyParams}m");

        var csi = AssertSingleCsi('m');
        Assert.True(csi.Params.Length <= 16);
        // First params should be preserved
        Assert.Equal(1, csi.Params[0]);
    }

    /// <summary>
    /// Ported from: xterm.js Params.test.ts "should cancel subdigits if beyond params limit"
    /// Parser should stop accumulating when limit reached.
    /// </summary>
    [Fact]
    public void Params_StopsAccumulatingAtLimit()
    {
        // 20 params, but we only store 16
        var seq = "\u001b[" + string.Join(";", Enumerable.Range(1, 20)) + "m";
        Parse(seq);

        var csi = AssertSingleCsi('m');
        // Should have exactly our limit
        Assert.Equal(16, csi.Params.Length);
        // Last stored param should be 16, not 20
        Assert.Equal(16, csi.Params[15]);
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_params_trailing_semicolon"
    /// Trailing semicolon should result in an implicit 0 param.
    /// </summary>
    [Fact]
    public void Params_TrailingSemicolon_ImplicitZero()
    {
        Parse("\u001b[4;m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([4, 0], csi.Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_params_leading_semicolon"
    /// Leading semicolon should result in an implicit 0 as first param.
    /// </summary>
    [Fact]
    public void Params_LeadingSemicolon_ImplicitZero()
    {
        Parse("\u001b[;4m");

        var csi = AssertSingleCsi('m');
        Assert.Equal([0, 4], csi.Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_long_csi_param"
    /// Very long param value should be clamped, not overflow.
    /// </summary>
    [Fact]
    public void Params_VeryLongNumber_Clamped()
    {
        // 9223372036854775808 is i64::MAX + 1
        Parse("\u001b[9223372036854775808m");

        var csi = AssertSingleCsi('m');
        // Should clamp to int.MaxValue or similar, not wrap to negative
        Assert.True(csi.Params[0] >= 0);
        Assert.Equal(int.MaxValue, csi.Params[0]);
    }

    /// <summary>
    /// Ported from: vte lib.rs "csi_reset"
    /// ESC interrupting CSI should reset CSI state for next sequence.
    /// </summary>
    [Fact]
    public void Csi_EscInterrupts_StateReset()
    {
        // First CSI is interrupted, second should parse fresh
        Parse("\u001b[3;1\u001b[?1049h");

        // Only the second CSI should dispatch
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('h', csi.Command);
        Assert.Equal([1049], csi.Params);
        Assert.True(csi.Private);
    }

    /// <summary>
    /// Ported from: vte lib.rs "parse_csi_max_params"
    /// CSI with more than max params should be handled gracefully.
    /// </summary>
    [Fact]
    public void Csi_MaxParams_Truncated()
    {
        // Build CSI with many params (more than 16)
        var manyParams = string.Join(";", Enumerable.Repeat("1", 32));
        Parse($"\u001b[{manyParams}m");

        var csi = AssertSingleCsi('m');
        // Should truncate to max (16)
        Assert.Equal(16, csi.Params.Length);
        Assert.All(csi.Params, p => Assert.Equal(1, p));
    }

    /// <summary>
    /// Ported from: vte lib.rs - multiple CSI in sequence
    /// Parser should correctly handle multiple CSI sequences.
    /// </summary>
    [Fact]
    public void Csi_Multiple_AllDispatch()
    {
        Parse("\u001b[1m\u001b[2m\u001b[3m");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(3, csis.Count);
        Assert.Equal([1], csis[0].Params);
        Assert.Equal([2], csis[1].Params);
        Assert.Equal([3], csis[2].Params);
    }

    /// <summary>
    /// Ported from: vte lib.rs - CSI with intermediate byte
    /// CSI with space as intermediate (e.g., CSI Ps SP q for cursor style).
    /// </summary>
    [Fact]
    public void Csi_WithIntermediate_Parsed()
    {
        // CSI 2 SP q - Set cursor style to steady block
        Parse("\u001b[2 q");

        var csi = AssertSingleCsi('q');
        Assert.Equal([2], csi.Params);
        Assert.Equal((byte)' ', csi.Intermediates);
    }

    #endregion
}
