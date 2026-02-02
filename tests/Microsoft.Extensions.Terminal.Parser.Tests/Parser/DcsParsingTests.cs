// Ported from xterm.js DcsParser.test.ts and libvterm 02parser.test
// Tests DCS (Device Control String) parsing behavior

using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Parser;

public class DcsParsingTests : ParserTestBase
{
    // ===== Basic DCS Parsing (from libvterm) =====

    [Fact]
    public void Dcs_StTerminator()
    {
        // DCS with final 'q' and data "Hello" - using ST terminator
        Parse("\u001bPqHello\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hook.Final);
        
        var put = Assert.Single(Events.OfType<DcsPutEvent>());
        Assert.Equal("Hello", put.Data);
        
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    [Fact]
    public void Dcs_BelTerminator_NotSupported()
    {
        // Note: Unlike OSC, strict VT standard says DCS is only terminated by ST
        // Our parser follows this strictly. Some terminals (xterm) accept BEL.
        // This test documents our current behavior.
        Parse("\u001bPqHello\x07");
        
        // BEL doesn't terminate DCS, so no unhook
        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Empty(Events.OfType<DcsUnhookEvent>());
    }

    [Fact]
    public void Dcs_WithParams()
    {
        // DCS with parameters - "\eP1;2;3qdata\e\\"
        Parse("\u001bP1;2;3qdata\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hook.Final);
        Assert.Equal(new[] { 1, 2, 3 }, hook.Params);
    }

    [Fact]
    public void Dcs_WithIntermediate()
    {
        // DCS with intermediate - "\eP+pdata\e\\"
        Parse("\u001bP+pdata\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('p', hook.Final);
        Assert.Equal("+", hook.Intermediates);
    }

    [Fact]
    public void Dcs_EscapeCancels_StartsEscape()
    {
        // Escape cancels DCS, starts Escape - "\ePSomething\e9"
        Parse("\u001bPSomething\u001b9");
        
        // DCS is cancelled when ESC 9 comes (not ESC \)
        var esc = Events.OfType<EscEvent>().FirstOrDefault();
        Assert.NotNull(esc);
        Assert.Equal('9', esc.Command);
    }

    [Fact]
    public void Dcs_CanCancels()
    {
        // CAN cancels DCS, returns to normal mode
        Parse("\u001bP12" + "\x18" + "AB");
        
        // After CAN (0x18), should print A and B
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Contains(prints, p => p.Char == 'A');
        Assert.Contains(prints, p => p.Char == 'B');
    }

    // ===== DCS Handler Tests (from xterm.js) =====

    [Fact]
    public void Dcs_EmptyPayload()
    {
        // DCS with no data between introducer and terminator
        Parse("\u001bP+p\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('p', hook.Final);
        Assert.Equal("+", hook.Intermediates);
        
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    [Fact]
    public void Dcs_LongPayload()
    {
        // DCS with substantial payload
        var payload = new string('A', 1000);
        Parse($"\u001bPq{payload}\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hook.Final);
        
        // All A's should be collected in put events
        var puts = Events.OfType<DcsPutEvent>().ToList();
        var totalData = string.Concat(puts.Select(p => p.Data));
        Assert.Equal(payload, totalData);
    }

    [Fact]
    public void Dcs_MultipleParams()
    {
        // XTGETTCAP-style with params
        Parse("\u001bP0;1;2+q\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hook.Final);
        Assert.Equal(new[] { 0, 1, 2 }, hook.Params);
        Assert.Equal("+", hook.Intermediates);
    }

    [Fact]
    public void Dcs_Sixel_Style()
    {
        // Sixel graphics start sequence style
        Parse("\u001bP0;0;0q\"1;1;100;100\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hook.Final);
        Assert.Equal(new[] { 0, 0, 0 }, hook.Params);
        
        var put = Assert.Single(Events.OfType<DcsPutEvent>());
        Assert.Equal("\"1;1;100;100", put.Data);
    }

    // ===== State Transitions (from Windows Terminal) =====

    [Theory]
    [InlineData('p')] // DECRQSS
    [InlineData('q')] // Sixel / XTGETTCAP
    [InlineData('{')] // DECDLD
    [InlineData('|')] // DECUDK
    [InlineData('r')] // DECRQSS reply
    public void Dcs_VariousFinalBytes(char final)
    {
        Parse($"\u001bP1{final}data\u001b\\");
        
        var hook = Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Equal(final, hook.Final);
    }

    [Fact]
    public void Dcs_SubCancels()
    {
        // SUB (0x1A) also cancels DCS
        Parse("\u001bPdata" + "\x1a" + "AB");
        
        // After SUB, should print A and B
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Contains(prints, p => p.Char == 'A');
        Assert.Contains(prints, p => p.Char == 'B');
    }
}
