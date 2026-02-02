// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Buffers;

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// VT terminal escape sequence parser implementing the Paul Williams state machine.
/// </summary>
/// <remarks>
/// Implementation influenced by:
/// - vte (Rust): Match-based state transitions, Perform trait pattern
/// - xterm.js: Zero Default Mode for CSI parameters
/// - libvterm: Reference behavior for edge cases
/// 
/// References:
/// - https://vt100.net/emu/dec_ansi_parser
/// - ECMA-48: https://www.ecma-international.org/publications-and-standards/standards/ecma-48/
/// </remarks>
public sealed class VtParser(IParserHandler handler) : ITerminalParser
{
    private readonly IParserHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    private ParserState _state = ParserState.Ground;

    // CSI/DCS parameter collection
    private readonly int[] _params = new int[16];
    private int _paramCount;
    private int _currentParam;
    private bool _paramHasValue;

    // Private marker (< = > ?) that appears before params
    private byte _privateMarker;
    
    // Intermediate bytes (e.g., '$' in CSI Ps $ p) that appear after params
    private byte _intermediates;

    // OSC data collection
    private readonly byte[] _oscData = new byte[4096];
    private int _oscDataLen;

    // UTF-8 decoding
    private int _utf8Remaining;
    private int _utf8Codepoint;
    private int _utf8MinCodepoint;  // For overlong detection

    // Fast lookup for C0 control codes that execute immediately
    private static readonly SearchValues<byte> ExecuteControls = 
        SearchValues.Create([0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D]);

    /// <inheritdoc />
    public void Parse(ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            ProcessByte(data[i]);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _state = ParserState.Ground;
        ClearParams();
        _oscDataLen = 0;
        _utf8Remaining = 0;
        _utf8Codepoint = 0;
    }

    private void ProcessByte(byte b)
    {
        // Handle C0 controls that interrupt any state (except within strings)
        if (b < 0x20 && _state != ParserState.OscString && _state != ParserState.DcsPassthrough && _state != ParserState.SosPmApcString)
        {
            if (b == 0x1B) // ESC
            {
                _state = ParserState.Escape;
                ClearParams();
                return;
            }
            if (b == 0x18 || b == 0x1A) // CAN, SUB - cancel sequence
            {
                _state = ParserState.Ground;
                return;
            }
            // NUL (0x00) is ignored per VT spec
            if (b == 0x00)
            {
                return;
            }
            // Other C0 controls execute immediately and don't change state
            if (IsC0Control(b))
            {
                _handler.Execute(b);
                return;
            }
        }

        switch (_state)
        {
            case ParserState.Ground:
                HandleGround(b);
                break;
            case ParserState.Escape:
                HandleEscape(b);
                break;
            case ParserState.EscapeIntermediate:
                HandleEscapeIntermediate(b);
                break;
            case ParserState.CsiEntry:
                HandleCsiEntry(b);
                break;
            case ParserState.CsiParam:
                HandleCsiParam(b);
                break;
            case ParserState.CsiIntermediate:
                HandleCsiIntermediate(b);
                break;
            case ParserState.CsiIgnore:
                HandleCsiIgnore(b);
                break;
            case ParserState.OscString:
                HandleOscString(b);
                break;
            case ParserState.DcsEntry:
                HandleDcsEntry(b);
                break;
            case ParserState.DcsParam:
                HandleDcsParam(b);
                break;
            case ParserState.DcsIntermediate:
                HandleDcsIntermediate(b);
                break;
            case ParserState.DcsPassthrough:
                HandleDcsPassthrough(b);
                break;
            case ParserState.DcsIgnore:
                HandleDcsIgnore(b);
                break;
            case ParserState.SosPmApcString:
                HandleSosPmApcString(b);
                break;
        }
    }

    private void HandleGround(byte b)
    {
        if (b < 0x20)
        {
            _handler.Execute(b);
        }
        else if (b == 0x7F)
        {
            // DEL is ignored
            return;
        }
        else if (b < 0x80)
        {
            _handler.Print((char)b);
        }
        else
        {
            // UTF-8 handling
            HandleUtf8(b);
        }
    }

    private void HandleUtf8(byte b)
    {
        if (_utf8Remaining > 0)
        {
            // Continuation byte
            if ((b & 0xC0) == 0x80)
            {
                _utf8Codepoint = (_utf8Codepoint << 6) | (b & 0x3F);
                _utf8Remaining--;
                if (_utf8Remaining == 0)
                {
                    // Complete codepoint - validate and convert to char(s)
                    // Check for overlong encoding
                    if (_utf8Codepoint < _utf8MinCodepoint)
                    {
                        // Overlong - emit replacement character
                        _handler.Print('\uFFFD');
                    }
                    // Check for UTF-16 surrogates (invalid in UTF-8)
                    else if (_utf8Codepoint >= 0xD800 && _utf8Codepoint <= 0xDFFF)
                    {
                        _handler.Print('\uFFFD');
                    }
                    else if (_utf8Codepoint <= 0xFFFF)
                    {
                        _handler.Print((char)_utf8Codepoint);
                    }
                    else if (_utf8Codepoint <= 0x10FFFF)
                    {
                        // Surrogate pair
                        int adjusted = _utf8Codepoint - 0x10000;
                        _handler.Print((char)(0xD800 + (adjusted >> 10)));
                        _handler.Print((char)(0xDC00 + (adjusted & 0x3FF)));
                    }
                    else
                    {
                        // Out of range
                        _handler.Print('\uFFFD');
                    }
                }
            }
            else
            {
                // Invalid continuation - reset and try as new sequence
                _utf8Remaining = 0;
                HandleUtf8(b);
            }
        }
        else if (b >= 0xC2 && b <= 0xDF)
        {
            // 2-byte sequence: valid range U+0080 to U+07FF
            // Note: 0xC0-0xC1 are invalid (would be overlong encoding of ASCII)
            _utf8Codepoint = b & 0x1F;
            _utf8Remaining = 1;
            _utf8MinCodepoint = 0x80;
        }
        else if ((b & 0xF0) == 0xE0)
        {
            // 3-byte sequence: valid range U+0800 to U+FFFF
            _utf8Codepoint = b & 0x0F;
            _utf8Remaining = 2;
            _utf8MinCodepoint = 0x800;
        }
        else if (b >= 0xF0 && b <= 0xF4)
        {
            // 4-byte sequence: valid range U+10000 to U+10FFFF
            // Note: 0xF5-0xF7 would encode > U+10FFFF, invalid
            _utf8Codepoint = b & 0x07;
            _utf8Remaining = 3;
            _utf8MinCodepoint = 0x10000;
        }
        else if (b >= 0x80 && b <= 0x9F)
        {
            // C1 control codes
            // Some are commonly used and should be executed, others treated as Latin-1
            switch (b)
            {
                case 0x84: // IND - Index
                case 0x85: // NEL - Next Line
                case 0x88: // HTS - Horizontal Tab Set
                case 0x8D: // RI - Reverse Index
                case 0x8E: // SS2 - Single Shift 2
                case 0x8F: // SS3 - Single Shift 3
                case 0x90: // DCS - Device Control String (handled by state machine)
                case 0x9B: // CSI - Control Sequence Introducer (handled by state machine)
                case 0x9C: // ST - String Terminator
                case 0x9D: // OSC - Operating System Command (handled by state machine)
                    _handler.Execute(b);
                    break;
                default:
                    // Other C1 codes treated as Latin-1 for compatibility
                    _handler.Print((char)b);
                    break;
            }
        }
        else
        {
            // Invalid UTF-8 start byte (0xA0-0xBF, 0xF8-0xFF) or Latin-1 high chars
            _handler.Print((char)b);
        }
    }

    private void HandleEscape(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            // Intermediate byte
            _intermediates = b;
            _state = ParserState.EscapeIntermediate;
        }
        else if (b == '[')
        {
            // CSI
            _state = ParserState.CsiEntry;
            ClearParams();
        }
        else if (b == ']')
        {
            // OSC
            _state = ParserState.OscString;
            _oscDataLen = 0;
        }
        else if (b == 'P')
        {
            // DCS
            _state = ParserState.DcsEntry;
            ClearParams();
        }
        else if (b == 'X' || b == '^' || b == '_')
        {
            // SOS, PM, APC
            _state = ParserState.SosPmApcString;
        }
        else if (b >= 0x30 && b <= 0x7E)
        {
            // Final byte - dispatch ESC sequence
            _handler.EscDispatch(_intermediates, (char)b);
            _state = ParserState.Ground;
            _intermediates = 0;
        }
        else
        {
            // Invalid - back to ground
            _state = ParserState.Ground;
        }
    }

    private void HandleEscapeIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            // Another intermediate - we only track first one for simplicity
        }
        else if (b >= 0x30 && b <= 0x7E)
        {
            // Final byte
            _handler.EscDispatch(_intermediates, (char)b);
            _state = ParserState.Ground;
            _intermediates = 0;
        }
        else
        {
            _state = ParserState.Ground;
            _intermediates = 0;
        }
    }

    private void HandleCsiEntry(byte b)
    {
        if (b >= 0x30 && b <= 0x39)
        {
            // Digit - start param
            _currentParam = b - '0';
            _paramHasValue = true;
            _state = ParserState.CsiParam;
        }
        else if (b == ';')
        {
            // Empty first param
            PushParam();
            _state = ParserState.CsiParam;
        }
        else if (b >= 0x3C && b <= 0x3F)
        {
            // Private marker (< = > ?)
            _privateMarker = b;
            _state = ParserState.CsiParam;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            // Intermediate
            _intermediates = b;
            _state = ParserState.CsiIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte with no params
            DispatchCsi((char)b);
        }
        else if (b == 0x7F)
        {
            // DEL is ignored within CSI per VT spec
        }
        else
        {
            _state = ParserState.CsiIgnore;
        }
    }

    private void HandleCsiParam(byte b)
    {
        if (b >= 0x30 && b <= 0x39)
        {
            // Digit - accumulate with overflow protection
            long newValue = (long)_currentParam * 10 + (b - '0');
            _currentParam = newValue > int.MaxValue ? int.MaxValue : (int)newValue;
            _paramHasValue = true;
        }
        else if (b == ';')
        {
            PushParam();
        }
        else if (b == ':')
        {
            // Sub-parameter separator (for SGR) - treat like semicolon for now
            PushParam();
        }
        else if (b >= 0x3C && b <= 0x3F)
        {
            // Private marker in wrong position - ignore sequence
            _state = ParserState.CsiIgnore;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            // Intermediate
            PushParam();
            _intermediates = b;
            _state = ParserState.CsiIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte
            PushParam();
            DispatchCsi((char)b);
        }
        else if (b == 0x7F)
        {
            // DEL is ignored within CSI per VT spec
        }
        else
        {
            _state = ParserState.CsiIgnore;
        }
    }

    private void HandleCsiIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            // More intermediates - ignore extras
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte
            DispatchCsi((char)b);
        }
        else if (b >= 0x30 && b <= 0x3F)
        {
            // Param byte after intermediate - invalid
            _state = ParserState.CsiIgnore;
        }
        else if (b == 0x7F)
        {
            // DEL is ignored within CSI per VT spec
        }
        else
        {
            _state = ParserState.CsiIgnore;
        }
    }

    private void HandleCsiIgnore(byte b)
    {
        if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte - end ignore mode
            _state = ParserState.Ground;
            ClearParams();
        }
    }

    private void HandleOscString(byte b)
    {
        if (b == 0x07) // BEL terminator
        {
            DispatchOsc();
            _state = ParserState.Ground;
        }
        else if (b == 0x1B) // Possible ST (ESC \)
        {
            // Stay in OSC, next byte should be backslash
            // For simplicity, we check next iteration
            _state = ParserState.Escape; // Will check for \ 
            DispatchOsc();
        }
        else if (b == 0x9C) // ST (8-bit)
        {
            DispatchOsc();
            _state = ParserState.Ground;
        }
        else if (b == 0x18 || b == 0x1A) // CAN, SUB - cancel OSC
        {
            _oscDataLen = 0;
            _state = ParserState.Ground;
        }
        else if (b >= 0x20 || b == 0x09)
        {
            // Collect data
            if (_oscDataLen < _oscData.Length)
            {
                _oscData[_oscDataLen++] = b;
            }
        }
        // Ignore other C0 controls
    }

    private void HandleDcsEntry(byte b)
    {
        if (b >= 0x30 && b <= 0x39)
        {
            _currentParam = b - '0';
            _paramHasValue = true;
            _state = ParserState.DcsParam;
        }
        else if (b == ';')
        {
            PushParam();
            _state = ParserState.DcsParam;
        }
        else if (b >= 0x3C && b <= 0x3F)
        {
            _intermediates = b;
            _state = ParserState.DcsParam;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            _intermediates = b;
            _state = ParserState.DcsIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte - hook
            _handler.DcsHook(_params.AsSpan(0, _paramCount), _intermediates, (char)b);
            _state = ParserState.DcsPassthrough;
        }
        else
        {
            _state = ParserState.DcsIgnore;
        }
    }

    private void HandleDcsParam(byte b)
    {
        if (b >= 0x30 && b <= 0x39)
        {
            _currentParam = _currentParam * 10 + (b - '0');
            _paramHasValue = true;
        }
        else if (b == ';')
        {
            PushParam();
        }
        else if (b >= 0x3C && b <= 0x3F)
        {
            _state = ParserState.DcsIgnore;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            PushParam();
            _intermediates = b;
            _state = ParserState.DcsIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            PushParam();
            _handler.DcsHook(_params.AsSpan(0, _paramCount), _intermediates, (char)b);
            _state = ParserState.DcsPassthrough;
        }
        else
        {
            _state = ParserState.DcsIgnore;
        }
    }

    private void HandleDcsIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            // More intermediates
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            _handler.DcsHook(_params.AsSpan(0, _paramCount), _intermediates, (char)b);
            _state = ParserState.DcsPassthrough;
        }
        else
        {
            _state = ParserState.DcsIgnore;
        }
    }

    private void HandleDcsPassthrough(byte b)
    {
        if (b == 0x1B) // ESC - possible ST
        {
            _handler.DcsUnhook();
            _state = ParserState.Escape;
        }
        else if (b == 0x9C) // ST (8-bit)
        {
            _handler.DcsUnhook();
            _state = ParserState.Ground;
        }
        else if (b >= 0x20 && b < 0x80)
        {
            _handler.DcsPut(b);
        }
        else if (b >= 0x00 && b < 0x20)
        {
            // C0 controls in passthrough - some are passed, some ignored
            if (b != 0x18 && b != 0x1A && b != 0x1B)
            {
                _handler.DcsPut(b);
            }
            else if (b == 0x18 || b == 0x1A)
            {
                _handler.DcsUnhook();
                _state = ParserState.Ground;
            }
        }
    }

    private void HandleDcsIgnore(byte b)
    {
        if (b == 0x1B)
        {
            _state = ParserState.Escape;
        }
        else if (b == 0x9C)
        {
            _state = ParserState.Ground;
        }
    }

    private void HandleSosPmApcString(byte b)
    {
        // Just consume until ST
        if (b == 0x1B)
        {
            _state = ParserState.Escape;
        }
        else if (b == 0x9C)
        {
            _state = ParserState.Ground;
        }
    }

    private void DispatchCsi(char command)
    {
        _handler.CsiDispatch(_params.AsSpan(0, _paramCount), _privateMarker, _intermediates, command);
        _state = ParserState.Ground;
        ClearParams();
    }

    private void DispatchOsc()
    {
        // Parse OSC command from data
        int command = 0;
        int dataStart = 0;

        for (int i = 0; i < _oscDataLen; i++)
        {
            if (_oscData[i] == ';')
            {
                dataStart = i + 1;
                break;
            }
            else if (_oscData[i] >= '0' && _oscData[i] <= '9')
            {
                command = command * 10 + (_oscData[i] - '0');
                dataStart = i + 1;
            }
            else
            {
                // Invalid command
                break;
            }
        }

        _handler.OscDispatch(command, _oscData.AsSpan(dataStart, _oscDataLen - dataStart));
        _oscDataLen = 0;
    }

    private void PushParam()
    {
        if (_paramCount < _params.Length)
        {
            // Zero Default Mode: empty params become 0
            _params[_paramCount++] = _paramHasValue ? _currentParam : 0;
        }
        _currentParam = 0;
        _paramHasValue = false;
    }

    private void ClearParams()
    {
        _paramCount = 0;
        _currentParam = 0;
        _paramHasValue = false;
        _privateMarker = 0;
        _intermediates = 0;
    }

    private static bool IsC0Control(byte b) => b < 0x20;
}
