// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A tabbed container with keyboard navigation.
/// </summary>
public class TabView : IInteractiveComponent
{
    private readonly List<(string Title, IComponent Content)> _tabs = new();
    private int _selectedIndex;

    /// <summary>
    /// Gets or sets the selected tab index.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, _tabs.Count - 1));
    }

    /// <summary>
    /// Gets the currently selected tab content, if any.
    /// </summary>
    public IComponent? SelectedContent => _tabs.Count > 0 && _selectedIndex < _tabs.Count
        ? _tabs[_selectedIndex].Content
        : null;

    /// <summary>
    /// Gets the number of tabs.
    /// </summary>
    public int Count => _tabs.Count;

    /// <summary>
    /// Gets or sets the color for the active tab.
    /// </summary>
    public TerminalColor ActiveTabColor { get; set; } = TerminalColor.Cyan;

    /// <summary>
    /// Gets or sets the color for inactive tabs.
    /// </summary>
    public TerminalColor InactiveTabColor { get; set; } = TerminalColor.Gray;

    /// <summary>
    /// Gets or sets the separator between tabs.
    /// </summary>
    public string TabSeparator { get; set; } = "  ";

    /// <summary>
    /// Gets or sets whether this component currently has focus.
    /// </summary>
    public bool IsFocused { get; set; }

    /// <summary>
    /// Adds a tab with the specified title and content.
    /// </summary>
    /// <param name="title">The tab title.</param>
    /// <param name="content">The tab content.</param>
    /// <returns>This TabView for fluent chaining.</returns>
    public TabView Add(string title, IComponent content)
    {
        _tabs.Add((title, content));
        return this;
    }

    /// <summary>
    /// Removes all tabs.
    /// </summary>
    public void Clear()
    {
        _tabs.Clear();
        _selectedIndex = 0;
    }

    /// <inheritdoc />
    public bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    return true;
                }
                break;

            case ConsoleKey.RightArrow:
                if (_selectedIndex < _tabs.Count - 1)
                {
                    _selectedIndex++;
                    return true;
                }
                break;
        }

        // Delegate to active tab content if interactive
        if (_tabs.Count > 0 && _selectedIndex < _tabs.Count &&
            _tabs[_selectedIndex].Content is IInteractiveComponent interactive)
        {
            return interactive.HandleKey(key);
        }

        return false;
    }

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        if (region.Height < 2)
        {
            return;
        }

        // Render tab bar (first row)
        int x = region.X;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var isActive = i == _selectedIndex;
            var color = isActive ? ActiveTabColor : InactiveTabColor;
            var title = isActive ? $"[{_tabs[i].Title}]" : $" {_tabs[i].Title} ";

            if (x + title.Length <= region.X + region.Width)
            {
                buffer.Write(x, region.Y, title.AsSpan(), color);
                x += title.Length;
            }

            if (i < _tabs.Count - 1 && x + TabSeparator.Length <= region.X + region.Width)
            {
                buffer.Write(x, region.Y, TabSeparator.AsSpan(), InactiveTabColor);
                x += TabSeparator.Length;
            }
        }

        // Clear rest of tab bar line
        for (int clearX = x; clearX < region.X + region.Width; clearX++)
        {
            buffer.Write(clearX, region.Y, ' ', TerminalColor.Default);
        }

        // Render active tab content
        if (_tabs.Count > 0 && _selectedIndex < _tabs.Count)
        {
            var contentRegion = new Region(region.X, region.Y + 1, region.Width, region.Height - 1);
            _tabs[_selectedIndex].Content.Render(buffer, contentRegion);
        }
    }
}
