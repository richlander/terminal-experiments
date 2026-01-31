// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Extensions.Terminal;

/// <summary>
/// Terminal writer that is used when writing ANSI is allowed. It is capable of batching as many updates as possible and writing them at the end,
/// because the terminal is responsible for rendering the colors and control codes.
/// </summary>
public sealed class AnsiTerminal : ITerminal
{
    /// <summary>
    /// File extensions that we will link to directly, all other files
    /// are linked to their directory, to avoid opening dlls, or executables.
    /// </summary>
    private static readonly string[] KnownFileExtensions =
    [
        // code files
        ".cs",
        ".vb",
        ".fs",
        // logs
        ".log",
        ".txt",
        // reports
        ".coverage",
        ".ctrf",
        ".html",
        ".junit",
        ".nunit",
        ".trx",
        ".xml",
        ".xunit",
    ];

    private readonly IConsole _console;
    private readonly string? _baseDirectory;
    // Output ansi code to get spinner on top of a terminal, to indicate in-progress task.
    // https://github.com/dotnet/msbuild/issues/8958: iTerm2 treats ;9 code to post a notification instead, so disable progress reporting on Mac.
    private readonly bool _useBusyIndicator = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private readonly StringBuilder _stringBuilder = new();
    private bool _isBatching;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnsiTerminal"/> class.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="baseDirectory">The base directory for relative path display in links.</param>
    public AnsiTerminal(IConsole console, string? baseDirectory = null)
    {
        _console = console;
        _baseDirectory = baseDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <inheritdoc />
    public int Width
        => _console.IsOutputRedirected ? int.MaxValue : _console.BufferWidth;

    /// <inheritdoc />
    public int Height
        => _console.IsOutputRedirected ? int.MaxValue : _console.BufferHeight;

    /// <inheritdoc />
    public void Append(char value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    /// <inheritdoc />
    public void Append(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    /// <inheritdoc />
    public void AppendLine()
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine();
        }
        else
        {
            _console.WriteLine();
        }
    }

    /// <inheritdoc />
    public void AppendLine(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine(value);
        }
        else
        {
            _console.WriteLine(value);
        }
    }

    /// <inheritdoc />
    public void SetColor(TerminalColor color)
    {
        string setColor = $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";
        if (_isBatching)
        {
            _stringBuilder.Append(setColor);
        }
        else
        {
            _console.Write(setColor);
        }
    }

    /// <inheritdoc />
    public void ResetColor()
    {
        string resetColor = AnsiCodes.SetDefaultColor;
        if (_isBatching)
        {
            _stringBuilder.Append(resetColor);
        }
        else
        {
            _console.Write(resetColor);
        }
    }

    /// <inheritdoc />
    public void ShowCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.ShowCursor);
        }
        else
        {
            _console.Write(AnsiCodes.ShowCursor);
        }
    }

    /// <inheritdoc />
    public void HideCursor()
    {
        if (_isBatching)
        {
            _stringBuilder.Append(AnsiCodes.HideCursor);
        }
        else
        {
            _console.Write(AnsiCodes.HideCursor);
        }
    }

    /// <inheritdoc />
    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException("Console is already in batching mode.");
        }

        _stringBuilder.Clear();
        _isBatching = true;
    }

    /// <inheritdoc />
    public void StopUpdate()
    {
        _console.Write(_stringBuilder.ToString());
        _isBatching = false;
    }

    /// <inheritdoc />
    public void AppendLink(string? path, int? lineNumber)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // For non code files, point to the directory, so we don't end up running the
        // exe by clicking at the link.
        string? extension = Path.GetExtension(path);
        bool linkToFile = !string.IsNullOrWhiteSpace(extension) && KnownFileExtensions.Contains(extension);

        bool knownNonExistingFile = path.StartsWith("/_/", ignoreCase: false, CultureInfo.CurrentCulture);

        string linkPath = path;
        if (!linkToFile)
        {
            try
            {
                linkPath = Path.GetDirectoryName(linkPath) ?? linkPath;
            }
            catch
            {
                // Ignore all GetDirectoryName errors.
            }
        }

        // If the output path is under the initial working directory, make the console output relative to that to save space.
        if (_baseDirectory != null && path.StartsWith(_baseDirectory, FileUtilities.PathComparison))
        {
            if (path.Length > _baseDirectory.Length
                && (path[_baseDirectory.Length] == Path.DirectorySeparatorChar
                    || path[_baseDirectory.Length] == Path.AltDirectorySeparatorChar))
            {
                path = path[(_baseDirectory.Length + 1)..];
            }
        }

        if (lineNumber != null)
        {
            path += $":{lineNumber}";
        }

        if (knownNonExistingFile)
        {
            Append(path);
            return;
        }

        // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
        if (Uri.TryCreate(linkPath, UriKind.Absolute, out Uri? uri))
        {
            // url.ToString() un-escapes the URL which is needed for our case file://
            linkPath = uri.ToString();
        }

        SetColor(TerminalColor.DarkGray);
        Append(AnsiCodes.LinkPrefix);
        Append(linkPath);
        Append(AnsiCodes.LinkInfix);
        Append(path);
        Append(AnsiCodes.LinkSuffix);
        ResetColor();
    }

    /// <summary>
    /// Moves the cursor up by the specified number of lines.
    /// </summary>
    public void MoveCursorUp(int lineCount)
    {
        string moveCursor = $"{AnsiCodes.CSI}{lineCount}{AnsiCodes.MoveUpToLineStart}";
        if (_isBatching)
        {
            _stringBuilder.Append(moveCursor);
        }
        else
        {
            _console.Write(moveCursor);
        }
    }

    /// <summary>
    /// Sets the cursor to the specified horizontal position.
    /// </summary>
    public void SetCursorHorizontal(int position)
    {
        string setCursor = AnsiCodes.SetCursorHorizontal(position);
        if (_isBatching)
        {
            _stringBuilder.Append(setCursor);
        }
        else
        {
            _console.Write(setCursor);
        }
    }

    /// <summary>
    /// Erases from the cursor to the end of the screen.
    /// </summary>
    public void EraseToEndOfScreen()
    {
        string erase = $"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}";
        if (_isBatching)
        {
            _stringBuilder.Append(erase);
        }
        else
        {
            _console.Write(erase);
        }
    }

    /// <summary>
    /// Erases from the cursor to the end of the line.
    /// </summary>
    public void EraseToEndOfLine()
    {
        string erase = $"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}";
        if (_isBatching)
        {
            _stringBuilder.Append(erase);
        }
        else
        {
            _console.Write(erase);
        }
    }

    /// <inheritdoc />
    public void StartBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.SetBusySpinner);
        }

        HideCursor();
    }

    /// <inheritdoc />
    public void StopBusyIndicator()
    {
        if (_useBusyIndicator)
        {
            Append(AnsiCodes.RemoveBusySpinner);
        }

        ShowCursor();
    }
}
