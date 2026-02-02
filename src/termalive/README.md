# termalive

Terminal session orchestration - create, manage, and remotely attach to persistent terminal sessions.

## Installation

```bash
dotnet tool install -g termalive
```

## Quick Start

```bash
# Start the session host daemon (required first)
termalive start -d

# Start an inline session (like running bash in bash)
termalive

# Or start a detached session (like docker run -d)
termalive run my-session -- bash

# List sessions
termalive list

# Attach to a detached session
termalive attach my-session

# Stream session output (like tail -f)
termalive logs -f my-session

# Stop the daemon
termalive stop
```

## Design Philosophy

termalive is **not** trying to be tmux. It focuses on:

- **Observability**: Watch sessions without taking control (`logs -f`)
- **Hijacking**: Take over an existing session interactively (`attach`)
- **Parking**: Detach and resume sessions later
- **LLM Interop**: Sessions as addressable endpoints for agent-to-agent interaction

See [docs/termalive-design.md](../../docs/termalive-design.md) for full design rationale.

## Session Modes

### Inline Mode (default)
```bash
termalive                    # Start shell inline (like bash in bash)
termalive -c python          # Start python inline
```
Output flows in your current terminal. Press `Ctrl+B, D` to detach.

### Detached Mode
```bash
termalive run claude -- claude     # Start in background
termalive run build -- npm build   # Run build detached
```
Returns immediately. Use `attach` or `logs` to interact.

### Attach Mode
```bash
termalive attach my-session        # Take over session
```
Full-screen terminal takeover (uses alternate screen buffer).

## Commands

| Command | Description |
|---------|-------------|
| *(none)* | Start inline attached session |
| `run <id>` | Start a detached session (like docker run -d) |
| `attach <id>` | Attach to session interactively |
| `list` | List active sessions |
| `logs <id>` | View session output (-f to follow) |
| `send <id> <text>` | Send input without attaching |
| `kill <id>` | Terminate a session |
| `start` | Start the session host daemon |
| `status` | Show daemon status |
| `stop` | Stop the daemon |

### run options

```
-c, --command <cmd>  Command to run (default: $SHELL)
-a, --attach         Attach immediately after creating
-e, --env <K=V>      Set environment variable
--cwd <dir>          Working directory
-t, --idle-timeout   Auto-terminate after idle time (e.g., 10m)
```

### logs options

```
-f, --follow         Stream output continuously (like tail -f)
--tail <n>           Show last n lines
--wait-idle <time>   Exit after idle period
```

## Use Cases

- **Persistent sessions** - Terminal sessions survive disconnection
- **Remote access** - Connect via WebSocket from another machine
- **LLM orchestration** - AI agents running in managed sessions
- **Build monitoring** - `termalive run build -- npm build && termalive logs -f build`
- **Session sharing** - Multiple clients can observe the same session

## License

MIT
