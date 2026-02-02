# Termalive Design Document

## Overview

Termalive is a **session orchestration layer** for terminal sessions. Unlike tmux, which focuses on terminal multiplexing with split panes and window management, termalive focuses on:

- **Observability**: Watch sessions without taking control
- **Hijacking**: Take over an existing session interactively
- **Parking**: Detach and resume sessions later
- **LLM Interop**: Sessions as addressable endpoints for agent-to-agent interaction

## Design Inspirations

| Capability | Inspiration |
|------------|-------------|
| Detached execution | `docker run -d` |
| Attach to running process | `docker exec -it`, `tmux attach` |
| Log streaming | `tail -f`, `docker logs -f` |
| State watching | `kubectl get -w` |
| Inline session | `bash`, `su`, `ssh` |

## Session Modes

### 1. Quick Attached Start (Inline)

**Command**: `termalive` or `termalive --command "python"`

**Behavior**:
- Starts a new managed session inline in the current terminal
- No alternate screen buffer - output flows naturally like running `bash` in `bash`
- Shows visual indication that you're in a managed session (e.g., modified prompt or status line)
- Supports detaching with `Ctrl+B, D`
- Session continues running after detach

**User Experience**:
```
$ termalive
[termalive: session abc123]
$ echo "I'm in a managed session"
I'm in a managed session
$ # Press Ctrl+B, D
[detached from abc123]
$
```

**Implementation Notes**:
- Do NOT use alternate screen buffer (`\x1b[?1049h`)
- Stream PTY output directly to stdout
- No initial "render screen" snapshot needed - just start streaming

### 2. Detached Start

**Command**: `termalive run <session-id> [-- command]`

**Behavior**:
- Starts session in background, returns immediately
- Like `docker run -d`
- Prints session ID for later attachment

**User Experience**:
```
$ termalive run claude-agent -- claude
Started session: claude-agent
$ termalive run build-worker -- npm run build
Started session: build-worker
$
```

### 3. Attach to Existing Session

**Command**: `termalive attach <session-id>`

**Behavior**:
- Takes over an existing session interactively
- Uses alternate screen buffer (like tmux attach)
- Shows current screen state, then streams updates
- Supports detaching with `Ctrl+B, D`

**User Experience**:
```
$ termalive attach claude-agent
[Attaching to claude-agent...]
# Full screen terminal session
# Press Ctrl+B, D to detach
[detached]
$
```

**Implementation Notes**:
- Uses alternate screen buffer since we're "taking over" the terminal
- Renders current screen state via `RenderScreen()` before streaming
- This is where the current implementation is mostly correct

## Observability Modes

### Log Streaming (`-f` / `--follow`)

**Command**: `termalive logs [-f] <session-id>`

**Behavior**:
- Without `-f`: Shows recent buffered output (like `docker logs`)
- With `-f`: Streams ongoing output (like `tail -f`, `docker logs -f`)
- Read-only - does not send input to session
- Multiple clients can stream simultaneously

**User Experience**:
```
$ termalive logs claude-agent           # Recent output
$ termalive logs -f claude-agent        # Follow mode
$ termalive logs -f --tail 100 agent    # Last 100 lines, then follow
```

**Attribution**: Follow mode inspired by `tail -f` (BSD, 1987) and `docker logs -f`.

### State Watching (`-w` / `--watch`)

**Command**: `termalive watch <session-id...>`

**Behavior**:
- Streams structured state change events, not raw terminal output
- Useful for orchestration and monitoring dashboards
- Can watch multiple sessions simultaneously

**Event Types**:
| Event | Description |
|-------|-------------|
| `state=starting` | Session is initializing |
| `state=running` | Session is executing normally |
| `state=waiting` | Session is waiting for input or permission |
| `state=completed` | Session has exited |
| `state=failed` | Session encountered an error |
| `permission_requested` | LLM is requesting permission for an action |
| `progress` | LLM has reported progress on a task |

**User Experience**:
```
$ termalive watch claude-agent copilot-session
14:32:01 [claude-agent]    state=running task="implementing feature"
14:32:45 [claude-agent]    state=waiting reason="permission_required" action="delete foo.txt"
14:33:02 [claude-agent]    state=running
14:33:15 [copilot-session] state=completed exit=0
14:35:17 [claude-agent]    state=completed exit=0
```

**Attribution**: State watching inspired by `kubectl get -w` for Kubernetes resources.

## State Signaling Protocol

For structured state watching to work, sessions need a way to signal state changes. Three mechanisms are supported:

### 1. Escape Sequence Signaling (Cooperative)

Tools that are aware of termalive can emit OSC escape sequences:

```
ESC ] 7777 ; state=waiting ; reason=permission_required BEL
```

This is similar to iTerm2/Terminal.app shell integration sequences.

### 2. CLI Signaling (Cooperative)

Tools can call the termalive CLI to signal state:

```bash
termalive signal --state=waiting --reason="permission_required"
termalive signal --progress="50% complete"
```

### 3. Pattern Detection (Non-cooperative)

For tools that don't know about termalive, common patterns can be detected:

- `[y/N]` or `[Y/n]` → `state=waiting, reason=confirmation`
- Known LLM permission prompts → `state=waiting, reason=permission_required`

## Command Summary

| Command | Description |
|---------|-------------|
| `termalive` | Start inline attached session (default shell) |
| `termalive --command <cmd>` | Start inline attached session with command |
| `termalive run <id> [-- cmd]` | Start detached session |
| `termalive attach <id>` | Attach to existing session (takes over) |
| `termalive logs <id>` | Show recent output |
| `termalive logs -f <id>` | Stream output (follow mode) |
| `termalive watch <id...>` | Watch structured state events |
| `termalive list` | List all sessions |
| `termalive kill <id>` | Terminate a session |
| `termalive signal --state=<s>` | Signal state change (from within session) |

## Implementation Changes Required

### Fix: Blank Screen on New Session

**Problem**: When running `termalive new`, the alternate screen buffer is entered before any content exists, showing a blank screen.

**Solution**: For inline attached mode (`termalive` without explicit attach):
1. Do NOT enter alternate screen buffer
2. Stream PTY output directly to terminal
3. Only use alternate screen for `termalive attach` (reconnecting to existing)

### New: Inline Session Mode

Implement the "quick attached start" mode:
1. New command: `termalive` (no subcommand) starts inline session
2. No alternate screen buffer
3. Visual indicator of managed session (optional prompt integration)
4. Detach with Ctrl+B, D leaves session running

### Rename: `new` → `run`

Change `termalive new` to `termalive run` for consistency with Docker:
- `termalive run my-session` - start detached
- `termalive run my-session -- claude` - start detached with command

### New: Watch Mode

Implement `termalive watch` for structured state events:
1. Define state event protocol
2. Implement OSC sequence parsing in ManagedSession
3. Implement `termalive signal` CLI for cooperative signaling
4. Create watch command that streams events

## Architecture Notes

### Session Host

The `SessionHost` remains the central daemon that:
- Manages all sessions via `ManagedSession` instances
- Exposes WebSocket and named pipe endpoints
- Routes client connections to sessions
- Maintains screen buffer state for each session

### Client Modes

| Mode | Screen Buffer | Input | Output |
|------|---------------|-------|--------|
| Inline attached | None (pass-through) | Yes | Stream |
| Attach | Alternate | Yes | Render + Stream |
| Logs | None | No | Stream (read-only) |
| Watch | None | No | Events (structured) |

### Event Bus (Future)

For watch mode and multi-agent orchestration, consider an event bus:
- Sessions publish state events
- Watchers subscribe to events
- Enables reactive orchestration (e.g., "when claude-agent requests permission, notify operator")

## Known Issues and Improvements

### TerminalParser (terminal-pty repo)

#### ScreenBuffer

1. **No alternate screen buffer support** (line 744-747): The `SetMode` handler for mode 1049 (alternate screen) has a comment "Would need separate buffer implementation". This means applications that use alternate screen (vim, less, htop) won't render correctly when reattaching.

2. **No scrollback buffer**: The current implementation only keeps the visible screen area. There's no scrollback history, which limits the usefulness of `termalive logs` for seeing historical output.

3. **Missing wide character support**: The `Print` method handles UTF-8 codepoints but doesn't account for East Asian wide characters that occupy 2 cells. The `Width` field exists in `TerminalCell` but isn't used properly.

4. **No damage tracking**: Unlike xterm.js, there's no dirty region tracking for efficient differential rendering. Every `RenderScreen()` call renders the entire buffer.

#### VtParser

1. **Limited OSC support**: Only handles OSC 0, 1, 2 (window titles). Missing OSC 7 (current directory), OSC 8 (hyperlinks), OSC 52 (clipboard), and the proposed OSC 7777 for termalive state signaling.

2. **No DECSET/DECRST mode persistence**: Modes like bracketed paste (mode 2004), focus events (mode 1004), and mouse tracking aren't implemented.

3. **Single intermediate byte**: The parser only tracks one intermediate byte (line 275, 359), but some sequences use multiple intermediates.

### Multiplexing (Microsoft.Extensions.Terminal.Multiplexing)

#### UnixPty

1. **Blocking reads wrapped in Task.Run** (line 185): This works but is inefficient. Should use `poll()` or `select()` with async I/O for better scalability.

2. **Polling for exit** (line 150-161): Uses 100ms polling loop for `WaitForExitAsync`. Could use `signalfd` or `SIGCHLD` handler for immediate notification.

3. **No SIGWINCH handling**: Resize signals from the client are sent but the PTY doesn't propagate size changes to child properly in all cases.

#### ManagedSession

1. **Screen buffer resize loses content** (line 171-187): When resizing, a new buffer is created and content is copied, but this doesn't handle the case where the terminal application should reflow text.

2. **CircularBuffer vs ScreenBuffer confusion**: There are two buffers - `_outputBuffer` (raw bytes) and `_screenBuffer` (parsed state). The relationship is confusing and `GetBufferedOutput()` vs `RenderScreen()` serve different purposes that aren't well documented.

#### SessionHost

1. **Single client per session for streaming**: The current design attaches one client at a time for input. Multiple observers should be able to stream output simultaneously (read-only).

### Recommended Priority

1. **High**: Add OSC 7777 parsing for state signaling (enables watch mode)
2. **High**: Support multiple read-only observers per session
3. **Medium**: Implement alternate screen buffer in ScreenBuffer
4. **Medium**: Add scrollback buffer for historical output
5. **Low**: Improve PTY async I/O efficiency
6. **Low**: Add damage tracking for differential rendering

## Future Considerations

### Multi-Session Views

While not a goal for v1, the architecture should not preclude:
- Dashboard views of multiple sessions
- TUI for session management
- Web UI for remote monitoring

### Session Groups

For managing related sessions:
```bash
termalive run --group=my-project worker-1 -- npm run build
termalive run --group=my-project worker-2 -- npm run test
termalive watch --group=my-project  # Watch all sessions in group
```

### Permissions and Access Control

For multi-user/multi-agent scenarios:
- Session ownership
- Read-only vs read-write access
- Permission delegation between agents
