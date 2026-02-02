// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// A terminal screen buffer that implements <see cref="IParserHandler"/> to track terminal state.
/// </summary>
/// <remarks>
/// Design influenced by:
/// - xterm.js: Circular buffer, damage tracking
/// - libvterm: Scrolling regions, erase modes
/// - VtNetCore: C# implementation patterns
/// </remarks>
public class ScreenBuffer : IParserHandler
{
    private readonly TerminalCell[] _cells;
    private readonly int _width;
    private readonly int _height;

    // Cursor state
    private int _cursorX;
    private int _cursorY;
    private bool _cursorVisible = true;

    // Current text attributes for new characters
    private uint _currentForeground = TerminalCell.DefaultForeground;
    private uint _currentBackground = TerminalCell.DefaultBackground;
    private CellAttributes _currentAttributes = CellAttributes.None;

    // Scrolling region (DECSTBM)
    private int _scrollTop;
    private int _scrollBottom;

    // Saved cursor state (DECSC/DECRC)
    private int _savedCursorX;
    private int _savedCursorY;
    private uint _savedForeground;
    private uint _savedBackground;
    private CellAttributes _savedAttributes;

    // Modes
    private bool _originMode; // DECOM - cursor relative to scroll region
    private bool _autoWrapMode = true; // DECAWM

    // Window title (from OSC)
    private string _title = "";

    /// <summary>
    /// Creates a new screen buffer with the specified dimensions.
    /// </summary>
    public ScreenBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);

        _width = width;
        _height = height;
        _cells = new TerminalCell[width * height];
        _scrollTop = 0;
        _scrollBottom = height - 1;

        Clear();
    }

    #region Properties

    /// <summary>Buffer width in columns.</summary>
    public int Width => _width;

    /// <summary>Buffer height in rows.</summary>
    public int Height => _height;

    /// <summary>Current cursor column (0-based).</summary>
    public int CursorX => _cursorX;

    /// <summary>Current cursor row (0-based).</summary>
    public int CursorY => _cursorY;

    /// <summary>Whether the cursor is visible.</summary>
    public bool CursorVisible => _cursorVisible;

    /// <summary>Window title set by OSC 0/2.</summary>
    public string Title => _title;

    #endregion

    #region Cell Access

    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    public ref TerminalCell GetCell(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException();
        return ref _cells[y * _width + x];
    }

    /// <summary>
    /// Gets a row of cells as a span.
    /// </summary>
    public Span<TerminalCell> GetRow(int y)
    {
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException(nameof(y));
        return _cells.AsSpan(y * _width, _width);
    }

    /// <summary>
    /// Gets the text content of a row.
    /// </summary>
    public string GetRowText(int y)
    {
        var row = GetRow(y);
        var chars = new char[_width];
        for (int i = 0; i < _width; i++)
        {
            chars[i] = row[i].Character == '\0' ? ' ' : row[i].Character;
        }
        return new string(chars).TrimEnd();
    }

    #endregion

    #region IParserHandler Implementation

    /// <inheritdoc/>
    public void Print(char c)
    {
        if (_cursorX >= _width)
        {
            if (_autoWrapMode)
            {
                _cursorX = 0;
                LineFeed();
            }
            else
            {
                _cursorX = _width - 1;
            }
        }

        GetCell(_cursorX, _cursorY) = new TerminalCell(c, _currentForeground, _currentBackground, _currentAttributes, 1);
        _cursorX++;
    }

    /// <inheritdoc/>
    public void Execute(byte controlCode)
    {
        switch (controlCode)
        {
            case 0x07: // BEL
                // Could trigger bell callback
                break;
            case 0x08: // BS - Backspace
                if (_cursorX > 0) _cursorX--;
                break;
            case 0x09: // HT - Horizontal Tab
                _cursorX = Math.Min((_cursorX / 8 + 1) * 8, _width - 1);
                break;
            case 0x0A: // LF - Line Feed
            case 0x0B: // VT - Vertical Tab
            case 0x0C: // FF - Form Feed
                LineFeed();
                break;
            case 0x0D: // CR - Carriage Return
                _cursorX = 0;
                break;
        }
    }

    /// <inheritdoc/>
    public void CsiDispatch(ReadOnlySpan<int> parameters, byte privateMarker, byte intermediates, char command)
    {
        // Check for private mode sequences (privateMarker contains '?', '>', etc.)
        bool isPrivate = privateMarker == '?';

        switch (command)
        {
            case 'A': // CUU - Cursor Up
                CursorUp(GetParam(parameters, 0, 1));
                break;
            case 'B': // CUD - Cursor Down
                CursorDown(GetParam(parameters, 0, 1));
                break;
            case 'C': // CUF - Cursor Forward
                CursorForward(GetParam(parameters, 0, 1));
                break;
            case 'D': // CUB - Cursor Back
                CursorBack(GetParam(parameters, 0, 1));
                break;
            case 'E': // CNL - Cursor Next Line
                _cursorX = 0;
                CursorDown(GetParam(parameters, 0, 1));
                break;
            case 'F': // CPL - Cursor Previous Line
                _cursorX = 0;
                CursorUp(GetParam(parameters, 0, 1));
                break;
            case 'G': // CHA - Cursor Horizontal Absolute
                _cursorX = Math.Clamp(GetParam(parameters, 0, 1) - 1, 0, _width - 1);
                break;
            case 'H': // CUP - Cursor Position
            case 'f': // HVP - Horizontal Vertical Position
                SetCursorPosition(
                    GetParam(parameters, 1, 1) - 1,
                    GetParam(parameters, 0, 1) - 1);
                break;
            case 'J': // ED - Erase in Display
                EraseInDisplay(GetParam(parameters, 0, 0));
                break;
            case 'K': // EL - Erase in Line
                EraseInLine(GetParam(parameters, 0, 0));
                break;
            case 'L': // IL - Insert Lines
                InsertLines(GetParam(parameters, 0, 1));
                break;
            case 'M': // DL - Delete Lines
                DeleteLines(GetParam(parameters, 0, 1));
                break;
            case 'P': // DCH - Delete Characters
                DeleteCharacters(GetParam(parameters, 0, 1));
                break;
            case 'S': // SU - Scroll Up
                ScrollUp(GetParam(parameters, 0, 1));
                break;
            case 'T': // SD - Scroll Down
                ScrollDown(GetParam(parameters, 0, 1));
                break;
            case 'X': // ECH - Erase Characters
                EraseCharacters(GetParam(parameters, 0, 1));
                break;
            case 'd': // VPA - Vertical Position Absolute
                _cursorY = Math.Clamp(GetParam(parameters, 0, 1) - 1, 0, _height - 1);
                break;
            case 'h': // SM/DECSET - Set Mode
                SetMode(parameters, isPrivate, true);
                break;
            case 'l': // RM/DECRST - Reset Mode
                SetMode(parameters, isPrivate, false);
                break;
            case 'm': // SGR - Select Graphic Rendition
                HandleSgr(parameters);
                break;
            case 'r': // DECSTBM - Set Top and Bottom Margins
                SetScrollRegion(
                    GetParam(parameters, 0, 1) - 1,
                    GetParam(parameters, 1, _height) - 1);
                break;
            case 's': // SCOSC - Save Cursor Position
                SaveCursor();
                break;
            case 'u': // SCORC - Restore Cursor Position
                RestoreCursor();
                break;
            case '@': // ICH - Insert Characters
                InsertCharacters(GetParam(parameters, 0, 1));
                break;
        }
    }

    /// <inheritdoc/>
    public void EscDispatch(byte intermediates, char command)
    {
        switch (command)
        {
            case '7': // DECSC - Save Cursor
                SaveCursor();
                break;
            case '8': // DECRC - Restore Cursor
                RestoreCursor();
                break;
            case 'D': // IND - Index (move down, scroll if needed)
                LineFeed();
                break;
            case 'E': // NEL - Next Line
                _cursorX = 0;
                LineFeed();
                break;
            case 'M': // RI - Reverse Index
                ReverseIndex();
                break;
            case 'c': // RIS - Reset to Initial State
                Reset();
                break;
        }
    }

    /// <inheritdoc/>
    public void OscDispatch(int command, ReadOnlySpan<byte> data)
    {
        switch (command)
        {
            case 0: // Set icon name and window title
            case 2: // Set window title
                _title = System.Text.Encoding.UTF8.GetString(data);
                break;
            case 1: // Set icon name (ignored)
                break;
        }
    }

    /// <inheritdoc/>
    public void DcsHook(ReadOnlySpan<int> parameters, byte intermediates, char command)
    {
        // DCS sequences not implemented yet
    }

    /// <inheritdoc/>
    public void DcsPut(byte data)
    {
        // DCS sequences not implemented yet
    }

    /// <inheritdoc/>
    public void DcsUnhook()
    {
        // DCS sequences not implemented yet
    }

    #endregion

    #region Cursor Movement

    private void CursorUp(int count)
    {
        int top = _originMode ? _scrollTop : 0;
        _cursorY = Math.Max(_cursorY - count, top);
    }

    private void CursorDown(int count)
    {
        int bottom = _originMode ? _scrollBottom : _height - 1;
        _cursorY = Math.Min(_cursorY + count, bottom);
    }

    private void CursorForward(int count)
    {
        _cursorX = Math.Min(_cursorX + count, _width - 1);
    }

    private void CursorBack(int count)
    {
        _cursorX = Math.Max(_cursorX - count, 0);
    }

    private void SetCursorPosition(int x, int y)
    {
        int top = _originMode ? _scrollTop : 0;
        int bottom = _originMode ? _scrollBottom : _height - 1;

        _cursorX = Math.Clamp(x, 0, _width - 1);
        _cursorY = Math.Clamp(y + (_originMode ? _scrollTop : 0), top, bottom);
    }

    private void SaveCursor()
    {
        _savedCursorX = _cursorX;
        _savedCursorY = _cursorY;
        _savedForeground = _currentForeground;
        _savedBackground = _currentBackground;
        _savedAttributes = _currentAttributes;
    }

    private void RestoreCursor()
    {
        _cursorX = _savedCursorX;
        _cursorY = _savedCursorY;
        _currentForeground = _savedForeground;
        _currentBackground = _savedBackground;
        _currentAttributes = _savedAttributes;
    }

    #endregion

    #region Scrolling

    private void LineFeed()
    {
        if (_cursorY >= _scrollBottom)
        {
            ScrollUp(1);
        }
        else
        {
            _cursorY++;
        }
    }

    private void ReverseIndex()
    {
        if (_cursorY <= _scrollTop)
        {
            ScrollDown(1);
        }
        else
        {
            _cursorY--;
        }
    }

    private void ScrollUp(int lines)
    {
        if (lines <= 0) return;
        lines = Math.Min(lines, _scrollBottom - _scrollTop + 1);

        // Move lines up
        for (int y = _scrollTop; y <= _scrollBottom - lines; y++)
        {
            var src = GetRow(y + lines);
            var dst = GetRow(y);
            src.CopyTo(dst);
        }

        // Clear bottom lines
        for (int y = _scrollBottom - lines + 1; y <= _scrollBottom; y++)
        {
            ClearRow(y);
        }
    }

    private void ScrollDown(int lines)
    {
        if (lines <= 0) return;
        lines = Math.Min(lines, _scrollBottom - _scrollTop + 1);

        // Move lines down
        for (int y = _scrollBottom; y >= _scrollTop + lines; y--)
        {
            var src = GetRow(y - lines);
            var dst = GetRow(y);
            src.CopyTo(dst);
        }

        // Clear top lines
        for (int y = _scrollTop; y < _scrollTop + lines; y++)
        {
            ClearRow(y);
        }
    }

    private void SetScrollRegion(int top, int bottom)
    {
        _scrollTop = Math.Clamp(top, 0, _height - 1);
        _scrollBottom = Math.Clamp(bottom, _scrollTop, _height - 1);
        SetCursorPosition(0, 0);
    }

    #endregion

    #region Erase Operations

    private void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // Erase from cursor to end of screen
                EraseInLine(0);
                for (int y = _cursorY + 1; y < _height; y++)
                    ClearRow(y);
                break;
            case 1: // Erase from start of screen to cursor
                for (int y = 0; y < _cursorY; y++)
                    ClearRow(y);
                EraseInLine(1);
                break;
            case 2: // Erase entire screen
            case 3: // Erase entire screen + scrollback
                for (int y = 0; y < _height; y++)
                    ClearRow(y);
                break;
        }
    }

    private void EraseInLine(int mode)
    {
        var row = GetRow(_cursorY);
        switch (mode)
        {
            case 0: // Erase from cursor to end of line
                for (int x = _cursorX; x < _width; x++)
                    row[x] = CreateBlankCell();
                break;
            case 1: // Erase from start of line to cursor
                for (int x = 0; x <= _cursorX; x++)
                    row[x] = CreateBlankCell();
                break;
            case 2: // Erase entire line
                ClearRow(_cursorY);
                break;
        }
    }

    private void EraseCharacters(int count)
    {
        var row = GetRow(_cursorY);
        count = Math.Min(count, _width - _cursorX);
        for (int i = 0; i < count; i++)
        {
            row[_cursorX + i] = CreateBlankCell();
        }
    }

    private void ClearRow(int y)
    {
        var row = GetRow(y);
        var blank = CreateBlankCell();
        for (int x = 0; x < _width; x++)
        {
            row[x] = blank;
        }
    }

    private TerminalCell CreateBlankCell()
    {
        return new TerminalCell
        {
            Character = ' ',
            Foreground = _currentForeground,
            Background = _currentBackground,
            Attributes = CellAttributes.None,
            Width = 1
        };
    }

    #endregion

    #region Insert/Delete

    private void InsertCharacters(int count)
    {
        var row = GetRow(_cursorY);
        count = Math.Min(count, _width - _cursorX);

        // Shift characters right
        for (int x = _width - 1; x >= _cursorX + count; x--)
        {
            row[x] = row[x - count];
        }

        // Insert blanks
        var blank = CreateBlankCell();
        for (int x = _cursorX; x < _cursorX + count; x++)
        {
            row[x] = blank;
        }
    }

    private void DeleteCharacters(int count)
    {
        var row = GetRow(_cursorY);
        count = Math.Min(count, _width - _cursorX);

        // Shift characters left
        for (int x = _cursorX; x < _width - count; x++)
        {
            row[x] = row[x + count];
        }

        // Fill end with blanks
        var blank = CreateBlankCell();
        for (int x = _width - count; x < _width; x++)
        {
            row[x] = blank;
        }
    }

    private void InsertLines(int count)
    {
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom) return;

        int savedTop = _scrollTop;
        _scrollTop = _cursorY;
        ScrollDown(count);
        _scrollTop = savedTop;
    }

    private void DeleteLines(int count)
    {
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom) return;

        int savedTop = _scrollTop;
        _scrollTop = _cursorY;
        ScrollUp(count);
        _scrollTop = savedTop;
    }

    #endregion

    #region SGR (Select Graphic Rendition)

    private void HandleSgr(ReadOnlySpan<int> parameters)
    {
        if (parameters.Length == 0)
        {
            ResetAttributes();
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            int p = parameters[i];

            switch (p)
            {
                case 0: // Reset
                    ResetAttributes();
                    break;
                case 1: // Bold
                    _currentAttributes |= CellAttributes.Bold;
                    break;
                case 2: // Dim
                    _currentAttributes |= CellAttributes.Dim;
                    break;
                case 3: // Italic
                    _currentAttributes |= CellAttributes.Italic;
                    break;
                case 4: // Underline
                    _currentAttributes |= CellAttributes.Underline;
                    break;
                case 5: // Blink
                    _currentAttributes |= CellAttributes.Blink;
                    break;
                case 7: // Inverse
                    _currentAttributes |= CellAttributes.Inverse;
                    break;
                case 8: // Hidden
                    _currentAttributes |= CellAttributes.Hidden;
                    break;
                case 9: // Strikethrough
                    _currentAttributes |= CellAttributes.Strikethrough;
                    break;
                case 22: // Normal intensity (not bold, not dim)
                    _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                    break;
                case 23: // Not italic
                    _currentAttributes &= ~CellAttributes.Italic;
                    break;
                case 24: // Not underline
                    _currentAttributes &= ~CellAttributes.Underline;
                    break;
                case 25: // Not blink
                    _currentAttributes &= ~CellAttributes.Blink;
                    break;
                case 27: // Not inverse
                    _currentAttributes &= ~CellAttributes.Inverse;
                    break;
                case 28: // Not hidden
                    _currentAttributes &= ~CellAttributes.Hidden;
                    break;
                case 29: // Not strikethrough
                    _currentAttributes &= ~CellAttributes.Strikethrough;
                    break;

                // Standard foreground colors (30-37)
                case >= 30 and <= 37:
                    _currentForeground = (uint)(p - 30);
                    break;
                case 38: // Extended foreground
                    i = HandleExtendedColor(parameters, i, ref _currentForeground);
                    break;
                case 39: // Default foreground
                    _currentForeground = TerminalCell.DefaultForeground;
                    break;

                // Standard background colors (40-47)
                case >= 40 and <= 47:
                    _currentBackground = (uint)(p - 40);
                    break;
                case 48: // Extended background
                    i = HandleExtendedColor(parameters, i, ref _currentBackground);
                    break;
                case 49: // Default background
                    _currentBackground = TerminalCell.DefaultBackground;
                    break;

                // Bright foreground colors (90-97)
                case >= 90 and <= 97:
                    _currentForeground = (uint)(p - 90 + 8);
                    break;

                // Bright background colors (100-107)
                case >= 100 and <= 107:
                    _currentBackground = (uint)(p - 100 + 8);
                    break;
            }
        }
    }

    private int HandleExtendedColor(ReadOnlySpan<int> parameters, int index, ref uint color)
    {
        if (index + 1 >= parameters.Length) return index;

        int mode = parameters[index + 1];
        if (mode == 5 && index + 2 < parameters.Length)
        {
            // 256 color: 38;5;n or 48;5;n
            color = (uint)parameters[index + 2];
            return index + 2;
        }
        else if (mode == 2 && index + 4 < parameters.Length)
        {
            // True color: 38;2;r;g;b or 48;2;r;g;b
            uint r = (uint)Math.Clamp(parameters[index + 2], 0, 255);
            uint g = (uint)Math.Clamp(parameters[index + 3], 0, 255);
            uint b = (uint)Math.Clamp(parameters[index + 4], 0, 255);
            color = 0x1000000 | (r << 16) | (g << 8) | b; // Mark as RGB with high bit
            return index + 4;
        }

        return index;
    }

    private void ResetAttributes()
    {
        _currentForeground = TerminalCell.DefaultForeground;
        _currentBackground = TerminalCell.DefaultBackground;
        _currentAttributes = CellAttributes.None;
    }

    #endregion

    #region Modes

    private void SetMode(ReadOnlySpan<int> parameters, bool isPrivate, bool enable)
    {
        foreach (int mode in parameters)
        {
            if (isPrivate)
            {
                switch (mode)
                {
                    case 6: // DECOM - Origin Mode
                        _originMode = enable;
                        if (enable) SetCursorPosition(0, 0);
                        break;
                    case 7: // DECAWM - Auto Wrap Mode
                        _autoWrapMode = enable;
                        break;
                    case 25: // DECTCEM - Cursor Visible
                        _cursorVisible = enable;
                        break;
                    case 1049: // Alternate screen buffer
                        // Would need separate buffer implementation
                        break;
                }
            }
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Clears the entire screen and resets cursor to home.
    /// </summary>
    public void Clear()
    {
        var blank = TerminalCell.Blank;
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = blank;
        }
        _cursorX = 0;
        _cursorY = 0;
    }

    /// <summary>
    /// Resets the terminal to initial state.
    /// </summary>
    public void Reset()
    {
        Clear();
        ResetAttributes();
        _scrollTop = 0;
        _scrollBottom = _height - 1;
        _originMode = false;
        _autoWrapMode = true;
        _cursorVisible = true;
        _title = "";
    }

    private static int GetParam(ReadOnlySpan<int> parameters, int index, int defaultValue)
    {
        if (index >= parameters.Length) return defaultValue;
        int value = parameters[index];
        return value == 0 ? defaultValue : value;
    }

    #endregion
}
