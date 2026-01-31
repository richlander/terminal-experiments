# Microsoft.Extensions.Terminal

A NativeAOT-compatible terminal library for .NET, consolidating scattered terminal infrastructure from across the dotnet ecosystem into a single, reusable package.

## Motivation

Terminal/ANSI code is duplicated across multiple dotnet repositories:

| Location | What It Does |
|----------|--------------|
| `dotnet/sdk` - `Cli.Utils/AnsiConsole.cs` | ANSI parser → ConsoleColor fallback |
| `dotnet/sdk` - `RazorSdk/Tool/AnsiConsole.cs` | Same code, copy-pasted |
| `dotnet/sdk` - `Test/MTP/Terminal/*` | Full ANSI terminal with batching, cursor, hyperlinks |
| `dotnet/msbuild` - `TerminalLogger` | Build output with progress, colors |
| External - `Spectre.Console` | Rich TUI (AOT concerns, large dependency) |

This library aims to:

1. **Unify** the terminal code into a single, well-maintained package
2. **Provide NativeAOT compatibility** as a first-class requirement
3. **Enable rich TUI applications** without heavy dependencies
4. **Support the .NET CLI tools ecosystem** (dotnet-*, global tools)

## Design Principles

- **100% NativeAOT compatible** - no reflection, no dynamic code generation
- **Zero external dependencies** - only `System.Console` and platform APIs
- **Layered architecture** - use only what you need
- **Struct-based where possible** - minimize allocations
- **Cross-platform** - Windows, macOS, Linux

## Package Structure

```text
Microsoft.Extensions.Terminal              <- Core: ANSI, TUI, zero deps
Microsoft.Extensions.Terminal.Multiplexing <- Session hosting, adds System.Net deps
```

**Core package** (`Microsoft.Extensions.Terminal`):
- Layers 0-4 (ANSI codes through components)
- Zero dependencies beyond `System.Console`
- Safe to reference from any project

**Multiplexing package** (`Microsoft.Extensions.Terminal.Multiplexing`):
- Layer 5 (PTY, session management, remote access)
- Uses `System.Net.WebSockets` + `System.IO.Pipes` (NOT ASP.NET Core)
- WebSocket server via `HttpListener` - no Kestrel dependency
- Only pulls BCL networking types

This keeps the core TUI library lightweight. A tool like `dotnet-runtimeinfo` can use just the core package with zero bloat.

## Architecture

```text
┌─────────────────────────────────────────────────────────────────┐
│ Layer 5: Session Multiplexing (PTY management, remote attach)  │
├─────────────────────────────────────────────────────────────────┤
│ Layer 4: Components (Table, Panel, Tree, List, ProgressBar)    │
├─────────────────────────────────────────────────────────────────┤
│ Layer 3: Layout (Grid, Rows, Columns, Padding)                 │
├─────────────────────────────────────────────────────────────────┤
│ Layer 2: Primitives (Text, Color, Border, Cursor)              │
├─────────────────────────────────────────────────────────────────┤
│ Layer 1: Terminal Abstraction (ITerminal, batching, detection) │
├─────────────────────────────────────────────────────────────────┤
│ Layer 0: Platform (ANSI codes, Windows console mode, P/Invoke) │
└─────────────────────────────────────────────────────────────────┘
```

### Session Multiplexing (Layer 5)

A key capability is **persistent terminal sessions** that survive client disconnection:

```text
┌─────────────────────────────────────────────────────────────────┐
│  SessionHost (daemon)                                          │
│    │                                                           │
│    ├─── ManagedSession "claude-1"                              │
│    │      └── PTY ──► claude (Claude Code process)             │
│    │                                                           │
│    ├─── ManagedSession "claude-2"                              │
│    │      └── PTY ──► claude (another session)                 │
│    │                                                           │
│    └─── WebSocket/Named Pipe Server                            │
│              ▲                                                 │
│              │                                                 │
│    ┌────────┴────────┐                                         │
│    │  Remote Client  │  (attach, send input, receive output)   │
│    └─────────────────┘                                         │
└─────────────────────────────────────────────────────────────────┘
```

This enables:
- **Long-lived sessions** - processes survive terminal disconnect (like tmux/screen)
- **Remote attachment** - connect from another machine (via Tailscale, SSH tunnel, etc.)
- **Session sharing** - multiple clients observe/interact with same session
- **Programmatic control** - tools can spawn and manage sessions via API

Unlike tmux, this is focused solely on session persistence and remote access—no terminal splitting UI.

## Existing Code to Consolidate

### From dotnet/sdk `Test/MTP/Terminal/`

These files form the foundation:

- **`AnsiCodes.cs`** - ANSI escape code constants
- **`AnsiTerminal.cs`** - Terminal with batching, cursor control, hyperlinks
- **`AnsiDetector.cs`** - Terminal capability detection (ported from Spectre.Console)
- **`TerminalColor.cs`** - VT100 16-color enum
- **`SimpleAnsiTerminal.cs`** - CI-friendly terminal without cursor movement
- **`NativeMethods.cs`** - Windows `ENABLE_VIRTUAL_TERMINAL_PROCESSING`

### From dotnet/sdk `Cli.Utils/`

- **`AnsiConsole.cs`** - ANSI → ConsoleColor parser (for legacy terminals)
- **`Reporter.cs`** - Thread-safe output with SpinLock, NO_COLOR support

## Planned Features

### Phase 1: Core (Extract + Package)

- [ ] Extract SDK terminal code into library structure
- [ ] Add proper interfaces (`ITerminal`, `IConsole`)
- [ ] Ensure NativeAOT compatibility with trimming annotations
- [ ] Add `NO_COLOR` environment variable support
- [ ] Package as `Microsoft.Extensions.Terminal`

### Phase 2: Enhanced Primitives

- [ ] 256-color support (codes 0-255)
- [ ] True color / 24-bit RGB support
- [ ] Box drawing characters (single, double, rounded borders)
- [ ] Unicode block elements for progress bars
- [ ] Background colors

### Phase 3: Layout System

- [ ] Screen buffer with dirty-region tracking
- [ ] Horizontal/vertical containers
- [ ] Padding and alignment
- [ ] Constraint-based sizing (fixed, percentage, fill)

### Phase 4: Components

- [ ] Panel with border and title
- [ ] Table with column alignment
- [ ] Progress bar / spinner
- [ ] Scrollable list view
- [ ] Tree view

### Phase 5: Input

- [ ] Non-blocking keyboard input
- [ ] Key event abstraction
- [ ] Mouse support (where available)
- [ ] Focus management

### Phase 6: Session Multiplexing

- [ ] PTY abstraction (Unix pty, Windows ConPTY)
- [ ] `ManagedSession` - spawns and owns a process with PTY
- [ ] `SessionHost` - manages multiple sessions, exposes server
- [ ] WebSocket transport for remote clients
- [ ] Named pipe transport for local IPC
- [ ] Session discovery and listing
- [ ] Input injection / output streaming
- [ ] Session resize handling (SIGWINCH)

## Usage Examples

### Basic Output

```csharp
using Microsoft.Extensions.Terminal;

var terminal = Terminal.Create();

terminal.WriteLine("Hello, ", TerminalColor.Green, "World", TerminalColor.Default, "!");
terminal.WriteLink("/path/to/file.cs", lineNumber: 42);
```

### Batched Updates (Flicker-Free)

```csharp
terminal.StartUpdate();
try
{
    terminal.MoveCursorUp(5);
    terminal.ClearToEndOfScreen();
    terminal.WriteLine("Updated content...");
}
finally
{
    terminal.StopUpdate();
}
```

### Capability Detection

```csharp
var caps = Terminal.DetectCapabilities();

if (caps.SupportsAnsi)
{
    // Use colors and cursor control
}
else
{
    // Fall back to plain text
}
```

### Session Multiplexing (Server)

```csharp
using Microsoft.Extensions.Terminal.Multiplexing;

// Start a session host
var host = new SessionHost();
await host.StartAsync(port: 7777);

// Spawn a managed session
var session = await host.CreateSessionAsync("my-session", new SessionOptions
{
    Command = "claude",
    WorkingDirectory = "/home/user/project",
    Environment = new Dictionary<string, string>
    {
        ["CLAUDE_CODE_ENTRYPOINT"] = "cli"
    }
});

// Session continues running even if this process exits
// (if host is configured as a daemon)
```

### Session Multiplexing (Client)

```csharp
using Microsoft.Extensions.Terminal.Multiplexing;

// Connect to a running session host (local or remote)
var client = await SessionClient.ConnectAsync("ws://localhost:7777");

// List available sessions
var sessions = await client.ListSessionsAsync();

// Attach to a session
await using var attachment = await client.AttachAsync("my-session");

// Send input
await attachment.SendInputAsync("hello\n");

// Receive output
await foreach (var output in attachment.OutputStream)
{
    Console.Write(output);
}
```

## Target Consumers

- **dotnet CLI** - `dotnet build`, `dotnet test`, `dotnet run`
- **MSBuild** - TerminalLogger
- **Global tools** - `dotnet-runtimeinfo`, `dotnet-counters`, etc.
- **TUI applications** - Dashboards, monitors, interactive tools
- **Session management tools** - persistent Claude Code sessions, remote dev environments

## Example Tool: dotnet-termhost

A thin CLI tool built on this library:

```bash
# Start the session host daemon
dotnet termhost start --port 7777

# Create a new persistent session running Claude Code
dotnet termhost new claude-work --command "claude" --cwd ~/projects/myapp

# List active sessions
dotnet termhost list
# ID           COMMAND    CWD                    CREATED
# claude-work  claude     ~/projects/myapp       2 hours ago
# claude-test  claude     ~/projects/tests       5 mins ago

# Attach to a session (from anywhere, including remote via Tailscale)
dotnet termhost attach claude-work

# Detach with Ctrl+B, D (configurable)

# Send a command to a session without attaching
dotnet termhost send claude-work "build the project"
```

The tool itself is simple—the library does the heavy lifting.

## Relationship to Spectre.Console

This is **not** a Spectre.Console replacement. Spectre.Console is excellent for rich console applications. This library targets:

- Scenarios where NativeAOT is required
- Minimal dependency footprint
- Integration with dotnet infrastructure
- Lower-level building blocks

The `AnsiDetector` in the SDK was already ported from Spectre.Console, acknowledging its quality.

## Contributing

This library is proposed to be part of the dotnet organization. Contributions welcome.

## License

MIT (matching dotnet/sdk)
