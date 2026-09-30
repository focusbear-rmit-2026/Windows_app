# Windows port: where it stands and what to do next

Written 2026-09-24 as the hand-off from the Mac session to whoever picks this up on a Windows
machine. The plan the port follows is #1568 (`docs/mac-to-windows-port-plan.md`). This file is the
"what is actually done, and what's next" companion to it.

## TL;DR

1. Check out **`port/windows-verify-stack`**. It is every open port PR merged together, plus latest
   `main`, so you can build and test once instead of eight times.
2. Build and run the tests (below). Then work through the **manual checks** on a real account; they
   are the one thing CI cannot do.
3. If they pass, review and merge the PRs **in stack order** (below). Don't merge
   `port/windows-verify-stack` itself: it exists only to test the stack together.
4. Two decisions are waiting on a human: **#1580** (blocking-schedule days) and the always-blocked
   matching change in **#1585**.

## The PR stack

Every PR targets the one above it, so merge from the top down. When a base merges, GitHub retargets
the next PR onto `main`. The later ones all add a line to `Focus Bear.csproj`, so expect small,
mechanical conflicts there.

| Order | PR | Branch | What it is |
|---|---|---|---|
| 1 | #1569 | `fix/focus-mode-metadata-preserve` | **FocusBear.Core**: the WPF-free logic library + 836 tests. Fixes the metadata-clobber bug and the AI-blocking ladder. **Gate for everything below.** |
| 2 | #1577 | `test/app-seam-and-net472` | `App.Tests` (headless tests of the app↔Core adapters) + Core's tests on net472 |
| 3 | #1579 | `ci/launch-smoke-test` | CI launches the built app, and CI runs report to Sentry as `ci` |
| 4 (any order) | #1578 | `fix/core-fidelity-limits` | Swift-compatible text ordering; kanban limit documented |
| | #1585 | `cutover/url-matching` | Browser URL matching → Core (**see decision 2**) |
| | #1583 | `cutover/routine-weekday-filter` | Routine day filter → Core ("Weekdays" habits never played) |
| | #1581 | `cutover/focus-alignment-score` | Alignment score → Core (empty session = 100, as on the Mac) |
| | #1584 | `cutover/dayplan-durations` | Day Plan durations → Core ("1h30" was 130 min) |
| hold | #1582 (draft) | `fix/blocking-schedule-days-wire` | Schedule `days_of_week` fix. **Not in the verify branch** (see decision 1) |

All of them are green on `windows-latest`. Each cutover **deletes** the WPF copy of the rule rather
than leaving a second one.

## Build and test on Windows

Prerequisites: Visual Studio 2022 (the .NET desktop workload), and the .NET 8 SDK for the test projects.

```powershell
git fetch origin
git checkout port/windows-verify-stack
nuget restore "Focus Bear.sln"
msbuild "Focus Bear.sln" /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal

dotnet test Core.Tests/FocusBear.Core.Tests.csproj -c Release              # net8.0 AND net472 on Windows
dotnet test App.Tests/FocusBear.App.Tests.csproj -c Release --no-build     # needs the msbuild step first
```

Or open `Focus Bear.sln` in Visual Studio and use Test Explorer, which shows both test projects.

- **Run in Release for the manual checks.** Debug builds speed some timers up (see README,
  "Difference of Debug and Release"), which makes routine and session timings misleading.
- **Log:** `%LOCALAPPDATA%\Focus Bear\Logs\Debug_Logs.txt`. An unhandled UI-thread exception is now
  written there as `Unhandled dispatcher exception - …`. Before, it went only to Sentry.
- **Telemetry:** if `CI`, `GITHUB_ACTIONS` or `FOCUSBEAR_DISABLE_TELEMETRY` is set in your
  environment, the app sends no PostHog events and reports to Sentry as environment `ci`. Unset them
  if you want a normal run.

## Manual checks (the part CI cannot do)

CI proves the app builds, the rules are right, the adapters pass the right inputs, and the app
launches logged-out without crashing. It has never seen a real account. Log in with one that has
block lists, a routine and a Day Plan, and check:

**AI blocking, per block list** (#1569)
- [ ] On the Mac, turn AI **off** for one allow-list block list. Start a focus session with it on
      Windows. URLs are allowed or blocked immediately, with no AI round trip.
- [ ] Edit and save that block list **on Windows**, then check the Mac: AI is still off. This is the
      metadata-clobber bug; Windows used to switch it back on.

**URL blocking** (#1585)
- [ ] Always-blocked `facebook.com` → `https://m.facebook.com` is blocked (it wasn't before).
- [ ] `https://WWW.Reddit.com/` is blocked by a `reddit.com` entry (matching is now case-insensitive).
- [ ] An entry saved with a trailing slash (`youtube.com/`) still blocks `youtube.com`.
- [ ] An allow-list session still allows a listed site and blocks an unlisted one, including the
      window-title fallback. Try a page whose URL doesn't contain the entry but whose tab title does.
- [ ] `netflix.com` is **not** blocked by an `x.com` entry (#1209).

**Routine** (#1583)
- [ ] A habit set to "Weekdays" plays in the morning routine on a weekday. It never did before.
- [ ] A "Weekend" habit does not play on a weekday.
- [ ] The Day Plan and the routine player now show the same habits for today.

**Focus session end** (#1581)
- [ ] End a session straight after starting it (nothing tracked). The summary shows a sensible
      alignment/deep-work figure, not 0%.

**Day Plan** (#1584)
- [ ] Edit a task's duration to `1h30`: it shows 90 min. `45` still gives 45.
- [ ] Add a to-do from the Add Tasks picker whose duration is set on the web: it shows that duration,
      not the 15-minute default.

**Startup**
- [ ] A normal launch and login work, and the log has no `Unhandled dispatcher exception`.

Record what you checked, and on which commit, in a comment on #1568.

## Decisions waiting on a human

### 1. Blocking-schedule days are a day out (#1580, fix in draft #1582)

The wire is `0 = Sunday` (JS `getDay()`, per the backend DTO and the Mac). Windows sends its own
`0 = Monday` index unconverted, so a Mon–Fri schedule made on Windows runs **Sun–Thu** on the Mac,
and a dashboard Mon–Fri schedule runs **Tue–Sat** on Windows.

The code fix is small and ready. The problem is **existing data**: nothing records which client saved
a schedule. Merging #1582 alone shifts every Windows-only user's schedules one day earlier *on
Windows*. The options are in #1580. **Recommended:** a one-off backend migration (+1 mod 7) for users
whose only schedule-writing client is Windows, shipped in the same release as #1582.

To try it locally: `git merge origin/fix/blocking-schedule-days-wire` on top of the verify branch.
It changes what your account's schedules mean, so look at them in the dashboard afterwards.

### 2. Always-blocked entries now match anywhere in the URL (#1585)

This is Mac parity: `facebook.com` now blocks subdomains. The same rule also means `reddit.com`
blocks a Google search whose URL contains "reddit.com". If that's unwanted, change it on **both**
clients (Core's `UrlBlockingHelper` and the Mac's), not just here.

## What's next after the stack merges

Remaining WPF copies of rules Core already has, best candidate first. #1568 has the full table with
file and line references.

1. **Blocking-schedule window + overlap** (`BlockingScheduleManager.GetActiveSchedule`,
   `HasOverlappingSchedules`) → `CustomBlockingSchedule`. Do it after #1580. Known differences:
   start == end is 0 h on Windows but 24 h in Core, and cross-midnight overlap is never detected on
   Windows.
2. **Schedule editor defaults** → `BlockingScheduleEditorModel`. Seeding only, so easy.
3. **App blocking** (`FocusChangedHandler.processCurrentFocusElement`) → `AppBlockingHelper`. Hard:
   Windows matches `.exe` names and window titles and has the `isDefault` inversion.
4. **Always-blocked meeting gate** (`ProcessBrowserURLs`, near the top) → `BlockingDecision`. The
   gate runs inverted relative to the Mac, so decide the semantics first.
5. **Day Plan schedule engine** (`DayPlanScheduleBuilder`) → `ScheduleBuilder`. Hard, and visible:
   it changes layout.
6. Small ones: `DayPlanSnooze.Parse` → `ToDoDueDateParser`; duration text → `HabitDurationText`;
   routine reorder → `RoutineReorder`; the visualise prompt → `RoutineVisualizePrompt`; timer text →
   `FocusSessionClock`; task completion should also write the column → `ToDoCompletionState`.

After that the port is feature work, not cutover. **Stats, To-Do, Projects/Kanban and Notes are
still WebViews on Windows** (phases 2–5 of #1568). Core already holds their logic and tests; the WPF
screens need building, and that has to be done on Windows because it needs eyes on the UI.

**Blocked elsewhere:** moving voice and the accountability buddy off the client's OpenAI key waits
on backend #2277 (unmerged since 2026-08-10). Until then the key ships inside the Windows binary.

## Gotchas that cost time

- **The WPF project is C# 7.3.** It has no `LangVersion` and targets net472. Core and the test
  projects are C# 12. Code that compiles in Core may not compile in the app: no target-typed `new()`
  or conditionals, no switch expressions, no `using` declarations.
- **`Focus Bear.csproj` is a legacy project: no globbing.** Every new `.cs` file needs a
  `<Compile Include=…>` line, or the build fails with "type not found". `App.Tests` and `Core.Tests`
  are SDK-style and pick files up automatically.
- **Name collision:** Core has a `CustomBlockingSchedule` too. In app files that import
  `FocusBear.Core.Blocking`, alias one of them. `Utils/AiBlocking.cs` shows how.
- **Keep app↔Core adapters free of the `AppSettings` singleton.** Its constructor touches the disk,
  the network adapters and the log, so a test can't build it. The pattern: snapshot the WPF state
  into a plain object, and map it with a pure function. See `AiBlocking.Snapshot` / `ToInputs`.
- **No `--` inside XML comments** in any `.csproj`: restore fails with MSB4025.
- **Preserve BOMs and line endings** when editing. The repo mixes files with and without a BOM.
- **Compare with the Mac by running Swift, not by reading docs.** Swift's string ordering and
  rounding both differ from .NET's defaults; see `SwiftStringComparer` and `SwiftRounding`.
