// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Native methods for Unix PTY operations.
/// </summary>
internal static partial class UnixNativeMethods
{
    internal const int SIGTERM = 15;
    internal const int SIGKILL = 9;

    // ioctl request codes
    internal const uint TIOCSWINSZ = 0x5414; // Linux
    internal const uint TIOCSWINSZ_DARWIN = 0x80087467; // macOS

    internal static uint GetTiocswinszCode()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? TIOCSWINSZ_DARWIN : TIOCSWINSZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    /// <summary>
    /// Creates a new pseudo-terminal and forks a child process.
    /// </summary>
    /// <param name="amaster">Receives the file descriptor for the master side.</param>
    /// <param name="name">Buffer to receive the name of the slave device (can be null).</param>
    /// <param name="termp">Terminal settings (can be null).</param>
    /// <param name="winp">Window size (can be null).</param>
    /// <returns>0 in the child, PID in the parent, -1 on error.</returns>
    [LibraryImport("libc", SetLastError = true)]
    internal static partial int forkpty(out int amaster, nint name, nint termp, ref Winsize winp);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial int close(int fd);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial nint read(int fd, nint buf, nuint count);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial nint write(int fd, nint buf, nuint count);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial int ioctl(int fd, nuint request, ref Winsize winsize);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial int kill(int pid, int sig);

    [LibraryImport("libc", SetLastError = true)]
    internal static partial int waitpid(int pid, out int status, int options);

    [LibraryImport("libc", EntryPoint = "execvp", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int execvp(string file, nint argv);

    [LibraryImport("libc", EntryPoint = "setenv", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int setenv(string name, string value, int overwrite);

    [LibraryImport("libc", EntryPoint = "chdir", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int chdir(string path);

    [LibraryImport("libc", EntryPoint = "_exit")]
    internal static partial void _exit(int status);

    // WNOHANG for non-blocking waitpid
    internal const int WNOHANG = 1;
}
