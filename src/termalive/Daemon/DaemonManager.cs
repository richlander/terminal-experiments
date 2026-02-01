// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Termalive;

/// <summary>
/// Manages the termalive daemon lifecycle.
/// </summary>
internal static class DaemonManager
{
    private static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".termalive");

    private static string PidFile => Path.Combine(DataDirectory, "termalive.pid");
    private static string PortFile => Path.Combine(DataDirectory, "termalive.port");

    /// <summary>
    /// Starts the daemon in background mode.
    /// </summary>
    public static int StartDetached(int port, string? pipeName)
    {
        EnsureDataDirectory();

        // Check if already running
        if (TryGetRunningPid(out var existingPid))
        {
            Console.WriteLine($"termalive host already running (pid {existingPid})");
            PrintConnectionInfo(port, pipeName);
            return 1;
        }

        // Get the path to the current executable
        var exePath = Environment.ProcessPath ?? "termalive";

        // Build arguments for the background process
        var args = $"start --port {port}";
        if (pipeName != null)
        {
            args += $" --pipe {pipeName}";
        }
        else
        {
            args += " --no-pipe";
        }

        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, use "start /b" or create a detached process
            psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }
        else
        {
            // On Unix/macOS
            if (File.Exists("/usr/bin/setsid"))
            {
                // Linux: use setsid to fully detach
                psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/setsid",
                    Arguments = $"--fork {exePath} {args}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                };
            }
            else
            {
                // macOS: use nohup to background the process
                var logDir = Path.Combine(DataDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"termalive-{DateTime.Now:yyyyMMdd-HHmmss}.log");

                // Build the command string carefully
                var bashCmd = $"nohup '{exePath}' {args} > '{logFile}' 2>&1 & echo $!";

                psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashCmd.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }
        }

        try
        {
            var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start daemon process");
                return 1;
            }

            int pid;

            // On macOS with nohup, read the PID from stdout
            if (!File.Exists("/usr/bin/setsid") && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Read the echoed PID from bash
                var pidOutput = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (int.TryParse(pidOutput, out pid))
                {
                    // Give the background process time to start and write PID file
                    Thread.Sleep(500);
                    Console.WriteLine($"termalive host started (pid {pid})");
                }
                else
                {
                    Console.Error.WriteLine($"Failed to get daemon PID. Output: {pidOutput}");
                    return 1;
                }
            }
            else
            {
                // Give the process a moment to start and write its PID
                Thread.Sleep(500);

                // Try to read the PID that the child process wrote
                if (TryGetRunningPid(out pid))
                {
                    Console.WriteLine($"termalive host started (pid {pid})");
                }
                else
                {
                    // Fallback to the process we started (may be setsid wrapper)
                    pid = process.Id;
                    Console.WriteLine($"termalive host starting (pid {pid})");
                }
            }

            PrintConnectionInfo(port, pipeName);
            Console.WriteLine($"  PID file: {PidFile}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start daemon: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Writes the current process PID to the PID file.
    /// Called by the daemon process when it starts.
    /// </summary>
    public static void WritePidFile(int port)
    {
        EnsureDataDirectory();
        File.WriteAllText(PidFile, Environment.ProcessId.ToString());
        File.WriteAllText(PortFile, port.ToString());
    }

    /// <summary>
    /// Removes the PID file on shutdown.
    /// </summary>
    public static void RemovePidFile()
    {
        try
        {
            if (File.Exists(PidFile)) File.Delete(PidFile);
            if (File.Exists(PortFile)) File.Delete(PortFile);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Gets the status of the daemon.
    /// </summary>
    public static (bool Running, int? Pid, int? Port) GetStatus()
    {
        if (!TryGetRunningPid(out var pid))
        {
            return (false, null, null);
        }

        int? port = null;
        if (File.Exists(PortFile) && int.TryParse(File.ReadAllText(PortFile).Trim(), out var p))
        {
            port = p;
        }

        return (true, pid, port);
    }

    /// <summary>
    /// Stops the daemon.
    /// </summary>
    public static int StopDaemon()
    {
        if (!TryGetRunningPid(out var pid))
        {
            Console.WriteLine("termalive host is not running");
            return 1;
        }

        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();
            process.WaitForExit(5000);

            RemovePidFile();
            Console.WriteLine($"termalive host stopped (pid {pid})");
            return 0;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist
            RemovePidFile();
            Console.WriteLine("termalive host was not running (stale PID file removed)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to stop daemon: {ex.Message}");
            return 1;
        }
    }

    private static bool TryGetRunningPid(out int pid)
    {
        pid = 0;

        if (!File.Exists(PidFile))
        {
            return false;
        }

        var content = File.ReadAllText(PidFile).Trim();
        if (!int.TryParse(content, out pid))
        {
            return false;
        }

        // Check if process is actually running
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist
            return false;
        }
    }

    private static void EnsureDataDirectory()
    {
        if (!Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }
    }

    private static void PrintConnectionInfo(int port, string? pipeName)
    {
        Console.WriteLine($"  WebSocket: ws://localhost:{port}/");
        if (pipeName != null)
        {
            Console.WriteLine($"  Pipe: {pipeName}");
        }
    }
}
