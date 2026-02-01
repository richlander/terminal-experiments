// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Specifies how a layout region should be sized.
/// </summary>
public readonly record struct LayoutSize
{
    /// <summary>
    /// Gets the kind of sizing.
    /// </summary>
    public LayoutSizeKind Kind { get; init; }

    /// <summary>
    /// Gets the value for Fixed or Percent sizing.
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// Creates a fixed-size layout specification.
    /// </summary>
    /// <param name="size">The fixed size in characters.</param>
    /// <returns>A LayoutSize with fixed sizing.</returns>
    public static LayoutSize Fixed(int size) => new() { Kind = LayoutSizeKind.Fixed, Value = size };

    /// <summary>
    /// Creates a percentage-based layout specification.
    /// </summary>
    /// <param name="percent">The percentage of parent region (0-100).</param>
    /// <returns>A LayoutSize with percentage sizing.</returns>
    public static LayoutSize Percent(int percent) => new() { Kind = LayoutSizeKind.Percent, Value = percent };

    /// <summary>
    /// Gets a layout specification that fills remaining space.
    /// </summary>
    public static LayoutSize Fill => new() { Kind = LayoutSizeKind.Fill };
}
