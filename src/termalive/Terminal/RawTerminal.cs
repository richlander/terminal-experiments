// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Termalive;

/// <summary>
/// Provides raw terminal mode support for proper PTY passthrough.
/// </summary>
internal static class RawTerminal
{
    private static Termios? _originalTermios;

    /// <summary>
    /// Enters raw mode, disabling line buffering and echo.
    /// </summary>
    public static bool EnterRawMode()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On Windows, use Console.TreatControlCAsInput
            Console.TreatControlCAsInput = true;
            return true;
        }

        try
        {
            if (tcgetattr(STDIN_FILENO, out var termios) != 0)
            {
                return false;
            }

            _originalTermios = termios;

            // Disable canonical mode (line buffering) and echo
            // Keep ISIG so Ctrl+C works if needed
            termios.c_lflag &= ~(ICANON | ECHO | IEXTEN);

            // Disable some input processing but keep ICRNL for newlines
            termios.c_iflag &= ~(IXON | BRKINT | INPCK | ISTRIP);

            // KEEP output processing enabled - this is critical for proper rendering
            // termios.c_oflag &= ~OPOST;  // DON'T disable this!

            // Set character size to 8 bits
            termios.c_cflag |= CS8;

            // Read returns immediately with whatever is available
            termios.c_cc[VMIN] = 1;
            termios.c_cc[VTIME] = 0;

            if (tcsetattr(STDIN_FILENO, TCSANOW, ref termios) != 0)
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.TreatControlCAsInput = false;
            return;
        }

        if (_originalTermios.HasValue)
        {
            var termios = _originalTermios.Value;
            tcsetattr(STDIN_FILENO, TCSANOW, ref termios);
            _originalTermios = null;
        }
    }

    // Constants
    private const int STDIN_FILENO = 0;

    // c_lflag bits
    private const uint ECHO = 0x00000008;
    private const uint ICANON = 0x00000100;
    private const uint ISIG = 0x00000080;
    private const uint IEXTEN = 0x00000400;

    // c_iflag bits
    private const uint IXON = 0x00000200;
    private const uint ICRNL = 0x00000100;
    private const uint BRKINT = 0x00000002;
    private const uint INPCK = 0x00000010;
    private const uint ISTRIP = 0x00000020;

    // c_oflag bits
    private const uint OPOST = 0x00000001;

    // c_cflag bits
    private const uint CS8 = 0x00000300;

    // c_cc indices
    private const int VMIN = 16;
    private const int VTIME = 17;

    // tcsetattr actions
    private const int TCSANOW = 0;
    private const int TCSAFLUSH = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Termios
    {
        public uint c_iflag;
        public uint c_oflag;
        public uint c_cflag;
        public uint c_lflag;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] c_cc;
        public uint c_ispeed;
        public uint c_ospeed;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, out Termios termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int optional_actions, ref Termios termios);
}
