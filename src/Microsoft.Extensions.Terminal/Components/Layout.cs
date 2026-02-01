// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A container that arranges child components in rows or columns.
/// </summary>
public sealed class Layout : IComponent
{
    private readonly List<(IComponent Component, LayoutSize Size)> _children = new();

    /// <summary>
    /// Gets or sets the direction for arranging children.
    /// </summary>
    public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;

    /// <summary>
    /// Adds a child component with the specified size.
    /// </summary>
    /// <param name="component">The component to add.</param>
    /// <param name="size">The size specification for the component.</param>
    /// <returns>This layout for fluent chaining.</returns>
    public Layout Add(IComponent component, LayoutSize size)
    {
        _children.Add((component, size));
        return this;
    }

    /// <summary>
    /// Removes all children from the layout.
    /// </summary>
    public void Clear()
    {
        _children.Clear();
    }

    /// <summary>
    /// Gets the number of children in the layout.
    /// </summary>
    public int Count => _children.Count;

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        if (_children.Count == 0)
        {
            return;
        }

        Span<LayoutSize> sizes = stackalloc LayoutSize[_children.Count];
        for (int i = 0; i < _children.Count; i++)
        {
            sizes[i] = _children[i].Size;
        }

        Span<Region> regions = stackalloc Region[_children.Count];

        if (Direction == LayoutDirection.Vertical)
        {
            region.SplitRows(sizes, regions);
        }
        else
        {
            region.SplitColumns(sizes, regions);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Component.Render(buffer, regions[i]);
        }
    }
}
