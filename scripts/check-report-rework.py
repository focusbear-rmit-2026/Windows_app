#!/usr/bin/env python3
"""List debug reports the app currently shows as "Rework needed" or "Queued".

Why this exists
---------------
Five reports sat in "Rework needed" for days with nobody looking, and four of them were
ALREADY FIXED — the fixes landed three to twelve minutes after the user tested, so the
feedback was about a build that did not contain them yet. Nothing was wrong with the code;
what was missing was any way to see the state.

"Rework needed" is not stored anywhere. The app computes it in
`LocalReportReader.Parse` (Core/Reports/LocalReports.cs; the Mac: `LocalReportStatus.status(...)`): the newest `- Added:` stamp under a `## Additional
instructions` heading in TASK.md, compared against CLAUDE-NOTES.md's modification time. So a
report whose fix shipped without its notes being stamped stays in rework forever, however
finished the work is.

This reproduces that computation from the outside. Run it before deciding there is nothing to
do — an empty queue of NEW folders is not an empty queue.

    scripts/check-report-rework.py [--root DIR]

Exit status is 1 when anything is in rework, so it can gate a routine.
"""
import argparse
import datetime
import os
import sys

# Windows consoles default to a legacy code page; the report text is UTF-8 (em dashes and all).
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

INSTRUCTIONS_HEADING = "\n## Additional instructions\n"
ADDED_PREFIX = "- Added: "
# The two the notes file is allowed to declare; every other state comes from the folder it sits in.
# Matched EXACTLY, like LocalReportReader.ExplicitReviewState: "Queued — the second half is still
# open" is prose, not a state, and the app treats it as nothing at all. Matching loosely here
# reported ten reports as open that the app does not.
OPEN_STATUSES = ("queued", "investigating")


def latest_instructions_date(task_text):
    """The newest `- Added:` stamp, mirroring LocalReportReader.LatestInstructionsDate."""
    stamps = []
    for section in task_text.split(INSTRUCTIONS_HEADING)[1:]:
        for line in section.split("\n"):
            if line.startswith(ADDED_PREFIX):
                raw = line[len(ADDED_PREFIX):].strip()
                try:
                    stamps.append(datetime.datetime.fromisoformat(raw.replace("Z", "+00:00")))
                except ValueError:
                    pass          # an unparseable stamp is not a reason to hide the report
                break
    return max(stamps) if stamps else None


def declared_status(notes_text):
    """The `## Status` value, which only ever means `queued` or `investigating`."""
    lines = notes_text.split("\n")
    for index, line in enumerate(lines):
        if line.strip() != "## Status":
            continue
        for candidate in lines[index + 1:]:
            if candidate.strip():
                return candidate.strip()
        return None
    return None


def read(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except OSError:
        return None


def scan(root):
    rework, open_work = [], []
    for folder, _, files in os.walk(root):
        if "TASK.md" not in files:
            continue
        notes_path = os.path.join(folder, "CLAUDE-NOTES.md")
        relative = os.path.relpath(folder, root)

        task_text = read(os.path.join(folder, "TASK.md"))
        if task_text is None:
            continue
        asked = latest_instructions_date(task_text)
        notes_text = read(notes_path)

        if asked is not None:
            if notes_text is None:
                rework.append((asked, None, relative))
            else:
                reviewed = datetime.datetime.fromtimestamp(
                    os.path.getmtime(notes_path), datetime.timezone.utc)
                if asked > reviewed:
                    rework.append((asked, reviewed, relative))

        status = declared_status(notes_text) if notes_text else None
        if status and status.strip().lower() in OPEN_STATUSES:
            open_work.append((status, relative))
    return sorted(rework), sorted(open_work, key=lambda row: row[1])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=os.path.expanduser("~/FocusBear-Claude-Tasks"))
    arguments = parser.parse_args()

    rework, open_work = scan(arguments.root)

    print("REWORK NEEDED: %d — the user added feedback after the last note" % len(rework))
    for asked, reviewed, folder in rework:
        seen = reviewed.strftime("%Y-%m-%d %H:%M") if reviewed else "no notes file"
        print("  %s\n      asked %s  ·  last note %s" % (folder, asked.strftime("%Y-%m-%d %H:%M"), seen))

    print("\nSTILL OPEN (queued / investigating): %d" % len(open_work))
    for status, folder in open_work:
        print("  %s\n      %s" % (folder, status))

    if rework:
        print("\nA fix that shipped without its notes being stamped stays in rework forever.")
        print("Append a stamped `## Update — <date time> — what changed` once the fix is INSTALLED.")
    return 1 if rework else 0


if __name__ == "__main__":
    sys.exit(main())
