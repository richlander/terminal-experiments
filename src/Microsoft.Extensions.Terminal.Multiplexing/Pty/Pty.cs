// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Factory for creating platform-specific PTY instances.
/// </summary>
public static class Pty
{
    /// <summary>
    /// Creates a new pseudo-terminal with the specified options.
    /// </summary>
    /// <param name="options">The PTY options.</param>
    /// <returns>A new PTY instance.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when PTY is not supported on the current platform.</exception>
    public static IPty Create(PtyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsConPty(options);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return new UnixPty(options);
        }
        else
        {
            throw new PlatformNotSupportedException("PTY is not supported on this platform.");
        }
    }
}
