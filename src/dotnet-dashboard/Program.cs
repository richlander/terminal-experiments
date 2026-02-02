// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Terminal;

// ============================================================================
// dotnet-dashboard: An animated .NET runtime dashboard demo
// 
// Features:
// - Alternate screen buffer (terminal restored on exit)
// - Animated dotnetbot with eye tracking and badge bounce
// - Scrolling marquee banner with custom message
// - Live system stats (CPU, memory, GC, heap)
// - Keyboard interaction
// - Neofetch-style layout
// ============================================================================

const string Version = "0.1.0";

// Parse arguments
string? message = null;
bool staticMode = false;
int? durationSeconds = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--static":
        case "-s":
            staticMode = true;
            break;
        case "--duration":
        case "-d":
            if (i + 1 < args.Length && int.TryParse(args[i + 1].TrimEnd('s'), out int dur))
            {
                durationSeconds = dur;
                i++;
            }
            break;
        case "--help":
        case "-h":
            PrintHelp();
            return 0;
        case "--version":
        case "-v":
            Console.WriteLine($"dotnet-dashboard {Version}");
            return 0;
        default:
            if (!args[i].StartsWith('-'))
            {
                message = string.Join(" ", args.Skip(i));
                i = args.Length;
            }
            break;
    }
}

// Read from stdin if piped
if (message == null && Console.IsInputRedirected)
{
    message = Console.In.ReadToEnd().Trim();
}

message ??= "Welcome to Microsoft.Extensions.Terminal!";

// Detect ANSI support
bool useAnsi = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

if (!useAnsi)
{
    Console.WriteLine("ANSI terminal required for dashboard. Use --static for plain output.");
    return 1;
}

// Create terminal
var console = new SystemConsole();
var terminal = new AnsiTerminal(console);

// Run the dashboard
var dashboard = new Dashboard(terminal, message, staticMode, durationSeconds);
return await dashboard.RunAsync();

static void PrintHelp()
{
    Console.WriteLine("""
        dotnet-dashboard - Animated .NET runtime dashboard

        Usage: dotnet-dashboard [options] [message]

        Options:
          -s, --static          Static mode (no animation)
          -d, --duration <sec>  Exit after specified seconds
          -h, --help            Show this help
          -v, --version         Show version

        Examples:
          dotnet-dashboard
          dotnet-dashboard "Hello from the SDK team!"
          echo "Build succeeded!" | dotnet-dashboard
          dotnet-dashboard --duration 10

        Controls:
          q, Escape   Quit
          Space       Pause/resume animation
          +/-         Adjust animation speed
        """);
}

// ============================================================================
// Dashboard - Main application (neofetch-style layout)
// ============================================================================

sealed class Dashboard
{
    // ANSI escape codes
    private const string ESC = "\x1b";
    private const string CSI = $"{ESC}[";
    private const string RESET = $"{CSI}0m";
    private const string BOLD = $"{CSI}1m";
    private const string PURPLE = $"{CSI}35m";
    private const string BOLD_PURPLE = $"{CSI}1;35m";
    private const string WHITE = $"{CSI}37m";
    private const string GREY = $"{CSI}90m";
    private const string GREEN = $"{CSI}32m";
    private const string YELLOW = $"{CSI}33m";
    private const string RED = $"{CSI}31m";
    private const string CYAN = $"{CSI}36m";
    private const string BLUE = $"{CSI}34m";

    private readonly AnsiTerminal _terminal;
    private readonly string _message;
    private readonly bool _staticMode;
    private readonly int? _durationSeconds;
    private readonly Stopwatch _stopwatch = new();
    private readonly StringBuilder _frameBuffer = new(4096);

    // Layout constants
    private const int BOT_WIDTH = 28;
    private const int BAR_WIDTH = 20;

    // Animation state
    private int _marqueeOffset;
    private int _eyePosition; // 0=left, 1=center, 2=right
    private bool _badgeUp = true;
    private int _animationFrame;
    private bool _paused;
    private int _tickDelayMs = 100;
    private bool _layoutFlipped; // false = bot left, true = bot right

    // Cached runtime info (don't fetch every frame)
    private string? _cachedSdkVersion;
    private int _cachedRuntimeCount;
    private int _cachedSdkCount;
    private string? _cachedOsName;
    private string? _cachedHostname;

    // Live stats
    private long _heapBytes;
    private long _heapCeiling = 5 * 1024 * 1024; // Start at 5 MiB ceiling
    private int _gcGen0;
    private int _gcGen1;
    private int _gcGen2;
    private int _lastGcGen0;
    private bool _needsFullClear; // Set when layout changes
    private readonly List<byte[]> _memoryPressure = new(); // For demo: causes heap to grow

    public Dashboard(AnsiTerminal terminal, string message, bool staticMode, int? durationSeconds)
    {
        _terminal = terminal;
        _message = message;
        _staticMode = staticMode;
        _durationSeconds = durationSeconds;
    }

    public async Task<int> RunAsync()
    {
        // Cache static info once
        CacheStaticInfo();

        // Enter alternate screen and hide cursor
        Console.Write($"{CSI}?1049h{CSI}?25l");
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            _stopwatch.Start();
            await RenderLoopAsync();
            return 0;
        }
        finally
        {
            // Restore terminal
            Console.Write($"{CSI}?25h{CSI}?1049l");
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private void CacheStaticInfo()
    {
        _cachedSdkVersion = GetDotnetSdkVersion();
        _cachedRuntimeCount = CountDotnetRuntimes();
        _cachedSdkCount = CountDotnetSdks();
        _cachedOsName = GetOsName();
        _cachedHostname = Dns.GetHostName();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _stopwatch.Stop();
    }

    private async Task RenderLoopAsync()
    {
        while (_stopwatch.IsRunning)
        {
            if (_durationSeconds.HasValue && _stopwatch.Elapsed.TotalSeconds >= _durationSeconds.Value)
            {
                break;
            }

            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (!HandleKey(key))
                {
                    return;
                }
            }

            if (!_paused && !_staticMode)
            {
                UpdateAnimation();
            }

            UpdateStats();
            RenderFrame();

            await Task.Delay(_tickDelayMs);
        }
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                return false;

            case ConsoleKey.Spacebar:
                _paused = !_paused;
                break;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
                _layoutFlipped = !_layoutFlipped;
                _needsFullClear = true; // Force full screen clear on flip
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                _tickDelayMs = Math.Max(30, _tickDelayMs - 20);
                break;

            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                _tickDelayMs = Math.Min(300, _tickDelayMs + 20);
                break;
        }

        return true;
    }

    private void UpdateAnimation()
    {
        _animationFrame++;
        _marqueeOffset++;

        // Update eye position every 30 frames
        if (_animationFrame % 30 == 0)
        {
            _eyePosition = (_eyePosition + 1) % 3;
        }

        // Bounce badge every 5 frames
        if (_animationFrame % 5 == 0)
        {
            _badgeUp = !_badgeUp;
        }
    }

    private void UpdateStats()
    {
        _heapBytes = GC.GetTotalMemory(false);
        
        // Calculate ceiling as next "nice" value above current heap
        // This keeps the bar from always being at 100% or always at 50%
        _heapCeiling = GetNiceCeiling(_heapBytes);

        _lastGcGen0 = _gcGen0;
        _gcGen0 = GC.CollectionCount(0);
        _gcGen1 = GC.CollectionCount(1);
        _gcGen2 = GC.CollectionCount(2);

        // Create some memory pressure for demo (allocate small chunks periodically)
        if (_animationFrame % 8 == 0 && _memoryPressure.Count < 200)
        {
            _memoryPressure.Add(new byte[1024 * 50]); // 50KB chunks
        }
        // Occasionally release to show GC activity and bar going down
        if (_animationFrame % 60 == 0 && _memoryPressure.Count > 30)
        {
            _memoryPressure.RemoveRange(0, 20);
            GC.Collect(0); // Trigger minor GC to show activity
        }
    }

    private static long GetNiceCeiling(long bytes)
    {
        // Nice ceiling values in bytes: 2, 5, 10, 20, 50, 100, 200, 500 MiB, etc.
        long[] niceValues = [
            2 * 1024 * 1024,
            5 * 1024 * 1024,
            10 * 1024 * 1024,
            20 * 1024 * 1024,
            50 * 1024 * 1024,
            100 * 1024 * 1024,
            200 * 1024 * 1024,
            500 * 1024 * 1024,
            1024 * 1024 * 1024,
        ];

        // Find the smallest nice value that's > bytes (so bar is never at 100%)
        foreach (var nice in niceValues)
        {
            if (nice > bytes)
            {
                return nice;
            }
        }
        
        // Fallback for very large heaps
        return bytes * 2;
    }

    private void RenderFrame()
    {
        _frameBuffer.Clear();

        // Full clear if layout changed, otherwise just home
        if (_needsFullClear)
        {
            _frameBuffer.Append($"{CSI}2J{CSI}H");
            _needsFullClear = false;
        }
        else
        {
            _frameBuffer.Append($"{CSI}H");
        }

        int row = 1;

        // === Marquee banner (single line, simple) ===
        row = RenderMarquee(row);
        row++;

        // === Main content: Bot on left, info + gauges on right ===
        row = RenderBotAndInfo(row);
        row++;

        // === Footer ===
        RenderFooter(row + 1);

        // Write entire frame at once
        Console.Write(_frameBuffer.ToString());
    }

    private int RenderMarquee(int startRow)
    {
        int width = Math.Min(_terminal.Width, 120);
        
        // Create scrolling text
        string paddedMessage = $"   {_message}   ";
        int textWidth = width - 4;
        
        _frameBuffer.Append($"{CSI}{startRow};1H");
        _frameBuffer.Append($"{CYAN}  ");
        
        // Render scrolling text
        for (int i = 0; i < textWidth; i++)
        {
            int charIndex = (_marqueeOffset + i) % paddedMessage.Length;
            _frameBuffer.Append(paddedMessage[charIndex]);
        }
        
        _frameBuffer.Append($"{RESET}");
        _frameBuffer.Append($"{CSI}K"); // Clear to end of line
        
        return startRow + 1;
    }

    private int RenderBotAndInfo(int startRow)
    {
        string[] botLines = GetBotFrame();
        string[] infoLines = GetInfoLines();
        string[] gaugeLines = GetGaugeLines();

        // Combine info and gauges into a boxed panel
        var rightContent = infoLines.Concat(gaugeLines).ToArray();
        
        // Calculate columns based on layout direction
        int leftCol = 3;
        int rightCol = _layoutFlipped ? 3 : 35;
        int botCol = _layoutFlipped ? 50 : 3;

        // Draw the info panel with box
        int panelWidth = 38;
        int maxLines = Math.Max(botLines.Length, rightContent.Length + 2);

        for (int i = 0; i < maxLines; i++)
        {
            int row = startRow + i;
            _frameBuffer.Append($"{CSI}{row};1H");

            if (_layoutFlipped)
            {
                // Info panel on left
                RenderInfoPanelLine(i, rightContent, panelWidth, leftCol);
                // Bot on right
                if (i < botLines.Length)
                {
                    _frameBuffer.Append($"{CSI}{row};{botCol}H");
                    _frameBuffer.Append(botLines[i]);
                }
            }
            else
            {
                // Bot on left
                if (i < botLines.Length)
                {
                    _frameBuffer.Append($"{CSI}{row};{botCol}H");
                    _frameBuffer.Append(botLines[i]);
                }
                // Info panel on right
                RenderInfoPanelLine(i, rightContent, panelWidth, rightCol);
            }

            _frameBuffer.Append($"{CSI}K");
        }

        return startRow + maxLines;
    }

    private void RenderInfoPanelLine(int lineIndex, string[] content, int width, int col)
    {
        _frameBuffer.Append($"{CSI}{col}G");
        
        if (lineIndex == 0)
        {
            // Top border
            _frameBuffer.Append($"{CYAN}╭{new string('─', width - 2)}╮{RESET}");
        }
        else if (lineIndex == content.Length + 1)
        {
            // Bottom border
            _frameBuffer.Append($"{CYAN}╰{new string('─', width - 2)}╯{RESET}");
        }
        else if (lineIndex > 0 && lineIndex <= content.Length)
        {
            // Content line with side borders
            _frameBuffer.Append($"{CYAN}│{RESET} ");
            _frameBuffer.Append(content[lineIndex - 1]);
            // Move to right edge and draw border
            _frameBuffer.Append($"{CSI}{col + width - 1}G{CYAN}│{RESET}");
        }
        else if (lineIndex > content.Length + 1)
        {
            // Empty space below panel
        }
    }

    private string[] GetBotFrame()
    {
        // Eye patterns
        string eyes = _eyePosition switch
        {
            0 => "(• )  (• )", // Looking left
            2 => "( •)  ( •)", // Looking right
            _ => " (•)  (•) ", // Center
        };

        // Badge bounce
        string badge1 = _badgeUp ? "|.NET|" : "|    |";
        string badge2 = _badgeUp ? "|    |" : "|.NET|";

        return
        [
            $"{PURPLE}         dNd{RESET}",
            $"{PURPLE}         dNd{RESET}",
            $"{PURPLE}     .dNNNNNNd.{RESET}",
            $"{PURPLE}   dNNNNNNNNNNNNd{RESET}",
            $"{PURPLE}  dNNNNNNNNNNNNNNNd{RESET}",
            $"{PURPLE} dNNN{RESET}{WHITE}.-----------.{RESET}{PURPLE}NNNd{RESET}",
            $"{PURPLE} dNNN{RESET}{WHITE}|{eyes}|{RESET}{PURPLE}NNNd{RESET}",
            $"{PURPLE} dNNN{RESET}{WHITE}'-----------'{RESET}{PURPLE}NNNd{RESET}",
            $"{PURPLE}   dNNNNd    dNNNNd{RESET}",
            $"{PURPLE}    dNd {RESET}{GREY}{badge1}{RESET}{PURPLE} dNd{RESET}",
            $"{PURPLE}    dNd {RESET}{GREY}{badge2}{RESET}{PURPLE} dNd{RESET}",
            $"{GREY}        '----'{RESET}",
        ];
    }

    private string[] GetInfoLines()
    {
        string separator = new('─', 32);
        string dotnetVersion = RuntimeInformation.FrameworkDescription.Split(' ').LastOrDefault() ?? "?";

        return
        [
            $"{BOLD_PURPLE}{Environment.UserName}{RESET}@{BOLD_PURPLE}{_cachedHostname}{RESET}",
            $"{GREY}{separator}{RESET}",
            $"{BOLD}.NET{RESET}: {dotnetVersion}",
            $"{BOLD}SDK{RESET}: {_cachedSdkVersion}",
            $"{BOLD}Runtimes{RESET}: {_cachedRuntimeCount}",
            $"{BOLD}SDKs{RESET}: {_cachedSdkCount}",
            "",
            $"{BOLD}OS{RESET}: {_cachedOsName}",
            $"{BOLD}Arch{RESET}: {RuntimeInformation.OSArchitecture}",
            $"{BOLD}CPU{RESET}: {Environment.ProcessorCount} cores",
        ];
    }

    private string[] GetGaugeLines()
    {
        // Calculate heap percentage relative to ceiling
        double heapPercent = _heapCeiling > 0 
            ? (double)_heapBytes / _heapCeiling * 100 
            : 0;
        heapPercent = Math.Min(99, heapPercent); // Never quite 100%
        
        // GC activity indicator (flashes when GC occurred)
        bool gcJustRan = _gcGen0 > _lastGcGen0;
        string gcIndicator = gcJustRan ? $"{YELLOW}●{RESET}" : $"{GREY}○{RESET}";

        string heapBar = RenderBar(heapPercent, BAR_WIDTH);
        string heapColor = heapPercent > 80 ? RED : heapPercent > 50 ? YELLOW : GREEN;

        return
        [
            "",
            $"{BOLD}Heap{RESET} {heapColor}{heapBar}{RESET} {FormatBytes(_heapBytes)}",
            "",
            $"{BOLD}GC{RESET} {gcIndicator}  Gen0:{CYAN}{_gcGen0,3}{RESET}  Gen1:{CYAN}{_gcGen1,2}{RESET}  Gen2:{CYAN}{_gcGen2,2}{RESET}",
        ];
    }

    private static string RenderBar(double percent, int width)
    {
        int filled = (int)(percent / 100 * width);
        filled = Math.Clamp(filled, 0, width);
        return new string('█', filled) + new string('░', width - filled);
    }

    private void RenderFooter(int row)
    {
        string pauseIndicator = _paused ? $"{YELLOW}[PAUSED]{RESET} " : "";
        string elapsed = $"Elapsed: {_stopwatch.Elapsed:mm\\:ss}";
        string layout = _layoutFlipped ? "◀ ▶ flip" : "◀ ▶ flip";
        string controls = "q: quit │ Space: pause │ +/-: speed";

        _frameBuffer.Append($"{CSI}{row};1H");
        _frameBuffer.Append($"  {GREY}{pauseIndicator}{elapsed} │ {layout} │ {controls}{RESET}");
        _frameBuffer.Append($"{CSI}K");
    }

    // Helper methods
    private static string FormatBytes(long bytes)
    {
        const double MiB = 1024 * 1024;
        const double GiB = MiB * 1024;
        return bytes switch
        {
            < (long)MiB => $"{bytes / 1024.0:F1} KiB",
            < (long)GiB => $"{bytes / MiB:F1} MiB",
            _ => $"{bytes / GiB:F2} GiB"
        };
    }

    private static string GetDotnetSdkVersion()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Trim() ?? "N/A";
        }
        catch { return "N/A"; }
    }

    private static int CountDotnetRuntimes()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    private static int CountDotnetSdks()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"macOS {Environment.OSVersion.Version}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var lines = File.ReadAllLines("/etc/os-release");
                    var prettyName = lines.FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
                    if (prettyName != null)
                    {
                        return prettyName.Split('=')[1].Trim('"');
                    }
                }
            }
            catch { }
            return "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSDescription;
        }
        return RuntimeInformation.OSDescription;
    }
}
