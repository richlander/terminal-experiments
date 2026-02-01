// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A component that can render itself to a screen buffer region.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Render this component into the given region.
    /// </summary>
    /// <param name="buffer">The screen buffer to render to.</param>
    /// <param name="region">The region to render within.</param>
    void Render(ScreenBuffer buffer, Region region);

    /// <summary>
    /// Gets the optional preferred size for layout calculations.
    /// </summary>
    Size? PreferredSize => null;
}
