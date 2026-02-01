// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A screen buffer that tracks dirty regions for efficient partial updates.
/// </summary>
public sealed class ScreenBuffer
{
    private readonly char[] _current;
    private readonly char[] _previous;
    private readonly TerminalColor[] _currentColors;
    private readonly TerminalColor[] _previousColors;
    private readonly BitArray _dirty;
    private bool _fullInvalidate;

    /// <summary>
    /// Gets the width of the buffer in characters.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the buffer in characters.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenBuffer"/> class.
    /// </summary>
    /// <param name="width">The width of the buffer in characters.</param>
    /// <param name="height">The height of the buffer in characters.</param>
    public ScreenBuffer(int width, int height)
    {
        Width = width;
        Height = height;

        int size = width * height;
        _current = new char[size];
        _previous = new char[size];
        _currentColors = new TerminalColor[size];
        _previousColors = new TerminalColor[size];
        _dirty = new BitArray(size);

        // Initialize with spaces
        Array.Fill(_current, ' ');
        Array.Fill(_previous, ' ');
        Array.Fill(_currentColors, TerminalColor.Default);
        Array.Fill(_previousColors, TerminalColor.Default);

        _fullInvalidate = true;
    }

    /// <summary>
    /// Write text at a position, marking cells as dirty if changed.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="color">The color to use.</param>
    public void Write(int x, int y, ReadOnlySpan<char> text, TerminalColor color = TerminalColor.Default)
    {
        if (y < 0 || y >= Height)
        {
            return;
        }

        for (int i = 0; i < text.Length; i++)
        {
            int currentX = x + i;
            if (currentX < 0 || currentX >= Width)
            {
                continue;
            }

            int index = y * Width + currentX;
            char ch = text[i];

            if (_current[index] != ch || _currentColors[index] != color)
            {
                _current[index] = ch;
                _currentColors[index] = color;
                _dirty[index] = true;
            }
        }
    }

    /// <summary>
    /// Write a single character at a position.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="ch">The character to write.</param>
    /// <param name="color">The color to use.</param>
    public void Write(int x, int y, char ch, TerminalColor color = TerminalColor.Default)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        int index = y * Width + x;
        if (_current[index] != ch || _currentColors[index] != color)
        {
            _current[index] = ch;
            _currentColors[index] = color;
            _dirty[index] = true;
        }
    }

    /// <summary>
    /// Clear a rectangular region.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="width">The width to clear.</param>
    /// <param name="height">The height to clear.</param>
    public void Clear(int x, int y, int width, int height)
    {
        for (int row = y; row < y + height && row < Height; row++)
        {
            if (row < 0)
            {
                continue;
            }

            for (int col = x; col < x + width && col < Width; col++)
            {
                if (col < 0)
                {
                    continue;
                }

                int index = row * Width + col;
                if (_current[index] != ' ' || _currentColors[index] != TerminalColor.Default)
                {
                    _current[index] = ' ';
                    _currentColors[index] = TerminalColor.Default;
                    _dirty[index] = true;
                }
            }
        }
    }

    /// <summary>
    /// Clear the entire buffer.
    /// </summary>
    public void Clear()
    {
        Clear(0, 0, Width, Height);
    }

    /// <summary>
    /// Flush only dirty cells to the terminal.
    /// </summary>
    /// <param name="terminal">The terminal to write to.</param>
    public void Flush(ITerminal terminal)
    {
        terminal.StartUpdate();

        try
        {
            TerminalColor currentColor = TerminalColor.Default;

            for (int y = 0; y < Height; y++)
            {
                int runStartX = -1;
                TerminalColor runColor = TerminalColor.Default;

                for (int x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    bool needsUpdate = _fullInvalidate || _dirty[index];

                    if (needsUpdate)
                    {
                        if (runStartX == -1)
                        {
                            runStartX = x;
                            runColor = _currentColors[index];
                        }
                        else if (_currentColors[index] != runColor)
                        {
                            // Flush the current run
                            FlushRun(terminal, runStartX, y, runColor, ref currentColor);
                            runStartX = x;
                            runColor = _currentColors[index];
                        }
                    }
                    else if (runStartX != -1)
                    {
                        // Flush the current run before a gap
                        FlushRun(terminal, runStartX, y, runColor, ref currentColor);
                        runStartX = -1;
                    }
                }

                // Flush any remaining run
                if (runStartX != -1)
                {
                    FlushRun(terminal, runStartX, y, runColor, ref currentColor);
                }
            }

            // Reset color if changed
            if (currentColor != TerminalColor.Default)
            {
                terminal.ResetColor();
            }

            // Copy current to previous and clear dirty flags
            Array.Copy(_current, _previous, _current.Length);
            Array.Copy(_currentColors, _previousColors, _currentColors.Length);
            _dirty.SetAll(false);
            _fullInvalidate = false;
        }
        finally
        {
            terminal.StopUpdate();
        }
    }

    private void FlushRun(ITerminal terminal, int startX, int y, TerminalColor color, ref TerminalColor currentColor)
    {
        // Position cursor
        terminal.Append($"{AnsiCodes.CSI}{y + 1};{startX + 1}H");

        // Set color if needed
        if (color != currentColor)
        {
            if (color == TerminalColor.Default)
            {
                terminal.ResetColor();
            }
            else
            {
                terminal.SetColor(color);
            }
            currentColor = color;
        }

        // Find end of run with same color
        int endX = startX;
        while (endX < Width)
        {
            int index = y * Width + endX;
            bool needsUpdate = _fullInvalidate || _dirty[index];
            if (!needsUpdate || _currentColors[index] != color)
            {
                break;
            }
            endX++;
        }

        // Write the run
        for (int x = startX; x < endX; x++)
        {
            terminal.Append(_current[y * Width + x]);
        }
    }

    /// <summary>
    /// Force full redraw on next flush (e.g., after terminal resize).
    /// </summary>
    public void Invalidate()
    {
        _fullInvalidate = true;
    }

    /// <summary>
    /// Resize the buffer. Contents are cleared.
    /// </summary>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    /// <returns>A new ScreenBuffer with the specified dimensions.</returns>
    public static ScreenBuffer Resize(int width, int height)
    {
        return new ScreenBuffer(width, height);
    }
}
