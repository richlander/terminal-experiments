// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal;

/// <summary>
/// A terminal that is capable of rendering text with colors and cursor control.
/// </summary>
public interface ITerminal
{
    /// <summary>
    /// Gets the width of the terminal in characters.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the terminal in characters.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Appends a character to the terminal output.
    /// </summary>
    void Append(char value);

    /// <summary>
    /// Appends a string to the terminal output.
    /// </summary>
    void Append(string value);

    /// <summary>
    /// Appends a new line to the terminal output.
    /// </summary>
    void AppendLine();

    /// <summary>
    /// Appends a string followed by a new line to the terminal output.
    /// </summary>
    void AppendLine(string value);

    /// <summary>
    /// Appends a clickable hyperlink to the terminal output.
    /// </summary>
    void AppendLink(string path, int? lineNumber);

    /// <summary>
    /// Sets the foreground color.
    /// </summary>
    void SetColor(TerminalColor color);

    /// <summary>
    /// Resets the foreground color to the default.
    /// </summary>
    void ResetColor();

    /// <summary>
    /// Shows the cursor.
    /// </summary>
    void ShowCursor();

    /// <summary>
    /// Hides the cursor.
    /// </summary>
    void HideCursor();

    /// <summary>
    /// Starts a batched update. All output will be buffered until <see cref="StopUpdate"/> is called.
    /// </summary>
    void StartUpdate();

    /// <summary>
    /// Ends a batched update and flushes the buffer to the terminal.
    /// </summary>
    void StopUpdate();

    /// <summary>
    /// Starts a busy indicator (spinner in taskbar on supported terminals).
    /// </summary>
    void StartBusyIndicator();

    /// <summary>
    /// Stops the busy indicator.
    /// </summary>
    void StopBusyIndicator();
}
