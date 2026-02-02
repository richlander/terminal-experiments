// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/26state_query.test
// Focus: Device queries (DA, DSR, CPR) and terminal reports

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for terminal query and report sequences.
/// These sequences are used for capability detection and status reporting.
/// </summary>
public class QueryTests : ParserTestBase
{
    #region DA - Device Attributes (CSI c)

    [Fact]
    public void DeviceAttributes_Primary()
    {
        // CSI c or CSI 0 c - Primary Device Attributes request
        // Source: libvterm t/26state_query.test line 6
        Parse("\u001b[c");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('c', evt.Final);
        Assert.True(evt.Params.Length == 0 || evt.Params[0] == 0);
    }

    [Fact]
    public void DeviceAttributes_Secondary()
    {
        // CSI > c or CSI > 0 c - Secondary Device Attributes request
        Parse("\u001b[>c");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('c', evt.Final);
        Assert.Equal('>', (char)evt.PrivateMarker);
    }

    [Fact]
    public void DeviceAttributes_Tertiary()
    {
        // CSI = c - Tertiary Device Attributes request
        Parse("\u001b[=c");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('c', evt.Final);
        Assert.Equal('=', (char)evt.PrivateMarker);
    }

    #endregion

    #region DSR - Device Status Report (CSI Ps n)

    [Fact]
    public void DeviceStatusReport_OperatingStatus()
    {
        // CSI 5 n - Operating status report
        // Source: libvterm t/26state_query.test line 16
        Parse("\u001b[5n");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('n', evt.Final);
        Assert.Equal(5, evt.Params[0]);
    }

    [Fact]
    public void DeviceStatusReport_CursorPosition()
    {
        // CSI 6 n - Cursor Position Report (CPR) request
        // Source: libvterm t/26state_query.test line 20
        Parse("\u001b[6n");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('n', evt.Final);
        Assert.Equal(6, evt.Params[0]);
    }

    [Fact]
    public void DeviceStatusReport_Extended()
    {
        // CSI ? 6 n - Extended Cursor Position Report (DECXCPR)
        // Source: libvterm t/26state_query.test line 26
        Parse("\u001b[?6n");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('n', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(6, evt.Params[0]);
    }

    #endregion

    #region XTVERSION - XTerm Version Query

    [Fact]
    public void XtermVersion()
    {
        // CSI > q - Request terminal version string
        // Source: libvterm t/26state_query.test line 11
        Parse("\u001b[>q");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('q', evt.Final);
        Assert.Equal('>', (char)evt.PrivateMarker);
    }

    #endregion

    #region DECXCPR Response Parsing

    [Fact]
    public void CursorPositionReport()
    {
        // CSI row ; col R - Cursor Position Report response
        Parse("\u001b[10;20R");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('R', evt.Final);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(10, evt.Params[0]);
        Assert.Equal(20, evt.Params[1]);
    }

    [Fact]
    public void ExtendedCursorPositionReport()
    {
        // CSI ? row ; col R - Extended Cursor Position Report response
        Parse("\u001b[?10;20R");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('R', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal(2, evt.Params.Length);
        Assert.Equal(10, evt.Params[0]);
        Assert.Equal(20, evt.Params[1]);
    }

    #endregion

    #region DECRQSS - Request Selection or Setting (DCS $ q Pt ST)

    [Fact]
    public void DECRQSS_SGR()
    {
        // DCS $ q m ST - Request SGR setting
        // Source: libvterm t/26state_query.test line 37
        Parse("\u001bP$qm\u001b\\");

        var events = Handler.Events.ToList();
        
        var hook = events.OfType<DcsHookEvent>().FirstOrDefault();
        Assert.NotNull(hook);
        Assert.Equal('q', hook.Final);
        Assert.Equal("$", hook.Intermediates);
    }

    [Fact]
    public void DECRQSS_CursorStyle()
    {
        // DCS $ q SP q ST - Request cursor style setting
        // Source: libvterm t/26state_query.test line 31
        Parse("\u001bP$q q\u001b\\");

        var events = Handler.Events.ToList();
        
        var hook = events.OfType<DcsHookEvent>().FirstOrDefault();
        Assert.NotNull(hook);
        Assert.Equal('q', hook.Final);
        Assert.Equal("$", hook.Intermediates);
    }

    #endregion

    #region DECRPM - Report Mode (CSI Ps ; Pm $ y)

    [Fact]
    public void ReportMode()
    {
        // CSI Ps $ p - Request mode value
        Parse("\u001b[?25$p");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('p', evt.Final);
        Assert.True(evt.Private);
        Assert.Equal('$', (char)evt.Intermediates);
    }

    #endregion

    #region XTWINOPS - Window Operations (CSI Ps ; Ps ; Ps t)

    [Fact]
    public void WindowOps_ReportState()
    {
        // CSI 11 t - Report window state
        Parse("\u001b[11t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(11, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportPosition()
    {
        // CSI 13 t - Report window position
        Parse("\u001b[13t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(13, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportSize()
    {
        // CSI 14 t - Report window size in pixels
        Parse("\u001b[14t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(14, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportTextAreaSize()
    {
        // CSI 18 t - Report text area size in characters
        Parse("\u001b[18t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(18, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportScreenSize()
    {
        // CSI 19 t - Report screen size in characters
        Parse("\u001b[19t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(19, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportIconTitle()
    {
        // CSI 20 t - Report icon title
        Parse("\u001b[20t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(20, evt.Params[0]);
    }

    [Fact]
    public void WindowOps_ReportWindowTitle()
    {
        // CSI 21 t - Report window title
        Parse("\u001b[21t");

        var evt = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('t', evt.Final);
        Assert.Equal(21, evt.Params[0]);
    }

    #endregion

    #region DECID - Identify Terminal (ESC Z)

    [Fact]
    public void IdentifyTerminal()
    {
        // ESC Z - Identify terminal (deprecated, returns same as DA)
        Parse("\u001bZ");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('Z', evt.Final);
    }

    #endregion

    #region S8C1T / S7C1T - 8-bit/7-bit Control Mode

    [Fact]
    public void Set8BitControls()
    {
        // ESC SP G - Set 8-bit control mode (S8C1T)
        // Source: libvterm t/26state_query.test line 60
        Parse("\u001b G");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('G', evt.Final);
        Assert.Equal(' ', (char)evt.Intermediates);
    }

    [Fact]
    public void Set7BitControls()
    {
        // ESC SP F - Set 7-bit control mode (S7C1T)
        // Source: libvterm t/26state_query.test line 63
        Parse("\u001b F");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('F', evt.Final);
        Assert.Equal(' ', (char)evt.Intermediates);
    }

    #endregion
}
