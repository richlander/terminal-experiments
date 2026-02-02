// Ported from Windows Terminal OutputEngineTest.cpp, StateMachineTest.cpp, and libvterm 02parser.test
// Tests state machine transitions and edge cases

using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Parser;

public class StateMachineTests : ParserTestBase
{
    // ===== Escape State Transitions (from Windows Terminal) =====

    [Fact]
    public void EscapeFromGround()
    {
        // ESC from Ground goes to Escape, then final returns to Ground
        Parse("\u001b7");
        
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('7', esc.Command);
    }

    [Fact]
    public void EscapeIntermediate_MultipleIntermediates()
    {
        // ESC # 6 - DECDHL (double height line)
        Parse("\u001b#6");
        
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('6', esc.Command);
    }

    [Fact]
    public void EscapeThenC0_DoesNotInterrupt()
    {
        // C0 in Escape state executes without interrupting
        Parse("\u001b\x03[31m");
        
        // ETX (0x03) should execute
        var exec = Events.OfType<ExecuteEvent>().First();
        Assert.Equal(0x03, exec.Code);
        
        // CSI 31 m should still complete
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 31 }, csi.Params);
    }

    // ===== CSI State Transitions (from Windows Terminal) =====

    [Fact]
    public void CsiEntry_ImmediateFinal()
    {
        // CSI m with no params
        Parse("\u001b[m");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void CsiParam_MultipleParamsWithSemicolons()
    {
        // CSI ;324;;8J - params with empty values
        Parse("\u001b[;324;;8J");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('J', csi.Command);
        // Empty params become 0 in ZDM
        Assert.Equal(4, csi.Params.Length);
        Assert.Equal(0, csi.Params[0]);
        Assert.Equal(324, csi.Params[1]);
        Assert.Equal(0, csi.Params[2]);
        Assert.Equal(8, csi.Params[3]);
    }

    [Fact]
    public void CsiMaxParamCount_Truncates()
    {
        // 100 params - our parser truncates to 16 (different from Windows Terminal's 32)
        var paramStr = string.Join(";", Enumerable.Range(0, 100).Select(i => (i % 10).ToString()));
        Parse($"\u001b[{paramStr}J");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('J', csi.Command);
        Assert.Equal(16, csi.Params.Length);
    }

    [Fact]
    public void CsiLeadingZeros_StillParsed()
    {
        // 50 leading zeros followed by 12345
        var zeros = new string('0', 50);
        Parse($"\u001b[{zeros}12345J");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal(12345, csi.Params[0]);
    }

    // ===== OSC State Transitions (from Windows Terminal) =====

    [Fact]
    public void OscString_Simple()
    {
        Parse("\u001b]0;some text\x07");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(0, osc.Command);
        Assert.Equal("some text", osc.DataString);
    }

    [Fact]
    public void OscString_StTerminator()
    {
        Parse("\u001b]2;Title\u001b\\");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(2, osc.Command);
        Assert.Equal("Title", osc.DataString);
    }

    [Fact]
    public void OscParam_NoSemicolon()
    {
        // OSC without semicolon - just command number
        Parse("\u001b]1234\x07");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(1234, osc.Command);
        Assert.Equal("", osc.DataString);
    }

    // ===== Control Character Handling (from libvterm) =====

    [Fact]
    public void NulIgnored()
    {
        Parse("\x00");
        Assert.Empty(Events);
    }

    [Fact]
    public void NulIgnoredWithinCsi()
    {
        // NUL within CSI param - should be ignored
        // Note: Must separate to avoid C# parsing \x003 as single char
        Parse("\u001b[12" + "\x00" + "3m");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal(123, csi.Params[0]);
    }

    [Fact]
    public void DelIgnored()
    {
        Parse("\x7f");
        Assert.Empty(Events);
    }

    [Fact]
    public void DelIgnoredWithinCsi()
    {
        // DEL within CSI param - per VT standards should be ignored
        // Parser correctly ignores DEL within CSI states per libvterm behavior
        Parse("\u001b[12" + "\x7f" + "3m");
        
        // CSI should dispatch with param 123 (12 + 3 with DEL ignored)
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        Assert.Equal([123], csi.Params);
    }

    [Fact]
    public void DelInsideText()
    {
        // DEL inside text - text before and after should print
        Parse("AB" + "\x7f" + "C");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(3, prints.Count);
        Assert.Equal('A', prints[0].Char);
        Assert.Equal('B', prints[1].Char);
        Assert.Equal('C', prints[2].Char);
    }

    // ===== Escape Sequences (from libvterm) =====

    [Fact]
    public void Escape_SingleByte()
    {
        // ESC = (keypad application mode)
        Parse("\u001b=");
        
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('=', esc.Command);
        Assert.Equal(0, esc.Intermediates);
    }

    [Fact]
    public void Escape_TwoByte()
    {
        // ESC ( X (designate character set)
        Parse("\u001b(X");
        
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('X', esc.Command);
        Assert.Equal((byte)'(', esc.Intermediates);
    }

    [Fact]
    public void Escape_SplitWrite()
    {
        // Split ESC ( Y across two chunks
        Parse("\u001b(");
        Assert.Empty(Events); // Not complete yet
        
        Parse("Y");
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('Y', esc.Command);
        Assert.Equal((byte)'(', esc.Intermediates);
    }

    [Fact]
    public void Escape_EscapeCancelsEscape()
    {
        // ESC ( ESC ) Z - first ESC sequence is cancelled
        Parse("\u001b(\u001b)Z");
        
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal('Z', esc.Command);
        Assert.Equal((byte)')', esc.Intermediates);
    }

    [Fact]
    public void Escape_CanCancels()
    {
        // CAN cancels escape, returns to normal
        // Note: Must use \x18 followed by space or use separate strings to avoid
        // C# hex parsing \x18AB as single Unicode char U+18AB
        Parse("\u001b(" + "\x18" + "AB");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(2, prints.Count);
        Assert.Equal('A', prints[0].Char);
        Assert.Equal('B', prints[1].Char);
    }

    [Fact]
    public void Escape_C0Interrupts()
    {
        // C0 in escape interrupts but continues
        Parse("\u001b(\nX");
        
        // Should have execute for LF
        var exec = Events.OfType<ExecuteEvent>().First();
        Assert.Equal(0x0a, exec.Code);
        
        // And escape for (X
        var esc = Events.OfType<EscEvent>().First();
        Assert.Equal('X', esc.Command);
    }

    // ===== CSI Parsing Details (from libvterm) =====

    [Fact]
    public void Csi_0Args()
    {
        Parse("\u001b[a");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('a', csi.Command);
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void Csi_1Arg()
    {
        Parse("\u001b[9b");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('b', csi.Command);
        Assert.Equal(new[] { 9 }, csi.Params);
    }

    [Fact]
    public void Csi_2Args()
    {
        Parse("\u001b[3;4c");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('c', csi.Command);
        Assert.Equal(new[] { 3, 4 }, csi.Params);
    }

    [Fact]
    public void Csi_ManyDigits()
    {
        Parse("\u001b[678d");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('d', csi.Command);
        Assert.Equal(new[] { 678 }, csi.Params);
    }

    [Fact]
    public void Csi_LeadingZero()
    {
        Parse("\u001b[007e");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('e', csi.Command);
        Assert.Equal(new[] { 7 }, csi.Params);
    }

    [Fact]
    public void Csi_Qmark_Private()
    {
        Parse("\u001b[?2;7f");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('f', csi.Command);
        Assert.Equal(new[] { 2, 7 }, csi.Params);
        // Private marker is stored in intermediates
    }

    [Fact]
    public void Csi_Greater_Private()
    {
        Parse("\u001b[>c");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('c', csi.Command);
    }

    [Fact]
    public void Csi_Space_Intermediate()
    {
        Parse("\u001b[12 q");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('q', csi.Command);
        Assert.Equal((byte)' ', csi.Intermediates);
        Assert.Equal(new[] { 12 }, csi.Params);
    }

    [Fact]
    public void Csi_Mixed_WithText()
    {
        // Mixed text and CSI
        Parse("A\u001b[8mB");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(2, prints.Count);
        Assert.Equal('A', prints[0].Char);
        Assert.Equal('B', prints[1].Char);
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 8 }, csi.Params);
    }

    [Fact]
    public void Csi_SplitWrite()
    {
        Parse("\u001b");
        Assert.Empty(Events);
        
        Parse("[a");
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('a', csi.Command);
    }

    [Fact]
    public void Csi_SplitWriteWithParam()
    {
        Parse("foo\u001b[");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(3, prints.Count);
        
        Parse("4b");
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('b', csi.Command);
        Assert.Equal(new[] { 4 }, csi.Params);
    }

    [Fact]
    public void Csi_SplitWriteMultiParam()
    {
        Parse("\u001b[12;");
        Assert.Empty(Events.OfType<CsiEvent>());
        
        Parse("3c");
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('c', csi.Command);
        Assert.Equal(new[] { 12, 3 }, csi.Params);
    }

    [Fact]
    public void Csi_EscapeCancels()
    {
        // ESC during CSI cancels it and starts new escape
        Parse("\u001b[123\u001b9");
        
        var esc = Events.OfType<EscEvent>().First();
        Assert.Equal('9', esc.Command);
    }

    [Fact]
    public void Csi_CanCancels()
    {
        // CAN cancels CSI
        Parse("\u001b[12" + "\x18" + "AB");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Contains(prints, p => p.Char == 'A');
        Assert.Contains(prints, p => p.Char == 'B');
    }

    [Fact]
    public void Csi_C0Interrupts()
    {
        // C0 in CSI interrupts but continues
        Parse("\u001b[12\n;3X");
        
        var exec = Events.OfType<ExecuteEvent>().First();
        Assert.Equal(0x0a, exec.Code);
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('X', csi.Command);
        Assert.Equal(new[] { 12, 3 }, csi.Params);
    }

    // ===== OSC Parsing Details (from libvterm) =====

    [Fact]
    public void Osc_Bel()
    {
        Parse("\u001b]1;Hello\x07");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(1, osc.Command);
        Assert.Equal("Hello", osc.DataString);
    }

    [Fact]
    public void Osc_St7bit()
    {
        Parse("\u001b]1;Hello\u001b\\");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(1, osc.Command);
        Assert.Equal("Hello", osc.DataString);
    }

    [Fact]
    public void Osc_BelWithoutSemicolon()
    {
        Parse("\u001b]1234\x07");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(1234, osc.Command);
        Assert.Equal("", osc.DataString);
    }

    [Fact]
    public void Osc_StWithoutSemicolon()
    {
        Parse("\u001b]1234\u001b\\");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(1234, osc.Command);
        Assert.Equal("", osc.DataString);
    }

    [Fact]
    public void Osc_EscapeCancels()
    {
        // ESC during OSC that's not ST cancels and starts escape
        Parse("\u001b]Something\u001b9");
        
        var esc = Events.OfType<EscEvent>().First();
        Assert.Equal('9', esc.Command);
    }

    [Fact]
    public void Osc_CanCancels()
    {
        Parse("\u001b]12" + "\x18" + "AB");
        
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Contains(prints, p => p.Char == 'A');
        Assert.Contains(prints, p => p.Char == 'B');
    }

    // ===== Alternate Screen Buffer (DECSET/DECRST 1049) =====
    // Note: These test that the parser correctly parses the sequences.
    // What happens with them is up to the handler implementation.

    [Fact]
    public void DecSet_1049_AlternateScreenEnable()
    {
        // CSI ? 1049 h - Enable alternate screen buffer
        Parse("\u001b[?1049h");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('h', csi.Command);
        Assert.Equal(new[] { 1049 }, csi.Params);
        // ? is stored somewhere - depends on parser implementation
    }

    [Fact]
    public void DecSet_1049_AlternateScreenDisable()
    {
        // CSI ? 1049 l - Disable alternate screen buffer
        Parse("\u001b[?1049l");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('l', csi.Command);
        Assert.Equal(new[] { 1049 }, csi.Params);
    }

    [Fact]
    public void DecSet_MultipleParams()
    {
        // CSI ? 1049 ; 2004 h - Enable multiple modes
        Parse("\u001b[?1049;2004h");
        
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('h', csi.Command);
        Assert.Equal(new[] { 1049, 2004 }, csi.Params);
    }
}
