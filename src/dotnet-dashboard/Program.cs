// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Terminal;
using Microsoft.Extensions.Terminal.Components;

// ============================================================================
// dotnet-dashboard: An animated .NET runtime dashboard demo
// 
// This demo stress-tests the Microsoft.Extensions.Terminal library by using:
// - TerminalApp for the main render loop and keyboard handling
// - Layout for arranging components (horizontal/vertical)
// - Panel for bordered containers
// - Rule for separator lines
// - Text for styled content
// - ScreenBuffer for efficient differential rendering
// - IInteractiveComponent for keyboard handling
// - Custom IComponent implementations for animation
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

// Create terminal and run using TerminalApp
var systemConsole = new SystemConsole();
var terminal = new AnsiTerminal(systemConsole);
var app = new TerminalApp(terminal);

// Create the dashboard controller (handles keys and updates)
var dashboard = new DashboardController(app, message, staticMode, durationSeconds);

// Enter alternate screen
Console.Write(AnsiCodes.EnterAlternateScreen);

try
{
    // Run the app (TerminalApp handles render loop, resize, key dedup)
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    
    await app.RunAsync(cts.Token);
    return 0;
}
finally
{
    Console.Write(AnsiCodes.ExitAlternateScreen);
}

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
          h           Toggle help
          Space       Pause/resume animation
          +/-         Adjust animation speed
          ←/→         Flip layout
        """);
}

// ============================================================================
// DashboardController - Manages state and registers with TerminalApp
// ============================================================================

sealed class DashboardController : IInteractiveComponent
{
    private readonly TerminalApp _app;
    private readonly bool _staticMode;
    private readonly int? _durationSeconds;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Components
    private readonly MarqueeComponent _marquee;
    private readonly DotnetBotComponent _bot;
    private readonly InfoPanelComponent _infoPanel;
    private readonly FooterComponent _footer;
    private readonly SpacerComponent _spacer = new();

    // Cached layouts
    private readonly Layout _contentLayoutNormal;
    private readonly Layout _contentLayoutFlipped;

    // State
    private bool _paused;
    private bool _layoutFlipped;
    private bool _showHelp = true;
    private readonly List<byte[]> _memoryPressure = new();
    private int _animationFrame;
    private Timer? _animationTimer;

    public bool IsFocused { get; set; } = true;

    public DashboardController(TerminalApp app, string message, bool staticMode, int? durationSeconds)
    {
        _app = app;
        _staticMode = staticMode;
        _durationSeconds = durationSeconds;

        // Create components
        _marquee = new MarqueeComponent(message);
        _bot = new DotnetBotComponent();
        _infoPanel = new InfoPanelComponent();
        _infoPanel.CacheStaticInfo();
        _footer = new FooterComponent(_stopwatch);

        // Build content layouts
        _contentLayoutNormal = new Layout { Direction = LayoutDirection.Horizontal };
        _contentLayoutNormal.Add(_bot, LayoutSize.Fixed(28));
        _contentLayoutNormal.Add(_spacer, LayoutSize.Fixed(2));
        _contentLayoutNormal.Add(_infoPanel, LayoutSize.Fixed(40));

        _contentLayoutFlipped = new Layout { Direction = LayoutDirection.Horizontal };
        _contentLayoutFlipped.Add(_infoPanel, LayoutSize.Fixed(40));
        _contentLayoutFlipped.Add(_spacer, LayoutSize.Fixed(2));
        _contentLayoutFlipped.Add(_bot, LayoutSize.Fixed(28));

        // Register with TerminalApp
        app.RegisterFocusable(this);

        // Set up the layout in TerminalApp
        RebuildLayout();

        // Start animation timer (triggers Invalidate on each tick)
        _animationTimer = new Timer(OnAnimationTick, null, 0, 100);

        // Handle duration limit
        if (durationSeconds.HasValue)
        {
            _ = Task.Delay(TimeSpan.FromSeconds(durationSeconds.Value)).ContinueWith(_ => app.Stop());
        }
    }

    private void OnAnimationTick(object? state)
    {
        if (!_paused && !_staticMode)
        {
            _animationFrame++;
            _marquee.Update();
            _bot.Update();
        }

        // Update stats
        _infoPanel.UpdateStats();

        // Memory pressure demo
        if (_animationFrame % 8 == 0 && _memoryPressure.Count < 200)
        {
            _memoryPressure.Add(new byte[1024 * 50]);
        }
        if (_animationFrame % 60 == 0 && _memoryPressure.Count > 30)
        {
            _memoryPressure.RemoveRange(0, 20);
            GC.Collect(0);
        }

        // Request redraw
        _app.Invalidate();
    }

    private void RebuildLayout()
    {
        _app.Layout.Clear();
        _app.Layout.Add(_marquee, LayoutSize.Fixed(1));
        _app.Layout.Add(_spacer, LayoutSize.Fixed(1));
        _app.Layout.Add(_layoutFlipped ? _contentLayoutFlipped : _contentLayoutNormal, LayoutSize.Fixed(16));
        _app.Layout.Add(_spacer, LayoutSize.Fill);

        if (_showHelp)
        {
            _app.Layout.Add(_footer, LayoutSize.Fixed(3));
        }
    }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                _animationTimer?.Dispose();
                _app.Stop();
                return true;

            case ConsoleKey.Spacebar:
                _paused = !_paused;
                _footer.IsPaused = _paused;
                return true;

            case ConsoleKey.H:
                _showHelp = !_showHelp;
                _app.Buffer.Clear();
                RebuildLayout();
                return true;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
                _layoutFlipped = !_layoutFlipped;
                _app.Buffer.Clear();
                RebuildLayout();
                return true;

            default:
                return false;
        }
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        // This component doesn't render itself - it manages other components
    }
}

// ============================================================================
// Custom Components - Implementing IComponent
// ============================================================================

/// <summary>
/// Scrolling marquee banner component.
/// </summary>
sealed class MarqueeComponent : IComponent
{
    private readonly string _message;
    private int _offset;

    public MarqueeComponent(string message)
    {
        _message = $"   {message}   ";
    }

    public void Update()
    {
        _offset++;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        int x = region.X + 2;
        for (int i = 0; i < region.Width - 4 && x < region.X + region.Width; i++)
        {
            int charIndex = (_offset + i) % _message.Length;
            buffer.Write(x++, region.Y, _message[charIndex], TerminalColor.Cyan);
        }
    }
}

/// <summary>
/// Animated dotnetbot ASCII art component.
/// </summary>
sealed class DotnetBotComponent : IComponent
{
    private int _eyePosition; // 0=left, 1=center, 2=right
    private int _frame;

    public void Update()
    {
        _frame++;
        // Slow, subtle eye movement - changes every ~3 seconds at 100ms tick rate
        if (_frame % 30 == 0)
        {
            _eyePosition = (_eyePosition + 1) % 3;
        }
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        string eyes = _eyePosition switch
        {
            0 => "(• )  (• )", // Looking left
            2 => "( •)  ( •)", // Looking right
            _ => " (•)  (•) ", // Center
        };

        var lines = new (string text, TerminalColor color)[]
        {
            ("         dNd", TerminalColor.DarkMagenta),
            ("         dNd", TerminalColor.DarkMagenta),
            ("     .dNNNNNNd.", TerminalColor.DarkMagenta),
            ("   dNNNNNNNNNNNNd", TerminalColor.DarkMagenta),
            ("  dNNNNNNNNNNNNNNNd", TerminalColor.DarkMagenta),
        };

        int y = region.Y;
        foreach (var (text, color) in lines)
        {
            if (y >= region.Y + region.Height) break;
            buffer.Write(region.X, y++, text.AsSpan(), color);
        }

        // Face area (mixed colors)
        if (y < region.Y + region.Height)
        {
            int x = region.X;
            buffer.Write(x, y, " dNNN".AsSpan(), TerminalColor.DarkMagenta);
            buffer.Write(x + 5, y, ".-----------.".AsSpan(), TerminalColor.Gray);
            buffer.Write(x + 18, y, "NNNd".AsSpan(), TerminalColor.DarkMagenta);
            y++;
        }

        if (y < region.Y + region.Height)
        {
            int x = region.X;
            buffer.Write(x, y, " dNNN".AsSpan(), TerminalColor.DarkMagenta);
            buffer.Write(x + 5, y, "|".AsSpan(), TerminalColor.Gray);
            buffer.Write(x + 6, y, eyes.AsSpan(), TerminalColor.Gray);
            buffer.Write(x + 16, y, "|".AsSpan(), TerminalColor.Gray);
            buffer.Write(x + 17, y, "NNNd".AsSpan(), TerminalColor.DarkMagenta);
            y++;
        }

        if (y < region.Y + region.Height)
        {
            int x = region.X;
            buffer.Write(x, y, " dNNN".AsSpan(), TerminalColor.DarkMagenta);
            buffer.Write(x + 5, y, "'-----------'".AsSpan(), TerminalColor.Gray);
            buffer.Write(x + 18, y, "NNNd".AsSpan(), TerminalColor.DarkMagenta);
            y++;
        }

        if (y < region.Y + region.Height)
        {
            buffer.Write(region.X, y++, "   dNNNNd    dNNNNd".AsSpan(), TerminalColor.DarkMagenta);
        }

        // Static badge (no animation)
        if (y < region.Y + region.Height)
        {
            int x = region.X;
            buffer.Write(x, y, "    dNd ".AsSpan(), TerminalColor.DarkMagenta);
            buffer.Write(x + 8, y, "|.NET|".AsSpan(), TerminalColor.DarkGray);
            buffer.Write(x + 14, y, " dNd".AsSpan(), TerminalColor.DarkMagenta);
            y++;
        }

        if (y < region.Y + region.Height)
        {
            int x = region.X;
            buffer.Write(x, y, "    dNd ".AsSpan(), TerminalColor.DarkMagenta);
            buffer.Write(x + 8, y, "|    |".AsSpan(), TerminalColor.DarkGray);
            buffer.Write(x + 14, y, " dNd".AsSpan(), TerminalColor.DarkMagenta);
            y++;
        }

        if (y < region.Y + region.Height)
        {
            buffer.Write(region.X, y, "        '----'".AsSpan(), TerminalColor.DarkGray);
        }
    }
}

/// <summary>
/// Info panel with system stats and gauges.
/// </summary>
sealed class InfoPanelComponent : IComponent
{
    private string? _sdkVersion;
    private int _runtimeCount;
    private int _sdkCount;
    private string? _osName;
    private string? _hostname;

    private long _heapBytes;
    private long _heapCeiling = 5 * 1024 * 1024;
    private int _gcGen0;
    private int _gcGen1;
    private int _gcGen2;
    private int _lastGcGen0;

    public void CacheStaticInfo()
    {
        _sdkVersion = GetDotnetSdkVersion();
        _runtimeCount = CountDotnetRuntimes();
        _sdkCount = CountDotnetSdks();
        _osName = GetOsName();
        _hostname = Dns.GetHostName();
    }

    public void UpdateStats()
    {
        _heapBytes = GC.GetTotalMemory(false);
        _heapCeiling = GetNiceCeiling(_heapBytes);
        _lastGcGen0 = _gcGen0;
        _gcGen0 = GC.CollectionCount(0);
        _gcGen1 = GC.CollectionCount(1);
        _gcGen2 = GC.CollectionCount(2);
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        // Render content directly without a panel box (neofetch style)
        var content = new InfoContentComponent(this);
        content.Render(buffer, region);
    }

    // Expose data for inner component
    public string SdkVersion => _sdkVersion ?? "N/A";
    public int RuntimeCount => _runtimeCount;
    public int SdkCount => _sdkCount;
    public string OsName => _osName ?? "Unknown";
    public string Hostname => _hostname ?? "localhost";
    public long HeapBytes => _heapBytes;
    public long HeapCeiling => _heapCeiling;
    public int GcGen0 => _gcGen0;
    public int GcGen1 => _gcGen1;
    public int GcGen2 => _gcGen2;
    public bool GcJustRan => _gcGen0 > _lastGcGen0;

    private static long GetNiceCeiling(long bytes)
    {
        long[] niceValues = [
            2 * 1024 * 1024, 5 * 1024 * 1024, 10 * 1024 * 1024,
            20 * 1024 * 1024, 50 * 1024 * 1024, 100 * 1024 * 1024,
            200 * 1024 * 1024, 500 * 1024 * 1024, 1024 * 1024 * 1024,
        ];
        foreach (var nice in niceValues)
        {
            if (nice > bytes) return nice;
        }
        return bytes * 2;
    }

    private static string GetDotnetSdkVersion()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
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
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        }
        catch { return 0; }
    }

    private static int CountDotnetSdks()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        }
        catch { return 0; }
    }

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"macOS {Environment.OSVersion.Version}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var lines = File.ReadAllLines("/etc/os-release");
                    var prettyName = lines.FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
                    if (prettyName != null)
                        return prettyName.Split('=')[1].Trim('"');
                }
            }
            catch { }
            return "Linux";
        }
        return RuntimeInformation.OSDescription;
    }
}

/// <summary>
/// Inner content for the info panel (rendered inside the Panel border).
/// </summary>
sealed class InfoContentComponent : IComponent
{
    private const int LabelWidth = 10; // Right-align labels to this width
    private readonly InfoPanelComponent _info;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly Rule _separator = new() { Color = TerminalColor.DarkGray };

    public InfoContentComponent(InfoPanelComponent info)
    {
        _info = info;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        int y = region.Y;
        string dotnetVersion = RuntimeInformation.FrameworkDescription.Split(' ').LastOrDefault() ?? "?";

        // User@hostname (neofetch style - both parts colored)
        buffer.Write(region.X, y, Environment.UserName.AsSpan(), TerminalColor.Magenta);
        buffer.Write(region.X + Environment.UserName.Length, y, "@".AsSpan(), TerminalColor.Default);
        buffer.Write(region.X + Environment.UserName.Length + 1, y, _info.Hostname.AsSpan(), TerminalColor.Magenta);
        y++;

        // Separator line using Rule component
        _separator.Render(buffer, new Region(region.X, y, Math.Min(34, region.Width), 1));
        y++;

        // System info with right-aligned labels (neofetch style)
        WriteKeyValue(buffer, region.X, y++, "OS", _info.OsName);
        WriteKeyValue(buffer, region.X, y++, "Arch", RuntimeInformation.OSArchitecture.ToString());
        WriteKeyValue(buffer, region.X, y++, "Uptime", FormatUptime(_uptime.Elapsed));
        WriteKeyValue(buffer, region.X, y++, "CPU", $"{Environment.ProcessorCount} cores");

        y++; // blank line

        // .NET info
        WriteKeyValue(buffer, region.X, y++, ".NET", dotnetVersion);
        WriteKeyValue(buffer, region.X, y++, "SDK", _info.SdkVersion);
        WriteKeyValue(buffer, region.X, y++, "Runtimes", _info.RuntimeCount.ToString());
        WriteKeyValue(buffer, region.X, y++, "GC Mode", GCSettings.IsServerGC ? "Server" : "Workstation");
        WriteKeyValue(buffer, region.X, y++, "Threads", ThreadPool.ThreadCount.ToString());

        y++; // blank line

        // Memory bar
        double heapPercent = _info.HeapCeiling > 0 ? (double)_info.HeapBytes / _info.HeapCeiling * 100 : 0;
        heapPercent = Math.Min(99, heapPercent);
        var heapColor = heapPercent > 80 ? TerminalColor.Red : heapPercent > 50 ? TerminalColor.Yellow : TerminalColor.Green;

        WriteLabel(buffer, region.X, y, "Memory");
        RenderBar(buffer, region.X + LabelWidth + 2, y, 12, heapPercent, heapColor);
        buffer.Write(region.X + LabelWidth + 15, y, $" {FormatBytes(_info.HeapBytes)}".AsSpan(), TerminalColor.Default);
        y++;

        // GC indicator
        WriteLabel(buffer, region.X, y, "GC");
        int gcX = region.X + LabelWidth + 2;
        buffer.Write(gcX, y, _info.GcJustRan ? '●' : '○', _info.GcJustRan ? TerminalColor.Yellow : TerminalColor.DarkGray);
        buffer.Write(gcX + 2, y, $"Gen0:{_info.GcGen0,3} Gen1:{_info.GcGen1,2} Gen2:{_info.GcGen2,2}".AsSpan(), TerminalColor.Default);
        y++;

        y++; // blank line

        // Color palette (neofetch signature)
        RenderColorPalette(buffer, region.X, y);
    }

    private static void WriteLabel(ScreenBuffer buffer, int x, int y, string label)
    {
        // Right-align the label
        string padded = label.PadLeft(LabelWidth);
        buffer.Write(x, y, padded.AsSpan(), TerminalColor.Magenta);
        buffer.Write(x + LabelWidth, y, ": ".AsSpan(), TerminalColor.Default);
    }

    private static void WriteKeyValue(ScreenBuffer buffer, int x, int y, string key, string value)
    {
        WriteLabel(buffer, x, y, key);
        buffer.Write(x + LabelWidth + 2, y, value.AsSpan(), TerminalColor.Default);
    }

    private static void RenderBar(ScreenBuffer buffer, int x, int y, int width, double percent, TerminalColor color)
    {
        int filled = (int)(percent / 100 * width);
        filled = Math.Clamp(filled, 0, width);
        
        for (int i = 0; i < filled; i++)
        {
            buffer.Write(x + i, y, '█', color);
        }
        for (int i = filled; i < width; i++)
        {
            buffer.Write(x + i, y, '░', TerminalColor.DarkGray);
        }
    }

    private static void RenderColorPalette(ScreenBuffer buffer, int x, int y)
    {
        // First row: dark colors
        TerminalColor[] darkColors = [
            TerminalColor.Black, TerminalColor.DarkRed, TerminalColor.DarkGreen, TerminalColor.DarkYellow,
            TerminalColor.DarkBlue, TerminalColor.DarkMagenta, TerminalColor.DarkCyan, TerminalColor.Gray
        ];
        
        // Second row: bright colors  
        TerminalColor[] brightColors = [
            TerminalColor.DarkGray, TerminalColor.Red, TerminalColor.Green, TerminalColor.Yellow,
            TerminalColor.Blue, TerminalColor.Magenta, TerminalColor.Cyan, TerminalColor.White
        ];

        int offset = x + 2;
        foreach (var color in darkColors)
        {
            buffer.Write(offset, y, "███".AsSpan(), color);
            offset += 3;
        }

        offset = x + 2;
        foreach (var color in brightColors)
        {
            buffer.Write(offset, y + 1, "███".AsSpan(), color);
            offset += 3;
        }
    }

    private static string FormatUptime(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours} hours, {elapsed.Minutes} mins";
        if (elapsed.TotalMinutes >= 1)
            return $"{elapsed.Minutes} mins, {elapsed.Seconds} secs";
        return $"{elapsed.Seconds} secs";
    }

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
}

/// <summary>
/// Footer component showing controls and elapsed time in a boxed panel.
/// </summary>
sealed class FooterComponent : IComponent
{
    private readonly Stopwatch _stopwatch;
    public bool IsPaused { get; set; }

    public FooterComponent(Stopwatch stopwatch)
    {
        _stopwatch = stopwatch;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        // Use a Panel for the box
        var panel = new Panel
        {
            Border = BoxBorderStyle.Rounded,
            BorderColor = TerminalColor.DarkGray,
            Content = new FooterContentComponent(_stopwatch, IsPaused)
        };
        panel.Render(buffer, region);
    }
}

/// <summary>
/// Inner content for the footer panel.
/// </summary>
sealed class FooterContentComponent : IComponent
{
    private readonly Stopwatch _stopwatch;
    private readonly bool _isPaused;

    public FooterContentComponent(Stopwatch stopwatch, bool isPaused)
    {
        _stopwatch = stopwatch;
        _isPaused = isPaused;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        int x = region.X;
        int y = region.Y;

        if (_isPaused)
        {
            buffer.Write(x, y, "[PAUSED] ".AsSpan(), TerminalColor.Yellow);
            x += 9;
        }

        string elapsed = $"Elapsed: {_stopwatch.Elapsed:mm\\:ss}";
        buffer.Write(x, y, elapsed.AsSpan(), TerminalColor.Cyan);
        x += elapsed.Length;

        buffer.Write(x, y, "  │  ".AsSpan(), TerminalColor.DarkGray);
        x += 5;
        buffer.Write(x, y, "q".AsSpan(), TerminalColor.White);
        buffer.Write(x + 1, y, " quit  ".AsSpan(), TerminalColor.DarkGray);
        x += 8;
        buffer.Write(x, y, "h".AsSpan(), TerminalColor.White);
        buffer.Write(x + 1, y, " hide  ".AsSpan(), TerminalColor.DarkGray);
        x += 8;
        buffer.Write(x, y, "Space".AsSpan(), TerminalColor.White);
        buffer.Write(x + 5, y, " pause  ".AsSpan(), TerminalColor.DarkGray);
        x += 13;
        buffer.Write(x, y, "←→".AsSpan(), TerminalColor.White);
        buffer.Write(x + 2, y, " flip  ".AsSpan(), TerminalColor.DarkGray);
        x += 9;
        buffer.Write(x, y, "+/-".AsSpan(), TerminalColor.White);
        buffer.Write(x + 3, y, " speed".AsSpan(), TerminalColor.DarkGray);
    }
}

/// <summary>
/// Empty spacer component.
/// </summary>
sealed class SpacerComponent : IComponent
{
    public void Render(ScreenBuffer buffer, Region region)
    {
        // Intentionally empty - just takes up space in layout
    }
}
