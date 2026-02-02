// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using TerminalParser;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Renders a ScreenBuffer to ANSI escape sequences for display on a terminal.
/// </summary>
internal static class ScreenBufferRenderer
{
    /// <summary>
    /// Renders the entire screen buffer to ANSI escape sequences.
    /// </summary>
    public static byte[] RenderFull(ScreenBuffer buffer)
    {
        var sb = new StringBuilder(buffer.Width * buffer.Height * 2);

        // Clear screen and home cursor
        sb.Append("\x1b[H\x1b[2J");

        uint lastFg = TerminalCell.DefaultForeground;
        uint lastBg = TerminalCell.DefaultBackground;
        CellAttributes lastAttr = CellAttributes.None;

        for (int y = 0; y < buffer.Height; y++)
        {
            // Move to start of line
            sb.Append($"\x1b[{y + 1};1H");

            var row = buffer.GetRow(y);
            for (int x = 0; x < buffer.Width; x++)
            {
                ref readonly var cell = ref row[x];

                // Apply attribute changes
                if (cell.Attributes != lastAttr || cell.Foreground != lastFg || cell.Background != lastBg)
                {
                    AppendSgr(sb, cell.Attributes, cell.Foreground, cell.Background, 
                              lastAttr, lastFg, lastBg);
                    lastAttr = cell.Attributes;
                    lastFg = cell.Foreground;
                    lastBg = cell.Background;
                }

                // Append character
                char c = cell.Character;
                if (c == '\0' || c == ' ')
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        // Reset attributes and position cursor
        sb.Append("\x1b[0m");
        sb.Append($"\x1b[{buffer.CursorY + 1};{buffer.CursorX + 1}H");

        // Show/hide cursor
        sb.Append(buffer.CursorVisible ? "\x1b[?25h" : "\x1b[?25l");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Renders a differential update (only changed cells).
    /// </summary>
    public static byte[] RenderDiff(ScreenBuffer current, ScreenBuffer previous)
    {
        // For now, just do full render. Differential rendering is an optimization.
        return RenderFull(current);
    }

    private static void AppendSgr(StringBuilder sb, 
        CellAttributes attr, uint fg, uint bg,
        CellAttributes prevAttr, uint prevFg, uint prevBg)
    {
        // Build SGR sequence
        sb.Append("\x1b[");
        bool first = true;

        // Check if we need a full reset
        bool needsReset = (prevAttr & ~attr) != 0; // Some attributes were turned off
        if (needsReset)
        {
            sb.Append('0');
            first = false;
            prevAttr = CellAttributes.None;
            prevFg = TerminalCell.DefaultForeground;
            prevBg = TerminalCell.DefaultBackground;
        }

        // Apply new attributes
        if ((attr & CellAttributes.Bold) != 0 && (prevAttr & CellAttributes.Bold) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('1');
            first = false;
        }
        if ((attr & CellAttributes.Dim) != 0 && (prevAttr & CellAttributes.Dim) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('2');
            first = false;
        }
        if ((attr & CellAttributes.Italic) != 0 && (prevAttr & CellAttributes.Italic) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('3');
            first = false;
        }
        if ((attr & CellAttributes.Underline) != 0 && (prevAttr & CellAttributes.Underline) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('4');
            first = false;
        }
        if ((attr & CellAttributes.Blink) != 0 && (prevAttr & CellAttributes.Blink) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('5');
            first = false;
        }
        if ((attr & CellAttributes.Inverse) != 0 && (prevAttr & CellAttributes.Inverse) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('7');
            first = false;
        }
        if ((attr & CellAttributes.Strikethrough) != 0 && (prevAttr & CellAttributes.Strikethrough) == 0)
        {
            if (!first) sb.Append(';');
            sb.Append('9');
            first = false;
        }

        // Foreground color
        if (fg != prevFg)
        {
            if (!first) sb.Append(';');
            AppendColor(sb, fg, isForeground: true);
            first = false;
        }

        // Background color
        if (bg != prevBg)
        {
            if (!first) sb.Append(';');
            AppendColor(sb, bg, isForeground: false);
            first = false;
        }

        if (first)
        {
            // No changes, append reset anyway to avoid empty sequence
            sb.Append('0');
        }

        sb.Append('m');
    }

    private static void AppendColor(StringBuilder sb, uint color, bool isForeground)
    {
        int baseCode = isForeground ? 30 : 40;

        if ((color & 0x1000000) != 0)
        {
            // True color (RGB)
            int r = (int)((color >> 16) & 0xFF);
            int g = (int)((color >> 8) & 0xFF);
            int b = (int)(color & 0xFF);
            sb.Append(isForeground ? "38;2;" : "48;2;");
            sb.Append(r);
            sb.Append(';');
            sb.Append(g);
            sb.Append(';');
            sb.Append(b);
        }
        else if (color < 8)
        {
            // Standard color (30-37 or 40-47)
            sb.Append(baseCode + color);
        }
        else if (color < 16)
        {
            // Bright color (90-97 or 100-107)
            sb.Append(baseCode + 60 + color - 8);
        }
        else if (color < 256)
        {
            // 256 color
            sb.Append(isForeground ? "38;5;" : "48;5;");
            sb.Append(color);
        }
        else
        {
            // Default
            sb.Append(isForeground ? "39" : "49");
        }
    }
}
