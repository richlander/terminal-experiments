// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Defines a rectangular region of the screen.
/// </summary>
public readonly record struct Region(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Creates a region from the terminal dimensions.
    /// </summary>
    /// <param name="terminal">The terminal to get dimensions from.</param>
    /// <returns>A region covering the entire terminal.</returns>
    public static Region FromTerminal(ITerminal terminal)
        => new(0, 0, terminal.Width, terminal.Height);

    /// <summary>
    /// Split this region horizontally into rows.
    /// </summary>
    /// <param name="sizes">The sizes for each row.</param>
    /// <param name="results">The resulting regions.</param>
    public void SplitRows(ReadOnlySpan<LayoutSize> sizes, Span<Region> results)
    {
        int totalFixed = 0;
        int totalPercent = 0;
        int fillCount = 0;

        foreach (var size in sizes)
        {
            switch (size.Kind)
            {
                case LayoutSizeKind.Fixed:
                    totalFixed += size.Value;
                    break;
                case LayoutSizeKind.Percent:
                    totalPercent += size.Value;
                    break;
                case LayoutSizeKind.Fill:
                    fillCount++;
                    break;
            }
        }

        int percentHeight = Height * totalPercent / 100;
        int remainingHeight = Math.Max(0, Height - totalFixed - percentHeight);
        int fillHeight = fillCount > 0 ? remainingHeight / fillCount : 0;

        int currentY = Y;
        for (int i = 0; i < sizes.Length && i < results.Length; i++)
        {
            int rowHeight = sizes[i].Kind switch
            {
                LayoutSizeKind.Fixed => sizes[i].Value,
                LayoutSizeKind.Percent => Height * sizes[i].Value / 100,
                LayoutSizeKind.Fill => fillHeight,
                _ => 0
            };

            // Clamp to remaining space
            rowHeight = Math.Min(rowHeight, Height - (currentY - Y));
            results[i] = new Region(X, currentY, Width, rowHeight);
            currentY += rowHeight;
        }
    }

    /// <summary>
    /// Split this region vertically into columns.
    /// </summary>
    /// <param name="sizes">The sizes for each column.</param>
    /// <param name="results">The resulting regions.</param>
    public void SplitColumns(ReadOnlySpan<LayoutSize> sizes, Span<Region> results)
    {
        int totalFixed = 0;
        int totalPercent = 0;
        int fillCount = 0;

        foreach (var size in sizes)
        {
            switch (size.Kind)
            {
                case LayoutSizeKind.Fixed:
                    totalFixed += size.Value;
                    break;
                case LayoutSizeKind.Percent:
                    totalPercent += size.Value;
                    break;
                case LayoutSizeKind.Fill:
                    fillCount++;
                    break;
            }
        }

        int percentWidth = Width * totalPercent / 100;
        int remainingWidth = Math.Max(0, Width - totalFixed - percentWidth);
        int fillWidth = fillCount > 0 ? remainingWidth / fillCount : 0;

        int currentX = X;
        for (int i = 0; i < sizes.Length && i < results.Length; i++)
        {
            int colWidth = sizes[i].Kind switch
            {
                LayoutSizeKind.Fixed => sizes[i].Value,
                LayoutSizeKind.Percent => Width * sizes[i].Value / 100,
                LayoutSizeKind.Fill => fillWidth,
                _ => 0
            };

            // Clamp to remaining space
            colWidth = Math.Min(colWidth, Width - (currentX - X));
            results[i] = new Region(currentX, Y, colWidth, Height);
            currentX += colWidth;
        }
    }
}
