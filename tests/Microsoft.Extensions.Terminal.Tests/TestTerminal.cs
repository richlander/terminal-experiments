// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;

namespace Microsoft.Extensions.Terminal.Tests;

/// <summary>
/// A test terminal that captures ANSI output for verification.
/// Output can be parsed by VtParser to verify rendering correctness.
/// </summary>
public class TestTerminal : ITerminal
{
    private readonly StringBuilder _output = new();
    private TerminalColor _currentColor = TerminalColor.Default;

    public TestTerminal(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    
    /// <summary>
    /// Gets the raw ANSI output captured from rendering.
    /// </summary>
    public string Output => _output.ToString();
    
    /// <summary>
    /// Gets the output as bytes for parsing.
    /// </summary>
    public byte[] OutputBytes => Encoding.UTF8.GetBytes(_output.ToString());
    
    public TerminalColor CurrentColor => _currentColor;

    public void Append(char c) => _output.Append(c);
    public void Append(string text) => _output.Append(text);
    public void AppendLine() => _output.AppendLine();
    public void AppendLine(string text) => _output.AppendLine(text);

    public void SetColor(TerminalColor color)
    {
        _currentColor = color;
    }

    public void ResetColor()
    {
        _currentColor = TerminalColor.Default;
    }

    public void StartUpdate() { }
    public void StopUpdate() { }
    public void ShowCursor() { }
    public void HideCursor() { }
    public void StartBusyIndicator() { }
    public void StopBusyIndicator() { }
    public void AppendLink(string filePath, int? lineNumber) => _output.Append(filePath);
    
    /// <summary>
    /// Clears the captured output.
    /// </summary>
    public void Clear() => _output.Clear();
}
