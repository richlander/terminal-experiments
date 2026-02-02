// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Microsoft.Extensions.Terminal.Parser;

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Records all parser events for test assertions.
/// </summary>
/// <remarks>
/// Pattern used by all major terminal parsers for testing:
/// - xterm.js: TestEscapeSequenceParser with mock handlers
/// - vte (Rust): MockHandler that collects dispatch calls
/// - libvterm: WANTPARSER mode with expected output
/// </remarks>
public class RecordingHandler : IParserHandler
{
    public List<ParserEvent> Events { get; } = new();
    
    // Track current DCS state for accumulating put bytes
    private List<byte>? _dcsData;

    public void Print(char c) => Events.Add(new PrintEvent(c));

    public void Execute(byte controlCode) => Events.Add(new ExecuteEvent(controlCode));

    public void CsiDispatch(ReadOnlySpan<int> parameters, byte privateMarker, byte intermediates, char command)
        => Events.Add(new CsiEvent(parameters.ToArray(), privateMarker, intermediates, command));

    public void EscDispatch(byte intermediates, char command)
        => Events.Add(new EscEvent(intermediates, command));

    public void OscDispatch(int command, ReadOnlySpan<byte> data)
        => Events.Add(new OscEvent(command, data.ToArray()));

    public void DcsHook(ReadOnlySpan<int> parameters, byte intermediates, char command)
    {
        var intermediatesStr = intermediates == 0 ? "" : ((char)intermediates).ToString();
        Events.Add(new DcsHookEvent(parameters.ToArray(), intermediatesStr, command));
        _dcsData = new List<byte>();
    }

    public void DcsPut(byte data)
    {
        _dcsData?.Add(data);
    }

    public void DcsUnhook()
    {
        // Emit accumulated DCS data as a string
        if (_dcsData != null && _dcsData.Count > 0)
        {
            Events.Add(new DcsPutEvent(Encoding.UTF8.GetString(_dcsData.ToArray())));
        }
        Events.Add(new DcsUnhookEvent());
        _dcsData = null;
    }

    /// <summary>
    /// Clear all recorded events.
    /// </summary>
    public void Clear() => Events.Clear();

    /// <summary>
    /// Get all printed characters as a string.
    /// </summary>
    public string GetPrintedText() =>
        new string(Events.OfType<PrintEvent>().Select(e => e.Char).ToArray());
}
