// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A table with columns and rows, supporting selection and scrolling.
/// </summary>
public sealed class Table : IInteractiveComponent
{
    private readonly List<TableColumn> _columns = new();
    private readonly List<TableRow> _rows = new();
    private int _selectedIndex = -1;
    private int _scrollOffset;
    private int _visibleRowCount;

    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    public TableBorderStyle Border { get; set; } = TableBorderStyle.Rounded;

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public TerminalColor BorderColor { get; set; } = TerminalColor.Gray;

    /// <summary>
    /// Gets or sets the header color.
    /// </summary>
    public TerminalColor HeaderColor { get; set; } = TerminalColor.White;

    /// <summary>
    /// Gets or sets the selected row background highlight color.
    /// </summary>
    public TerminalColor SelectionColor { get; set; } = TerminalColor.DarkCyan;

    /// <summary>
    /// Gets or sets whether rows can be selected.
    /// </summary>
    public bool IsSelectable { get; set; }

    /// <summary>
    /// Gets or sets whether to show the header row.
    /// </summary>
    public bool ShowHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the selected row index.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, -1, _rows.Count - 1);
    }

    /// <summary>
    /// Gets the selected row, if any.
    /// </summary>
    public TableRow? SelectedRow => _selectedIndex >= 0 && _selectedIndex < _rows.Count
        ? _rows[_selectedIndex]
        : null;

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int RowCount => _rows.Count;

    /// <inheritdoc />
    public bool IsFocused { get; set; }

    /// <summary>
    /// Adds a column to the table.
    /// </summary>
    public Table AddColumn(string header, int? width = null, Alignment alignment = Alignment.Left)
    {
        _columns.Add(new TableColumn(header, width, alignment));
        return this;
    }

    /// <summary>
    /// Adds a row with the specified cells.
    /// </summary>
    public Table AddRow(params TableCell[] cells)
    {
        _rows.Add(new TableRow(cells));
        return this;
    }

    /// <summary>
    /// Adds a row with string values.
    /// </summary>
    public Table AddRow(params string[] values)
    {
        var cells = values.Select(v => new TableCell(v)).ToArray();
        _rows.Add(new TableRow(cells));
        return this;
    }

    /// <summary>
    /// Bind the table to a data source with a row mapper.
    /// Clears existing rows and repopulates from the items.
    /// </summary>
    /// <typeparam name="T">The type of items in the data source.</typeparam>
    /// <param name="items">The items to bind.</param>
    /// <param name="rowMapper">A function that maps an item and its index to a TableRow.</param>
    public void Bind<T>(IReadOnlyList<T> items, Func<T, int, TableRow> rowMapper)
    {
        int previousSelection = _selectedIndex;
        Clear();

        for (int i = 0; i < items.Count; i++)
        {
            _rows.Add(rowMapper(items[i], i));
        }

        // Restore selection if possible
        if (previousSelection >= 0 && previousSelection < _rows.Count)
        {
            _selectedIndex = previousSelection;
        }
        else if (_rows.Count > 0 && IsSelectable)
        {
            _selectedIndex = Math.Min(previousSelection, _rows.Count - 1);
            if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
            }
        }
    }

    /// <summary>
    /// Bind the table to a data source with a cell array mapper.
    /// Clears existing rows and repopulates from the items.
    /// </summary>
    /// <typeparam name="T">The type of items in the data source.</typeparam>
    /// <param name="items">The items to bind.</param>
    /// <param name="cellMapper">A function that maps an item to an array of TableCells.</param>
    public void Bind<T>(IReadOnlyList<T> items, Func<T, TableCell[]> cellMapper)
    {
        Bind(items, (item, _) => new TableRow(cellMapper(item)));
    }

    /// <summary>
    /// Clears all rows.
    /// </summary>
    public void Clear()
    {
        _rows.Clear();
        _selectedIndex = -1;
        _scrollOffset = 0;
    }

    /// <summary>
    /// Clears all columns and rows.
    /// </summary>
    public void ClearAll()
    {
        _columns.Clear();
        Clear();
    }

    /// <inheritdoc />
    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!IsSelectable || _rows.Count == 0)
        {
            return false;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.K:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    EnsureVisible(_selectedIndex);
                    return true;
                }
                break;

            case ConsoleKey.DownArrow:
            case ConsoleKey.J:
                if (_selectedIndex < _rows.Count - 1)
                {
                    _selectedIndex++;
                    EnsureVisible(_selectedIndex);
                    return true;
                }
                break;

            case ConsoleKey.PageUp:
                _selectedIndex = Math.Max(0, _selectedIndex - _visibleRowCount);
                EnsureVisible(_selectedIndex);
                return true;

            case ConsoleKey.PageDown:
                _selectedIndex = Math.Min(_rows.Count - 1, _selectedIndex + _visibleRowCount);
                EnsureVisible(_selectedIndex);
                return true;

            case ConsoleKey.Home:
                _selectedIndex = 0;
                EnsureVisible(_selectedIndex);
                return true;

            case ConsoleKey.End:
                _selectedIndex = _rows.Count - 1;
                EnsureVisible(_selectedIndex);
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        if (region.Width < 3 || region.Height < 3)
        {
            return;
        }

        // Calculate layout
        bool hasBorder = Border != TableBorderStyle.None;
        int borderOffset = hasBorder ? 1 : 0;
        int headerHeight = ShowHeader ? 1 : 0;
        int headerSeparatorHeight = ShowHeader && hasBorder ? 1 : 0;

        int contentStartY = region.Y + borderOffset + headerHeight + headerSeparatorHeight;
        int contentHeight = region.Height - (borderOffset * 2) - headerHeight - headerSeparatorHeight;
        _visibleRowCount = Math.Max(1, contentHeight);

        // Calculate column widths
        int[] columnWidths = CalculateColumnWidths(region.Width - (borderOffset * 2));

        // Render border
        if (hasBorder)
        {
            RenderBorder(buffer, region);
        }

        // Render header
        if (ShowHeader)
        {
            RenderHeader(buffer, region, columnWidths, borderOffset);

            // Header separator
            if (hasBorder)
            {
                RenderHeaderSeparator(buffer, region, columnWidths, borderOffset);
            }
        }

        // Render visible rows
        for (int i = 0; i < _visibleRowCount && i + _scrollOffset < _rows.Count; i++)
        {
            int rowIndex = i + _scrollOffset;
            bool isSelected = rowIndex == _selectedIndex && IsFocused;
            RenderRow(buffer, region, contentStartY + i, _rows[rowIndex], columnWidths, borderOffset, isSelected);
        }

        // Render scroll indicators
        if (_scrollOffset > 0)
        {
            RenderScrollIndicator(buffer, region, ScrollDirection.Up, borderOffset);
        }
        if (_scrollOffset + _visibleRowCount < _rows.Count)
        {
            RenderScrollIndicator(buffer, region, ScrollDirection.Down, borderOffset);
        }
    }

    private int[] CalculateColumnWidths(int availableWidth)
    {
        if (_columns.Count == 0)
        {
            return Array.Empty<int>();
        }

        int[] widths = new int[_columns.Count];
        int separatorWidth = _columns.Count - 1; // Space between columns
        int remainingWidth = availableWidth - separatorWidth;

        int fixedWidth = 0;
        int flexCount = 0;

        for (int i = 0; i < _columns.Count; i++)
        {
            int? columnWidth = _columns[i].Width;
            if (columnWidth.HasValue)
            {
                widths[i] = columnWidth.Value;
                fixedWidth += widths[i];
            }
            else
            {
                flexCount++;
            }
        }

        int flexWidth = flexCount > 0 ? Math.Max(1, (remainingWidth - fixedWidth) / flexCount) : 0;

        for (int i = 0; i < _columns.Count; i++)
        {
            if (!_columns[i].Width.HasValue)
            {
                widths[i] = flexWidth;
            }
        }

        return widths;
    }

    private void RenderBorder(ScreenBuffer buffer, Region region)
    {
        var style = Border switch
        {
            TableBorderStyle.Rounded => BoxBorderStyle.Rounded,
            TableBorderStyle.Simple => BoxBorderStyle.Simple,
            TableBorderStyle.Double => BoxBorderStyle.Double,
            _ => BoxBorderStyle.None
        };

        var (topLeft, topRight, bottomLeft, bottomRight, horizontal, vertical) = BoxChars.GetChars(style);

        // Top border
        buffer.Write(region.X, region.Y, topLeft, BorderColor);
        for (int x = region.X + 1; x < region.X + region.Width - 1; x++)
        {
            buffer.Write(x, region.Y, horizontal, BorderColor);
        }
        buffer.Write(region.X + region.Width - 1, region.Y, topRight, BorderColor);

        // Side borders
        for (int y = region.Y + 1; y < region.Y + region.Height - 1; y++)
        {
            buffer.Write(region.X, y, vertical, BorderColor);
            buffer.Write(region.X + region.Width - 1, y, vertical, BorderColor);
        }

        // Bottom border
        buffer.Write(region.X, region.Y + region.Height - 1, bottomLeft, BorderColor);
        for (int x = region.X + 1; x < region.X + region.Width - 1; x++)
        {
            buffer.Write(x, region.Y + region.Height - 1, horizontal, BorderColor);
        }
        buffer.Write(region.X + region.Width - 1, region.Y + region.Height - 1, bottomRight, BorderColor);
    }

    private void RenderHeader(ScreenBuffer buffer, Region region, int[] columnWidths, int borderOffset)
    {
        int x = region.X + borderOffset;
        int y = region.Y + borderOffset;

        for (int i = 0; i < _columns.Count && i < columnWidths.Length; i++)
        {
            string header = FormatCell(_columns[i].Header, columnWidths[i], _columns[i].Alignment);
            buffer.Write(x, y, header.AsSpan(), HeaderColor);
            x += columnWidths[i];

            if (i < _columns.Count - 1)
            {
                buffer.Write(x, y, ' ', BorderColor);
                x++;
            }
        }
    }

    private void RenderHeaderSeparator(ScreenBuffer buffer, Region region, int[] columnWidths, int borderOffset)
    {
        var style = Border switch
        {
            TableBorderStyle.Rounded => BoxBorderStyle.Rounded,
            TableBorderStyle.Simple => BoxBorderStyle.Simple,
            TableBorderStyle.Double => BoxBorderStyle.Double,
            _ => BoxBorderStyle.None
        };

        var (_, _, _, _, horizontal, vertical) = BoxChars.GetChars(style);
        char leftT = Border switch
        {
            TableBorderStyle.Rounded or TableBorderStyle.Simple => BoxChars.Simple.LeftT,
            TableBorderStyle.Double => BoxChars.Double.LeftT,
            _ => horizontal
        };
        char rightT = Border switch
        {
            TableBorderStyle.Rounded or TableBorderStyle.Simple => BoxChars.Simple.RightT,
            TableBorderStyle.Double => BoxChars.Double.RightT,
            _ => horizontal
        };

        int y = region.Y + borderOffset + 1;

        buffer.Write(region.X, y, leftT, BorderColor);
        for (int x = region.X + 1; x < region.X + region.Width - 1; x++)
        {
            buffer.Write(x, y, horizontal, BorderColor);
        }
        buffer.Write(region.X + region.Width - 1, y, rightT, BorderColor);
    }

    private void RenderRow(ScreenBuffer buffer, Region region, int y, TableRow row, int[] columnWidths, int borderOffset, bool isSelected)
    {
        int x = region.X + borderOffset;
        int maxX = region.X + region.Width - borderOffset;

        // Clear the row area first if selected
        if (isSelected)
        {
            for (int clearX = x; clearX < maxX; clearX++)
            {
                buffer.Write(clearX, y, ' ', SelectionColor);
            }
        }

        for (int i = 0; i < columnWidths.Length; i++)
        {
            if (x >= maxX)
            {
                break;
            }

            TableCell cell = i < row.Cells.Length ? row.Cells[i] : new TableCell("");
            Alignment alignment = i < _columns.Count ? _columns[i].Alignment : Alignment.Left;
            string text = FormatCell(cell.Text, columnWidths[i], alignment);

            TerminalColor color = isSelected ? SelectionColor : (cell.Color ?? TerminalColor.Default);
            buffer.Write(x, y, text.AsSpan(), color);
            x += columnWidths[i];

            if (i < columnWidths.Length - 1 && x < maxX)
            {
                buffer.Write(x, y, ' ', isSelected ? SelectionColor : TerminalColor.Default);
                x++;
            }
        }
    }

    private static string FormatCell(string text, int width, Alignment alignment)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        if (text.Length > width)
        {
            return text[..width];
        }

        return alignment switch
        {
            Alignment.Right => text.PadLeft(width),
            Alignment.Center => text.PadLeft((width + text.Length) / 2).PadRight(width),
            _ => text.PadRight(width)
        };
    }

    private void RenderScrollIndicator(ScreenBuffer buffer, Region region, ScrollDirection direction, int borderOffset)
    {
        char indicator = direction == ScrollDirection.Up ? '▲' : '▼';
        int x = region.X + region.Width - borderOffset - 2;
        int y = direction == ScrollDirection.Up
            ? region.Y + borderOffset + (ShowHeader ? 2 : 0)
            : region.Y + region.Height - borderOffset - 1;

        buffer.Write(x, y, indicator, TerminalColor.DarkGray);
    }

    private void EnsureVisible(int index)
    {
        if (index < _scrollOffset)
        {
            _scrollOffset = index;
        }
        else if (index >= _scrollOffset + _visibleRowCount)
        {
            _scrollOffset = index - _visibleRowCount + 1;
        }
    }
}
