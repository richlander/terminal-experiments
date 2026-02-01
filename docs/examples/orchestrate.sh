#!/bin/bash
# orchestrate.sh - Example: Two LLMs collaborate on a task
#
# This script demonstrates how to use termalive to coordinate
# two Claude sessions: a Planner and a Coder.
#
# Usage: ./orchestrate.sh "Build a REST API for a todo app"

set -e

TASK="${1:-Build a simple REST API for managing todos}"
HOST_URI="pipe://termalive"

echo "🚀 Starting LLM orchestration..."
echo "   Task: $TASK"
echo ""

# Ensure host is running (in production, this would be a daemon)
# termalive start &

# Create two sessions
echo "📦 Creating sessions..."
termalive new planner --command "claude" --uri "$HOST_URI" 2>/dev/null || true
termalive new coder --command "claude" --uri "$HOST_URI" 2>/dev/null || true

echo "✅ Sessions ready"
echo ""

# Step 1: Ask Planner to break down the task
echo "📋 Step 1: Planning..."
termalive send planner "Break down this task into implementation steps. Be concise. End with PLAN_DONE when finished: $TASK"

# Wait for planner response (stop on PLAN_DONE or 30s idle or 2m timeout)
plan=$(termalive logs planner --follow --until "PLAN_DONE" --wait-idle 30s --timeout 2m | \
    jq -r 'select(.event == "data") | .content' | tr -d '\n')

echo "   Plan received (${#plan} chars)"
echo ""

# Step 2: Send plan to Coder
echo "💻 Step 2: Implementing..."
termalive send coder "Implement this plan. Show the code. End with CODE_DONE when finished:

$plan"

# Wait for coder response
code=$(termalive logs coder --follow --until "CODE_DONE" --wait-idle 60s --timeout 5m | \
    jq -r 'select(.event == "data") | .content' | tr -d '\n')

echo "   Code received (${#code} chars)"
echo ""

# Step 3: Send back to Planner for review
echo "🔍 Step 3: Reviewing..."
termalive send planner "Review this implementation. List any issues or say LGTM if it looks good. End with REVIEW_DONE:

$code"

# Wait for review
review=$(termalive logs planner --follow --until "REVIEW_DONE" --wait-idle 30s --timeout 2m | \
    jq -r 'select(.event == "data") | .content' | tr -d '\n')

echo "   Review complete"
echo ""

# Check if changes needed
if echo "$review" | grep -qi "LGTM"; then
    echo "✅ Implementation approved!"
else
    echo "⚠️  Changes requested. Would iterate here..."
fi

echo ""
echo "📊 Final output saved to ./orchestration-output.txt"

# Save all outputs
cat > orchestration-output.txt << EOF
# LLM Orchestration Results
Task: $TASK
Generated: $(date)

## Plan
$plan

## Implementation  
$code

## Review
$review
EOF

echo "🏁 Done!"
