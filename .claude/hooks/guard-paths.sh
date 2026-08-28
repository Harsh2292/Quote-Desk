#!/usr/bin/env bash
# PreToolUse on Edit|Write|NotebookEdit.
# CLAUDE.md asks Claude not to write secrets. This makes it impossible.
set -uo pipefail

if ! command -v jq >/dev/null 2>&1; then exit 0; fi

input=$(cat)
path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // .tool_input.notebook_path // ""')
[ -z "$path" ] && exit 0

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

case "$path" in
  *appsettings.Development.json|*appsettings.Local.json)
    deny "Blocked: local settings files hold real API keys and are gitignored. Use 'dotnet user-secrets set' instead, and put the key name (not the value) in appsettings.json." ;;
  *.env|*.env.*)
    [[ "$path" == *.env.example ]] || deny "Blocked: .env files are gitignored and hold secrets. Add the variable to .env.example with a placeholder value instead." ;;
  */secrets/*)
    deny "Blocked: nothing is written under secrets/." ;;
  *.pfx|*.pem|*.key|*id_rsa*)
    deny "Blocked: credential material must never be created in this repo." ;;
esac

exit 0
