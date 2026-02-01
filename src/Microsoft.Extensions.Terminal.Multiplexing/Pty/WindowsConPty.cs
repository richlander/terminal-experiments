// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Windows ConPTY implementation.
/// </summary>
internal sealed class WindowsConPty : IPty
{
    private readonly nint _pseudoConsole;
    private readonly nint _processHandle;
    private readonly nint _threadHandle;
    private readonly nint _pipeIn;  // Write to this to send input to PTY
    private readonly nint _pipeOut; // Read from this to get output from PTY
    private readonly int _processId;
    private readonly nint _attributeList;
    private readonly object _lock = new();
    private uint? _exitCode;
    private bool _disposed;
    private TaskCompletionSource<int>? _exitTcs;

    public WindowsConPty(PtyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Create pipes for PTY I/O
        var securityAttributes = new WindowsConPtyNativeMethods.SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<WindowsConPtyNativeMethods.SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1
        };

        // Pipe for PTY input (we write, PTY reads)
        if (WindowsConPtyNativeMethods.CreatePipe(out nint ptyInputRead, out nint ptyInputWrite, ref securityAttributes, 0) == 0)
        {
            throw new InvalidOperationException($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
        }

        // Pipe for PTY output (PTY writes, we read)
        if (WindowsConPtyNativeMethods.CreatePipe(out nint ptyOutputRead, out nint ptyOutputWrite, ref securityAttributes, 0) == 0)
        {
            WindowsConPtyNativeMethods.CloseHandle(ptyInputRead);
            WindowsConPtyNativeMethods.CloseHandle(ptyInputWrite);
            throw new InvalidOperationException($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
        }

        _pipeIn = ptyInputWrite;
        _pipeOut = ptyOutputRead;

        // Create pseudo console
        var size = new WindowsConPtyNativeMethods.COORD
        {
            X = (short)options.Columns,
            Y = (short)options.Rows
        };

        int hr = WindowsConPtyNativeMethods.CreatePseudoConsole(size, ptyInputRead, ptyOutputWrite, 0, out _pseudoConsole);
        if (hr != 0)
        {
            CleanupPipes(ptyInputRead, ptyInputWrite, ptyOutputRead, ptyOutputWrite);
            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");
        }

        // Close the handles we passed to CreatePseudoConsole (it has its own copies)
        WindowsConPtyNativeMethods.CloseHandle(ptyInputRead);
        WindowsConPtyNativeMethods.CloseHandle(ptyOutputWrite);

        // Initialize process thread attribute list
        nuint attrListSize = 0;
        WindowsConPtyNativeMethods.InitializeProcThreadAttributeList(nint.Zero, 1, 0, ref attrListSize);
        _attributeList = Marshal.AllocHGlobal((int)attrListSize);

        if (WindowsConPtyNativeMethods.InitializeProcThreadAttributeList(_attributeList, 1, 0, ref attrListSize) == 0)
        {
            Marshal.FreeHGlobal(_attributeList);
            WindowsConPtyNativeMethods.ClosePseudoConsole(_pseudoConsole);
            CleanupPipes(nint.Zero, _pipeIn, _pipeOut, nint.Zero);
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");
        }

        // Associate pseudo console with the process
        if (WindowsConPtyNativeMethods.UpdateProcThreadAttribute(
            _attributeList, 0,
            WindowsConPtyNativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            _pseudoConsole, (nuint)nint.Size,
            nint.Zero, nint.Zero) == 0)
        {
            WindowsConPtyNativeMethods.DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            WindowsConPtyNativeMethods.ClosePseudoConsole(_pseudoConsole);
            CleanupPipes(nint.Zero, _pipeIn, _pipeOut, nint.Zero);
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
        }

        // Build command line
        var commandLine = new StringBuilder();
        commandLine.Append(options.Command);
        if (options.Arguments is { Length: > 0 })
        {
            foreach (var arg in options.Arguments)
            {
                commandLine.Append(' ');
                if (arg.Contains(' ') || arg.Contains('"'))
                {
                    commandLine.Append('"');
                    commandLine.Append(arg.Replace("\"", "\\\""));
                    commandLine.Append('"');
                }
                else
                {
                    commandLine.Append(arg);
                }
            }
        }

        // Build environment block if needed
        nint environmentBlock = nint.Zero;
        if (options.Environment is { Count: > 0 })
        {
            environmentBlock = CreateEnvironmentBlock(options.Environment);
        }

        try
        {
            // Create the process
            var startupInfo = new WindowsConPtyNativeMethods.STARTUPINFOEX
            {
                StartupInfo = new WindowsConPtyNativeMethods.STARTUPINFO
                {
                    cb = Marshal.SizeOf<WindowsConPtyNativeMethods.STARTUPINFOEX>()
                },
                lpAttributeList = _attributeList
            };

            uint creationFlags = WindowsConPtyNativeMethods.EXTENDED_STARTUPINFO_PRESENT;
            if (environmentBlock != nint.Zero)
            {
                creationFlags |= WindowsConPtyNativeMethods.CREATE_UNICODE_ENVIRONMENT;
            }

            if (WindowsConPtyNativeMethods.CreateProcessW(
                null,
                commandLine.ToString(),
                nint.Zero,
                nint.Zero,
                0,
                creationFlags,
                environmentBlock,
                options.WorkingDirectory,
                ref startupInfo,
                out var processInfo) == 0)
            {
                throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");
            }

            _processHandle = processInfo.hProcess;
            _threadHandle = processInfo.hThread;
            _processId = processInfo.dwProcessId;
        }
        finally
        {
            if (environmentBlock != nint.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }
        }
    }

    private static nint CreateEnvironmentBlock(IDictionary<string, string> environment)
    {
        // Get current environment and merge
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = (string)entry.Value!;
        }

        foreach (var kvp in environment)
        {
            env[kvp.Key] = kvp.Value;
        }

        // Build null-separated, double-null-terminated Unicode string
        var sb = new StringBuilder();
        foreach (var kvp in env)
        {
            sb.Append(kvp.Key);
            sb.Append('=');
            sb.Append(kvp.Value);
            sb.Append('\0');
        }
        sb.Append('\0');

        var bytes = Encoding.Unicode.GetBytes(sb.ToString());
        var block = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, block, bytes.Length);
        return block;
    }

    private static void CleanupPipes(nint h1, nint h2, nint h3, nint h4)
    {
        if (h1 != nint.Zero) WindowsConPtyNativeMethods.CloseHandle(h1);
        if (h2 != nint.Zero) WindowsConPtyNativeMethods.CloseHandle(h2);
        if (h3 != nint.Zero) WindowsConPtyNativeMethods.CloseHandle(h3);
        if (h4 != nint.Zero) WindowsConPtyNativeMethods.CloseHandle(h4);
    }

    public int ProcessId => _processId;

    public bool HasExited
    {
        get
        {
            if (_exitCode.HasValue)
            {
                return true;
            }

            if (WindowsConPtyNativeMethods.GetExitCodeProcess(_processHandle, out uint exitCode) != 0)
            {
                if (exitCode != WindowsConPtyNativeMethods.STILL_ACTIVE)
                {
                    _exitCode = exitCode;
                    _exitTcs?.TrySetResult((int)exitCode);
                    return true;
                }
            }

            return false;
        }
    }

    public int? ExitCode
    {
        get
        {
            _ = HasExited;
            return _exitCode.HasValue ? (int)_exitCode.Value : null;
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_exitCode.HasValue)
        {
            return (int)_exitCode.Value;
        }

        lock (_lock)
        {
            _exitTcs ??= new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = Task.Run(async () =>
        {
            while (!_exitCode.HasValue && !cancellationToken.IsCancellationRequested)
            {
                uint result = WindowsConPtyNativeMethods.WaitForSingleObject(_processHandle, 100);
                if (result == WindowsConPtyNativeMethods.WAIT_OBJECT_0)
                {
                    if (WindowsConPtyNativeMethods.GetExitCodeProcess(_processHandle, out uint exitCode) != 0)
                    {
                        _exitCode = exitCode;
                        _exitTcs?.TrySetResult((int)exitCode);
                        return;
                    }
                }
            }
        }, cancellationToken);

        using var registration = cancellationToken.Register(() => _exitTcs?.TrySetCanceled());
        return await _exitTcs.Task.ConfigureAwait(false);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            unsafe
            {
                fixed (byte* ptr = buffer.Span)
                {
                    if (WindowsConPtyNativeMethods.ReadFile(_pipeOut, (nint)ptr, buffer.Length, out int bytesRead, nint.Zero) == 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 109) // ERROR_BROKEN_PIPE
                        {
                            return 0;
                        }
                        throw new IOException($"ReadFile failed: {error}");
                    }
                    return bytesRead;
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            unsafe
            {
                fixed (byte* ptr = buffer.Span)
                {
                    int written = 0;
                    while (written < buffer.Length)
                    {
                        if (WindowsConPtyNativeMethods.WriteFile(_pipeIn, (nint)(ptr + written), buffer.Length - written, out int bytesWritten, nint.Zero) == 0)
                        {
                            throw new IOException($"WriteFile failed: {Marshal.GetLastWin32Error()}");
                        }
                        written += bytesWritten;
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var size = new WindowsConPtyNativeMethods.COORD
        {
            X = (short)columns,
            Y = (short)rows
        };

        int hr = WindowsConPtyNativeMethods.ResizePseudoConsole(_pseudoConsole, size);
        if (hr != 0)
        {
            throw new InvalidOperationException($"ResizePseudoConsole failed: 0x{hr:X8}");
        }
    }

    public void Kill(bool force = false)
    {
        if (HasExited)
        {
            return;
        }

        // On Windows, TerminateProcess is always forceful
        WindowsConPtyNativeMethods.TerminateProcess(_processHandle, 1);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Close pseudo console first (this signals the process)
        WindowsConPtyNativeMethods.ClosePseudoConsole(_pseudoConsole);

        // Clean up attribute list
        WindowsConPtyNativeMethods.DeleteProcThreadAttributeList(_attributeList);
        Marshal.FreeHGlobal(_attributeList);

        // Close pipes
        WindowsConPtyNativeMethods.CloseHandle(_pipeIn);
        WindowsConPtyNativeMethods.CloseHandle(_pipeOut);

        // Close process and thread handles
        WindowsConPtyNativeMethods.CloseHandle(_threadHandle);
        WindowsConPtyNativeMethods.CloseHandle(_processHandle);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
