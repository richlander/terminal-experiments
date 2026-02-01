// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A component that can handle keyboard input.
/// </summary>
public interface IInteractiveComponent : IComponent
{
    /// <summary>
    /// Handle a key press.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <returns>True if the key was handled, false otherwise.</returns>
    bool HandleKey(ConsoleKeyInfo key);

    /// <summary>
    /// Gets or sets whether this component currently has focus.
    /// </summary>
    bool IsFocused { get; set; }
}
