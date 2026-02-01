# termalive

Terminal session multiplexer - host, manage, and remotely attach to terminal sessions.

## Installation

```bash
dotnet tool install -g termalive
```

## Quick Start

```bash
# Start the session host (background, like docker -d)
termalive start -d

# Check if running
termalive status

# Create a new session
termalive new my-session --command bash

# List sessions
termalive list

# Attach to a session (Ctrl+B, D to detach)
termalive attach my-session

# Stream session output
termalive logs my-session --follow --wait-idle 5s

# Send input without attaching
termalive send my-session "echo hello"

# Terminate a session
termalive kill my-session

# Stop the host
termalive stop
```

## Commands

| Command | Description |
|---------|-------------|
| `start` | Start the session host daemon |
| `status` | Show daemon status |
| `stop` | Stop the daemon |
| `new <id>` | Create a new terminal session |
| `list` | List active sessions |
| `attach <id>` | Attach to a session interactively |
| `logs <id>` | Read/stream session output |
| `send <id> <text>` | Send input to a session |
| `kill <id>` | Terminate a session |

### start options

```
-d, --detach         Run in background (like docker -d)
-p, --port <port>    WebSocket port (default: 7777)
--pipe <name>        Named pipe name (default: termalive)
--no-pipe            Disable named pipe server
```

## Use Cases

- **Persistent sessions** - Terminal sessions survive disconnection
- **Remote access** - Connect via WebSocket from another machine
- **LLM orchestration** - Multiple AI sessions communicating with each other
- **Session sharing** - Multiple clients can observe the same session

## License

MIT
