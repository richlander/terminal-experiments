#!/bin/bash
# Test script that simulates a TUI app with various terminal features
# This mimics what Claude CLI does: alternate screen, colors, cursor movement

# Enter alternate screen buffer
printf '\033[?1049h'

# Clear screen
printf '\033[2J'

# Move to top-left
printf '\033[H'

# Draw a colored header
printf '\033[1;44;37m ═══ termalive TUI Test ═══ \033[0m\n'
printf '\n'

# Show some styled text
printf '\033[1mBold text\033[0m | '
printf '\033[4mUnderlined\033[0m | '
printf '\033[31mRed\033[0m | '
printf '\033[32mGreen\033[0m | '
printf '\033[33mYellow\033[0m\n'
printf '\n'

# Draw a box
printf '\033[36m┌────────────────────┐\033[0m\n'
printf '\033[36m│\033[0m  Box with content  \033[36m│\033[0m\n'
printf '\033[36m└────────────────────┘\033[0m\n'
printf '\n'

# 256-color gradient
printf '256 colors: '
for i in {16..21}; do
    printf "\033[48;5;${i}m  \033[0m"
done
printf '\n'

# True color gradient
printf 'True color: '
for i in 0 50 100 150 200 255; do
    printf "\033[48;2;${i};100;150m  \033[0m"
done
printf '\n\n'

# Move cursor around
printf '\033[10;5HCursor at row 10, col 5'
printf '\033[12;5HCursor at row 12, col 5'

# Move to bottom area
printf '\033[20;1H'
printf '\033[90m── Press Enter to exit ──\033[0m\n'

# Wait for input (simulates interactive app)
read -r

# Exit alternate screen buffer
printf '\033[?1049l'
printf 'Exited TUI test.\n'
