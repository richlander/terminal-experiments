// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ESC sequence parsing tests (non-CSI escapes).
/// </summary>
/// <remarks>
/// Ported from:
/// - xterm.js: src/common/parser/EscapeSequenceParser.test.ts
/// - libvterm: t/02parser.test
/// </remarks>
public class EscParsingTests : ParserTestBase
{
    #region Basic ESC Sequences

    /// <summary>
    /// Ported from: xterm.js "should parse ESC with final byte"
    /// </summary>
    [Fact]
    public void Esc_SaveCursor_ParsesCorrectly()
    {
        Parse("\u001b7"); // DECSC - Save cursor

        var esc = AssertSingleEsc('7');
        Assert.Equal(0, esc.Intermediates);
    }

    /// <summary>
    /// Ported from: xterm.js "should parse ESC with final byte"
    /// </summary>
    [Fact]
    public void Esc_RestoreCursor_ParsesCorrectly()
    {
        Parse("\u001b8"); // DECRC - Restore cursor

        var esc = AssertSingleEsc('8');
        Assert.Equal(0, esc.Intermediates);
    }

    /// <summary>
    /// Ported from: libvterm t/02parser.test
    /// </summary>
    [Theory]
    [InlineData("\u001bD", 'D')]   // IND - Index (move down)
    [InlineData("\u001bE", 'E')]   // NEL - Next line
    [InlineData("\u001bH", 'H')]   // HTS - Horizontal tab set
    [InlineData("\u001bM", 'M')]   // RI - Reverse index (move up)
    [InlineData("\u001bc", 'c')]   // RIS - Full reset
    public void Esc_CommonSequences_ParseCorrectly(string input, char expectedCommand)
    {
        Parse(input);

        var esc = AssertSingleEsc(expectedCommand);
    }

    #endregion

    #region ESC with Intermediates

    /// <summary>
    /// Ported from: xterm.js "should parse ESC with intermediate"
    /// 
    /// Character set designation uses intermediates.
    /// </summary>
    [Fact]
    public void Esc_CharacterSet_ParsesIntermediate()
    {
        Parse("\u001b(0"); // Designate G0 as DEC Special Graphics

        var esc = AssertSingleEsc('0');
        Assert.Equal((byte)'(', esc.Intermediates);
    }

    /// <summary>
    /// Ported from: libvterm - charset designations
    /// </summary>
    [Theory]
    [InlineData("\u001b(B", '(', 'B')]  // G0 = US ASCII
    [InlineData("\u001b)0", ')', '0')]  // G1 = DEC Special
    [InlineData("\u001b*A", '*', 'A')]  // G2 = UK
    [InlineData("\u001b+B", '+', 'B')]  // G3 = US ASCII
    public void Esc_CharacterSetDesignation_ParsesCorrectly(string input, char intermediate, char command)
    {
        Parse(input);

        var esc = AssertSingleEsc(command);
        Assert.Equal((byte)intermediate, esc.Intermediates);
    }

    #endregion

    #region Application Keypad Mode

    /// <summary>
    /// Ported from: xterm.js
    /// </summary>
    [Fact]
    public void Esc_ApplicationKeypadMode_ParsesCorrectly()
    {
        Parse("\u001b="); // DECKPAM - Application keypad mode

        var esc = AssertSingleEsc('=');
    }

    /// <summary>
    /// Ported from: xterm.js
    /// </summary>
    [Fact]
    public void Esc_NumericKeypadMode_ParsesCorrectly()
    {
        Parse("\u001b>"); // DECKPNM - Numeric keypad mode

        var esc = AssertSingleEsc('>');
    }

    #endregion

    #region Split Sequences

    /// <summary>
    /// Ported from: xterm.js "should handle split ESC sequences"
    /// </summary>
    [Fact]
    public void Esc_SplitAcrossCalls_ParsesCorrectly()
    {
        Parse("\u001b");
        Parse("7");

        var esc = AssertSingleEsc('7');
    }

    #endregion

    #region vte (Rust) Ported Tests

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset"
    /// CSI interrupted by ESC should reset state, ESC sequence should parse.
    /// </summary>
    [Fact]
    public void Esc_InterruptsCsi_StateReset()
    {
        // CSI 3;1 then ESC ( A
        Parse("\u001b[3;1\u001b(A");

        // Only ESC should dispatch (CSI was abandoned)
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('A', esc.Command);
        Assert.Equal((byte)'(', esc.Intermediates);
        
        // No CSI should have dispatched
        Assert.Empty(Events.OfType<CsiEvent>());
    }

    /// <summary>
    /// Ported from: vte lib.rs "esc_reset_intermediates"
    /// Intermediates should be reset between sequences.
    /// </summary>
    [Fact]
    public void Esc_IntermediatesResetBetweenSequences()
    {
        // CSI ? 2004 l then ESC # 8
        Parse("\u001b[?2004l\u001b#8");

        var events = Events.ToList();
        
        // First: CSI ? 2004 l
        var csi = Assert.Single(events.OfType<CsiEvent>());
        Assert.Equal('l', csi.Command);
        Assert.Equal((byte)'?', csi.PrivateMarker);
        Assert.Equal([2004], csi.Params);

        // Second: ESC # 8 (DECALN - Screen alignment test)
        var esc = Assert.Single(events.OfType<EscEvent>());
        Assert.Equal('8', esc.Command);
        Assert.Equal((byte)'#', esc.Intermediates);
    }

    /// <summary>
    /// Ported from: vte lib.rs - Multiple ESC sequences
    /// </summary>
    [Fact]
    public void Esc_Multiple_AllDispatch()
    {
        Parse("\u001b7\u001b8\u001bD");

        var escs = Events.OfType<EscEvent>().ToList();
        Assert.Equal(3, escs.Count);
        Assert.Equal('7', escs[0].Command);  // DECSC
        Assert.Equal('8', escs[1].Command);  // DECRC
        Assert.Equal('D', escs[2].Command);  // IND
    }

    /// <summary>
    /// Ported from: vte lib.rs - ESC with various intermediates
    /// </summary>
    [Theory]
    [InlineData("\u001b#8", '#', '8')]  // DECALN
    [InlineData("\u001b(0", '(', '0')]  // G0 = DEC Special
    [InlineData("\u001b)B", ')', 'B')]  // G1 = US ASCII
    [InlineData("\u001b*A", '*', 'A')]  // G2 = UK
    [InlineData("\u001b+0", '+', '0')]  // G3 = DEC Special
    public void Esc_VariousIntermediates_Parsed(string input, char intermediate, char command)
    {
        Parse(input);

        var esc = AssertSingleEsc(command);
        Assert.Equal((byte)intermediate, esc.Intermediates);
    }

    #endregion
}
