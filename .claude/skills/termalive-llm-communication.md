# Skill: Using termalive for LLM-to-LLM Communication

When your Claude session is running inside termalive, you have access to powerful inter-session communication capabilities. This skill explains how to detect this environment and use it effectively.

## Detecting termalive Environment

Check if you're running inside a termalive-managed session:

```bash
# Check if termalive is available
which termalive

# List sessions (if this works, termalive host is running)
termalive list
```

If these commands succeed, you're in a termalive-enabled environment.

## Your Session Identity

When running in termalive, your session has a unique ID. You can discover other sessions:

```bash
termalive list
# Output:
# ID              STATE      COMMAND    CREATED
# ----------------------------------------------
# planner         running    claude     2 hours ago
# coder           running    claude     1 hour ago
# reviewer        running    claude     30 min ago
```

## Communicating with Other LLM Sessions

### Sending Messages to Another Session

```bash
# Send a task to another Claude session
termalive send coder "Please implement a REST API for user management. Reply with DONE when complete."
```

### Reading Another Session's Response

```bash
# Wait for response (stops on pattern or idle timeout)
termalive logs coder --follow --until "DONE" --wait-idle 30s

# Or capture to variable for processing
response=$(termalive logs coder -f --until "DONE" --wait-idle 30s)
```

### Understanding Exit Codes

When reading logs, the exit code tells you why it stopped:

| Exit Code | Meaning | Action |
|-----------|---------|--------|
| 0 | Pattern matched ("DONE" found) | Response complete, pattern found |
| 2 | Timeout reached | Session took too long, may need retry |
| 3 | Idle timeout | No output for N seconds = response complete |
| 4 | Session exited | Target session terminated |

```bash
termalive logs worker -f --wait-idle 10s
exit_code=$?

if [ $exit_code -eq 3 ]; then
    echo "Response complete (idle timeout)"
elif [ $exit_code -eq 0 ]; then
    echo "Pattern matched"
fi
```

## Communication Patterns

### Pattern 1: Request-Response

```bash
# You are the orchestrator
# Send task
termalive send worker "Analyze this code and list bugs. End with ANALYSIS_DONE."

# Wait for response
analysis=$(termalive logs worker -f --until "ANALYSIS_DONE" --wait-idle 30s)

# Process response
echo "Received analysis: ${#analysis} characters"
```

### Pattern 2: Delegation with Review

```bash
# Delegate to coder
termalive send coder "Implement login feature. Say IMPL_DONE when finished."
implementation=$(termalive logs coder -f --until "IMPL_DONE" --wait-idle 60s)

# Send to reviewer
termalive send reviewer "Review this implementation: $implementation. Say REVIEW_DONE."
review=$(termalive logs reviewer -f --until "REVIEW_DONE" --wait-idle 30s)

# Check review result
if echo "$review" | grep -qi "approved\|LGTM"; then
    echo "Implementation approved"
else
    echo "Changes requested, iterating..."
    termalive send coder "Address these review comments: $review"
fi
```

### Pattern 3: Parallel Workers

```bash
# Send same task to multiple workers
termalive send worker-1 "Solve problem X approach A. Say DONE when finished."
termalive send worker-2 "Solve problem X approach B. Say DONE when finished."

# Collect both responses
response1=$(termalive logs worker-1 -f --until "DONE" --wait-idle 30s)
response2=$(termalive logs worker-2 -f --until "DONE" --wait-idle 30s)

# Compare/combine results
echo "Worker 1 approach: ${#response1} chars"
echo "Worker 2 approach: ${#response2} chars"
```

### Pattern 4: Supervisor Monitoring

```bash
# Check on worker status
termalive list

# Get recent output from worker (last 1000 bytes, no follow)
termalive logs worker --tail 1000

# Intervene if needed
termalive send worker "Status update: what are you working on?"
```

## Best Practices

### 1. Use Clear Markers

Always use distinct end markers so you know when a response is complete:

```bash
termalive send worker "Do the task. When done, say exactly: ===TASK_COMPLETE==="
termalive logs worker -f --until "===TASK_COMPLETE===" --wait-idle 20s
```

### 2. Set Reasonable Timeouts

```bash
# Short task: 30s idle, 2m total
termalive logs worker -f --wait-idle 30s --timeout 2m

# Long task: 60s idle, 10m total  
termalive logs worker -f --wait-idle 60s --timeout 10m
```

### 3. Handle Failures

```bash
termalive send worker "Do the task"
result=$(termalive logs worker -f --wait-idle 30s --timeout 2m)
exit_code=$?

case $exit_code in
    0|3) 
        echo "Success"
        ;;
    2)
        echo "Timeout - task too complex or worker stuck"
        termalive send worker "Please summarize your progress so far"
        ;;
    4)
        echo "Worker session died - need to restart"
        termalive new worker --command "claude"
        ;;
esac
```

### 4. Keep Messages Focused

When sending to other LLMs, be specific:

```bash
# Good: specific, actionable
termalive send coder "Create a Python function that validates email addresses using regex. Include docstring and type hints. Say DONE when complete."

# Bad: vague
termalive send coder "write some code"
```

## JSON Mode for Structured Communication

For machine-readable output between LLMs:

```bash
# Request structured output
termalive send analyzer "Analyze this code and respond in JSON format:
{\"bugs\": [...], \"suggestions\": [...], \"score\": N}
End with JSON_DONE"

# Capture JSON response
response=$(termalive logs analyzer -f --until "JSON_DONE" --wait-idle 20s)

# Parse with jq
bugs=$(echo "$response" | grep -o '{.*}' | jq '.bugs')
```

## Creating New Sessions

If you need to spawn additional workers:

```bash
# Create a new Claude session
termalive new helper-1 --command "claude" --cwd /path/to/project

# Send it an initial task
termalive send helper-1 "You are a code review specialist. Wait for code to review."
```

## Session Lifecycle

```bash
# List all sessions
termalive list

# Terminate a session when done
termalive kill helper-1

# Force terminate if stuck
termalive kill helper-1 --force
```

## Example: Full Orchestration Script

```bash
#!/bin/bash
# You can run this as a bash command

TASK="Build a REST API for a todo application"

# Check environment
if ! termalive list &>/dev/null; then
    echo "termalive not available"
    exit 1
fi

# Create workers if they don't exist
termalive new planner --command claude 2>/dev/null || true
termalive new coder --command claude 2>/dev/null || true

# Step 1: Plan
termalive send planner "Create implementation plan for: $TASK. Say PLAN_DONE when finished."
plan=$(termalive logs planner -f --until "PLAN_DONE" --wait-idle 30s)

# Step 2: Implement
termalive send coder "Implement this plan: $plan. Say CODE_DONE when finished."
code=$(termalive logs coder -f --until "CODE_DONE" --wait-idle 60s)

# Step 3: Review
termalive send planner "Review this implementation: $code. Say REVIEW_DONE."
review=$(termalive logs planner -f --until "REVIEW_DONE" --wait-idle 30s)

echo "=== RESULT ==="
echo "$review"
```

## Summary

When running inside termalive:

1. **Detect**: `termalive list` to check environment
2. **Send**: `termalive send <session> "message"` to communicate
3. **Receive**: `termalive logs <session> -f --wait-idle Ns` to get responses
4. **Markers**: Use clear end patterns like `DONE`, `COMPLETE`, `===END===`
5. **Timeouts**: Always set `--wait-idle` and optionally `--timeout`
6. **Exit codes**: Check `$?` to know why logs stopped (0=pattern, 3=idle, 2=timeout)

This enables powerful multi-agent workflows where Claude sessions collaborate, delegate, and review each other's work.
