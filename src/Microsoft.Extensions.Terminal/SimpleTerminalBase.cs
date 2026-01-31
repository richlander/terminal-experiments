// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal;

/// <summary>
/// Base class for simple terminal implementations that don't support cursor movement.
/// </summary>
public abstract class SimpleTerminal : ITerminal
{
    private object? _batchingLock;
    private bool _isBatching;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleTerminal"/> class.
    /// </summary>
    protected SimpleTerminal(IConsole console)
        => Console = console;

#pragma warning disable CA1416 // Validate platform compatibility
    /// <inheritdoc />
    public int Width => Console.IsOutputRedirected ? int.MaxValue : Console.BufferWidth;

    /// <inheritdoc />
    public int Height => Console.IsOutputRedirected ? int.MaxValue : Console.BufferHeight;

    /// <summary>
    /// Gets the console instance.
    /// </summary>
    protected IConsole Console { get; }

    /// <inheritdoc />
    public void Append(char value)
        => Console.Write(value);

    /// <inheritdoc />
    public virtual void Append(string value)
        => Console.Write(value);

    /// <inheritdoc />
    public void AppendLine()
        => Console.WriteLine();

    /// <inheritdoc />
    public virtual void AppendLine(string value)
        => Console.WriteLine(value);

    /// <inheritdoc />
    public void AppendLink(string path, int? lineNumber)
    {
        Append(path);
        if (lineNumber.HasValue)
        {
            Append($":{lineNumber}");
        }
    }

    /// <inheritdoc />
    public void HideCursor()
    {
        // nop - simple terminals don't support cursor control
    }

    /// <inheritdoc />
    public void ShowCursor()
    {
        // nop - simple terminals don't support cursor control
    }

    /// <inheritdoc />
    public void StartBusyIndicator()
    {
        // nop - simple terminals don't support busy indicators
    }

    /// <inheritdoc />
    public void StopBusyIndicator()
    {
        // nop - simple terminals don't support busy indicators
    }

    /// <inheritdoc />
    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException("Console is already in batching mode.");
        }

        bool lockTaken = false;

        // We store Console.Out in a field to make sure we will be doing
        // the Monitor.Exit call on the same instance.
        _batchingLock = System.Console.Out;

        // Note that we need to lock on System.Out for batching to work correctly.
        // Consider the following scenario:
        // 1. We call StartUpdate
        // 2. We call a Write("A")
        // 3. User calls Console.Write("B") from another thread.
        // 4. We call a Write("C").
        // 5. We call StopUpdate.
        // The expectation is that we see either ACB, or BAC, but not ABC.
        // Basically, when doing batching, we want to ensure that everything we write is
        // written continuously, without anything in-between.
        Monitor.Enter(_batchingLock, ref lockTaken);
        if (!lockTaken)
        {
            throw new InvalidOperationException("Failed to acquire batching lock.");
        }

        _isBatching = true;
    }

    /// <inheritdoc />
    public void StopUpdate()
    {
        Monitor.Exit(_batchingLock!);
        _batchingLock = null;
        _isBatching = false;
    }

    /// <inheritdoc />
    public abstract void SetColor(TerminalColor color);

    /// <inheritdoc />
    public abstract void ResetColor();
}
