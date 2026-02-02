// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Scroll-related CSI sequence parsing tests.
/// </summary>
/// <remarks>
/// Ported from:
/// - libvterm: t/12state_scroll.test (parsing aspects)
/// 
/// These tests verify the parser correctly handles scroll region and scroll commands.
/// </remarks>
public class ScrollParsingTests : ParserTestBase
{
    #region DECSTBM - Set Top and Bottom Margins

    [Fact]
    public void Decstbm_FullRange()
    {
        // CSI 1 ; 24 r - set scroll region to full screen
        Parse($"{Esc}[1;24r");

        var csi = AssertSingleCsi('r');
        Assert.Equal([1, 24], csi.Params);
    }

    [Fact]
    public void Decstbm_PartialRange()
    {
        // CSI 5 ; 20 r - set scroll region to rows 5-20
        Parse($"{Esc}[5;20r");

        var csi = AssertSingleCsi('r');
        Assert.Equal([5, 20], csi.Params);
    }

    [Fact]
    public void Decstbm_SingleRow()
    {
        // CSI 10 ; 10 r - single row scroll region
        Parse($"{Esc}[10;10r");

        var csi = AssertSingleCsi('r');
        Assert.Equal([10, 10], csi.Params);
    }

    [Fact]
    public void Decstbm_Reset_NoParams()
    {
        // CSI r - reset scroll region to full screen
        Parse($"{Esc}[r");

        var csi = AssertSingleCsi('r');
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void Decstbm_TopOnly()
    {
        // CSI 5 r - only top margin specified
        Parse($"{Esc}[5r");

        var csi = AssertSingleCsi('r');
        Assert.Equal([5], csi.Params);
    }

    [Fact]
    public void Decstbm_BottomOnly()
    {
        // CSI ; 20 r - only bottom margin specified
        Parse($"{Esc}[;20r");

        var csi = AssertSingleCsi('r');
        Assert.Equal([0, 20], csi.Params);
    }

    #endregion

    #region DECSLRM - Set Left and Right Margins

    [Fact]
    public void Decslrm_FullRange()
    {
        // CSI 1 ; 80 s - set left/right margins (when DECVSSM is set)
        // Note: This conflicts with SCOSC (save cursor) without DECVSSM
        Parse($"{Esc}[1;80s");

        var csi = AssertSingleCsi('s');
        Assert.Equal([1, 80], csi.Params);
    }

    [Fact]
    public void Decslrm_PartialRange()
    {
        Parse($"{Esc}[10;40s");

        var csi = AssertSingleCsi('s');
        Assert.Equal([10, 40], csi.Params);
    }

    #endregion

    #region SU - Scroll Up (Pan Down)

    [Fact]
    public void ScrollUp_Default()
    {
        // CSI S - scroll up 1 line (default)
        Parse($"{Esc}[S");

        var csi = AssertSingleCsi('S');
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void ScrollUp_SingleLine()
    {
        Parse($"{Esc}[1S");

        var csi = AssertSingleCsi('S');
        Assert.Equal([1], csi.Params);
    }

    [Fact]
    public void ScrollUp_MultipleLines()
    {
        Parse($"{Esc}[5S");

        var csi = AssertSingleCsi('S');
        Assert.Equal([5], csi.Params);
    }

    [Fact]
    public void ScrollUp_LargeValue()
    {
        Parse($"{Esc}[100S");

        var csi = AssertSingleCsi('S');
        Assert.Equal([100], csi.Params);
    }

    #endregion

    #region SD - Scroll Down (Pan Up)

    [Fact]
    public void ScrollDown_Default()
    {
        // CSI T - scroll down 1 line (default)
        Parse($"{Esc}[T");

        var csi = AssertSingleCsi('T');
        Assert.Empty(csi.Params);
    }

    [Fact]
    public void ScrollDown_SingleLine()
    {
        Parse($"{Esc}[1T");

        var csi = AssertSingleCsi('T');
        Assert.Equal([1], csi.Params);
    }

    [Fact]
    public void ScrollDown_MultipleLines()
    {
        Parse($"{Esc}[5T");

        var csi = AssertSingleCsi('T');
        Assert.Equal([5], csi.Params);
    }

    #endregion

    #region IND and RI (ESC sequences)

    [Fact]
    public void Index_EscD()
    {
        // ESC D - Index (move cursor down, scroll if at bottom)
        Parse($"{Esc}D");

        var esc = AssertSingleEsc('D');
    }

    [Fact]
    public void ReverseIndex_EscM()
    {
        // ESC M - Reverse Index (move cursor up, scroll if at top)
        Parse($"{Esc}M");

        var esc = AssertSingleEsc('M');
    }

    [Fact]
    public void NextLine_EscE()
    {
        // ESC E - Next Line (CR + LF, scroll if at bottom)
        Parse($"{Esc}E");

        var esc = AssertSingleEsc('E');
    }

    #endregion

    #region Combined Scroll and Position

    [Fact]
    public void ScrollRegion_ThenCursorPosition()
    {
        // Set scroll region, then move cursor
        Parse($"{Esc}[5;20r{Esc}[10;10H");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);
        
        Assert.Equal('r', csis[0].Command);
        Assert.Equal([5, 20], csis[0].Params);
        
        Assert.Equal('H', csis[1].Command);
        Assert.Equal([10, 10], csis[1].Params);
    }

    [Fact]
    public void ScrollUp_ThenScrollDown()
    {
        Parse($"{Esc}[3S{Esc}[2T");

        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);
        
        Assert.Equal('S', csis[0].Command);
        Assert.Equal([3], csis[0].Params);
        
        Assert.Equal('T', csis[1].Command);
        Assert.Equal([2], csis[1].Params);
    }

    #endregion

    #region DECRQSS - Request Status (Scroll Regions)

    [Fact]
    public void Decrqss_DecstbmQuery()
    {
        // DCS $ q r ST - request DECSTBM setting
        Parse($"{Esc}P$qr{Esc}\\");

        var hooks = Events.OfType<DcsHookEvent>().ToList();
        Assert.Single(hooks);
        Assert.Equal('q', hooks[0].Command);
    }

    [Fact]
    public void Decrqss_DecslrmQuery()
    {
        // DCS $ q s ST - request DECSLRM setting
        Parse($"{Esc}P$qs{Esc}\\");

        var hooks = Events.OfType<DcsHookEvent>().ToList();
        Assert.Single(hooks);
        Assert.Equal('q', hooks[0].Command);
    }

    #endregion
}
