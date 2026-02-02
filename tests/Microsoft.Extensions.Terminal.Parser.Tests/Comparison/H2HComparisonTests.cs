// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Comparison;

/// <summary>
/// Head-to-head comparison tests between our parser and VtNetCore.
/// 
/// VtNetCore is a full terminal emulator, so we compare by:
/// 1. Feeding the same sequences to both
/// 2. Verifying both produce consistent results
/// 
/// These tests help catch behavioral differences not covered by ported unit tests.
/// </summary>
public class H2HComparisonTests : ParserTestBase
{
    /// <summary>
    /// Creates a VtNetCore terminal for comparison.
    /// </summary>
    private static (VirtualTerminalController controller, DataConsumer consumer) CreateVtNetCore()
    {
        var controller = new VirtualTerminalController();
        var consumer = new DataConsumer(controller);
        controller.ResizeView(80, 24);
        return (controller, consumer);
    }

    /// <summary>
    /// Push data to VtNetCore.
    /// </summary>
    private static void PushToVtNetCore(DataConsumer consumer, string data)
    {
        consumer.Push(Encoding.UTF8.GetBytes(data));
    }

    #region Text Printing Comparison

    /// <summary>
    /// Compare simple text printing between parsers.
    /// </summary>
    [Fact]
    public void H2H_SimpleText_BothPrintSame()
    {
        var input = "Hello, World!";
        
        // Our parser
        Parse(input);
        var ourText = Handler.GetPrintedText();
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        var vtNetText = controller.GetText(0, 0, input.Length, 0).TrimEnd();
        
        Assert.Equal(input, ourText);
        Assert.StartsWith(input, vtNetText);
    }

    /// <summary>
    /// Compare text with newlines.
    /// </summary>
    [Fact]
    public void H2H_TextWithNewlines_BothHandle()
    {
        var input = "Line1\nLine2\nLine3";
        
        // Our parser
        Parse(input);
        var ourText = Handler.GetPrintedText();
        var ourNewlines = Handler.Events.OfType<ExecuteEvent>().Count(e => e.Code == 0x0A);
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        var screenText = controller.GetScreenText();
        
        Assert.Equal("Line1Line2Line3", ourText);
        Assert.Equal(2, ourNewlines);
        // VtNetCore puts text on separate rows
        Assert.Contains("Line1", screenText);
        Assert.Contains("Line2", screenText);
    }

    #endregion

    #region CSI Sequence Comparison

    /// <summary>
    /// Compare CSI SGR (color) handling.
    /// </summary>
    [Fact]
    public void H2H_CsiSgr_BothRecognize()
    {
        var input = "\u001b[31mRed\u001b[0m";
        
        // Our parser
        Parse(input);
        var ourCsiCount = Handler.Events.OfType<CsiEvent>().Count();
        var ourText = Handler.GetPrintedText();
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        var screenText = controller.GetScreenText();
        
        Assert.Equal(2, ourCsiCount);  // [31m and [0m
        Assert.Equal("Red", ourText);
        Assert.Contains("Red", screenText);
    }

    /// <summary>
    /// Compare cursor positioning (CUP).
    /// </summary>
    [Fact]
    public void H2H_CursorPosition_BothHandle()
    {
        var input = "\u001b[5;10HX";  // Move to row 5, col 10, print X
        
        // Our parser
        Parse(input);
        var csi = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('H', csi.Command);
        Assert.Equal([5, 10], csi.Params);
        Assert.Equal("X", Handler.GetPrintedText());
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        // VtNetCore should have placed X somewhere
        var screenText = controller.GetScreenText();
        Assert.Contains("X", screenText);
    }

    /// <summary>
    /// Compare private mode sequences (DECSET).
    /// </summary>
    [Fact]
    public void H2H_PrivateMode_BothRecognize()
    {
        var input = "\u001b[?1049h";  // Alternate screen buffer
        
        // Our parser
        Parse(input);
        var csi = Assert.Single(Handler.Events.OfType<CsiEvent>());
        Assert.Equal('h', csi.Command);
        Assert.Equal((byte)'?', csi.PrivateMarker);
        Assert.Equal([1049], csi.Params);
        
        // VtNetCore should also handle this
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        // Just verify no crash - behavior depends on terminal mode support
    }

    #endregion

    #region OSC Comparison

    /// <summary>
    /// Compare OSC title setting.
    /// </summary>
    [Fact]
    public void H2H_OscTitle_BothRecognize()
    {
        var input = "\u001b]0;My Title\x07";
        
        // Our parser
        Parse(input);
        var osc = Assert.Single(Handler.Events.OfType<OscEvent>());
        Assert.Equal(0, osc.Command);
        Assert.Equal("My Title", Encoding.UTF8.GetString(osc.Data));
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        
        Assert.Equal("My Title", controller.WindowTitle);
    }

    #endregion

    #region UTF-8 Comparison

    /// <summary>
    /// Compare UTF-8 handling.
    /// </summary>
    [Fact]
    public void H2H_Utf8Text_BothHandle()
    {
        var input = "Hello, 世界!";
        
        // Our parser
        Parse(input);
        var ourText = Handler.GetPrintedText();
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        var screenText = controller.GetScreenText();
        
        Assert.Equal(input, ourText);
        // VtNetCore should also have the same text
        Assert.Contains("Hello", screenText);
        Assert.Contains("世界", screenText);
    }

    #endregion

    #region Edge Cases Comparison

    /// <summary>
    /// Compare handling of incomplete sequences.
    /// </summary>
    [Fact]
    public void H2H_IncompleteSequence_BothHandle()
    {
        // ESC [ without final byte, then normal text
        var input = "\u001b[X";  // Invalid CSI (X is not valid after just ESC[)
        
        // Our parser - should handle gracefully
        Parse(input);
        // Parser should not crash
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        // Should not crash
    }

    /// <summary>
    /// Compare handling of many rapid sequences.
    /// </summary>
    [Fact]
    public void H2H_ManySequences_BothHandle()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.Append($"\u001b[{i % 8 + 30}m{(char)('A' + i % 26)}");
        }
        sb.Append("\u001b[0m");
        var input = sb.ToString();
        
        // Our parser
        Parse(input);
        var ourCsiCount = Handler.Events.OfType<CsiEvent>().Count();
        
        // VtNetCore
        var (controller, consumer) = CreateVtNetCore();
        PushToVtNetCore(consumer, input);
        
        Assert.Equal(101, ourCsiCount);  // 100 colors + 1 reset
    }

    #endregion

    #region Real-World Sequence Tests

    /// <summary>
    /// Parse a realistic terminal output snippet.
    /// </summary>
    [Fact]
    public void RealWorld_LsColorOutput()
    {
        var input = "\u001b[0m\u001b[01;34mDocuments\u001b[0m";
        Parse(input);

        var csiEvents = Handler.Events.OfType<CsiEvent>().ToList();
        Assert.Equal(3, csiEvents.Count);
        Assert.Equal("Documents", Handler.GetPrintedText());
    }

    /// <summary>
    /// Parse vim-style cursor positioning.
    /// </summary>
    [Fact]
    public void RealWorld_CursorPositioning()
    {
        Parse("\u001b[5;10H");
        var csi = AssertSingleCsi('H');
        Assert.Equal([5, 10], csi.Params);
    }

    /// <summary>
    /// Parse a typical shell prompt with colors.
    /// </summary>
    [Fact]
    public void RealWorld_ShellPrompt()
    {
        var input = "\u001b[32muser@host\u001b[0m:\u001b[34m~/project\u001b[0m$ ";
        Parse(input);
        Assert.Equal("user@host:~/project$ ", Handler.GetPrintedText());
        Assert.Equal(4, Handler.Events.OfType<CsiEvent>().Count());
    }

    /// <summary>
    /// Parse htop-style screen clear and position.
    /// </summary>
    [Fact]
    public void RealWorld_ScreenClear()
    {
        Parse("\u001b[2J\u001b[H");
        var csiEvents = Handler.Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csiEvents.Count);
        Assert.Equal('J', csiEvents[0].Command);
        Assert.Equal('H', csiEvents[1].Command);
    }

    /// <summary>
    /// Parse OSC title setting.
    /// </summary>
    [Fact]
    public void RealWorld_SetTitle()
    {
        Parse("\u001b]0;vim - file.txt\x07");
        var osc = AssertSingleOsc(0);
        Assert.Equal("vim - file.txt", Encoding.UTF8.GetString(osc.Data));
    }

    /// <summary>
    /// Parse hyperlink (OSC 8).
    /// </summary>
    [Fact]
    public void RealWorld_Hyperlink()
    {
        Parse("\u001b]8;;https://github.com\x07Click here\u001b]8;;\x07");
        var oscEvents = Handler.Events.OfType<OscEvent>().ToList();
        Assert.Equal(2, oscEvents.Count);
        Assert.Equal(8, oscEvents[0].Command);
    }

    #endregion

    #region Stress Tests

    /// <summary>
    /// Parse many rapid sequences.
    /// </summary>
    [Fact]
    public void Stress_ManySequences()
    {
        for (int i = 0; i < 1000; i++)
        {
            Parse($"\u001b[{i % 100}m");
        }
        Assert.Equal(1000, Handler.Events.OfType<CsiEvent>().Count());
    }

    /// <summary>
    /// Parse large text blocks.
    /// </summary>
    [Fact]
    public void Stress_LargeTextBlock()
    {
        var text = new string('A', 100000);
        Parse(text);
        Assert.Equal(100000, Handler.Events.OfType<PrintEvent>().Count());
    }

    #endregion
}
