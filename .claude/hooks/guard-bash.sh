#!/usr/bin/env bash
# PreToolUse on Bash. Blocks the small set of commands that are never right in this repo.
set -uo pipefail

if ! command -v jq >/dev/null 2>&1; then exit 0; fi

input=$(cat)
cmd=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')
[ -z "$cmd" ] && exit 0

deny() {
  jq -n --arg reason "$1" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: $reason
    }
  }'
  exit 0
}

case "$cmd" in
  *"rm -rf "*|*"rm -fr "*)
    deny "Blocked: recursive delete. Remove specific files, or ask me to do it." ;;
  *"git push"*"--force"*|*"git push -f"*)
    deny "Blocked: force push. This repo's git history is part of the portfolio." ;;
  *"git reset --hard"*)
    deny "Blocked: hard reset discards work. Use 'git stash' or ask me first." ;;
  *"dotnet nuget push"*)
    deny "Blocked: nothing from this repo gets published to NuGet." ;;
  *"curl "*"| sh"*|*"curl "*"| bash"*|*"wget "*"| sh"*)
    deny "Blocked: piping a downloaded script into a shell. Install via the package manager instead." ;;
esac

exit 0
