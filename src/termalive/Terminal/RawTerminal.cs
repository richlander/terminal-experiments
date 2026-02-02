// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Termalive;

/// <summary>
/// Provides raw terminal mode support for proper PTY passthrough.
/// </summary>
internal static class RawTerminal
{
    private static TermiosMacOS? _originalTermiosMacOS;
    private static TermiosLinux? _originalTermiosLinux;

    /// <summary>
    /// Enters raw mode, disabling line buffering and echo.
    /// </summary>
    public static bool EnterRawMode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return EnterRawModeMacOS();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return EnterRawModeLinux();
        }
        else
        {
            // Windows - use Console API
            Console.TreatControlCAsInput = true;
            return true;
        }
    }

    private static bool EnterRawModeMacOS()
    {
        try
        {
            if (tcgetattr_macos(STDIN_FILENO, out var termios) != 0)
            {
                return false;
            }

            _originalTermiosMacOS = termios;

            // Disable canonical mode (line buffering) and echo
            termios.c_lflag &= ~(ICANON_MAC | ECHO_MAC | IEXTEN_MAC);

            // Disable some input processing
            termios.c_iflag &= ~(IXON_MAC | BRKINT_MAC | INPCK_MAC | ISTRIP_MAC);

            // Set character size to 8 bits
            termios.c_cflag |= CS8_MAC;

            // Read returns immediately with whatever is available
            termios.c_cc[VMIN_MAC] = 1;
            termios.c_cc[VTIME_MAC] = 0;

            if (tcsetattr_macos(STDIN_FILENO, TCSANOW, ref termios) != 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnterRawModeLinux()
    {
        try
        {
            if (tcgetattr_linux(STDIN_FILENO, out var termios) != 0)
            {
                return false;
            }

            _originalTermiosLinux = termios;

            // Disable canonical mode (line buffering) and echo
            termios.c_lflag &= ~(ICANON_LINUX | ECHO_LINUX | IEXTEN_LINUX);

            // Disable some input processing
            termios.c_iflag &= ~(IXON_LINUX | BRKINT_LINUX | INPCK_LINUX | ISTRIP_LINUX);

            // Set character size to 8 bits
            termios.c_cflag |= CS8_LINUX;

            // Read returns immediately with whatever is available
            termios.c_cc[VMIN_LINUX] = 1;
            termios.c_cc[VTIME_LINUX] = 0;

            if (tcsetattr_linux(STDIN_FILENO, TCSANOW, ref termios) != 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restores the terminal to its original mode.
    /// </summary>
    public static void RestoreMode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (_originalTermiosMacOS.HasValue)
            {
                var termios = _originalTermiosMacOS.Value;
                // Use TCSAFLUSH to discard any pending input
                tcsetattr_macos(STDIN_FILENO, TCSAFLUSH, ref termios);
                _originalTermiosMacOS = null;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (_originalTermiosLinux.HasValue)
            {
                var termios = _originalTermiosLinux.Value;
                // Use TCSAFLUSH to discard any pending input
                tcsetattr_linux(STDIN_FILENO, TCSAFLUSH, ref termios);
                _originalTermiosLinux = null;
            }
        }
        else
        {
            Console.TreatControlCAsInput = false;
        }
    }

    // Constants
    private const int STDIN_FILENO = 0;
    private const int TCSANOW = 0;
    private const int TCSAFLUSH = 2;  // Flush pending input before applying changes

    // macOS constants (from /usr/include/sys/termios.h)
    private const ulong ECHO_MAC = 0x00000008;
    private const ulong ICANON_MAC = 0x00000100;
    private const ulong IEXTEN_MAC = 0x00000400;
    private const ulong IXON_MAC = 0x00000200;
    private const ulong BRKINT_MAC = 0x00000002;
    private const ulong INPCK_MAC = 0x00000010;
    private const ulong ISTRIP_MAC = 0x00000020;
    private const ulong CS8_MAC = 0x00000300;
    private const int VMIN_MAC = 16;
    private const int VTIME_MAC = 17;

    // Linux constants (from /usr/include/bits/termios.h)
    private const uint ECHO_LINUX = 0x00000008;
    private const uint ICANON_LINUX = 0x00000002;
    private const uint IEXTEN_LINUX = 0x00008000;
    private const uint IXON_LINUX = 0x00000400;
    private const uint BRKINT_LINUX = 0x00000002;
    private const uint INPCK_LINUX = 0x00000010;
    private const uint ISTRIP_LINUX = 0x00000020;
    private const uint CS8_LINUX = 0x00000030;
    private const int VMIN_LINUX = 6;
    private const int VTIME_LINUX = 5;

    // macOS termios struct (72 bytes total)
    [StructLayout(LayoutKind.Sequential)]
    private struct TermiosMacOS
    {
        public ulong c_iflag;   // 8 bytes
        public ulong c_oflag;   // 8 bytes
        public ulong c_cflag;   // 8 bytes
        public ulong c_lflag;   // 8 bytes
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] c_cc;     // 20 bytes
        public ulong c_ispeed;  // 8 bytes
        public ulong c_ospeed;  // 8 bytes
    }

    // Linux termios struct
    [StructLayout(LayoutKind.Sequential)]
    private struct TermiosLinux
    {
        public uint c_iflag;    // 4 bytes
        public uint c_oflag;    // 4 bytes
        public uint c_cflag;    // 4 bytes
        public uint c_lflag;    // 4 bytes
        public byte c_line;     // 1 byte
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] c_cc;     // 32 bytes (NCCS on Linux)
        public uint c_ispeed;   // 4 bytes
        public uint c_ospeed;   // 4 bytes
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "tcgetattr")]
    private static extern int tcgetattr_macos(int fd, out TermiosMacOS termios);

    [DllImport("libc", SetLastError = true, EntryPoint = "tcsetattr")]
    private static extern int tcsetattr_macos(int fd, int optional_actions, ref TermiosMacOS termios);

    [DllImport("libc", SetLastError = true, EntryPoint = "tcgetattr")]
    private static extern int tcgetattr_linux(int fd, out TermiosLinux termios);

    [DllImport("libc", SetLastError = true, EntryPoint = "tcsetattr")]
    private static extern int tcsetattr_linux(int fd, int optional_actions, ref TermiosLinux termios);
}
