#!/bin/bash
# Emit a line the moment a filed report gains new user feedback.
#
# The queue monitor watches for NEW FOLDERS. Feedback appended to an existing report's TASK.md
# creates no folder, so it woke nobody — on 2026-09-22 two reports sat in rework for five hours
# because of exactly this, hours after check-report-rework.py had been written to make the state
# visible. Detectable-on-demand is not the same as noticed.
#
# Prints one line per report ENTERING rework, so one already in rework stays quiet.
set -uo pipefail
ROOT="${1:-$HOME/FocusBear-Claude-Tasks}"
CHECK="$(cd "$(dirname "$0")" && pwd)/check-report-rework.py"
SEEN_FILE=$(mktemp)
: > "$SEEN_FILE"
ticks=0
trap 'rm -f "$SEEN_FILE"' EXIT

while true; do
  # Folder lines carry exactly two leading spaces; their detail lines carry six. Folder names
  # contain spaces ("Ready for Human QA/…"), so take the whole line rather than a field.
  # `|| true`: the checker exits 1 whenever anything is in rework, which is its normal state.
  current=$(python3 "$CHECK" --root "$ROOT" 2>/dev/null | sed -n 's/^  \([A-Z].*\)$/\1/p' | sort || true)
  while IFS= read -r folder; do
    [ -n "$folder" ] || continue
    if ! grep -qxF "$folder" "$SEEN_FILE"; then
      echo "REWORK: new feedback on ${folder##*/}"
    fi
  done <<< "$current"
  printf '%s\n' "$current" > "$SEEN_FILE"

  # Blocked reports, far less often: each check is a `gh` call per referenced PR, and a merge is not
  # something that needs noticing within the minute.
  ticks=$((ticks + 1))
  if [ $((ticks % 15)) -eq 0 ]; then
    python3 "$(dirname "$CHECK")/check-blocked-reports.py" --root "$ROOT" 2>/dev/null \
      | sed -n 's/^  \([a-z].*\)$/UNBLOCKED: \1/p' || true
  fi
  sleep 60
done
