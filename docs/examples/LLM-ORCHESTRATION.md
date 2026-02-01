# LLM-to-LLM Orchestration with termalive

termalive enables multiple LLM sessions to communicate and collaborate on tasks.

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     termalive host                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ Session: planner│  │ Session: coder  │  │ Session: tester │  │
│  │   ┌─────────┐   │  │   ┌─────────┐   │  │   ┌─────────┐   │  │
│  │   │ Claude  │   │  │   │ Claude  │   │  │   │ Claude  │   │  │
│  │   └─────────┘   │  │   └─────────┘   │  │   └─────────┘   │  │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘  │
│           │                    │                    │           │
│           └────────────────────┼────────────────────┘           │
│                                │                                │
│                    WebSocket / Named Pipe                       │
└────────────────────────────────┼────────────────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │     Orchestrator        │
                    │  (script or supervisor) │
                    └─────────────────────────┘
```

## Key Commands

```bash
# Create sessions
termalive new planner --command "claude"
termalive new coder --command "claude"

# Send task to a session
termalive send planner "Create a plan for: $TASK"

# Read response (raw, with exit code)
response=$(termalive logs planner --follow --until "DONE" --wait-idle 10s)
exit_code=$?

# Check why it stopped
case $exit_code in
  0) echo "Pattern matched" ;;
  2) echo "Timeout" ;;
  3) echo "Idle timeout - response complete" ;;
  4) echo "Session exited" ;;
esac

# Or use JSONL for structured output
termalive logs planner -f --wait-idle 10s --json | jq -r 'select(.event=="data") | .content'
```

## JSONL Output Format

```jsonl
{"ts":"2026-02-01T03:40:00.000Z","session":"planner","event":"start"}
{"ts":"2026-02-01T03:40:00.100Z","session":"planner","event":"data","content":"Here's the plan:\n"}
{"ts":"2026-02-01T03:40:01.500Z","session":"planner","event":"data","content":"1. Create models\n2. Add routes\nDONE\n"}
{"ts":"2026-02-01T03:40:01.500Z","session":"planner","event":"end","reason":"pattern","pattern":"DONE"}
```

## Stop Conditions

The `logs` command supports multiple stop conditions (first one wins):

| Option | Description |
|--------|-------------|
| `--until <pattern>` | Stop when pattern appears in output |
| `--wait-idle <duration>` | Stop after N seconds of no output |
| `--timeout <duration>` | Stop after total duration |

```bash
# Stop on pattern OR 10s idle OR 2min total
termalive logs worker --follow --until "COMPLETE" --wait-idle 10s --timeout 2m
```

## Example: Planner + Coder

```bash
#!/bin/bash

# 1. Send task to planner
termalive send planner "Design a REST API for todos. Say PLAN_DONE when finished."

# 2. Wait for plan
plan=$(termalive logs planner -f --until "PLAN_DONE" --wait-idle 30s | \
    jq -rs 'map(select(.event=="data").content) | join("")')

# 3. Send plan to coder
termalive send coder "Implement this: $plan. Say CODE_DONE when finished."

# 4. Wait for code
code=$(termalive logs coder -f --until "CODE_DONE" --wait-idle 60s | \
    jq -rs 'map(select(.event=="data").content) | join("")')

echo "Generated code:"
echo "$code"
```

## Patterns for LLM Coordination

### 1. Sequential Pipeline
```
Planner → Coder → Reviewer → (iterate if needed)
```

### 2. Supervisor Pattern
```
Supervisor monitors multiple workers, redistributes failed tasks
```

### 3. Debate/Critique
```
LLM-A proposes → LLM-B critiques → LLM-A refines → repeat
```

### 4. Ensemble Voting
```
Multiple LLMs solve same problem → compare/vote on best solution
```

## Tips

1. **Use marker patterns** like `DONE`, `COMPLETE`, `---END---` for reliable detection
2. **Set reasonable timeouts** to avoid hanging on failed tasks
3. **Use `--wait-idle`** to detect when LLM stops generating
4. **Parse JSONL with jq** for easy extraction
5. **Log everything** for debugging multi-agent workflows
