# Local reports: "report a problem" → a local Claude loop

The tester account (`LocalReportCapability.AccountId`) gets a second way to send a report from
**Help → Report Problem**: **Create a local Claude task** writes the note, the screenshots and the
app's logs to a folder instead of uploading them to support. Everything else about the flow is shared
with the Mac app — same folder format, same status rules — so one reviewer workflow can serve both.

## The folder

```
%USERPROFILE%\FocusBear-Claude-Tasks\
  2026-09-25_09-30-00\          ← queued (the root)
    TASK.md                     ← the report; user feedback is appended as
                                   "## Additional instructions" / "- Added: <ISO-8601>"
    logs\  screenshots\
    CLAUDE-NOTES.md             ← the reviewer's notes: one diagnosis, then "## Update — <stamp> — …"
    USER-STATUS.json            ← the user's "confirmed fixed"
  Ready for Human QA\<report>\
  Needs Clarification\<report>\
  Blocked on Backend\<report>\
```

Where a folder sits **is** its state. "Rework needed" is computed, never stored: the newest
`- Added:` stamp in TASK.md is newer than both CLAUDE-NOTES.md's modification time and the user's
confirmation. So **stamp the notes when the fix is installed**, not when it is committed, or a
finished report stays in rework.

## Reviewer workflow

- Work the queue: a new folder in the root, then move it to `Ready for Human QA\` with notes
  (`CLAUDE-NOTES.md`), renamed `descriptive-hyphenated-<timestamp>` if you like; or to
  `Needs Clarification\` with the question in a `## What I need from you` section.
- Add to notes, never rewrite them: append `## Update — 2026-09-25 22:40 — what changed`. The app
  shows the newest update on the row and every update as its own card.
- `python scripts/check-report-rework.py` lists reports back in rework (exit 1 when any are) — run it
  before concluding there is nothing to do. `bash scripts/watch-report-rework.sh` prints a line the
  moment a report enters rework; feedback on an existing report creates no new folder, so the queue
  watcher alone never notices it.
- `python scripts/check-blocked-reports.py` checks whether the backend PRs a `Blocked on Backend`
  report waits for have merged.

The logic lives in `Core/Reports/LocalReports.cs` (tests in `Core.Tests/Reports`), the list in
`Reports/LocalReportsView.cs`, and the form wiring in `Preferences/HelpTab.xaml.cs`.
