// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Manages a stack of views with push/pop navigation.
/// </summary>
public class ViewStack : IInteractiveComponent
{
    private readonly Stack<IComponent> _views = new();

    /// <summary>
    /// Gets the currently visible view.
    /// </summary>
    public IComponent? Current => _views.TryPeek(out var view) ? view : null;

    /// <summary>
    /// Gets the number of views in the stack.
    /// </summary>
    public int Count => _views.Count;

    /// <summary>
    /// Gets or sets whether this component currently has focus.
    /// </summary>
    public bool IsFocused { get; set; }

    /// <summary>
    /// Push a new view onto the stack.
    /// </summary>
    /// <param name="view">The view to push.</param>
    public void Push(IComponent view)
    {
        // Transfer focus to the new view if applicable
        if (Current is IInteractiveComponent oldInteractive)
        {
            oldInteractive.IsFocused = false;
        }

        _views.Push(view);

        if (view is IInteractiveComponent newInteractive && IsFocused)
        {
            newInteractive.IsFocused = true;
        }
    }

    /// <summary>
    /// Pop the current view and return to the previous one.
    /// </summary>
    /// <returns>True if a view was popped, false if already at root.</returns>
    public bool Pop()
    {
        if (_views.Count > 1)
        {
            if (_views.Pop() is IInteractiveComponent oldInteractive)
            {
                oldInteractive.IsFocused = false;
            }

            if (Current is IInteractiveComponent newInteractive && IsFocused)
            {
                newInteractive.IsFocused = true;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Replace all views with a single root view.
    /// </summary>
    /// <param name="root">The new root view.</param>
    public void Reset(IComponent root)
    {
        while (_views.Count > 0)
        {
            if (_views.Pop() is IInteractiveComponent interactive)
            {
                interactive.IsFocused = false;
            }
        }

        _views.Push(root);

        if (root is IInteractiveComponent newInteractive && IsFocused)
        {
            newInteractive.IsFocused = true;
        }
    }

    /// <inheritdoc />
    public bool HandleKey(ConsoleKeyInfo key)
    {
        // Escape/Backspace pops the stack (but not if at root)
        if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace && _views.Count > 1)
        {
            return Pop();
        }

        // Delegate to current view if interactive
        if (Current is IInteractiveComponent interactive)
        {
            return interactive.HandleKey(key);
        }

        return false;
    }

    /// <inheritdoc />
    public void Render(ScreenBuffer buffer, Region region)
    {
        Current?.Render(buffer, region);
    }
}
