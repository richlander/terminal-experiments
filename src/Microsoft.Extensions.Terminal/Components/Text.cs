// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Styled text content.
/// </summary>
public sealed class Text : IComponent
{
    private readonly List<TextSpan> _spans = new();

    /// <summary>
    /// Appends text with the specified color.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <param name="color">The color for the text.</param>
    /// <returns>This Text instance for fluent chaining.</returns>
    public Text Append(string text, TerminalColor color = TerminalColor.Default)
    {
        _spans.Add(new TextSpan(text, color));
        return this;
    }

    /// <summary>
    /// Appends text followed by a newline with the specified color.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <param name="color">The color for the text.</param>
    /// <returns>This Text instance for fluent chaining.</returns>
    public Text AppendLine(string text = "", TerminalColor color = TerminalColor.Default)
    {
        _spans.Add(new TextSpan(text + "\n", color));
        return this;
    }

    /// <summary>
    /// Clears all text spans.
    /// </summary>
    public void Clear() => _spans.Clear();

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        int x = region.X;
        int y = region.Y;

        foreach (var span in _spans)
        {
            foreach (var ch in span.Text)
            {
                if (ch == '\n')
                {
                    x = region.X;
                    y++;
                    continue;
                }

                if (y >= region.Y + region.Height)
                {
                    return;
                }

                if (x >= region.X + region.Width)
                {
                    x = region.X;
                    y++;
                    if (y >= region.Y + region.Height)
                    {
                        return;
                    }
                }

                buffer.Write(x, y, ch, span.Color);
                x++;
            }
        }
    }
}
