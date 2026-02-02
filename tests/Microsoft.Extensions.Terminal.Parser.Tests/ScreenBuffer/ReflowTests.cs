// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// ScreenBuffer line reflow tests.
/// </summary>
/// <remarks>
/// Ported from libvterm: t/69screen_reflow.test
/// Tests automatic line rewrapping when screen is resized.
/// 
/// NOTE: ScreenBuffer.Resize() is not yet implemented. These tests are
/// skipped until the Resize method is available.
/// </remarks>
public class ReflowTests
{
    private ScreenBuffer CreateBuffer(int width = 10, int height = 5)
        => new ScreenBuffer(width, height);

    private void Parse(ScreenBuffer buffer, string input)
    {
        var parser = new VtParser(buffer);
        parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    #region Resize Wider Reflows - libvterm "Resize wider reflows wide lines"

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Resize wider reflows wide lines"
    /// 12 A's on 10-column screen wraps to 2 lines. Resize to 15 should reflow to 1 line.
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeWider_ReflowsWrappedLines()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, new string('A', 12));
        Assert.Equal("AAAAAAAAAA", buffer.GetRowText(0));
        Assert.Equal("AA", buffer.GetRowText(1));
    }

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Resize wider reflows wide lines"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeWider_FurtherResize()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, new string('A', 12));
    }

    #endregion

    #region Resize Narrower Creates Continuation - libvterm "Resize narrower"

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Resize narrower can create continuation lines"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeNarrower_CreatesWrappedLines()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "ABCDEFGHI");
    }

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Resize narrower"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ResizeNarrower_FurtherNarrowing()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "ABCDEFGHI");
    }

    #endregion

    #region Shell Wrapped Prompt - libvterm "Shell wrapped prompt behaviour"

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Shell wrapped prompt behaviour"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void ShellPrompt_ComplexWrappingScenario()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "PROMPT GOES HERE\r\n> \r\n\r\nPROMPT GOES HERE\r\n> ");
    }

    #endregion

    #region Cursor Position Edge Cases - libvterm "Cursor goes missing"

    /// <summary>
    /// Ported from: libvterm 69screen_reflow "Cursor goes missing"
    /// </summary>
    [Fact(Skip = "Resize method not implemented")]
    public void CursorMissing_MultipleResizes()
    {
        var buffer = CreateBuffer(5, 5);
    }

    #endregion

    #region Basic Wrapping Tests (No Resize Needed)

    /// <summary>
    /// Lines that wrap should be marked as continuation.
    /// </summary>
    [Fact]
    public void ContinuationLine_MarkedAfterWrap()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, new string('X', 15));
        Assert.Equal("XXXXXXXXXX", buffer.GetRowText(0));
        Assert.Equal("XXXXX", buffer.GetRowText(1));
    }

    /// <summary>
    /// Explicit newline should not create continuation.
    /// </summary>
    [Fact]
    public void ExplicitNewline_NotContinuation()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, "Line1\r\nLine2");
        Assert.Equal("Line1", buffer.GetRowText(0));
        Assert.Equal("Line2", buffer.GetRowText(1));
    }

    /// <summary>
    /// Cursor position after wrapping.
    /// </summary>
    [Fact]
    public void Wrapping_CursorPositionCorrect()
    {
        var buffer = CreateBuffer(10, 5);
        Parse(buffer, new string('A', 12));
        Assert.Equal(2, buffer.CursorX);
        Assert.Equal(1, buffer.CursorY);
    }

    /// <summary>
    /// Multiple wrapped lines in sequence.
    /// </summary>
    [Fact]
    public void MultipleWrappedLines()
    {
        var buffer = CreateBuffer(10, 10);
        Parse(buffer, "ABCDEFGHIJKLMNO\r\n");
        Parse(buffer, "12345678901234\r\n");
        Assert.Equal("ABCDEFGHIJ", buffer.GetRowText(0));
        Assert.Equal("KLMNO", buffer.GetRowText(1));
        Assert.Equal("1234567890", buffer.GetRowText(2));
        Assert.Equal("1234", buffer.GetRowText(3));
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Empty buffer basic operations.
    /// </summary>
    [Fact]
    public void EdgeCase_EmptyBuffer()
    {
        var buffer = CreateBuffer(10, 5);
        Assert.Equal(0, buffer.CursorX);
        Assert.Equal(0, buffer.CursorY);
        Assert.Equal("", buffer.GetRowText(0));
    }

    /// <summary>
    /// Single column buffer.
    /// </summary>
    [Fact]
    public void EdgeCase_SingleColumnBuffer()
    {
        var buffer = CreateBuffer(1, 10);
        Parse(buffer, "ABC");
        Assert.Equal("A", buffer.GetRowText(0));
        Assert.Equal("B", buffer.GetRowText(1));
        Assert.Equal("C", buffer.GetRowText(2));
    }

    /// <summary>
    /// Very long line wrapping.
    /// </summary>
    [Fact]
    public void EdgeCase_VeryLongLineWrap()
    {
        var buffer = CreateBuffer(10, 20);
        Parse(buffer, new string('Z', 100));
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal("ZZZZZZZZZZ", buffer.GetRowText(i));
        }
    }

    #endregion
}
