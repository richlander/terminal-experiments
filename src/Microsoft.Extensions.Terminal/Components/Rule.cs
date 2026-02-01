// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A horizontal line, optionally with a title.
/// </summary>
public sealed class Rule : IComponent
{
    /// <summary>
    /// Gets or sets the optional title text.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the color of the rule.
    /// </summary>
    public TerminalColor Color { get; set; } = TerminalColor.Gray;

    /// <summary>
    /// Gets or sets the character used for the line.
    /// </summary>
    public char LineChar { get; set; } = '─';

    /// <inheritdoc />
    public Size? PreferredSize => new Size(0, 1);

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        if (region.Height < 1 || region.Width < 1)
        {
            return;
        }

        if (string.IsNullOrEmpty(Title))
        {
            // Just draw a line
            for (int x = region.X; x < region.X + region.Width; x++)
            {
                buffer.Write(x, region.Y, LineChar, Color);
            }
        }
        else
        {
            // ─── Title ───
            string titleWithPadding = $" {Title} ";
            int remainingWidth = region.Width - titleWithPadding.Length;

            if (remainingWidth <= 0)
            {
                // Title is too long, just show what fits
                string truncated = Title.Length > region.Width ? Title[..region.Width] : Title;
                buffer.Write(region.X, region.Y, truncated.AsSpan(), Color);
            }
            else
            {
                int leftWidth = remainingWidth / 2;
                int rightWidth = remainingWidth - leftWidth;

                int x = region.X;

                // Left line
                for (int i = 0; i < leftWidth; i++)
                {
                    buffer.Write(x++, region.Y, LineChar, Color);
                }

                // Title
                buffer.Write(x, region.Y, titleWithPadding.AsSpan(), Color);
                x += titleWithPadding.Length;

                // Right line
                for (int i = 0; i < rightWidth; i++)
                {
                    buffer.Write(x++, region.Y, LineChar, Color);
                }
            }
        }
    }
}
