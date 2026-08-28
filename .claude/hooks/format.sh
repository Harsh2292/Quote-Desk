#!/usr/bin/env bash
# PostToolUse on Edit|Write, async. Formats only the file that changed. Never blocks.
set -uo pipefail

if ! command -v jq >/dev/null 2>&1; then exit 0; fi

input=$(cat)
path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // ""')
[ -z "$path" ] || [ ! -f "$path" ] && exit 0

root="${CLAUDE_PROJECT_DIR:-$(pwd)}"

case "$path" in
  *.ts|*.tsx|*.css)
    web="$root/src/QuoteDesk.Web"
    if [ -d "$web/node_modules/prettier" ]; then
      (cd "$web" && npx --no-install prettier --write "$path" >/dev/null 2>&1) || true
    fi
    ;;
esac

exit 0
