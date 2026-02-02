// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Microsoft.Extensions.Terminal.Parser;
using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Base class for parser tests with common helpers.
/// </summary>
public abstract class ParserTestBase
{
    /// <summary>
    /// Escape character constant for readable test strings.
    /// Use: $"{Esc}[31m" instead of "\x1b[31m" to avoid C# hex escape issues.
    /// </summary>
    protected const string Esc = "\u001b";

    protected RecordingHandler Handler { get; } = new();
    protected VtParser Parser { get; }

    /// <summary>
    /// Access to all captured events.
    /// </summary>
    protected List<ParserEvent> Events => Handler.Events;

    protected ParserTestBase()
    {
        Parser = new VtParser(Handler);
    }

    /// <summary>
    /// Parse a string as UTF-8 bytes.
    /// Note: Use $"{Esc}[..." syntax for escape sequences to avoid C# hex parsing issues.
    /// The issue is "\x1b7" is parsed as single char U+01B7, not ESC + '7'.
    /// </summary>
    protected void Parse(string input)
    {
        Parser.Parse(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    /// Parse raw bytes.
    /// </summary>
    protected void Parse(ReadOnlySpan<byte> input)
    {
        Parser.Parse(input);
    }

    /// <summary>
    /// Assert an event at a given index is of the expected type.
    /// </summary>
    protected T AssertEventType<T>(int index = 0) where T : ParserEvent
    {
        Assert.True(Events.Count > index, $"Expected at least {index + 1} events, got {Events.Count}");
        var evt = Events[index];
        Assert.IsType<T>(evt);
        return (T)evt;
    }

    /// <summary>
    /// Assert a single CSI event was dispatched.
    /// </summary>
    protected CsiEvent AssertSingleCsi(char expectedCommand)
    {
        var csi = Assert.Single(Events.OfType<CsiEvent>());
        Assert.Equal(expectedCommand, csi.Command);
        return csi;
    }

    /// <summary>
    /// Assert a single ESC event was dispatched.
    /// </summary>
    protected EscEvent AssertSingleEsc(char expectedCommand)
    {
        var esc = Assert.Single(Events.OfType<EscEvent>());
        Assert.Equal(expectedCommand, esc.Command);
        return esc;
    }

    /// <summary>
    /// Assert a single OSC event was dispatched.
    /// </summary>
    protected OscEvent AssertSingleOsc(int expectedCommand)
    {
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(expectedCommand, osc.Command);
        return osc;
    }
}
