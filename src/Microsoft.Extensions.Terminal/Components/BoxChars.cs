// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Box drawing characters for different border styles.
/// </summary>
public static class BoxChars
{
    /// <summary>
    /// Rounded corner box characters.
    /// </summary>
    public static class Rounded
    {
        public const char TopLeft = '╭';
        public const char TopRight = '╮';
        public const char BottomLeft = '╰';
        public const char BottomRight = '╯';
        public const char Horizontal = '─';
        public const char Vertical = '│';
        public const char LeftT = '├';
        public const char RightT = '┤';
        public const char TopT = '┬';
        public const char BottomT = '┴';
        public const char Cross = '┼';
    }

    /// <summary>
    /// Simple single-line box characters.
    /// </summary>
    public static class Simple
    {
        public const char TopLeft = '┌';
        public const char TopRight = '┐';
        public const char BottomLeft = '└';
        public const char BottomRight = '┘';
        public const char Horizontal = '─';
        public const char Vertical = '│';
        public const char LeftT = '├';
        public const char RightT = '┤';
        public const char TopT = '┬';
        public const char BottomT = '┴';
        public const char Cross = '┼';
    }

    /// <summary>
    /// Double-line box characters.
    /// </summary>
    public static class Double
    {
        public const char TopLeft = '╔';
        public const char TopRight = '╗';
        public const char BottomLeft = '╚';
        public const char BottomRight = '╝';
        public const char Horizontal = '═';
        public const char Vertical = '║';
        public const char LeftT = '╠';
        public const char RightT = '╣';
        public const char TopT = '╦';
        public const char BottomT = '╩';
        public const char Cross = '╬';
    }

    /// <summary>
    /// Gets box characters for the specified border style.
    /// </summary>
    public static (char TopLeft, char TopRight, char BottomLeft, char BottomRight, char Horizontal, char Vertical) GetChars(BoxBorderStyle style)
    {
        return style switch
        {
            BoxBorderStyle.Rounded => (Rounded.TopLeft, Rounded.TopRight, Rounded.BottomLeft, Rounded.BottomRight, Rounded.Horizontal, Rounded.Vertical),
            BoxBorderStyle.Simple => (Simple.TopLeft, Simple.TopRight, Simple.BottomLeft, Simple.BottomRight, Simple.Horizontal, Simple.Vertical),
            BoxBorderStyle.Double => (Double.TopLeft, Double.TopRight, Double.BottomLeft, Double.BottomRight, Double.Horizontal, Double.Vertical),
            _ => (' ', ' ', ' ', ' ', ' ', ' ')
        };
    }
}
