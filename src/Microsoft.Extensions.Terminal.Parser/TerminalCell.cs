// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser;

/// <summary>
/// Represents a single cell in the terminal screen buffer.
/// </summary>
/// <remarks>
/// Design influenced by:
/// - xterm.js: Bitfield encoding for memory efficiency
/// - libvterm: Separate pen attributes
/// </remarks>
/// <param name="Character">The character displayed in this cell.</param>
/// <param name="Foreground">Foreground color (0-255 for indexed, or RGB packed with high bit set).</param>
/// <param name="Background">Background color (0-255 for indexed, or RGB packed with high bit set).</param>
/// <param name="Attributes">Text attributes (bold, italic, underline, etc.).</param>
/// <param name="Width">Width of this cell (1 for normal, 2 for wide chars like CJK).</param>
public readonly record struct TerminalCell(
    char Character = ' ',
    uint Foreground = TerminalCell.DefaultForeground,
    uint Background = TerminalCell.DefaultBackground,
    CellAttributes Attributes = CellAttributes.None,
    byte Width = 1)
{
    /// <summary>Default foreground color (white).</summary>
    public const uint DefaultForeground = 7;

    /// <summary>Default background color (black).</summary>
    public const uint DefaultBackground = 0;

    /// <summary>
    /// Creates a blank cell with default attributes.
    /// </summary>
    public static TerminalCell Blank => new(' ');

    /// <summary>
    /// Checks if the foreground is a true color (RGB) value.
    /// </summary>
    public bool IsForegroundRgb => Foreground > 0xFFFFFF;

    /// <summary>
    /// Checks if the background is a true color (RGB) value.
    /// </summary>
    public bool IsBackgroundRgb => Background > 0xFFFFFF;

    /// <summary>
    /// Gets the RGB components of the foreground if it's a true color.
    /// </summary>
    public (byte R, byte G, byte B) ForegroundRgb =>
        ((byte)((Foreground >> 16) & 0xFF), (byte)((Foreground >> 8) & 0xFF), (byte)(Foreground & 0xFF));

    /// <summary>
    /// Gets the RGB components of the background if it's a true color.
    /// </summary>
    public (byte R, byte G, byte B) BackgroundRgb =>
        ((byte)((Background >> 16) & 0xFF), (byte)((Background >> 8) & 0xFF), (byte)(Background & 0xFF));
}

/// <summary>
/// Text attributes for a terminal cell.
/// </summary>
[Flags]
public enum CellAttributes : ushort
{
    None = 0,
    Bold = 1 << 0,
    Dim = 1 << 1,
    Italic = 1 << 2,
    Underline = 1 << 3,
    Blink = 1 << 4,
    Inverse = 1 << 5,
    Hidden = 1 << 6,
    Strikethrough = 1 << 7,
    DoubleUnderline = 1 << 8,
    CurlyUnderline = 1 << 9,
}
