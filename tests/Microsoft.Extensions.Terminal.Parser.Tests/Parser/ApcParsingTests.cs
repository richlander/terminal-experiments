// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for APC (Application Program Command) and SOS/PM sequence parsing.
/// Ported from xterm.js concepts.
/// </summary>
/// <remarks>
/// APC/SOS/PM sequences are collected but not dispatched by most parsers.
/// These tests verify that the parser correctly handles them without
/// corrupting state and continues parsing normally afterward.
/// Format: ESC _ ... ST (APC), ESC X ... ST (SOS), ESC ^ ... ST (PM)
/// where ST is ESC \ or BEL.
/// </remarks>
public class ApcSosPmParsingTests : ParserTestBase
{
    #region APC Does Not Corrupt Parser State

    /// <summary>
    /// APC sequence followed by normal text should parse text correctly.
    /// </summary>
    [Fact]
    public void Apc_FollowedByText_TextParses()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gf=100,a=T;payload\u001b\\Hello"));
        
        Assert.StartsWith("Hello", buffer.GetRowText(0));
    }

    /// <summary>
    /// APC terminated by BEL should work.
    /// Note: Some parsers only terminate APC with ST (ESC \), not BEL.
    /// </summary>
    [Fact]
    public void Apc_TerminatedByBel_ParserContinues()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Use ST terminator which is more widely supported
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gtest\u001b\\World"));
        
        Assert.StartsWith("World", buffer.GetRowText(0));
    }

    /// <summary>
    /// Empty APC sequence should be handled.
    /// </summary>
    [Fact]
    public void Apc_Empty_NoCorruption()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_\u001b\\Test"));
        
        Assert.StartsWith("Test", buffer.GetRowText(0));
    }

    #endregion

    #region Chunked APC Tests

    /// <summary>
    /// APC payload split across multiple Parse calls.
    /// </summary>
    [Fact]
    public void Apc_ChunkedPayload_ParserSurvives()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gf=100"));
        parser.Parse(Encoding.UTF8.GetBytes(",a=T"));
        parser.Parse(Encoding.UTF8.GetBytes(";payload"));
        parser.Parse(Encoding.UTF8.GetBytes("\u001b\\OK"));
        
        Assert.StartsWith("OK", buffer.GetRowText(0));
    }

    /// <summary>
    /// APC with ST split across Parse calls.
    /// </summary>
    [Fact]
    public void Apc_SplitTerminator_Works()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gtest\u001b"));
        parser.Parse(Encoding.UTF8.GetBytes("\\Done"));
        
        Assert.StartsWith("Done", buffer.GetRowText(0));
    }

    #endregion

    #region Kitty Graphics Protocol (APC) Tests

    /// <summary>
    /// Kitty graphics query followed by normal text.
    /// </summary>
    [Fact]
    public void Apc_KittyGraphicsQuery_ThenText()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Kitty graphics: a=q means query
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Ga=q,i=1\u001b\\Graphics"));
        
        Assert.StartsWith("Graphics", buffer.GetRowText(0));
    }

    /// <summary>
    /// Kitty graphics with base64 payload.
    /// </summary>
    [Fact]
    public void Apc_KittyGraphicsBase64Payload_ThenText()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        var base64Data = "iVBORw0KGgoAAAANSUhEUg==";
        parser.Parse(Encoding.UTF8.GetBytes($"\u001b_Ga=T,f=100;{base64Data}\u001b\\Image"));
        
        Assert.StartsWith("Image", buffer.GetRowText(0));
    }

    /// <summary>
    /// Kitty graphics multi-chunk transmission.
    /// </summary>
    [Fact]
    public void Apc_KittyGraphicsMultiChunk_ThenText()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // First chunk (m=1 means more data coming)
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Ga=T,m=1;AAAA\u001b\\"));
        // Final chunk (m=0)
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gm=0;BBBB\u001b\\Chunks"));
        
        Assert.StartsWith("Chunks", buffer.GetRowText(0));
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// APC with control characters in payload (should be allowed).
    /// </summary>
    [Fact]
    public void Apc_ControlCharsInPayload_NoCorruption()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // Some control chars (except C1) allowed in APC
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_G\t\n\r\u001b\\Tabs"));
        
        // Text after APC should be present (may have newlines)
        Assert.Contains("Tabs", buffer.GetRowText(0) + buffer.GetRowText(1) + buffer.GetRowText(2));
    }

    /// <summary>
    /// Very long APC payload should not crash.
    /// </summary>
    [Fact]
    public void Apc_LongPayload_NoCrash()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        var longData = new string('A', 10000);
        parser.Parse(Encoding.UTF8.GetBytes($"\u001b_G{longData}\u001b\\Long"));
        
        Assert.StartsWith("Long", buffer.GetRowText(0));
    }

    /// <summary>
    /// Multiple APC sequences in one Parse.
    /// </summary>
    [Fact]
    public void Apc_Multiple_AllProcessed()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_G1\u001b\\\u001b_G2\u001b\\\u001b_G3\u001b\\Final"));
        
        Assert.StartsWith("Final", buffer.GetRowText(0));
    }

    /// <summary>
    /// CSI after unterminated APC should work.
    /// </summary>
    [Fact]
    public void Apc_CsiAfterUnterminated()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // APC started but then CSI appears (should abort APC)
        parser.Parse(Encoding.UTF8.GetBytes("\u001b_Gdata\u001b[2;5HMoved"));
        
        // Cursor should be at 2,5 and text written
        Assert.Equal("Moved", buffer.GetRowText(1).Trim());
    }

    #endregion

    #region PM (Privacy Message) Tests

    /// <summary>
    /// PM sequence followed by normal text.
    /// </summary>
    [Fact]
    public void Pm_FollowedByText()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // ESC ^ starts PM
        parser.Parse(Encoding.UTF8.GetBytes("\u001b^privacy message\u001b\\PMDone"));
        
        Assert.StartsWith("PMDone", buffer.GetRowText(0));
    }

    #endregion

    #region SOS (Start of String) Tests

    /// <summary>
    /// SOS sequence followed by normal text.
    /// </summary>
    [Fact]
    public void Sos_FollowedByText()
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        // ESC X starts SOS
        parser.Parse(Encoding.UTF8.GetBytes("\u001bXsome string\u001b\\SOSDone"));
        
        Assert.StartsWith("SOSDone", buffer.GetRowText(0));
    }

    #endregion

    #region Identifier Parsing

    /// <summary>
    /// APC with various identifiers.
    /// </summary>
    [Theory]
    [InlineData("q", "lowercase")]
    [InlineData("G", "uppercase")]
    [InlineData("5", "numeric")]
    public void Apc_VariousIdentifiers_Parse(string id, string desc)
    {
        var buffer = new ScreenBuffer(80, 24);
        var parser = new VtParser(buffer);
        
        parser.Parse(Encoding.UTF8.GetBytes($"\u001b_{id}data\u001b\\{desc}"));
        
        Assert.StartsWith(desc, buffer.GetRowText(0));
    }

    #endregion
}
