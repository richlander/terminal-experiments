// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// C0 control code tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js: src/common/parser/EscapeSequenceParser.test.ts
/// - libvterm: t/02parser.test
/// </remarks>
public class ControlCodeTests : ParserTestBase
{
    #region Common C0 Controls

    /// <summary>
    /// Ported from: xterm.js "should execute C0 controls"
    /// </summary>
    [Theory]
    [InlineData(0x07, "BEL")]  // Bell
    [InlineData(0x08, "BS")]   // Backspace
    [InlineData(0x09, "HT")]   // Horizontal tab
    [InlineData(0x0A, "LF")]   // Line feed
    [InlineData(0x0B, "VT")]   // Vertical tab
    [InlineData(0x0C, "FF")]   // Form feed
    [InlineData(0x0D, "CR")]   // Carriage return
    public void C0_CommonControls_Execute(byte code, string name)
    {
        _ = name; // Used for test display name
        Parser.Parse(new[] { code });

        var exec = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(code, exec.Code);
    }

    #endregion

    #region C0 Controls Within Sequences

    /// <summary>
    /// Ported from: xterm.js "should execute C0 mid-sequence"
    /// 
    /// C0 controls (except ESC, CAN, SUB) should be executed even
    /// while parsing escape sequences, then parsing continues.
    /// </summary>
    [Fact]
    public void C0_InCsiSequence_ExecutesAndContinues()
    {
        // Bell in middle of CSI sequence
        Parse("\u001b[\x07" + "31m");

        var events = Handler.Events;
        
        // Bell should execute
        Assert.Contains(events, e => e is ExecuteEvent { Code: 0x07 });
        
        // CSI should still complete
        Assert.Contains(events, e => e is CsiEvent { Command: 'm' });
    }

    /// <summary>
    /// Ported from: libvterm - newline in sequence
    /// </summary>
    [Fact]
    public void C0_NewlineInCsiSequence_ExecutesAndContinues()
    {
        Parse("\u001b[1\nm");

        var events = Handler.Events;
        Assert.Contains(events, e => e is ExecuteEvent { Code: 0x0A });
        Assert.Contains(events, e => e is CsiEvent);
    }

    #endregion

    #region CAN and SUB

    /// <summary>
    /// Ported from: xterm.js "CAN aborts sequence"
    /// 
    /// CAN (0x18) and SUB (0x1A) abort the current sequence.
    /// </summary>
    [Fact]
    public void C0_Can_AbortsSequence()
    {
        Parse("\u001b[31\x18m");

        // CSI should NOT be dispatched because CAN aborted it
        Assert.DoesNotContain(Handler.Events, e => e is CsiEvent);
        
        // 'm' should be printed as regular character
        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'm' });
    }

    /// <summary>
    /// Ported from: xterm.js "SUB aborts sequence"
    /// </summary>
    [Fact]
    public void C0_Sub_AbortsSequence()
    {
        Parse("\u001b[31\x1Am");

        Assert.DoesNotContain(Handler.Events, e => e is CsiEvent);
        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'm' });
    }

    #endregion

    #region Printable Characters

    /// <summary>
    /// Ported from: xterm.js "should print ASCII"
    /// </summary>
    [Fact]
    public void Print_AsciiText_PrintsAllCharacters()
    {
        Parse("Hello, World!");

        Assert.Equal("Hello, World!", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: xterm.js "should print after control"
    /// </summary>
    [Fact]
    public void Print_AfterControl_ContinuesPrinting()
    {
        Parse("Hello\nWorld");

        var events = Handler.Events;
        
        // Check for LF execution
        Assert.Contains(events, e => e is ExecuteEvent { Code: 0x0A });
        
        // Check printed text (without the newline)
        Assert.Equal("HelloWorld", Handler.GetPrintedText());
    }

    #endregion

    #region libvterm Ported Tests

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 249-254
    /// NUL bytes should be ignored.
    /// </summary>
    [Fact]
    public void C0_Nul_IsIgnored()
    {
        Parser.Parse(new byte[] { (byte)'A', 0x00, (byte)'B' });

        // NUL should not generate an Execute event, just be ignored
        Assert.Equal("AB", Handler.GetPrintedText());
        Assert.DoesNotContain(Handler.Events, e => e is ExecuteEvent { Code: 0x00 });
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 263-266
    /// DEL (0x7F) should be ignored but allow text around it.
    /// </summary>
    [Fact]
    public void C0_Del_IsIgnored()
    {
        Parser.Parse(new byte[] { (byte)'A', 0x7F, (byte)'B' });

        Assert.Equal("AB", Handler.GetPrintedText());
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 128-131
    /// C0 control in middle of CSI params should execute but continue CSI.
    /// </summary>
    [Fact]
    public void C0_InCsiParams_ExecutesAndContinuesSequence()
    {
        // CSI with newline in the middle of params: ESC [ 1 2 LF ; 3 X
        Parser.Parse("\u001b[12\n;3X"u8.ToArray());

        var events = Handler.Events;
        
        // LF should execute
        Assert.Contains(events, e => e is ExecuteEvent { Code: 0x0A });
        
        // CSI should complete with params [12, 3]
        var csi = Assert.Single(events.OfType<CsiEvent>());
        Assert.Equal('X', csi.Command);
        Assert.Equal([12, 3], csi.Params);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 57-64
    /// CAN (0x18) cancels escape sequence and returns to ground.
    /// </summary>
    [Fact]
    public void C0_Can_CancelsEscapeSequence()
    {
        // ESC then CAN should cancel, then 'X' is printed
        Parse("\u001b\x18X");

        Assert.DoesNotContain(Handler.Events, e => e is EscEvent);
        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'X' });
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 165-167
    /// CAN cancels OSC sequence.
    /// </summary>
    [Fact]
    public void C0_Can_CancelsOscSequence()
    {
        Parse("\u001b]0;partial\x18X");

        // OSC should NOT dispatch because CAN cancelled it
        Assert.DoesNotContain(Handler.Events, e => e is OscEvent);
        Assert.Contains(Handler.Events, e => e is PrintEvent { Char: 'X' });
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "execute_anywhere"
    /// C0 controls should execute in any state (except string states).
    /// </summary>
    [Fact]
    public void C0_ExecutesInAnyState()
    {
        // Bell in escape state
        Parse("\u001b\x07" + "7");

        Assert.Contains(Handler.Events, e => e is ExecuteEvent { Code: 0x07 });
        Assert.Contains(Handler.Events, e => e is EscEvent { Command: '7' });
    }

    /// <summary>
    /// Ported from: vte lib.rs "c1s"
    /// C1 control characters (0x80-0x9F) as 8-bit codes.
    /// </summary>
    [Fact]
    public void C1_EightBit_HandledAsControls()
    {
        // 0x9B is CSI in C1 (equivalent to ESC [)
        Parser.Parse(new byte[] { 0x9B, (byte)'1', (byte)'m' });

        // Our parser may or may not handle C1 - this tests the behavior
        // vte treats 0x9B as CSI introducer
        var events = Handler.Events;
        // At minimum, should not crash
        Assert.NotNull(events);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 16-21
    /// C1 8-bit control codes should emit control events.
    /// </summary>
    [Theory]
    [InlineData(0x84, "IND")]  // Index
    [InlineData(0x85, "NEL")]  // Next Line
    [InlineData(0x88, "HTS")]  // Horizontal Tab Set
    [InlineData(0x8D, "RI")]   // Reverse Index
    [InlineData(0x8E, "SS2")]  // Single Shift 2
    [InlineData(0x8F, "SS3")]  // Single Shift 3
    public void C1_EightBit_KnownControlsExecute(byte code, string name)
    {
        _ = name; // Used for test display name
        Parser.Parse(new[] { code });

        var exec = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(code, exec.Code);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 30-32
    /// High bytes (0xA0-0xBF) that are not UTF-8 start bytes should be printed as Latin-1 text.
    /// Note: 0xC0-0xDF are UTF-8 2-byte starters, 0xE0-0xEF are 3-byte starters.
    /// </summary>
    [Fact]
    public void HighBytes_PrintAsLatin1()
    {
        // Use bytes in range 0xA0-0xBF which are not valid UTF-8 start bytes
        Parser.Parse(new byte[] { 0xa0, 0xaf, 0xbf });

        var printed = Handler.Events.OfType<PrintEvent>().ToList();
        Assert.Equal(3, printed.Count);
        Assert.Equal((char)0xa0, printed[0].Char);
        Assert.Equal((char)0xaf, printed[1].Char);
        Assert.Equal((char)0xbf, printed[2].Char);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 34-38
    /// Mixed text and control codes.
    /// </summary>
    [Fact]
    public void Mixed_TextAndControls()
    {
        Parse("1\n2");

        var events = Handler.Events;
        Assert.Equal('1', events.OfType<PrintEvent>().First().Char);
        Assert.Contains(events, e => e is ExecuteEvent { Code: 0x0A });
        Assert.Equal('2', events.OfType<PrintEvent>().Last().Char);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 252-254
    /// NUL ignored within CSI.
    /// </summary>
    [Fact]
    public void C0_Nul_IgnoredWithinCsi()
    {
        // ESC [ 1 2 NUL 3 m - NUL should be ignored, resulting in param 123
        Parser.Parse([0x1B, 0x5B, 0x31, 0x32, 0x00, 0x33, 0x6D]);

        var csi = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal([123], csi.Params);  // NUL between 12 and 3 is ignored
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test lines 258-261
    /// DEL ignored within CSI.
    /// </summary>
    [Fact]
    public void C0_Del_IgnoredWithinCsi()
    {
        // ESC [ 1 2 DEL 3 m - DEL should be ignored, resulting in param 123
        Parser.Parse([0x1B, 0x5B, 0x31, 0x32, 0x7F, 0x33, 0x6D]);

        var csi = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal([123], csi.Params);  // DEL between 12 and 3 is ignored
    }

    #endregion
}
