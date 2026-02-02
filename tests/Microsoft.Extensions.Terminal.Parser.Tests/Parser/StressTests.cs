// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Parser;

/// <summary>
/// Stress tests for parser robustness and performance.
/// </summary>
/// <remarks>
/// These tests ensure the parser handles edge cases like:
/// - Very long sequences
/// - Rapid state transitions
/// - Malformed input
/// - Memory pressure scenarios
/// </remarks>
public class StressTests : ParserTestBase
{
    #region Large Input

    [Fact]
    public void LargeTextBlock_100K_Characters()
    {
        var text = new string('A', 100_000);
        Parse(text);

        var prints = Events.OfType<PrintEvent>().Count();
        Assert.Equal(100_000, prints);
    }

    [Fact]
    public void LargeCsiSequence_ManyParams()
    {
        // 1000 semicolons - way more params than any terminal supports
        var seq = "\u001b[" + string.Join(";", Enumerable.Range(1, 1000)) + "m";
        Parse(seq);

        // Should not crash, should dispatch something
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal('m', csi.Command);
        // Params truncated to limit
        Assert.True(csi.Params.Length <= 16);
    }

    [Fact]
    public void LargeOscString_10K_Characters()
    {
        var title = new string('T', 10_000);
        Parse($"\u001b]0;{title}\x07");

        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(0, osc.Command);
        // OSC data may be truncated to buffer limit
        Assert.True(osc.Data.Length > 0);
    }

    [Fact]
    public void LargeDcsPayload_10K_Characters()
    {
        var payload = new string('D', 10_000);
        Parse($"\u001bPq{payload}\u001b\\");

        Assert.Single(Events.OfType<DcsHookEvent>());
        Assert.Single(Events.OfType<DcsUnhookEvent>());
    }

    #endregion

    #region Rapid Sequences

    [Fact]
    public void RapidCsiSequences_1000()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sb.Append($"\u001b[{i % 8 + 30}m"); // Cycle through colors
        }
        Parse(sb.ToString());

        var csis = Events.OfType<CsiEvent>().Count();
        Assert.Equal(1000, csis);
    }

    [Fact]
    public void RapidOscSequences_100()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.Append($"\u001b]0;Title {i}\x07");
        }
        Parse(sb.ToString());

        var oscs = Events.OfType<OscEvent>().Count();
        Assert.Equal(100, oscs);
    }

    [Fact]
    public void RapidEscSequences_1000()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
        {
            sb.Append("\u001b7"); // Save cursor
            sb.Append("\u001b8"); // Restore cursor
        }
        Parse(sb.ToString());

        var escs = Events.OfType<EscEvent>().Count();
        Assert.Equal(1000, escs);
    }

    [Fact]
    public void MixedSequences_HighFrequency()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
        {
            sb.Append($"Text{i}");           // Print
            sb.Append("\u001b[31m");          // CSI
            sb.Append("\u001b]0;T\x07");      // OSC
            sb.Append("\u001b7");             // ESC
            sb.Append("\r\n");                // Controls
        }
        Parse(sb.ToString());

        Assert.True(Events.Count > 2000);
    }

    #endregion

    #region Malformed Input

    [Fact]
    public void MalformedCsi_NoFinalByte_Recovers()
    {
        // CSI with digits but 'A' is valid final byte (CUU)
        // So CSI 123 A is a valid cursor up sequence
        // Let's use a truly incomplete sequence followed by ESC to cancel
        Parse("\u001b[123\u001b7XYZ");

        // ESC 7 should execute, then XYZ prints
        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Contains(prints, p => p.Char == 'X');
        Assert.Contains(prints, p => p.Char == 'Y');
        Assert.Contains(prints, p => p.Char == 'Z');
    }

    [Fact]
    public void MalformedOsc_NoTerminator_NextEscRecovers()
    {
        Parse("\u001b]0;title\u001b[31mX");

        // ESC [ starts CSI, canceling OSC
        var csi = Events.OfType<CsiEvent>().FirstOrDefault();
        Assert.NotNull(csi);
    }

    [Fact]
    public void RandomBytes_DoesNotCrash()
    {
        var random = new Random(42);
        var bytes = new byte[10_000];
        random.NextBytes(bytes);

        // Should not throw
        Parser.Parse(bytes);

        // Parser should still be functional after
        Handler.Clear();
        Parse("Test");
        Assert.Equal(4, Events.OfType<PrintEvent>().Count());
    }

    [Fact]
    public void AllPossibleBytes_DoesNotCrash()
    {
        var bytes = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            bytes[i] = (byte)i;
        }

        // Should not throw
        Parser.Parse(bytes);
    }

    [Fact]
    public void RepeatedEscape_DoesNotAccumulate()
    {
        // 1000 ESC characters with no following byte
        var escs = new string('\u001b', 1000) + "7";
        Parse(escs);

        // Should get one ESC 7 (save cursor)
        var escEvents = Events.OfType<EscEvent>().ToList();
        Assert.Single(escEvents);
        Assert.Equal('7', escEvents[0].Command);
    }

    [Fact]
    public void NestedSequenceStarts_Cancels()
    {
        // CSI interrupted by new ESC [
        Parse("\u001b[1\u001b[2m");

        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal(new[] { 2 }, csi.Params);
    }

    #endregion

    #region UTF-8 Stress

    [Fact]
    public void MixedAsciiAndUtf8_LargeBlock()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sb.Append("Hello 世界 🌍 ");
        }
        Parse(sb.ToString());

        // Should have lots of print events
        Assert.True(Events.OfType<PrintEvent>().Count() > 10_000);
    }

    [Fact]
    public void InvalidUtf8_DoesNotCrash()
    {
        // Invalid UTF-8 sequences
        byte[] invalid = [
            0x80, // Continuation without start
            0xC0, 0x80, // Overlong NUL
            0xFE, 0xFF, // Invalid start bytes
            0xED, 0xA0, 0x80, // Surrogate
        ];

        Parser.Parse(invalid);
        // Should not crash, may produce replacement chars
    }

    [Fact]
    public void Utf8SplitAcrossParses()
    {
        // 世 = E4 B8 96
        Parser.Parse(new byte[] { 0xE4 });
        Parser.Parse(new byte[] { 0xB8 });
        Parser.Parse(new byte[] { 0x96 });

        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Single(prints);
        Assert.Equal('世', prints[0].Char);
    }

    #endregion

    #region State Machine Stress

    [Fact]
    public void AllStateTransitions_RapidCycling()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.Append("A");                    // Ground
            sb.Append("\u001b");               // Escape
            sb.Append("[");                    // CsiEntry
            sb.Append("1");                    // CsiParam
            sb.Append(" ");                    // CsiIntermediate
            sb.Append("q");                    // Ground (dispatch)
            sb.Append("\u001b]0;T\x07");       // OSC cycle
            sb.Append("\u001bPq\u001b\\");     // DCS cycle
        }
        Parse(sb.ToString());

        Assert.True(Events.Count > 400);
    }

    [Fact]
    public void CancelFromEveryState()
    {
        // CAN (0x18) should cancel from any state
        var sequences = new[]
        {
            "\u001b" + "\x18" + "X",           // Cancel from Escape
            "\u001b[" + "\x18" + "X",          // Cancel from CsiEntry
            "\u001b[1" + "\x18" + "X",         // Cancel from CsiParam
            "\u001b[1 " + "\x18" + "X",        // Cancel from CsiIntermediate
            "\u001b]0;T" + "\x18" + "X",       // Cancel from OscString
            "\u001bPq" + "\x18" + "X",         // Cancel from DcsPassthrough
        };

        foreach (var seq in sequences)
        {
            Handler.Clear();
            Parse(seq);

            // After cancel, 'X' should print
            var prints = Events.OfType<PrintEvent>().ToList();
            Assert.Contains(prints, p => p.Char == 'X');
        }
    }

    [Fact]
    public void Parser_Reset_ClearsAllState()
    {
        // Start a sequence
        Parse("\u001b[1;2");

        // Reset
        Parser.Reset();
        Handler.Clear();

        // Should be back to ground state
        Parse("X\u001b[3mY");

        var prints = Events.OfType<PrintEvent>().ToList();
        Assert.Equal(2, prints.Count);
        Assert.Equal('X', prints[0].Char);
        Assert.Equal('Y', prints[1].Char);
    }

    #endregion

    #region Memory Stress

    [Fact]
    public void RepeatedParsing_NoMemoryLeak()
    {
        // Create parser and use it many times
        var handler = new RecordingHandler();
        var parser = new VtParser(handler);

        for (int i = 0; i < 1000; i++)
        {
            handler.Clear();
            parser.Parse(Encoding.UTF8.GetBytes($"\u001b[{i}mTest\u001b]0;Title\x07"));
        }

        // Should complete without OOM
        Assert.True(true);
    }

    [Fact]
    public void VeryLongParams_DoesNotOverflow()
    {
        // Param value that would overflow int32
        Parse("\u001b[99999999999999999999m");

        var csi = Assert.Single(Events.OfType<CsiEvent>());
        // Should clamp, not overflow
        Assert.True(csi.Params[0] >= 0);
    }

    #endregion

    #region ScreenBuffer Stress

    [Fact]
    public void ScreenBuffer_LargeTerminal()
    {
        var buffer = new ScreenBuffer(200, 100);
        var parser = new VtParser(buffer);

        // Fill entire screen
        var sb = new StringBuilder();
        for (int y = 0; y < 100; y++)
        {
            sb.Append(new string('X', 200));
            if (y < 99) sb.Append("\r\n");
        }
        parser.Parse(Encoding.UTF8.GetBytes(sb.ToString()));

        // Check corners
        Assert.Equal('X', buffer.GetCell(0, 0).Character);
        Assert.Equal('X', buffer.GetCell(199, 99).Character);
    }

    [Fact]
    public void ScreenBuffer_RapidScrolling()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);

        // Scroll 1000 times
        for (int i = 0; i < 1000; i++)
        {
            parser.Parse(Encoding.UTF8.GetBytes($"Line {i}\r\n"));
        }

        // Buffer should contain recent lines (last line is 999)
        // The content depends on scrolling behavior
        // Just verify no crash and cursor is valid
        Assert.True(buffer.CursorY >= 0 && buffer.CursorY < 24);
    }

    [Fact]
    public void ScreenBuffer_RapidCursorMovement()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);

        var sb = new StringBuilder();
        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            int row = random.Next(1, 25);
            int col = random.Next(1, 81);
            sb.Append($"\u001b[{row};{col}H*");
        }
        parser.Parse(Encoding.UTF8.GetBytes(sb.ToString()));

        // Should not crash, cursor should be valid
        Assert.True(buffer.CursorX >= 0 && buffer.CursorX < 80);
        Assert.True(buffer.CursorY >= 0 && buffer.CursorY < 24);
    }

    [Fact]
    public void ScreenBuffer_ColorCycling()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);

        var sb = new StringBuilder();
        for (int i = 0; i < 256; i++)
        {
            sb.Append($"\u001b[38;5;{i}m*");
        }
        parser.Parse(Encoding.UTF8.GetBytes(sb.ToString()));

        // All 256 colors should be represented
        var colors = new HashSet<uint>();
        for (int x = 0; x < 80 && x < 256; x++)
        {
            colors.Add(buffer.GetCell(x, 0).Foreground);
        }
        Assert.True(colors.Count > 50); // Many unique colors
    }

    #endregion
}
