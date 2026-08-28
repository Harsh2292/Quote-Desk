#!/usr/bin/env bash
# SessionStart. Context does not survive between sessions — this puts the essentials back.
set -uo pipefail

root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$root" 2>/dev/null || exit 0

echo "## QuoteDesk session context"
echo

branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "not a git repo")
echo "Branch: ${branch}"

dirty=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')
if [ "${dirty:-0}" != "0" ]; then
  echo "Uncommitted changes: ${dirty} file(s) — check whether the last task was finished before starting a new one."
fi

echo
echo "Last 3 commits:"
git log --oneline -3 2>/dev/null || echo "  (none)"

if [ -f tasks/README.md ]; then
  next=$(grep -m1 '| todo |' tasks/README.md 2>/dev/null | cut -c1-160)
  inprog=$(grep -m1 '| in progress |' tasks/README.md 2>/dev/null | cut -c1-160)
  echo
  if [ -n "$inprog" ]; then
    echo "IN PROGRESS — finish this before starting anything new:"
    echo "  ${inprog}"
  fi
  [ -n "$next" ] && { echo "Next todo task:"; echo "  ${next}"; }
fi

if [ -f docs/SESSION-LOG.md ]; then
  echo
  echo "Most recent session-log entry:"
  tail -n 12 docs/SESSION-LOG.md
fi

echo
echo "Reminder: one task at a time, from tasks/. Confirm Agent Framework APIs with the api-researcher subagent before using them. Run /verify-all before calling a task done. Disagree with me if a task looks wrong."

exit 0
