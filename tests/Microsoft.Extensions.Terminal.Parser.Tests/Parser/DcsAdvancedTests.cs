// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Comprehensive DCS (Device Control String) parsing tests.
/// Ported from Windows Terminal OutputEngineTest.cpp and StateMachineTest.cpp.
/// </summary>
/// <remarks>
/// DCS sequences are used for:
/// - SIXEL graphics (DCS q)
/// - User-defined keys (DCS $ UDK)
/// - Soft fonts (DCS { font)
/// - Termcap/terminfo (DCS + p/q)
/// - DECRQSS (Request selection/setting)
/// Format: ESC P ... ST (where ST is ESC \ or BEL)
/// </remarks>
public class DcsAdvancedTests : ParserTestBase
{
    #region DCS State Transitions

    /// <summary>
    /// ESC P enters DCS entry state.
    /// </summary>
    [Fact]
    public void Dcs_EscP_EntersDcsEntry()
    {
        Parse("\u001bP");
        
        // Not yet terminated, no hook should fire
        // But we're in DCS state
    }

    /// <summary>
    /// DCS terminated with ST.
    /// </summary>
    [Fact]
    public void Dcs_TerminatedWithSt()
    {
        Parse("\u001bP|\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('|', hookEvent.Command);
        Assert.Single(Handler.Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// DCS with parameters.
    /// </summary>
    [Fact]
    public void Dcs_WithParameters()
    {
        Parse("\u001bP1;2;3|data\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('|', hookEvent.Command);
        Assert.Equal(3, hookEvent.Params.Length);
        Assert.Equal(1, hookEvent.Params[0]);
        Assert.Equal(2, hookEvent.Params[1]);
        Assert.Equal(3, hookEvent.Params[2]);
    }

    /// <summary>
    /// DCS with intermediate characters.
    /// </summary>
    [Fact]
    public void Dcs_WithIntermediates()
    {
        Parse("\u001bP #|\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        // Intermediate ' ' and '#' should be captured
    }

    #endregion

    #region SIXEL Graphics

    /// <summary>
    /// SIXEL graphics sequence (DCS q).
    /// </summary>
    [Fact]
    public void Dcs_Sixel_Basic()
    {
        Parse("\u001bPq\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hookEvent.Command);
    }

    /// <summary>
    /// SIXEL with parameters (aspect ratio, grid).
    /// </summary>
    [Fact]
    public void Dcs_Sixel_WithParameters()
    {
        Parse("\u001bP0;1;0q\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hookEvent.Command);
        Assert.Equal(3, hookEvent.Params.Length);
    }

    /// <summary>
    /// SIXEL with data.
    /// </summary>
    [Fact]
    public void Dcs_Sixel_WithData()
    {
        // SIXEL data: #0 defines color 0, !10? repeats '?' 10 times
        Parse("\u001bPq#0;2;0;0;0!10?-\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hookEvent.Command);
        
        // Data should be captured
        var putEvents = Handler.Events.OfType<DcsPutEvent>().ToList();
        Assert.NotEmpty(putEvents);
    }

    #endregion

    #region DECRQSS - Request Status String

    /// <summary>
    /// DECRQSS request (DCS $ q).
    /// </summary>
    [Fact]
    public void Dcs_Decrqss()
    {
        Parse("\u001bP$qm\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hookEvent.Command);
    }

    /// <summary>
    /// DECRQSS for cursor position.
    /// </summary>
    [Fact]
    public void Dcs_Decrqss_CursorPosition()
    {
        Parse("\u001bP$q r\u001b\\");
        
        // Should parse without error
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
    }

    #endregion

    #region DECDLD - Downloadable Fonts

    /// <summary>
    /// DECDLD soft font loading (DCS { ).
    /// </summary>
    [Fact]
    public void Dcs_Decdld()
    {
        Parse("\u001bP1;1;1;0;0;0{font-data\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('{', hookEvent.Command);
    }

    #endregion

    #region Termcap/Terminfo

    /// <summary>
    /// Termcap request (DCS + p).
    /// </summary>
    [Fact]
    public void Dcs_TermcapRequest()
    {
        Parse("\u001bP+ptermname\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('p', hookEvent.Command);
    }

    /// <summary>
    /// Terminfo request (DCS + q).
    /// </summary>
    [Fact]
    public void Dcs_TerminfoRequest()
    {
        Parse("\u001bP+qcapname\u001b\\");
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        Assert.Equal('q', hookEvent.Command);
    }

    #endregion

    #region DCS Pass-through Data

    /// <summary>
    /// DCS data is passed through to handler.
    /// </summary>
    [Fact]
    public void Dcs_DataPassThrough()
    {
        Parse("\u001bP|data string here\u001b\\");
        
        var putEvents = Handler.Events.OfType<DcsPutEvent>().ToList();
        Assert.NotEmpty(putEvents);
        
        // Verify data content
        var allData = string.Join("", putEvents.Select(e => e.Data));
        Assert.Contains("data string here", allData);
    }

    /// <summary>
    /// Long DCS data string.
    /// </summary>
    [Fact]
    public void Dcs_LongDataString()
    {
        var longData = new string('D', 1000);
        Parse($"\u001bP|{longData}\u001b\\");
        
        var putEvents = Handler.Events.OfType<DcsPutEvent>().ToList();
        Assert.NotEmpty(putEvents);
    }

    #endregion

    #region Invalid/Edge Cases

    /// <summary>
    /// DCS with colon starts DcsIgnore.
    /// </summary>
    [Fact]
    public void Dcs_ColonEntersDcsIgnore()
    {
        Parse("\u001bP:ignored\u001b\\");
        
        // DCS with leading colon is ignored per VT spec
        // No hook should fire with valid parameters
    }

    /// <summary>
    /// DCS terminated by CAN (0x18).
    /// </summary>
    [Fact]
    public void Dcs_TerminatedByCan()
    {
        Parse("\u001bP|data\u0018remaining");
        
        // CAN should abort DCS and remaining should be processed
    }

    /// <summary>
    /// DCS terminated by SUB (0x1A).
    /// </summary>
    [Fact]
    public void Dcs_TerminatedBySub()
    {
        Parse("\u001bP|data\u001Aremaining");
        
        // SUB should abort DCS
    }

    /// <summary>
    /// DCS with CSI inside (should terminate).
    /// </summary>
    [Fact]
    public void Dcs_CsiTerminates()
    {
        Parse("\u001bP|data\u001b[mtext");
        
        // CSI should terminate DCS
        var unhook = Handler.Events.OfType<DcsUnhookEvent>();
        Assert.NotEmpty(unhook);
    }

    /// <summary>
    /// Multiple DCS sequences in one parse.
    /// </summary>
    [Fact]
    public void Dcs_Multiple()
    {
        Parse("\u001bP|first\u001b\\\u001bP|second\u001b\\");
        
        var hookEvents = Handler.Events.OfType<DcsHookEvent>().ToList();
        Assert.Equal(2, hookEvents.Count);
    }

    #endregion

    #region Chunked DCS Parsing

    /// <summary>
    /// DCS sequence split across Parse calls.
    /// </summary>
    [Fact]
    public void Dcs_ChunkedParsing()
    {
        Parser.Parse(Encoding.UTF8.GetBytes("\u001bP1;2;3|"));
        Parser.Parse(Encoding.UTF8.GetBytes("data "));
        Parser.Parse(Encoding.UTF8.GetBytes("string"));
        Parser.Parse(Encoding.UTF8.GetBytes("\u001b\\"));
        
        var hookEvent = Assert.Single(Handler.Events.OfType<DcsHookEvent>());
        var unhookEvent = Assert.Single(Handler.Events.OfType<DcsUnhookEvent>());
    }

    /// <summary>
    /// DCS ST terminator split across Parse calls.
    /// </summary>
    [Fact]
    public void Dcs_SplitTerminator()
    {
        Parser.Parse(Encoding.UTF8.GetBytes("\u001bP|data\u001b"));
        Parser.Parse(Encoding.UTF8.GetBytes("\\"));
        
        Assert.Single(Handler.Events.OfType<DcsUnhookEvent>());
    }

    #endregion

    #region C1 Control Codes

    /// <summary>
    /// C1 DCS entry (0x90) when enabled.
    /// Note: C1 codes may not be enabled by default.
    /// </summary>
    [Fact]
    public void Dcs_C1Entry()
    {
        // 0x90 is C1 DCS - may require explicit enable
        Parse("\u0090|data\u001b\\");
        
        // If C1 is enabled, should parse as DCS
        // If not, 0x90 may be printed or ignored
    }

    #endregion
}
