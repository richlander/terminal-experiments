# Microsoft.Extensions.Terminal

A NativeAOT-compatible terminal library for .NET providing terminal output, VT parsing, and session multiplexing.

## Packages

| Package | Description |
|---------|-------------|
| `Microsoft.Extensions.Terminal` | Terminal output, ANSI detection, and TUI components |
| `Microsoft.Extensions.Terminal.Parser` | VT100/ANSI escape sequence parser and screen buffer |
| `Microsoft.Extensions.Terminal.Multiplexing` | PTY management, session hosting, and remote access |

## Design Principles

- **100% NativeAOT compatible** - no reflection, no dynamic code generation
- **Zero external dependencies** - only BCL types
- **Cross-platform** - Windows, macOS, Linux

---

## Microsoft.Extensions.Terminal

Core terminal abstraction with ANSI output, capability detection, and UI components.

### Terminal Output

```csharp
using Microsoft.Extensions.Terminal;

// Create a terminal (auto-detects ANSI support)
var terminal = new AnsiTerminal(new SystemConsole());

// Colored output
terminal.SetColor(TerminalColor.Green);
terminal.Append("Success: ");
terminal.ResetColor();
terminal.AppendLine("Operation completed");

// Clickable file links (supported terminals)
terminal.AppendLink("/src/Program.cs", lineNumber: 42);

// Batched updates (flicker-free)
terminal.StartUpdate();
terminal.HideCursor();
// ... multiple writes ...
terminal.ShowCursor();
terminal.StopUpdate();
```

### Capability Detection

```csharp
// Detect terminal type from TERM environment variable
bool supportsAnsi = AnsiDetector.IsAnsiSupported(Environment.GetEnvironmentVariable("TERM"));
```

### TUI Components

The library includes a component framework for building terminal UIs:

- **Layout**: `Layout`, `LayoutDirection`, `LayoutSize`, `Region`
- **Components**: `Panel`, `Table`, `TabView`, `Text`, `Rule`
- **Styling**: `BoxBorderStyle`, `TableBorderStyle`, `Alignment`
- **App Framework**: `TerminalApp`, `ViewStack`, `IComponent`

---

## Microsoft.Extensions.Terminal.Parser

VT100/VT500-compatible escape sequence parser based on the [Paul Williams state machine](https://vt100.net/emu/dec_ansi_parser). Design influenced by [vte (Rust/Alacritty)](https://github.com/alacritty/vte) and [xterm.js](https://github.com/xtermjs/xterm.js).

### Parsing VT Sequences

```csharp
using Microsoft.Extensions.Terminal.Parser;

// Create a screen buffer (implements IParserHandler)
var screen = new ScreenBuffer(80, 24);

// Create parser with handler
var parser = new VtParser(screen);

// Parse terminal output
parser.Parse(Encoding.UTF8.GetBytes("\x1b[31mRed text\x1b[0m"));

// Access screen state
var cell = screen[0, 0];
Console.WriteLine($"Character: {cell.Character}, Color: {cell.Foreground}");
```

### Custom Handler

```csharp
public class MyHandler : IParserHandler
{
    public void Print(char c) => Console.Write(c);
    public void Execute(byte controlCode) { /* Handle control codes */ }
    public void CsiDispatch(ReadOnlySpan<int> parameters, byte privateMarker, 
                            byte intermediates, char command) { /* CSI sequences */ }
    public void EscDispatch(byte intermediates, char command) { /* ESC sequences */ }
    public void OscDispatch(int command, ReadOnlySpan<byte> data) { /* OSC sequences */ }
    public void DcsHook(ReadOnlySpan<int> parameters, byte intermediates, char command) { }
    public void DcsPut(byte data) { }
    public void DcsUnhook() { }
}
```

### Screen Buffer Features

- Full cursor movement and positioning
- Text attributes: bold, italic, underline, strikethrough, blink, inverse
- Colors: 16 standard, 256 indexed, and 24-bit true color
- Scroll regions (DECSTBM) with origin mode (DECOM)
- Insert/delete characters and lines
- Erase operations (display, line, characters)

---

## Microsoft.Extensions.Terminal.Multiplexing

PTY management and session multiplexing for persistent terminal sessions.

### PTY Abstraction

```csharp
using Microsoft.Extensions.Terminal.Multiplexing;

// Create a PTY (cross-platform: Unix pty or Windows ConPTY)
var options = new PtyOptions
{
    Command = "/bin/bash",
    Arguments = Array.Empty<string>(),
    WorkingDirectory = Environment.CurrentDirectory,
    Columns = 80,
    Rows = 24
};

using var pty = Pty.Create(options);

// Read/write to the PTY
await pty.WriteAsync(Encoding.UTF8.GetBytes("echo hello\n"));
var buffer = new byte[1024];
int bytesRead = await pty.ReadAsync(buffer);
```

### Session Hosting

```csharp
// Start a session host
var host = new SessionHost(new SessionHostOptions { Port = 7777 });
await host.StartAsync();

// Create a managed session
var session = await host.CreateSessionAsync("my-session", new PtyOptions
{
    Command = "bash",
    WorkingDirectory = "/home/user/project"
});

// Sessions persist even if clients disconnect
```

### Session Client

```csharp
// Connect to a session host
var client = await SessionClient.ConnectAsync("ws://localhost:7777");

// List sessions
var sessions = await client.ListSessionsAsync();

// Attach to a session
await using var attachment = await client.AttachAsync("my-session");
await attachment.SendInputAsync("ls -la\n");

await foreach (var output in attachment.OutputStream)
{
    Console.Write(Encoding.UTF8.GetString(output));
}
```

### Architecture

```text
┌─────────────────────────────────────────────────────────────────┐
│  SessionHost                                                    │
│    ├── ManagedSession "session-1"                               │
│    │     ├── PTY (Unix/ConPTY)                                  │
│    │     ├── CircularBuffer (output history)                    │
│    │     └── ScreenBuffer (virtual terminal state)              │
│    │                                                            │
│    └── WebSocket/Named Pipe Server                              │
│              ▲                                                  │
│              │                                                  │
│         SessionClient (attach, input, resize)                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Acknowledgments

### Design Influences

The Parser library's architecture is based on established terminal emulator designs:

| Project | Influence |
|---------|-----------|
| [vte (Rust/Alacritty)](https://github.com/alacritty/vte) | Primary design influence. `IParserHandler` mirrors vte's `Perform` trait. State machine follows the same Paul Williams VT500 model. |
| [xterm.js](https://github.com/xtermjs/xterm.js) | Handler dispatch pattern and Zero Default Mode for CSI parameters. |
| [libvterm](https://www.leonerd.org.uk/code/libvterm/) | Reference implementation for VT behavior and screen buffer operations. |

### Test Suite Attribution

The test suite includes tests ported from other terminal projects to ensure compatibility:

| Source | Tests | Categories |
|--------|-------|------------|
| [vte](https://github.com/alacritty/vte) | ~300 | Parser state machine, UTF-8 handling, escape sequences |
| [libvterm](https://www.leonerd.org.uk/code/libvterm/) | ~240 | Screen buffer, scrolling, cursor movement, vttest, SGR attributes |
| [xterm.js](https://github.com/xtermjs/xterm.js) | ~160 | CSI parsing, OSC sequences, control codes |
| [VtNetCore](https://github.com/9a4db/VtNetCore) | ~10 | Editing operations |

These projects are licensed under MIT, Apache 2.0, or similar permissive licenses. See [THIRD-PARTY-NOTICES.TXT](THIRD-PARTY-NOTICES.TXT) for details.

---

## License

MIT
