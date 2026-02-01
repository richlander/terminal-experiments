// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Unix PTY implementation using forkpty.
/// </summary>
internal sealed class UnixPty : IPty
{
    private readonly int _masterFd;
    private readonly int _childPid;
    private readonly object _lock = new();
    private int? _exitCode;
    private bool _disposed;
    private TaskCompletionSource<int>? _exitTcs;

    public UnixPty(PtyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var winsize = new UnixNativeMethods.Winsize
        {
            ws_col = (ushort)options.Columns,
            ws_row = (ushort)options.Rows,
            ws_xpixel = 0,
            ws_ypixel = 0
        };

        int pid = UnixNativeMethods.forkpty(out _masterFd, nint.Zero, nint.Zero, ref winsize);

        if (pid < 0)
        {
            throw new InvalidOperationException($"forkpty failed with error {Marshal.GetLastPInvokeError()}");
        }

        if (pid == 0)
        {
            // Child process
            ExecuteChild(options);
            // Should never reach here
            UnixNativeMethods._exit(1);
        }

        // Parent process
        _childPid = pid;
    }

    private static void ExecuteChild(PtyOptions options)
    {
        // Set working directory
        if (!string.IsNullOrEmpty(options.WorkingDirectory))
        {
            if (UnixNativeMethods.chdir(options.WorkingDirectory) != 0)
            {
                UnixNativeMethods._exit(1);
            }
        }

        // Set environment variables
        if (options.Environment is not null)
        {
            foreach (var kvp in options.Environment)
            {
                UnixNativeMethods.setenv(kvp.Key, kvp.Value, 1);
            }
        }

        // Build argv array
        var args = new List<string> { options.Command };
        if (options.Arguments is not null)
        {
            args.AddRange(options.Arguments);
        }

        // Create null-terminated array of string pointers
        var argvPtrs = new nint[args.Count + 1];
        for (int i = 0; i < args.Count; i++)
        {
            argvPtrs[i] = Marshal.StringToHGlobalAnsi(args[i]);
        }
        argvPtrs[args.Count] = nint.Zero;

        // Pin and pass to execvp
        var argvHandle = GCHandle.Alloc(argvPtrs, GCHandleType.Pinned);
        try
        {
            UnixNativeMethods.execvp(options.Command, argvHandle.AddrOfPinnedObject());
        }
        finally
        {
            argvHandle.Free();
        }

        // If execvp returns, it failed
        UnixNativeMethods._exit(127);
    }

    public int ProcessId => _childPid;

    public bool HasExited
    {
        get
        {
            if (_exitCode.HasValue)
            {
                return true;
            }

            // Non-blocking check
            int result = UnixNativeMethods.waitpid(_childPid, out int status, UnixNativeMethods.WNOHANG);
            if (result == _childPid)
            {
                _exitCode = ExtractExitCode(status);
                _exitTcs?.TrySetResult(_exitCode.Value);
                return true;
            }

            return false;
        }
    }

    public int? ExitCode
    {
        get
        {
            _ = HasExited; // Trigger check
            return _exitCode;
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_exitCode.HasValue)
        {
            return _exitCode.Value;
        }

        lock (_lock)
        {
            _exitTcs ??= new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // Start a background task to poll for exit
        _ = Task.Run(async () =>
        {
            while (!_exitCode.HasValue && !cancellationToken.IsCancellationRequested)
            {
                int result = UnixNativeMethods.waitpid(_childPid, out int status, UnixNativeMethods.WNOHANG);
                if (result == _childPid)
                {
                    _exitCode = ExtractExitCode(status);
                    _exitTcs?.TrySetResult(_exitCode.Value);
                    return;
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken);

        using var registration = cancellationToken.Register(() => _exitTcs?.TrySetCanceled());
        return await _exitTcs.Task.ConfigureAwait(false);
    }

    private static int ExtractExitCode(int status)
    {
        // WEXITSTATUS macro: (status >> 8) & 0xFF
        if ((status & 0x7F) == 0) // WIFEXITED
        {
            return (status >> 8) & 0xFF;
        }

        // Killed by signal
        return 128 + (status & 0x7F);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use Task.Run to make blocking read async
        return await Task.Run(() =>
        {
            unsafe
            {
                fixed (byte* ptr = buffer.Span)
                {
                    nint result = UnixNativeMethods.read(_masterFd, (nint)ptr, (nuint)buffer.Length);
                    if (result < 0)
                    {
                        int error = Marshal.GetLastPInvokeError();
                        if (error == 5) // EIO - PTY closed
                        {
                            return 0;
                        }
                        throw new IOException($"read failed with error {error}");
                    }
                    return (int)result;
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
                        nint result = UnixNativeMethods.write(_masterFd, (nint)(ptr + written), (nuint)(buffer.Length - written));
                        if (result < 0)
                        {
                            throw new IOException($"write failed with error {Marshal.GetLastPInvokeError()}");
                        }
                        written += (int)result;
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var winsize = new UnixNativeMethods.Winsize
        {
            ws_col = (ushort)columns,
            ws_row = (ushort)rows,
            ws_xpixel = 0,
            ws_ypixel = 0
        };

        if (UnixNativeMethods.ioctl(_masterFd, UnixNativeMethods.GetTiocswinszCode(), ref winsize) < 0)
        {
            throw new IOException($"ioctl TIOCSWINSZ failed with error {Marshal.GetLastPInvokeError()}");
        }
    }

    public void Kill(bool force = false)
    {
        if (HasExited)
        {
            return;
        }

        int signal = force ? UnixNativeMethods.SIGKILL : UnixNativeMethods.SIGTERM;
        UnixNativeMethods.kill(_childPid, signal);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnixNativeMethods.close(_masterFd);

        // Clean up zombie process
        if (!HasExited)
        {
            Kill(force: true);
            UnixNativeMethods.waitpid(_childPid, out _, 0);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
