// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A bordered container with an optional header.
/// </summary>
public sealed class Panel : IComponent
{
    /// <summary>
    /// Gets or sets the content to display inside the panel.
    /// </summary>
    public IComponent? Content { get; set; }

    /// <summary>
    /// Gets or sets the header text.
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the header color.
    /// </summary>
    public TerminalColor HeaderColor { get; set; } = TerminalColor.White;

    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    public BoxBorderStyle Border { get; set; } = BoxBorderStyle.Rounded;

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public TerminalColor BorderColor { get; set; } = TerminalColor.Gray;

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        if (region.Width < 2 || region.Height < 2)
        {
            return;
        }

        if (Border != BoxBorderStyle.None)
        {
            RenderBorder(buffer, region);
        }

        // Draw header if present
        if (!string.IsNullOrEmpty(Header))
        {
            int headerX = region.X + 2;
            int maxHeaderWidth = region.Width - 4;
            if (maxHeaderWidth > 0)
            {
                string headerText = Header.Length > maxHeaderWidth
                    ? Header[..maxHeaderWidth]
                    : Header;
                buffer.Write(headerX, region.Y, headerText.AsSpan(), HeaderColor);
            }
        }

        // Render content in interior region
        if (Content != null)
        {
            var interior = new Region(
                region.X + 1,
                region.Y + 1,
                region.Width - 2,
                region.Height - 2);

            if (interior.Width > 0 && interior.Height > 0)
            {
                Content.Render(buffer, interior);
            }
        }
    }

    private void RenderBorder(ScreenBuffer buffer, Region region)
    {
        var (topLeft, topRight, bottomLeft, bottomRight, horizontal, vertical) = BoxChars.GetChars(Border);

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
}
