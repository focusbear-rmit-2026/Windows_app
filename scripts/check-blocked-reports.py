#!/usr/bin/env python3
"""List reports in "Blocked on Backend" whose blocking PR has merged.

Why this exists
---------------
Three reports sat in Blocked on Backend after their PRs merged and deployed — the user had to ask
"have you been checking the backend PRs? I think some of them have been merged" before anyone
looked. Nothing watched for the blocker clearing: the queue monitor sees new folders, the rework
watcher sees new feedback, and neither sees a PR merging in another repo.

A merged PR is not a deployed one. This reports the merge and says so; deployment still needs
checking before a report is called testable.

    scripts/check-blocked-reports.py [--root DIR] [--repo Focus-Bear/backend]

Exit status is 1 when something is merged and still filed as blocked.
"""
import argparse
import json
import os
import re
import subprocess
import sys

# Windows consoles default to a legacy code page; the report text is UTF-8 (em dashes and all).
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# "backend#2437", "backend/pull/2437", "PR #2437" — four digits is the current range.
PR_PATTERN = re.compile(r'(?:backend[#/ ]|pull/|PR\s*#)(\d{4})', re.IGNORECASE)


def referenced_prs(folder):
    numbers = set()
    for name in ('CLAUDE-NOTES.md', 'TASK.md'):
        path = os.path.join(folder, name)
        try:
            with open(path, encoding='utf-8', errors='replace') as handle:
                numbers.update(PR_PATTERN.findall(handle.read()))
        except OSError:
            pass
    return sorted(numbers)


def merge_state(repo, number):
    """(state, mergedAt) for a PR, or None when it isn't one (an issue number, a typo)."""
    try:
        out = subprocess.run(
            ['gh', 'pr', 'view', number, '--repo', repo, '--json', 'state,mergedAt,title'],
            capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        return None
    if out.returncode != 0:
        return None
    try:
        data = json.loads(out.stdout)
    except ValueError:
        return None
    return data.get('state'), data.get('mergedAt'), data.get('title', '')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', default=os.path.expanduser('~/FocusBear-Claude-Tasks'))
    parser.add_argument('--repo', default='Focus-Bear/backend')
    arguments = parser.parse_args()

    blocked_dir = os.path.join(arguments.root, 'Blocked on Backend')
    if not os.path.isdir(blocked_dir):
        print('no "Blocked on Backend" folder'); return 0

    ready = []
    for name in sorted(os.listdir(blocked_dir)):
        folder = os.path.join(blocked_dir, name)
        if not os.path.isdir(folder):
            continue
        for number in referenced_prs(folder):
            state = merge_state(arguments.repo, number)
            if state and state[0] == 'MERGED':
                ready.append((name, number, state[1], state[2]))

    print('UNBLOCKED: %d report(s) whose PR has merged' % len(ready))
    for name, number, merged_at, title in ready:
        print('  %s\n      %s#%s merged %s — %s' % (name, arguments.repo, number,
                                                    (merged_at or '')[:16], title[:60]))
    if ready:
        print('\nMerged is not deployed. Check the production deploy before calling these testable.')
    return 1 if ready else 0


if __name__ == '__main__':
    sys.exit(main())
