// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Specifies the type of sizing for a layout region.
/// </summary>
public enum LayoutSizeKind
{
    /// <summary>
    /// A fixed size in characters.
    /// </summary>
    Fixed,

    /// <summary>
    /// A percentage of the parent region.
    /// </summary>
    Percent,

    /// <summary>
    /// Fill remaining space after fixed and percent sizes.
    /// </summary>
    Fill
}
