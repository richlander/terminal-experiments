// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// An interactive terminal application with a render loop and focus management.
/// </summary>
public sealed class TerminalApp
{
    private readonly ITerminal _terminal;
    private readonly Layout _layout;
    private readonly List<IInteractiveComponent> _focusableComponents = new();
    private ScreenBuffer _buffer;
    private int _focusIndex;
    private bool _running = true;
    private bool _needsRender = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalApp"/> class.
    /// </summary>
    /// <param name="terminal">The terminal to render to.</param>
    public TerminalApp(ITerminal terminal)
    {
        _terminal = terminal;
        _layout = new Layout();
        _buffer = new ScreenBuffer(terminal.Width, terminal.Height);
    }

    /// <summary>
    /// Gets the layout for this application.
    /// </summary>
    public Layout Layout => _layout;

    /// <summary>
    /// Gets the terminal.
    /// </summary>
    public ITerminal Terminal => _terminal;

    /// <summary>
    /// Registers a component as focusable for keyboard navigation.
    /// </summary>
    /// <param name="component">The component to register.</param>
    public void RegisterFocusable(IInteractiveComponent component)
    {
        _focusableComponents.Add(component);
        if (_focusableComponents.Count == 1)
        {
            component.IsFocused = true;
            _focusIndex = 0;
        }
    }

    /// <summary>
    /// Unregisters a focusable component.
    /// </summary>
    /// <param name="component">The component to unregister.</param>
    public void UnregisterFocusable(IInteractiveComponent component)
    {
        int index = _focusableComponents.IndexOf(component);
        if (index >= 0)
        {
            component.IsFocused = false;
            _focusableComponents.RemoveAt(index);

            if (_focusableComponents.Count > 0)
            {
                _focusIndex = Math.Min(_focusIndex, _focusableComponents.Count - 1);
                _focusableComponents[_focusIndex].IsFocused = true;
            }
            else
            {
                _focusIndex = 0;
            }
        }
    }

    /// <summary>
    /// Requests a render on the next frame.
    /// </summary>
    public void Invalidate()
    {
        _needsRender = true;
    }

    /// <summary>
    /// Stops the application loop.
    /// </summary>
    public void Stop()
    {
        _running = false;
    }

    /// <summary>
    /// Runs the application loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the loop.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _terminal.HideCursor();
        _buffer.Invalidate();

        try
        {
            using var ticker = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

            while (_running && !cancellationToken.IsCancellationRequested)
            {
                // Check for terminal resize
                if (_buffer.Width != _terminal.Width || _buffer.Height != _terminal.Height)
                {
                    _buffer = new ScreenBuffer(_terminal.Width, _terminal.Height);
                    _buffer.Invalidate();
                    _needsRender = true;
                }

                // Process input
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (HandleKey(key))
                    {
                        _needsRender = true;
                    }
                }

                // Render if needed
                if (_needsRender)
                {
                    _layout.Render(_buffer, Region.FromTerminal(_terminal));
                    _buffer.Flush(_terminal);
                    _needsRender = false;
                }

                await ticker.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            _terminal.ShowCursor();
        }
    }

    /// <summary>
    /// Handles a key press.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <returns>True if the key was handled, false otherwise.</returns>
    public bool HandleKey(ConsoleKeyInfo key)
    {
        // Global quit key
        if (key.Key == ConsoleKey.Q && key.Modifiers == 0)
        {
            _running = false;
            return true;
        }

        // Tab navigation
        if (key.Key == ConsoleKey.Tab)
        {
            MoveFocus(key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -1 : 1);
            return true;
        }

        // Delegate to focused component
        if (_focusIndex >= 0 && _focusIndex < _focusableComponents.Count)
        {
            return _focusableComponents[_focusIndex].HandleKey(key);
        }

        return false;
    }

    private void MoveFocus(int direction)
    {
        if (_focusableComponents.Count == 0)
        {
            return;
        }

        _focusableComponents[_focusIndex].IsFocused = false;
        _focusIndex = (_focusIndex + direction + _focusableComponents.Count) % _focusableComponents.Count;
        _focusableComponents[_focusIndex].IsFocused = true;
    }
}
