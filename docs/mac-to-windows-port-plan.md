# Porting the macOS rebuild to the Windows app

**Status:** planning document · written 2026-09-20 · **no code changed, nothing committed**
**Audience:** the Windows agent/developer who will execute this on a Windows machine.

---

## TL;DR

* **Work from `origin/main`, not the checked-out `feat/teacher-mode`** — it is 131 commits stale and
  will make you believe features are missing that already exist (Day Plan, most of all). §0.3
* **Three screens are the real webview→native backlog:** Stats, To-Do/Projects, Notes/Brain Dump.
  Mac converted all three; Windows still hosts `dashboard.focusbear.io/webview/*` for each. §2.2
* **Two live cross-platform bugs come before any feature work.** Windows rebuilds the focus-mode
  `metadata` blob from a string literal and so wipes Mac-written keys (§4.1), and the "Use AI"
  switch is attached to blocking schedules on Windows but to block lists on Mac, so the two
  desktops disagree about when AI blocking runs (§4.1b). Both are Phase 1.
* **The biggest structural gap is testing.** Mac gates ~80 XCTest suites in CI; Windows has no test
  project at all. Almost every phase below cites a ported Mac test suite as its verification, so
  Phase 0b is load-bearing. §5 Phase 0
* **Managed/school blocking is gated.** It must not ship ahead of the Mac decision. §5 Phase 8
* **Seven open questions need answers before work starts.** §7

---

## 0. Read this first — provenance, and what is *not* known

### 0.1 What was actually inspected

| Repo | Ref inspected | Notes |
|---|---|---|
| `Focus-Bear/Mac-App` | `integration/mac-swiftui-rebuild` @ `0b4946bc8`, and `main-human-verified` @ `880189f92` | 704 commits between them, but see 0.2 |
| `Focus-Bear/windows-app-v2` | **`origin/main`** | *not* `feat/teacher-mode` — see 0.3 |
| `Focus-Bear/assets` | `main` @ `e1a0ff9` | the i18n source of truth |

Every file path in this document was verified to exist on the ref named next to it. Where a claim
could not be verified, it is marked **UNVERIFIED** rather than smoothed over. Section 7 lists the
open questions that need a human decision before work starts.

### 0.2 "The Mac rebuild" is *not* one branch — this matters

The natural assumption is that `integration/mac-swiftui-rebuild` *is* the rebuild. It is not.
Content comparison (`git show main-human-verified:<path>` vs `HEAD:<path>`) shows that the
To-Do, Projects, Stats, Notes, Leaderboard and Pep-Talk native screens are **already byte-identical
on `main-human-verified`**, complete with their "replaced the webview" header comments — even though
`git log main-human-verified..HEAD` still lists the commits that created them (a rebase/cherry-pick
artifact; commit ancestry is not preserved on this branch).

**Practical consequence for the Windows agent:** do not use the Mac branch name to decide whether a
feature is "done". Use the **current file tree of `integration/mac-swiftui-rebuild`** as the
behavioural source of truth, because it is the newest state and it is a superset of
`main-human-verified`. The integration branch is the only Mac-App PR ([#2569]) and everything Mac
lands there directly.

What is genuinely *newer* on the integration branch than on `main-human-verified` — i.e. the last
~9 days of work — is listed in §1.3.

### 0.3 The checked-out Windows branch is stale — use `origin/main`

`feat/teacher-mode` is **131 commits behind `origin/main`** and only 6 ahead. Whole feature
directories are missing from it: `Habits/DayPlan/` (15 files), `AudhdResults/`, `TabAudit/`,
`Services/WebViewTokenSyncService.cs`, `Focus/TeacherPactInviteWindow.xaml`,
`Services/VoiceCommand/`, `Utils/GlobalHotkeyManager.cs`. Its CHANGELOG stops at v3.0.225;
`origin/main` is at v3.0.249.

Worse, `feat/teacher-mode`'s own teacher-mode work is a **second, less mature reimplementation** of
a feature `origin/main` already has in a much more developed form (PACT invite window, rejoin after
decline, AI-blocking-in-session, idle/alignment heartbeat). See §7 Q2 — this needs a decision, not a
merge.

**All Windows file paths in this document are as they exist on `origin/main`.**

---

## 1. Inventory of the Mac work

### 1.1 How the Mac app is built (the shape you are porting *from*)

Read `Mac-App/ARCHITECTURE.md` in full before starting — it is accurate and current. The three
things that matter for porting:

* **Global singletons everywhere.** `getViewControllersObject.swift` (~200 VC singletons),
  `globalFile.swift` (~190 globals). There is no DI. This is architecturally *very close* to the
  Windows app's `Utils/AppSettings.cs` god-singleton, so the port is less of an impedance mismatch
  than it first looks.
* **New Mac UI is SwiftUI** hosted in `NSHostingController`; older screens are storyboard AppKit.
  WPF has no SwiftUI equivalent — see §6.1 for what to do instead.
* **All user-facing strings come from a remote `config.json`**, never from `Localizable.strings`.
  Windows already uses the same file from the same URL. This is the single biggest piece of
  leverage in the whole port — see §4.2.

### 1.2 Subsystem inventory

Classification: **(a)** WebView→native conversion · **(b)** new native functionality ·
**(c)** existing native, redesigned/rebuilt · **(d)** unchanged.

| Subsystem | Class | Mac files (on `integration/mac-swiftui-rebuild`) |
|---|---|---|
| **To-Do list + Projects + Kanban** | **(a)** | `FocusBear/ToDo/ToDoListView.swift`, `ToDoStore.swift`, `ToDoApi.swift`, `ToDoModels.swift`, `ToDoLogic.swift`, `ToDoEditorView.swift`, `ToDoStrings.swift`, `ToDoToolsViews.swift`, `ToDoDueDateField.swift`, `ToDoAttachments.swift`, `TodoCompletionSync.swift`, `ProjectsView.swift`, `ProjectsStore.swift`, `ProjectDetailView.swift`, `ProjectDetailStore.swift`; host `FocusBear/menuView/todoListToolVc.swift` |
| **Stats + Leaderboard + Pep Talk** | **(a)** | `FocusBear/Stats/` — `StatsView.swift`, `StatsStore.swift`, `StatsApi.swift`, `StatsModels.swift`, `StatsMath.swift`, `StatsCharts.swift`, `StatsStrings.swift`, `LeaderboardView.swift`, `FocusLogView.swift`, `ProductivityView.swift`, `HabitStatsView/Store.swift`, `SingleHabitStatsView/Store.swift`, `MarkdownText.swift`; host `FocusBear/Preferences/setUpVc.swift`. Pep Talk re-hosted in `FocusBear/Routine/visualizeInstructionsVc.swift` |
| **Notes / Brain Dump** | **(a)** | `FocusBear/Notes/` — `NotesView.swift`, `NotesStore.swift`, `NotesApi.swift`, `NoteModels.swift`, `NoteHTML.swift`, `NoteRichTextEditor.swift`, `ProjectNotesFeed.swift`; break-time entry point `FocusBear/Focus mode/brainDumpVc.swift` |
| **Project note editor (rich text)** | **(b)** | `FocusBear/ToDo/ProjectNoteEditor.swift` + a *bundled local* TipTap/Yjs web bundle in `FocusBear/ToDo/ProjectNoteEditorWeb/` (built by `build.mjs`, shipped as `Resources/ProjectNoteEditorWeb/editor.bundle.js`). Native REST, local HTML — **not** a hosted webview |
| **Day Plan / Schedule** | **(c)** | `FocusBear/Schedule/` — `ScheduleKit.swift` (pure engine), `ScheduleViewController.swift`, `DayPlanTasks.swift`, `CalendarManager.swift`, plus new-on-branch `BusyHoursCalculator.swift`, `BusyHoursReporter.swift`, `DayFixPlan.swift`, `DayPlanCustomRoutines.swift`, `DayPlanDestination.swift`, `DayPlanProjectRepair.swift`, `EventCancellation.swift`, `EventDateTimeField.swift`, `EventDescriptionHTML.swift`, `EventTimeParser.swift`, `LocalTimeForecast.swift`, `UserTimezoneSync.swift` |
| **Local forecast / work plans** | **(b)** | `FocusBear/ToDo/LocalForecastView.swift`, `LocalForecastStore.swift`, `LocalForecastPortfolio.swift`, `ForecastDayFocus.swift`, `DayPlanRepositioning.swift`; contract in `Mac-App/docs/local-time-forecast.md` |
| **Block lists (focus modes)** | **(c)** | `FocusBear/Preferences/Settings/Block List/` — `BlockListView.swift`, `BlockListStore.swift`, `BlockListLayout.swift`, `BlockListText.swift`, `DistractionCategory.swift`. SwiftUI rebuild of the already-native `focusPreferencesVc`; **not** a webview conversion |
| **Focus Start (new start screen)** | **(c)** | `FocusBear/FocusStart/` — `FocusStartSimpleScreen.swift`, `FocusStartAdvancedScreen.swift`, `FocusStartState.swift`, `FocusStartModel.swift`, `FocusStartControls.swift`, `FocusStartStrings.swift`, `FocusStartTokens.swift`. Figma "0.0/ Flow Start" |
| **Running focus session** | **(c)** | `FocusBear/FocusSession/` — `FocusSessionView.swift`, `FocusSessionStore.swift`, `FocusSessionModels.swift`, `FocusSessionApi.swift`, `FocusSessionSheets.swift`, `FocusSessionEditSheets.swift`, `FocusSessionSideRail.swift`, `FocusSessionBearView.swift`, `FocusSessionBrainDumpAction.swift`, `FocusSessionStrings.swift`, `FocusSessionTokens.swift`. Figma "1. Ongoing Session"; replaces the storyboard `focusModeStateVc` scene |
| **AI block buddy / relevance chat** | **(b)/(c)** | `FocusBear/Focus mode/AiBlockBuddyScreen.swift`, `AiBlockBuddyView.swift`, `AiRelevanceDecisionCache.swift`, `RelevanceShadowLogger.swift` |
| **On-device relevance model (ONNX)** | **(b)** | `FocusBear/RelevanceModel/` — `RelevanceOnnxModel.swift`, `RelevanceShadowNativeModel.swift`, `RelevanceModelDownloader.swift`, `BertWordPieceTokenizer.swift`, `KerasTextTokenizer.swift` |
| **Accountability buddy** | **(c)** | `FocusBear/AccountabilityBuddy/` — `aiAccountabilityBuddy.swift`, `AccountabilityHoldButton.swift`, `AccountabilityPostponeRecovery.swift`, `extendReasonBuddyVc.swift` |
| **Habit / routine player** | **(c)/(d)** | Core (`habitsVc`, `timerViewcontroller`, `activityCompletionVc`) is **(d) unchanged in shape**; new adjuncts are (b): `Routine/HabitTaskChecklist(.Pane/.View).swift`, `RoutineResume.swift`, `MorningRoutineStartGuard.swift`, `CutoffPostponeLimit.swift`, `MeetingPostponeCoverage.swift`, `AllowedActivityFullScreen.swift`, `ShutdownNagPolicy.swift` |
| **Breaks** | **(c)** | `FocusBear/Breaks/BreakStartKit.swift`, `BreakStartModel.swift`, `BreakStartView.swift`, `breakStartVc.swift` |
| **Menu bar redesign** | **(c)** | `FocusBear/menuView/MenuView.swift`, `MenuOutsideClickPolicy.swift`, `SharedDashboardWebViewManager.swift` |
| **Managed / school blocking** | **(b)** | `FocusBear/ManagedBlocking/` (8 files) + `FocusBear/Preferences/BlockLists/Managed*.swift` (4 files). **Enforcement layer is NOT on the integration branch** — see §5 Phase 8 |
| **Local report status (in-app bug reports)** | **(b)** | `FocusBear/Preferences/LocalReportStatus.swift`, `LocalReportStatusView.swift` — Mac-dev-workflow only, **do not port** |
| **CleanShot picker, Focus Music, Focus Button** | **(b)/(d)** | macOS-specific or already present on Windows; not part of this plan |

### 1.2b Where the engineering effort actually went

Commit scopes on `main-human-verified..HEAD` (330 of 704 commits carry a `type(scope):` prefix;
the other 374 are unscoped merges/fixes, dominated by managed-safety and meeting/tab-blocking work).
Useful as a heat map of which subsystems are churning:

| Scope | Commits | Scope | Commits |
|---|---|---|---|
| `dayplan` / `day-plan` | 51 | `focus-start` | 8 |
| `ai-buddy` | 35 | `routine` | 7 |
| `todo` | 25 | `block-list` | 6 |
| `focus` | 24 | `notes` | 6 |
| `projects` | 14 | `stats` | 5 |
| `meeting(s)` | 14 | `dashboard-webview` | 5 |
| `breaks` | 13 | `crash` | 4 |
| `ai-block-buddy` / `forecast` / `blocking` / `menu` | 9 each | `mac` (managed safety) | 4 |

Line churn by directory: `ToDo` 14.3k · `Schedule` 9.3k · `Focus mode` 6.6k · `Preferences` 6.0k ·
`FocusSession` 5.2k · `Pomodoro mode` 4.1k · `FocusStart` 2.7k · `Routine` 1.9k ·
`AccountabilityBuddy` 1.9k · `ManagedBlocking` 1.7k · `menuView` 1.7k · `Notes` 1.2k ·
`Breaks` 0.7k · `Stats` 0.6k. (`config*.json` adds ~26k lines but is i18n, not code.)

Of the 99 new Swift files: 34 import SwiftUI, 6 touch `NSViewController` (4 of those hybrid), and
**64 are non-UI** — pure models, stores and logic. **That 64 is the portable core.** Port those
first in each phase; they are framework-free and carry the behaviour.

### 1.3 What is newer on `integration/mac-swiftui-rebuild` than on `main-human-verified`

New files only (9 days of work). Useful as a "what is still hot / still moving" signal:

`ManagedBlocking/*` (8), `FocusSession/*` (11), `FocusStart/*` (7), `Preferences/Settings/Block List/*` (5),
`RelevanceModel/*` (5, less `RelevanceShadowNativeModel`), `ToDo/ProjectNoteEditor*` + the web bundle,
`ToDo/LocalForecast*` / `ForecastDayFocus` / `DayPlanRepositioning`, `Schedule/LocalTimeForecast.swift`
+ 11 more Schedule files, `Breaks/*` (4), `Routine/*` (9), `AccountabilityBuddy/*` (4),
`Stats/SingleHabitStats*`, `Notes/ProjectNotesFeed.swift`, `Auth0/CredentialsKeychainStorage.swift`,
`MainThreadWatchdog.swift`, `Focus mode/AiBlockBuddy*`, `AiRelevanceDecisionCache.swift`.

**Implication:** Focus Start, Focus Session, Block List and Managed Blocking are the *least settled*
areas on Mac. Porting them first means porting a moving target. The sequencing in §5 accounts for this.

---

## 2. WebView → native conversions (the part the user called out)

### 2.1 The shared web surface

Both desktop apps host pages from the **same web app**: `https://dashboard.focusbear.io/webview/<screen>`
(the `web_dashboard` repo; its screen-token enum is `src/enums/bridge.ts`). The Mac side documents
this in `FocusBear/menuView/SharedDashboardWebViewManager.swift`. This is why the conversion table
below maps 1:1 — the two apps were hosting literally the same URLs.

### 2.2 Conversion status, screen by screen

| `dashboard.focusbear.io` route | Mac today | Mac native implementation | Windows today (`origin/main`) |
|---|---|---|---|
| `/webview/stats` | **native** | `FocusBear/Stats/StatsView.swift` et al. | **webview** — `Preferences/Stats.xaml.cs:109` |
| `/webview/tools-todo-list` | **native** | `FocusBear/ToDo/ToDoListView.swift`, `ProjectsView.swift` | **webview** — `Focus/ToDoList.xaml.cs:115` |
| `/webview/notes` | **native** | `FocusBear/Notes/NotesView.swift` | **webview** — `BrainDump/BrainDumpWindow.xaml.cs:106` (draft native replacement exists: PR #1507) |
| `/webview/motivational-summary` (Pep Talk) | **native** | native `StatsScreen` embedded in `Routine/visualizeInstructionsVc.swift` | **webview** — `Habits/HabitExtraPersonal.xaml.cs:319` |
| `/webview/todo-player` | webview | `Focus mode/taskPlayerVc.swift` | webview — `Focus/ToDoPlayer.xaml.cs:211` |
| `/webview/todo-list`, `/webview/focus-end` | webview | `Focus mode/addTaskVc.swift` | webview — `Focus/SelectTasks.xaml.cs:138,142` |
| `/webview/settings` (Edit Habits) | webview | `Routine/editHabitRoutineVc.swift`, `menuView/SharedDashboardWebViewManager.swift` | webview — `Preferences/EditHabits.xaml.cs:121`, `Habits/HabitsWelcomeList.xaml.cs:348` |
| `/webview/manage-subscription` | webview | `KeyActivation/manageSubscriptionVc.swift` | webview — `Upgrade/ManageSubscription.xaml.cs:95` |
| `/webview/get-support`, `/webview/enrolled-courses` | webview | `Preferences/helpVc.swift` | webview — `Preferences/HelpTab.xaml.cs:173,245` |
| `/webview/survey` | webview | `Popups/surveyVc.swift` | webview — `Survey/SurveyWindow.xaml.cs:91` |
| `/webview/routine-suggestion-revamp` | webview | `Onboarding/installHabitPacksVc.swift` | webview — `Onboarding/SetupHabitsWebView.xaml.cs:98` |
| `/webview/course` | webview | `Routine/timerViewcontroller.swift:792` | **UNVERIFIED** — no Windows equivalent found |

Not a dashboard webview, listed to avoid confusion:

* `Popups/AudhdResultsWebViewVc.swift` (Mac) / `AudhdResults/AudhdResultsWindow.xaml.cs` (Windows) —
  a **third-party** host, `https://audhdtest-focusbear.pages.dev/view-results`.
* `Routine/activityCompletionVc.swift` (Mac) / `Habits/HabitActivityFinished.xaml.cs`,
  `HabitActivityOnGoing.xaml.cs` (Windows) — local `file:///.../graph/*.html` chart pages + embedded
  YouTube. Not conversion candidates.
* `AccountabilityBuddy/AccountabilityBuddyView.xaml.cs:167` (Windows) opens
  `https://dashboard.focusbear.io/accountability-buddy` in the **system browser**
  (`Process.Start`), not a WebView2. The buddy UI itself is already native WPF.

### 2.3 What replaced each converted screen, and what it calls

The native Mac screens talk to `https://api.focusbear.io` directly. These are the endpoint sets the
Windows ports must implement. Every path below was read out of the Swift source.

**Stats** — `FocusBear/Stats/StatsApi.swift` (`enum StatsEndpoint`):

| Method | Path |
|---|---|
| GET | `/user/stats/onboarding` |
| GET, PATCH | `/user/long-term-goals` |
| GET | `/user-settings` |
| POST | `/completed-activity/consistency-stats` *(read-only despite the verb)* |
| GET | `/completed-activity/{activityId}/stats?days_number=&timezone=` |
| GET | `/completed-activity/log-question/{questionId}/stats` |
| GET | `/completed-focus-blocks?from_time=&to_time=` |
| GET, POST | `/user/weekly-focus-block-summary` |
| PATCH, DELETE | `/user/weekly-focus-block-summary/{id}` |
| GET | `/user/focus-modes` |
| GET | `/user-stats/leaderboard?limit=&streak_type=` |
| PUT | `/user/username` |
| GET (SSE) | `/user/motivational-summary` — streamed, decoded by `MotivationalSummaryDecoder` |

**To-Do / Projects / Kanban** — `FocusBear/ToDo/ToDoApi.swift` (`enum ToDoEndpoint`):

| Method | Path |
|---|---|
| GET | `/to-do?page&take&status&sort_mode&project_id&synced_project_id` |
| GET | `/to-do?sort_mode=DUE_DATE&exclude_completed=true` *(forecast)* |
| GET | `/to-do/search?title=&take=`, `/to-do/by-ids?ids=` |
| PUT | `/to-do` *(upsert — also the Day Plan planned-date write)* |
| DELETE | `/to-do?todo_id=` |
| POST | `/to-do/convert-brain-dump`, `/to-do/generate-subtasks` |
| GET/POST/PUT/DELETE | `/to-do/{id}/comments[/{commentId}]` |
| GET/POST/DELETE | `/to-do/{id}/reactions[?emoji=]`, `/to-do/{id}/comments/{commentId}/reactions` |
| GET/POST/DELETE | `/to-do/{id}/attachments[/{attachmentId}]`, `POST /to-do/{id}/attachments/generate-upload-url` |
| GET/POST | `/projects`, `GET/PUT/DELETE /projects/{id}` |
| GET | `/projects/{id}/todos?page&take&sort_mode` |
| POST/PUT/DELETE | `/projects/{id}/members[/{memberId}]`, `/projects/{id}/members/leave` |
| POST | `/projects/{id}/members/accept`, `/decline`; GET `/projects/invitations/pending` |
| GET/POST/PATCH/DELETE | `/projects/{id}/notes[/{noteId}]`; `/projects/{id}/notes/{noteId}/doc`, `/realtime-auth` |
| GET | `/portfolio`; GET/PUT `/portfolio/settings`; PUT `/portfolio/busy-hours` |
| GET/POST/DELETE | `/saved-website[/delete]` |
| PATCH | `/user/timezone` |

**Notes / Brain Dump** — `FocusBear/Notes/NotesApi.swift`: `/note`, `/note/search`, `/note/tags`.
(Windows PR #1507 uses `/note`, `/note/search`, `/note/tag` — **singular `tag`**. One of these is
wrong. Flagged in §4.3.)

**Focus session** — `FocusBear/FocusSession/FocusSessionApi.swift`: `GET /day-plan/day/{day}`.

**Managed blocking** — `FocusBear/ManagedBlocking/ManagedBlockingAPI.swift`, all under
`https://api.focusbear.io/managed-blocking/`:
`GET effective-policy` · `POST check-url` · `POST violations/batch` · `POST device-receipts` ·
`POST violations/{id}/appeals` · `GET/POST settings-edit-requests` ·
`GET settings-edit-requests/{id}` · `POST settings-edit-requests/{id}/propose`.

### 2.4 Where Windows still hosts a WebView today

WebView2 (`Microsoft.Web.WebView2` 1.0.3405.78, WPF + WinForms + Core assemblies referenced in
`Focus Bear.csproj`) is used in the twelve screens in §2.2, plus the local-chart pages. Token
injection into those webviews is centralised in **`Services/WebViewTokenSyncService.cs`**
(`origin/main` only), which does three things:

1. per-host `loadAccessTokenFor*` / `loadSettingsData` injection on initial load;
2. a reactive `REQUEST_ACCESS_TOKEN` web message → silent Auth0 refresh → host reloads with an
   echoed `requestId`;
3. a proactive `window.syncWithDevices({accessToken})` push to every live webview on a timer, via
   `AliveWebViewProvider`.

**Every screen converted to native removes one consumer of this service.** When the last one goes,
`WebViewTokenSyncService` and the WebView2 dependency can be retired — that is the long-term prize,
but it is not reachable within this plan (Edit Habits, Manage Subscription, Survey, Support and the
onboarding routine suggester are all still web on *both* platforms).

Related open Windows PR: **#1328** `fix/1327-auth0-webview-alignment` — align Auth0 config and
standardise webview token injection. Land or close it before touching webview hosts.

---

## 3. Gap analysis against Windows

Everything in this section is against `origin/main`.

| Area | Windows today | Gap vs Mac |
|---|---|---|
| **Day Plan / schedule** | **Exists and is well-built.** `Habits/HabitsDayPlan.xaml(.cs)` (~1,879 lines, a `Page`) + `HabitsDayPlan.Tasks.cs` (~1,094) + `Habits/DayPlan/` (15 files). Headed "Windows port of the macOS 'Day Plan' (Schedule) view — see Mac-App PR #1935". It **replaces `HabitsWelcomeList` at routine start**, and the left-nav leaf was renamed to "Day Plan" (`menuDayPlan`). `DayPlanScheduleBuilder.cs` is an explicit C# port of `ScheduleKit.swift`; `DayPlanSync.cs` ports `DayPlanSync.shared`; `DayPlanText.cs` ports `ScheduleText`. Native task CRUD/snooze lives in `DayPlanAddTasksWindow`, `DayPlanTaskDetailWindow`, `DayPlanTaskModels.cs` incl. a natural-language `DayPlanSnooze` parser | Missing the last ~9 days of Mac Schedule work: local forecast, `DayFixPlan`, `BusyHoursCalculator/Reporter`, `DayPlanProjectRepair`, `EventCancellation`, `UserTimezoneSync`, per-event editing. Calendar source differs fundamentally (§6.5) |
| **To-Do list** | **WebView.** `Focus/ToDoList.xaml.cs`; API client has `AllTasks` → `/to-do`, `GET /to-do/recent?take=50`, `PUT /to-do` | Whole native screen missing: filters, quick-add parser, editor, attachments, comments, reactions, due dates |
| **Projects / Kanban** | **Partial.** `GET /projects` exists on `origin/main` (Day Plan project suggestions only). No projects screen, no Kanban board | Everything: portfolio, project detail, members/invitations, notes, Kanban drag-between-columns |
| **Stats / Leaderboard** | **WebView.** `Preferences/Stats.xaml.cs`. API client has `/completed-activity/{id}/stats` and `/user/long-term-goals` only | All other stats endpoints, all charts, leaderboard, focus log CRUD, username editing, SSE pep talk |
| **Notes / Brain Dump** | **WebView on `origin/main`.** `BrainDump/BrainDumpWindow.xaml.cs` → `/webview/notes?open=braindump`, described in-code as replacing "the old local-only text box". (On the stale `feat/teacher-mode` it is still that old text box, persisted to the registry via `RegistryManager.SaveBrainDump()` — a third, divergent behaviour.) **Draft native replacement: PR #1507** (`feat/native-notes-view`, RichTextBox + ListView over `/note`, adds `Models/Note.cs`, `Utils/NoteHtml.cs`) | Finish/land #1507. Then: tags, project notes feed, archive, search debounce |
| **Block lists / focus modes** | **Native already.** `Preferences/BlocksTab.xaml`, `FocusPage.xaml`, `FocusAdvanced.xaml`, `AddFocusType.xaml`, `AddWebUrl.xaml`, `AddApp.xaml`, `AlwaysBlockedURLs.xaml`, `AlwaysBlockedApps.xaml`, `SettingsTab/FocusModeSetting.xaml` | **Two live cross-platform bugs** — §4.1 (metadata clobber) and §4.1b (AI toggle on the wrong object). Missing `is_ai_enabled` and `categories` entirely |
| **Focus start** | **Native.** `Focus/StartFocusMode.xaml` (`Window`) + `Pomodoro/StartPomodoro.xaml`, `Pomodoro/StartSimplePomodoro.xaml` (both `UserControl`) — **structurally the same split as Mac**, where `pomodoroVc.swift:118` hosts `FocusStartAdvancedScreen` and `simplePomodoroVc.swift:88` hosts `FocusStartSimpleScreen` | Not the new Figma Simple/Advanced two-screen design; no goal bubble, task picker, block-list picker, micro-break controls, alignment-score row |
| **Running focus session** | **Native.** `Focus/FocusProgress.xaml`, `Focus/GentleBlock.xaml` (in-session distraction popup, heavily reworked on `origin/main` for teacher-mode-aware AI checks), `Focus/FocusModeEnd.xaml`, `Focus/StopFocusMode.xaml`, `Focus/FocusActivitySummary.xaml` | Not the Figma "Ongoing Session": no side rail, task queue with active-task card, inline duration ±, brain-dump action, edit sheets |
| **Accountability buddy** | **Native** WPF, but a *different feature shape*: `AccountabilityBuddy/AccountabilityBuddyView.xaml(.cs)` is an **unlock-request form** (pick a buddy, duration slider, reason, create/refresh/check-approval) over `GET /accountability-buddy`, `POST /accountability-buddy/unlock-request`, `GET /accountability-buddy/unlock-requests`. "Add a new buddy" opens the dashboard in the **system browser**. No chat | Missing the whole AI buddy conversation (`POST /user/evaluate-focus-postponement`, `POST /user/task-intention-check`), the ten-second hold button, and postpone recovery — 35 Mac commits' worth (§1.2b) |
| **Habit / routine player** | **Native**, large and mature (`Habits/*`, ~30 screens) | Missing habit task checklist, routine-resume guard, cutoff postpone limit, meeting postpone coverage |
| **Managed / school blocking** | **Not in `origin/main` at all.** Entirely in draft **PR #1522** (`feat/classroom-managed-policies`, +5,854 / 29 files, incl. `ManagedBlocking/` dir and `tests/ManagedBlocking.Tests/`) | See §5 Phase 8 — gated on a Mac/product decision |
| **Teacher mode / class sessions** | **Native and mature on `origin/main`**: `Focus/TeacherSessionManager.cs`, `TeacherPactInviteWindow`, `TeacherUnlockRequestWindow`, `/class-session/*`, `/class-management/*` | Roughly at parity with Mac's `teacherSessionManager.swift`. Not a port target; see §7 Q2 |
| **Calendar** | `Habits/DayPlan/DayPlanCalendar.cs` → `GET /calendar/{platform}/calendar-datas`, `GET /calendar/{platform}/events` (platform = `google`\|`microsoft`), **read-only** | Mac uses local EventKit and can create/update/delete events with recurrence, colours, notes. Fundamental difference — §6.5 |
| **Pomodoro** | **Native.** `Pomodoro/`, `Preferences/PomodoroTabAdvanced.xaml`, `SettingsTab/PomodoroTab.xaml` | Mac's two pomodoro VCs are themselves flagged tech debt; **do not port** the duplication |
| **On-device relevance model** | Not present (AI blocking is server-side: `/user/is-url-safe-to-use`) | Mac's ONNX shadow-mode work is itself unsettled (see Mac memory: "shadow mode should be ONNX not Python"). **Out of scope** |
| **Tests** | **None.** No `*.Tests.csproj` anywhere; only `Habits/DayPlan/DayPlanScheduleBuilderSelfTest.cs`, a hand-rolled in-process self-check. PR #1522 would add the first real test project | Mac runs ~80 XCTest suites as a CI gate. §5 Phase 0 |
| **i18n CI gates** | None | Mac has `config-assets-sync.yml` + `hardcoded-strings.yml`. §5 Phase 0 |

---

## 4. Shared contracts that must not drift

### 4.1 🔴 Focus-mode `metadata` is clobbered by Windows — a live cross-platform data-loss bug

This is the highest-value finding in this document and should be fixed before any feature work.

The backend focus-mode write takes `{ name, allowed_urls, allowed_apps, tags, categories, metadata }`.
**`metadata` is replaced wholesale on every save.** Mac documents this explicitly in
`Mac-App/FocusBear/Preferences/Settings/Block List/BlockListStore.swift:37-51`:

> Categories must NOT ride inside `metadata`: that blob is replaced wholesale on each save, so a
> client that predates categories would silently wipe them.

Mac writes these keys into `metadata`: `blockUrlsArray`, `blockAppsArray`, `color`, `isDefault`,
`isMeetings`, `isLockedFocus`, `isRelaxFocus`, `isDeepWorkFocus`, `isAllowedListApproach`, and
**`is_ai_enabled`**.

Windows builds `metadata` from a **fixed string literal** in `API/FocusBearAPIClient.cs` (~lines
649-661 for `AddFocusMode`, ~799-819 for the edit path):

```
blockUrlsArray, allowed_apps_win, color, isDefault, isMeetings, isLockedFocus,
isRelaxFocus, isDeepWorkFocus, isWorkFocus, autoSuggestFocusModeOnAllowedURL,
focusAlignmentScoreThreshold, isAllowedAllApps, isAllowedListApproach
```

Verified by grep on `origin/main`: **`is_ai_enabled` has zero occurrences in any `.cs` file.
`blockAppsArray` has zero occurrences. `categories` has zero occurrences in `API/*.cs`.**

Therefore, today: **a user who edits a block list on Windows silently wipes the `is_ai_enabled` and
`blockAppsArray` values that the Mac app wrote.** `is_ai_enabled` is a *veto* on Mac
(`SettingsManager.swift:1885` — absent means AI on, `false` means AI off), so the visible symptom is
"I turned AI off for this block list on my Mac and it turned itself back on."

`categories` is safer only by luck: it is a **top-level** field the backend preserves when omitted
(backend#2400), and Windows omits it.

**Confirmed at the source, not just inferred from Mac's comment.**
`backend/apps/api-server/src/modules/focus-mode/services/focus-mode/focus-mode.service.ts:281`
builds the row as `new FocusMode({ ...focusMode, ...updateFocusModeDto, … })` — a **shallow**
spread. So a `metadata` key present in the DTO replaces the stored object outright; there is no
merge anywhere in the write path. The DTO (`dto/update-focus-mode.dto.ts:66-79`) only strips an
echoed `categories` copy, and carries this warning of its own: *"Older macOS builds copy every
top-level key they receive back into `metadata` on save."*

**Required fix (Phase 1):** make the Windows focus-mode write *preserve unknown metadata keys*
rather than rebuilding the blob from a literal. Read the server's existing `metadata` object, merge
the keys this client owns, send the merged result.

Note for whoever implements it: merging on the client is the pragmatic fix, but it is still
last-writer-wins across devices. If this keeps biting, the durable answer is to promote the
long-lived per-mode flags out of `metadata` into real columns, the way `categories` already was
(backend#2400) — worth raising before adding a third client that has to know the merge rule.

### 4.1b 🔴 "Use AI" is attached to a different object on each platform

Mac's rule (`Mac-App/CLAUDE.md`, "AI blocking ownership") is explicit:

> "Use AI for this block list" belongs to the **block list (focus mode)**, not to blocking schedules.
> Custom blocking schedules must NOT expose their own "Use AI" option.

Mac implemented that by **keeping the wire field but hiding the control**:
`Preferences/Settings/Custom Blocking Schedules/blockingScheduleEditorVC.swift:102` sets
`checkBoxAiEnabled.isHidden = true`, with a comment pointing at the CLAUDE.md rule. The struct
`customBlockingSchedule` still decodes `is_ai_blocking_enabled`
(`Focus mode/customBlockingScheduleData.swift:38,206`) so the value round-trips untouched — it is
simply no longer the governing switch. The governing switch is the focus mode's `is_ai_enabled`.

Windows does the **opposite**. `Models/CustomBlockingSchedule.cs:332-341` exposes
`bAIBlockingEnabled`, `Services/BlockingScheduleManager.cs:67` serialises it as
`is_ai_blocking_enabled`, the checkbox is live in `Preferences/BlockingSchedules/BlockingScheduleDetail.xaml`
and `Onboarding/CreateBlockingSchedule.xaml`, and **five consumers gate AI decisions on it**:
`Focus/GentleBlock.xaml.cs`, `Focus/FocusAlignmentScoreManager.cs`, `Utils/FocusChangedHandler.cs`,
`Utils/ProcessBrowserURLs.cs`, `TrayMenu.xaml.cs`. Windows has no `is_ai_enabled` at all (§4.1).

**Net user-visible effect:** turning AI off for a block list on the Mac has no effect on Windows,
and turning AI off for a schedule on Windows has no effect on the Mac. The same account behaves
differently on the two desktops with no indication why.

**Required fix (Phase 1):** move the Windows switch onto the block list — read/write the focus
mode's `is_ai_enabled` and have the five consumers read *that*. Keep `is_ai_blocking_enabled` on the
schedule payload (do not drop the field; Mac still round-trips it), but stop editing it and stop
reading it as the decision input. **Do not** mirror Mac's approach of merely hiding the checkbox
without moving the decision source — that would leave the divergence intact.

### 4.2 `config.json` — the i18n contract

Both apps download **the same file** from `https://focus-bear.github.io/assets/config/config.json`
(Mac: `allApiObject.getConfigFile`; Windows: `Utils/AppSettings.DownloadConfigFile()` →
`%LOCALAPPDATA%\Focus Bear\config.json` → `AppSettings.ConfigFileStrings`, a `JObject`). The
repo-root `config.json` / `config-es.json` are **offline fallbacks only**.

Source of truth is the **`Focus-Bear/assets`** repo, `config/`. Current language coverage there:
`config.json` (EN, 187 sections), `config-es.json` (187), `config-de.json` (186),
`config-fr.json` (186), `config-pl.json` (186) — the three trailing ones lack only `cleanShotPicker`
(a macOS-only section, harmless).

**The bundled Windows fallback is 21 sections behind the Mac config**, so an offline/first-run
Windows user gets English fallbacks for anything new. Missing sections:

```
aiBlockBuddyInfo, blockListViewInfo, breakFrequencyVcInfo, cleanShotPicker,
dragAwayPostponeVcInfo, eveningShutdownInfo, focusButtonInfo, focusMusicInfo,
focusMusicPromptInfo, focusStartVcInfo, localForecastViewInfo, localReportStatus,
managedBlockingInfo, menuView, notesViewInfo, officeModeSettingsVcInfo,
projectNoteEditor, statsViewInfo, talkingClockSettingsInfo, timeTrackerViewInfo,
toDoListViewInfo
```

Plus large key-level gaps inside shared sections, e.g. `scheduleViewInfo` (Mac has **266** keys the
Windows fallback lacks), `focusStateVcInfo` (102), `accountabilityBuddyInfo` (40),
`accountVcInfo` (10), `leftMenuVcInfo` (10), `browsersTabAuditVcInfo` (10).

**Windows has keys Mac does not** (drift in the other direction — Mac would show English fallbacks
for these if a Mac screen ever used them):

* `geekModeInfo`: `alertConfirmSwitchGeekMode`, `alertConfirmSwitchGeekModeDescription`,
  `alertSimpleModeEnabled`, `buttonActivateGeekModeText`, `buttonDontShowAgainText`,
  `buttonStickSimpleModeText`
* `scheduleViewInfo`: `deepWorkNoRoom`

There is also a shared `WindowsSpecific` section (20 keys) in the Mac `config.json` — proof the file
is deliberately cross-platform. **Never rename a key's *value*; never repurpose an existing key.**

`%@` / `%d` / `%1$@` placeholders in shared strings are authored for Swift. Windows already has the
translation helper: `Habits/DayPlan/DayPlanText.Fmt()` converts them to `{0}` before
`string.Format`. **Reuse that helper (or lift it somewhere shared) for every new config section** —
do not re-invent it per screen.

### 4.3 🟠 Notes endpoint spelling disagreement

Mac `FocusBear/Notes/NotesApi.swift` uses `/note/tags` (plural). Windows draft PR #1507 uses
`/note/tag` (singular). Exactly one is right. **Confirm against the backend before landing #1507.**

### 4.4 Block-list category taxonomy

`Mac-App/FocusBear/Preferences/Settings/Block List/DistractionCategory.swift` defines the wire
format, shared with the backend's `focus_mode.categories` column:

```
social_media, news, video, gaming, dating, real_estate, porn, gambling, other
```

Element shape: `[{ "entry": "<url or app>", "category": "<raw value above>" }]`, sent **top-level**,
not inside `metadata`. Unknown ids are dropped, not errored, so a mixed-version fleet degrades to
"uncategorised". Raw values must never be renamed. Only `displayName` is localised.

### 4.5 Managed-blocking conformance fixtures

Mac ships three versioned JSON fixtures that `Mac-App/docs/managed-blocking-native.md` states are
"the same 55 policy and 21 URL conformance cases as the backend and Windows":

| Mac path | Windows equivalent (PR #1522) |
|---|---|
| `FocusBearTests/Fixtures/managed-policy-evaluation.v1.json` (55 cases) | `tests/ManagedBlocking.Tests/Fixtures/policy-evaluation.v1.json` |
| `FocusBearTests/Fixtures/managed-url-normalization.v1.json` (21 cases) | `tests/ManagedBlocking.Tests/Fixtures/url-normalization.v1.json` |
| `FocusBearTests/Fixtures/managed-wire-examples.v1.json` | **no Windows equivalent** |

The wire-example fixture carries `identity`, `snapshot`, `receipt_request/response`,
`stale_receipt_response`, `minimal_batch_request/response`, `online_responses` (6),
`settings_create_request`, `settings_pending_response`, `settings_approved_response`. **Port it** —
it is the only thing pinning the request/response envelopes.

Policy model (`FocusBear/ManagedBlocking/ManagedPolicy.swift`), snake_case on the wire:
`id, school_id, student_id, kind, revision, status, published_revision, name, time_zone,
is_enabled, windows[{days_of_week, start_time, end_time, all_day}], blocked_categories,
allowlist[{url, include_subdomains, reason, expires_at}], excluded_dates[{start_date, end_date}],
unknown_action, unavailable_action`. The nine parent-editable fields are enumerated in
`ManagedPolicyChanges.editableFields`.

Mac also gates CI on `scripts/check-managed-blocking-translations.py` (EN/ES key + placeholder +
literal-reference parity). Port it.

### 4.6 Other shared surfaces worth naming

* **Block page.** Both apps download the same HTML from
  `https://focus-bear.github.io/assets/focus-blocked/index.html`. Mac serves it from an embedded
  Flask binary on `localhost:50501`; Windows caches it to `%LOCALAPPDATA%\Focus Bear\focus-block.html`
  and navigates to a `file:///` URL. Query params differ per platform
  (`Utils/ProcessBrowserURLs.cs:33-35` vs Mac's `blockWebUrlString?old_url=…`) — **UNVERIFIED**
  whether the page reads both shapes.
* **Pusher realtime.** Mac `pusherEvents.swift` (`private-<userId>`, auth `<baseUrl>/pusher/user-auth`,
  events `focus-mode-started`, `focus-mode-finished`, `activity-completed`, `settings-updated`);
  Windows `Utils/PusherHandler.cs`. New cross-device features must agree on event names.
* **`/user-local-device-settings`** — both clients round-trip device settings here
  (Mac `SettingsManager`; Windows `GetUserLocalDeviceSettings`). Any new device-scoped setting needs
  agreement on the key.

---

## 5. Sequenced work plan

Each phase is self-contained and finishable. Build verification is the same everywhere unless noted:

```
nuget restore "Focus Bear.sln"
msbuild "Focus Bear.sln" /p:Configuration=Release /p:Platform="Any CPU" /m /v:minimal
```

…which is exactly what `.github/workflows/build.yml` runs on `windows-latest`.

> **Before every phase:** the csproj is **legacy, non-SDK**. New `.cs`/`.xaml` files must be added
> to `Focus Bear.csproj` by hand with `<Compile Include>` / `<Page Include>` entries — there is no
> globbing. A file that builds locally in the IDE but is missing from the csproj will still break
> CI. PR #1522's checklist calls this out ("project XML … and explicit source inclusion checks").

### Phase 0 — Foundations (blocks everything; do first)

**0a. Resolve the branch situation.** `feat/teacher-mode` is 131 behind and duplicates work
`origin/main` already does better. Decide (§7 Q2), then start the port work from `origin/main`.

**0b. Stand up a real test project. — ✅ DONE (#1569).** `FocusBear.Core` +
`FocusBear.Core.Tests` exist, and a `dotnet test` step runs in `.github/workflows/build.yml`.

The shape matters more than the fact, because it changes how every later phase gets verified.
`FocusBear.Core` is **WPF-free** and multi-targets `net472;netstandard2.0`; the test project targets
`net8.0` and consumes the netstandard build. The consequence:

```
dotnet test Core.Tests/FocusBear.Core.Tests.csproj     # ~20ms, on any machine
```

This was measured rather than assumed. `dotnet build` handles a **net472** class library on macOS
perfectly well — the SDK resolves .NET Framework reference assemblies from NuGet — and only
`UseWPF` fails, because the WindowsDesktop SDK is Windows-only. The barrier was never the framework
version; it was the UI dependency. So logic that lives in `Core` has a sub-second local loop on any
developer's machine, and logic that lives in the app has a two-minute CI loop.

**The rule that follows: port logic into `Core` first, wire the WPF view to it second.** A phase
that puts its rules inside the app assembly gives up local testing for no reason.

PR #1522's `tests/ManagedBlocking.Tests` scaffolding is now redundant; it can be dropped or
retargeted at `Core.Tests` when that gated PR is revisited.

**0c. Port the pure-logic conformance fixtures.** Copy Mac's three `FocusBearTests/Fixtures/managed-*.v1.json`
and add `ScheduleKitTests` equivalents by converting `Habits/DayPlan/DayPlanScheduleBuilderSelfTest.cs`
into real test-project tests (it is already a faithful port of `ScheduleKitTests.swift`; it just
lives in the app assembly). *Verify:* tests green in CI; delete the in-app self-test class.

**0d. Port the two i18n CI gates.** `Mac-App/scripts/check-config-assets-sync.py` and
`check-hardcoded-strings.py` + their workflows. The first fails a PR that adds a string to the
bundled `config.json` without adding it to the `assets` repo; the second fails a PR that adds a
hardcoded English user-facing string. Both are language-agnostic Python and need only path changes.
*Verify:* deliberately add a hardcoded string in a scratch commit and watch CI fail.

**0e. Refresh the bundled `config.json` / `config-es.json` from the assets repo.** 21 missing
sections, hundreds of missing keys (§4.2). Also decide whether to bundle `config-de/fr/pl.json`
(Windows currently bundles EN/ES only, while the remote download already serves five languages —
**UNVERIFIED** whether `AppSettings.DownloadConfigFile()` requests de/fr/pl at all; check
`sLangCode` handling). *Verify:* app launches offline in Spanish and shows no `%@` or raw keys.

**0f. Lift `DayPlanText.Fmt` / `T` into a shared config-string helper** (suggested home:
`Utils/ConfigText.cs`) so Phases 2-6 do not each re-implement `%@` → `{0}` translation.
*Verify:* Day Plan still renders identically; new helper unit-tested.

### Phase 1 — Focus-mode wire parity (fixes a live data-loss bug)

*Depends on: 0b (somewhere to put tests).*

1. **Stop clobbering `metadata`.** In `API/FocusBearAPIClient.cs`, replace the two hand-built
   `metadata` string literals (`AddFocusMode` ~line 649, the edit path ~line 799) with a
   read-merge-write: parse the focus mode's existing `metadata` `JObject`, set the keys this client
   owns, serialise the merged object. *Verify:* unit test that an unknown key (`is_ai_enabled`,
   `blockAppsArray`) present in the input survives a round-trip through the payload builder.
2. **Move the AI switch from the schedule to the block list** (§4.1b). Add `is_ai_enabled` to the
   focus-mode model and payload; mirror `SettingsManager.swift:1876-1890` (absent ⇒ AI on; `false`
   on the *running* focus mode ⇒ AI relevance checking off). Repoint all five consumers —
   `Focus/GentleBlock.xaml.cs`, `Focus/FocusAlignmentScoreManager.cs`, `Utils/FocusChangedHandler.cs`,
   `Utils/ProcessBrowserURLs.cs`, `TrayMenu.xaml.cs` — from `ActiveSchedule.bAIBlockingEnabled` to
   the running focus mode's `is_ai_enabled`. Keep serialising `is_ai_blocking_enabled` on the
   schedule (Mac still round-trips it) but stop treating it as the decision input.
3. **Move the "Use AI to decide…" checkbox** out of `Preferences/BlockingSchedules/BlockingScheduleDetail.xaml`
   and `Onboarding/CreateBlockingSchedule.xaml`, and onto the block-list settings screen
   (`Preferences/FocusAdvanced.xaml` or `SettingsTab/FocusModeSetting.xaml`), matching Mac's
   `focusPreferencesAdvancedVc`. **Migration question for the user:** existing Windows users have
   per-schedule values that will stop being honoured. Decide whether to seed each block list's
   `is_ai_enabled` from the schedules that reference it, or to leave it defaulting to on.
4. **Add `categories`** as a top-level field on focus-mode writes, with the nine-value taxonomy from
   §4.4 as a C# enum with explicit string values. *Verify:* fixture test over the taxonomy;
   round-trip test that omitting `categories` does not clear it.

*Why first:* these are correctness bugs affecting shipped users on both platforms today, they are
small, and every later block-list change depends on the payload being non-destructive.

*Conflict watch:* open PRs #1216 (inline delete buttons in focus-mode block lists) and #1138
(block-list action icons) touch the same screens. Reconcile before starting.

### Phase 2 — Stats native (`/webview/stats` → WPF)

*Depends on: 0b, 0d, 0e, 0f.*

* **Touch:** new `Stats/` directory (suggested: `Stats/StatsWindow.xaml`, `StatsApi.cs`,
  `StatsModels.cs`, `StatsMath.cs`, `StatsText.cs`); replace the WebView2 host in
  `Preferences/Stats.xaml(.cs)`; add the endpoints from §2.3 to `API/FocusBearAPIClient.cs`;
  add `statsViewInfo` handling.
* **Approach:** port `FocusBear/Stats/StatsMath.swift` first as pure logic with tests (it is the
  only part worth unit-testing); then models; then the tab strip and charts. Mac's chart rendering
  is `StatsCharts.swift` (custom drawing) — on WPF, **UNVERIFIED** whether to use a charting
  package or hand-drawn `Path` geometry; the app has no charting dependency today and
  `packages.config` additions need care.
* **Deliberately deferred:** the SSE `/user/motivational-summary` pep talk. `.NET Framework 4.7.2`
  has no `HttpClient` server-sent-events support out of the box; doing it needs a manual chunked
  reader. Keep `Habits/HabitExtraPersonal.xaml.cs`'s `/webview/motivational-summary` webview until
  Phase 2b.
* *Verify:* `StatsMath` tests; manual check of each tab against the still-live webview on a second
  machine.

### Phase 2b — Pep Talk (SSE) native

Small follow-on once Phase 2's models exist. *Verify:* streamed text renders incrementally; a
dropped connection falls back gracefully.

### Phase 3 — Notes / Brain Dump native

*Depends on: 0e, 0f. Already ~80% written.*

* **Touch:** finish **PR #1507** (`feat/native-notes-view`) — `BrainDump/BrainDumpWindow.xaml(.cs)`,
  `Models/Note.cs`, `Utils/NoteHtml.cs`. It is marked "draft, unbuilt".
* **Before landing:** resolve the `/note/tags` vs `/note/tag` disagreement (§4.3); confirm
  `notesViewInfo` keys are in the bundled config (Phase 0e); confirm the HTML⇄FlowDocument mapping
  matches Mac's `Notes/NoteHTML.swift` so a note written on one platform renders on the other.
* **Then extend:** tags, archive, search (Mac debounces 500 ms — `NotesStore.swift` header),
  project-notes feed (`ProjectNotesFeed.swift`).
* *Verify:* build it (it currently is not built); round-trip a note with bold/lists/links between
  Mac and Windows.

### Phase 4 — To-Do list native (`/webview/tools-todo-list` → WPF)

*Depends on: 0b, 0e, 0f, and Phase 3 (shares the rich-text and tag plumbing).*

* **Touch:** new `ToDo/` directory; replace the WebView2 in `Focus/ToDoList.xaml(.cs)`; extend
  `API/FocusBearAPIClient.cs` with the `/to-do*` endpoints from §2.3; `toDoListViewInfo` strings.
* **Approach, in order:** (1) models + list read + filters; (2) quick-add parser — port
  `Mac-App/FocusBearTests/QuickAddTaskParserTests.swift` as the spec; (3) editor (due date, project,
  quadrant); (4) comments/reactions; (5) attachments (needs the generate-upload-url flow).
* **Note:** Day Plan already writes to `PUT /to-do` through `Habits/DayPlan/DayPlanSync.cs`, which
  refuses to sync a task without a captured `EisenhowerQuadrant` precisely because the create-DTO
  upsert would clobber it. **The new To-Do screen must respect the same rule** or it will reintroduce
  that clobber.
* *Verify:* parser tests; a task created on Windows appears correctly on Mac and vice versa,
  including quadrant and planned date.

### Phase 5 — Projects + Kanban

*Depends on: Phase 4.*

* **Touch:** `ToDo/Projects*`; `/projects*` and `/portfolio*` endpoints; `Focus/ToDoList` gains a
  Projects tab.
* **Approach:** portfolio list → project detail → members/invitations → Kanban board. Port
  `Mac-App/FocusBearTests/ProjectKanbanDragTests.swift` and `DayPlanProjectRepairTests.swift` /
  `DayPlanProjectMergeFollowTests.swift` as the drag and merge specs.
* **Open:** the collaborative project-note editor (Mac's bundled TipTap/Yjs
  `ToDo/ProjectNoteEditorWeb/`) — §7 Q4. Simplest v1: read-only rendering plus "open on the web".
* *Verify:* drag tests; a project created on Mac is editable on Windows.

### Phase 6 — Day Plan catch-up

*Depends on: 0f. Independent of Phases 2-5.*

* **Touch:** `Habits/DayPlan/*`, `Habits/HabitsDayPlan*`.
* **Approach:** re-diff `DayPlanScheduleBuilder.cs` against the current `ScheduleKit.swift` (they
  have drifted by ~9 days of Mac changes), then port, in this order: `UserTimezoneSync`,
  `EventTimeParser` + `EventDateTimeField`, `EventCancellation`, `BusyHoursCalculator/Reporter`,
  `DayPlanProjectRepair`, `DayFixPlan`, then `LocalTimeForecast` + `LocalForecastStore/View`
  (largest; read `Mac-App/docs/local-time-forecast.md` first — it is an explicit contract).
* **Constraint:** Mac's forecast depends on `exclude_completed=true` + `meta.excludesCompleted`
  from backend #2387. **Confirm that is deployed** before shipping the Windows forecast.
* **Constraint:** Windows calendar is read-only via the backend (§6.5). Event *editing* features
  (`EventDateTimeField`, `EventCancellation`) have **no Windows path today** — descope or raise a
  backend request.
* *Verify:* port `ScheduleKitTests` / `LocalTimeForecastTests` / `ForecastReschedulingTests` /
  `BusyHoursCalculatorTests` as real tests in the Phase 0b project.

### Phase 7 — Focus Start + running Focus Session redesign

*Depends on: Phase 1 (block-list picker needs correct focus-mode writes), Phase 4 (task picker
needs the native to-do store). **Also depends on a product decision — §7 Q1.***

* **Touch:** `Pomodoro/StartSimplePomodoro.xaml(.cs)` and `Pomodoro/StartPomodoro.xaml(.cs)` — these
  are the direct structural counterparts of Mac's `simplePomodoroVc` / `pomodoroVc`, which host
  `FocusStartSimpleScreen` and `FocusStartAdvancedScreen` respectively, so the Simple/Advanced split
  already exists here; plus `Focus/StartFocusMode.xaml(.cs)`, `Focus/FocusProgress.xaml(.cs)`,
  `Focus/StopFocusMode.xaml(.cs)`, `Focus/FocusModeEnd.xaml(.cs)`; `focusStartVcInfo` (59 keys) and
  the ongoing-session strings (`focusStateVcInfo`, 102 keys missing from the Windows bundle).
* **Approach:** port `FocusStartState.swift` / `FocusStartModel.swift` and
  `FocusSessionStore.swift` / `FocusSessionModels.swift` as pure logic with tests
  (`FocusStartTests.swift`, `FocusSessionTests.swift`, `FocusSessionExtensionTests.swift`,
  `FocusSessionStopPermissionTests.swift` are all portable), then build the XAML on top.
* **Caveat:** these are the *least settled* Mac areas (§1.3). Consider waiting for the Mac
  integration branch to reach `main-human-verified` before starting.
* *Verify:* the four ported test suites; visual comparison against the Figma files named in the
  Swift headers ("0.0/ Flow Start", "1. Ongoing Session").

### Phase 8 — Managed / school blocking ⚠️ GATED — must not ship ahead of the Mac decision

**Do not merge this to a shipping branch until the Mac-side decision is made.**

Current state:
* Mac: enforcement lives in **PR #2578** (`poc/mac-safety-enforcement`), explicitly titled
  **"DO NOT MERGE"**. Its predecessor was merged to the integration branch by mistake and
  **reverted** (`0b4946bc8`). *Verified:* the policy model, evaluator, store, inspector and
  home-policy editor **are** on the integration branch, compiled (8 `project.pbxproj` entries) and
  live — `ManagedBlockingRuntime.shared` is called from `AppDelegate.swift` at lines 220, 243, 1382,
  1591, 7926, 7948-9, 9444 and from `Focus mode/focusModeData.swift:612-613`. What PR #2578 adds on
  top, and what is therefore **not** on the branch, is the *browser-enforcement hardening*:
  background/incognito Chrome tab sweeping, a self-contained localized block page instead of
  `about:blank`, and redirects bound to the observed process/window/tab/URL and re-checked against
  policy revision, session and lease.
* Windows: **PR #1522** (`feat/classroom-managed-policies`) is open, draft, 29 files, +5,854 lines,
  with its own `ManagedBlocking/` directory and test project. Nothing is in `origin/main`.
* Backend dependency: `Focus-Bear/backend` PR **#2370**. Classifier dependency:
  `FocusBear-Distraction-Detection-Model` PR **#25**.
* Planning doc: `Mac-App/plans/classroom-managed-blocking-schedules.md` (the full product spec,
  including the 39-category taxonomy and the parent-approval flow).

**Sub-phases, in order:**
1. Extract the **test project + fixtures** from #1522 into Phase 0b (already listed there).
2. Keep #1522 as a draft, rebased on `origin/main`, with the evaluator + fixtures passing.
3. Ship **nothing user-visible** until: the Mac managed-safety decision is made (owner: Manish, per
   PR #2578's body), backend #2370 is deployed, and the school offline-lease / recovery policy is
   agreed. The Mac doc explicitly calls the current 24-hour lease "a development default".
4. Enforcement last, and only after the browser-guarantee question in §6.3 is answered for Windows.

**Known Mac limitations to carry over, not re-discover:** post-navigation enforcement only (not a
network or OS-level guarantee); background-sweep latency grows with tab count; non-Chromium
browsers are foreground-only; SigmaOS is explicitly unsupported because address-bar automation
cannot give the conditional-navigation guarantee. The safety classifier currently treats *absence of
signal* as `allow` and its suffix comparison accepts lookalike hosts (`evilwikipedia.org`) — the
plan says fix before school use.

---

## 6. Risks and platform differences

### 6.1 WPF has no SwiftUI — pick the substitute deliberately

Mac's team rule is "new UI in SwiftUI, hosted via `NSHostingController`". The Windows app is
**code-behind-driven WPF with essentially no MVVM**: no `ViewModels/` directory anywhere,
`INotifyPropertyChanged` in 6 files, `ICommand` in 1 (`FocusBearMainWindow.xaml.cs`). Navigation is
mixed by convention — `Window` for dialogs, `UserControl` for embedded content, `Frame` + `Page` for
multi-step flows (`Preferences/PreferencesWindow.xaml`, `Habits/HabitWindow.xaml`,
`Onboarding/OnboardingViewController.xaml`).

**Recommendation:** port the Mac **Store/Model layer faithfully** (`*Store.swift` → a C# class with
`INotifyPropertyChanged`; `*Models.swift` → POCOs; `*Logic.swift`/`*Math.swift` → pure static
classes with tests) and build **plain WPF `UserControl`s with code-behind** on top, matching the
existing house style. Do **not** introduce a new MVVM framework as part of a port — that is a
separate, contentious change. The Day Plan port already set this precedent and it worked.

### 6.2 Build/toolchain constraints

* `.NET Framework 4.7.2`, legacy non-SDK csproj, `packages.config` + `<Reference>` — **not**
  PackageReference. Adding a NuGet package means editing three places (`packages.config`, csproj
  `<Reference>` with `<HintPath>`, and the restore).
* **Newtonsoft.Json only**; no `System.Text.Json`. Most API responses are kept as raw JSON strings
  on `AppSettings` (`sRawUserSettings`, `sRawTasks`, …) and walked with `JObject.Parse`. New ports
  should introduce typed models (`Models/`) rather than extending the raw-string pattern — but do
  it inside the new feature, not by refactoring `AppSettings`.
* Every new file needs a manual csproj entry (see the callout in §5).
* Release builds set `SentryOrg=focus-bear`; `build.yml` passes `FOCUSBEAR_OPENAI_API_KEY` as a
  secret — note there is a live plan (`Mac-App/docs/backend-ai-endpoints-plan.md`) to move
  client-side OpenAI calls behind backend endpoints for *all* clients. Coordinate before adding new
  direct OpenAI usage in `Services/OpenAIService.cs`.

### 6.3 🔴 Browser integration and URL blocking — the biggest platform gap

| | Mac | Windows |
|---|---|---|
| Read active tab URL | Per-browser **AppleScript**, serialized through `runAppleScriptSafely` (`globalFile.swift`); browsers enumerated in `allBrowsers.swift` | **UI Automation** (`Interop.UIAutomationClient`), `Utils/FocusChangedHandler.cs` (`IUIAutomationFocusChangedEventHandler`) walking the tree to the address-bar edit control |
| Redirect a blocked tab | AppleScript sets the tab's URL | `IUIAutomationValuePattern.SetValue(...)` on the address bar, then `SendKeys.SendWait("{ENTER}")` |
| Block page | Embedded **Flask** binary on `localhost:50501` (`flaskServer.swift`) | Local `file:///%LOCALAPPDATA%/Focus Bear/focus-block.html` (`Utils/AppSettings.FocusBlockPagePath`) |
| Firefox | Dedicated **WebExtension** (`FirefoxExtension/focus-bear-blocker/`) + Swifter HTTP bridge on `127.0.0.1:50502` | Special-cased MSAA/`IAccessible` fallback inside `FocusChangedHandler` (`bForFirefox`, `AppSettings.FirefoxLastAccWindow`); **no extension, no native-messaging host** |
| App blocking | `runningApp.hide()` | `ShowWindow(hwnd, SW_FORCEMINIMIZE)` via P/Invoke |

**Consequences:**
* `SendKeys` + address-bar `SetValue` is materially less reliable than AppleScript navigation. Any
  Mac feature whose correctness depends on "the redirect definitely happened" — **managed blocking
  in particular**, whose Mac doc requires a "conditional navigation guarantee" — needs a Windows
  answer before it can claim the same guarantee. **This is an open design question, not a port.**
* Mac's background/incognito tab sweep (PR #2578) has **no UI-Automation equivalent**: UIA sees the
  foreground window's address bar, not background tabs. **UNVERIFIED** whether a Windows equivalent
  is feasible at all without an extension.
* Porting a URL-classification fix is 3-4 files on Mac (`focusTimerManager`,
  `focusHabitTimerManager`, `suggestWorkFocusManager`, `AppDelegate.blockUrlsInBackground`) but one
  place on Windows (`FocusChangedHandler` + `ProcessBrowserURLs`). Windows is *better* here; don't
  import the Mac duplication.

### 6.4 Window management and full-screen

Mac's "full screen" is its own **borderless kiosk window** (`styleMask = [.borderless, .resizable]`,
`.canJoinAllSpaces`, `canHide = false`, `mainWindowController.showFullSizeWindow()`), plus blank
windows on secondary monitors (`showBlankWindowInSecondaryMonitor`). Branch on
`checkIsFullScreenEnabled()`.

Windows already has a close functional equivalent and it is better-defended than Mac's:
`Utils/FullScreenParent.cs` (`class FullScreenParent : Window`, namespace `Focus_Bear.Habits`)
strips `WS_MINIMIZEBOX`/`WS_MAXIMIZEBOX` via `GetWindowLong`/`SetWindowLong` **and installs a
`WH_KEYBOARD_LL` low-level keyboard hook** to intercept Alt-Tab / Win-key escapes.
`FocusBearMainWindow` and `Habits/BlankWindow` derive from it; `App.xaml.cs` sets
`WindowStyle = None; WindowState = Maximized; Topmost = true` while a routine plays when
`AppSettings.bPlayActivitiesInFullScreenMode` is set, and reverts afterwards. The user-facing toggle
lives in `Preferences/SettingsTab/UI.xaml.cs` (`FullScreenCheckbox`).

So the Windows analogue of `checkIsFullScreenEnabled()` is `AppSettings.bPlayActivitiesInFullScreenMode`.
Port the *branch*, not the mechanism — do not introduce a second full-screen path.

**Mac-specific trap that does not apply:** the borderless-full-screen Return-key/`defaultButtonCell`
bug (`EditOnClickTextField`, `NewlineKeepingTextView`). WPF has no `defaultButtonCell`; use
`IsDefault`/`AcceptsReturn` normally.

### 6.5 🟠 Calendar: local EventKit vs backend-proxied, read-only

Mac `Schedule/CalendarManager.swift` uses **EventKit**: local calendar access (with a TCC
permission prompt), `meetings(for:)` with declined-event filtering, and **create/update/delete with
recurrence, per-event colour and notes**.

Windows has **no local calendar integration at all**. `Habits/DayPlan/DayPlanCalendar.cs` calls the
backend: `GET /calendar/{platform}/calendar-datas` and `GET /calendar/{platform}/events`
(`platform` = `google` | `microsoft`), **read-only** — there is no create-event endpoint.

**This blocks a real subset of Phase 6**: `EventDateTimeField`, `EventCancellation`, per-event
colour/notes editing and "add meeting from Day Plan" have no Windows path. Either descope them or
raise a backend request for write endpoints. Do not silently build a local Outlook/Graph integration
as part of a port.

### 6.6 Credential storage

Mac stores Auth0 credentials in the **Keychain** (`CredentialsManager`; signed Release builds use
the data-protection keychain via `Auth0/CredentialsKeychainStorage.swift`).

Windows stores the access token as a **plain, unencrypted `REG_BINARY`** under
`HKCU\Software\Focus Bear\<userID>`, value `userat` (refresh token `userrt`, email `userem`) —
`Models/RegistryManager.cs:1374-1416`. No DPAPI (`ProtectedData`), no Windows Credential Manager
anywhere in the repo.

This is **pre-existing**, not something the port introduces. But: managed blocking's Mac doc claims
snapshots use "local OS-protected storage", and PR #1522 adds `Utils/LocalSettingsManager.cs` for
managed local state. **If managed blocking ships on Windows, its offline policy cache and lease must
not be plaintext-in-registry** — that would be a weaker guarantee than the Mac doc states. Decide
whether to introduce DPAPI for the managed subsystem (and, separately, whether to retrofit the
existing tokens).

### 6.7 Startup / login items

Mac uses a second app bundle, **`FBHelper`**, installed as a user LaunchAgent
(`~/Library/LaunchAgents/com.focusbear.focusbearhelper.plist`, `KeepAlive=true`) with
`DistributedNotificationCenter` + App Group IPC — it resurrects the main app if force-quit.

Windows has **two** mechanisms already: a logon-trigger task via Task Scheduler COM interop
(`Models/AppTaskScheduler.cs` → `CreateTask()`, `SetTaskStatus()`, `CheckTaskExists()`, using
`TaskSchedulerPackage/Interop.TaskScheduler.dll`), **and** an `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
value written by `Models/RegistryManager.AddToStartup()`. There is an existing force-quit watchdog
(PR #1026, `FCZ-ForceQuitWatchDog`).

**Do not port FBHelper.** Any "the app must stay running" requirement from a Mac feature maps onto
the existing Task Scheduler / watchdog machinery. (Worth a separate look at some point: having both
a Run key *and* a scheduled task is duplicate startup registration — out of scope here.)

### 6.8 Notifications and permissions

* Mac still uses the deprecated `NSUserNotification` (`globalFile.swift:1481`, registered tech debt
  P1-10). Windows uses **custom WPF windows, not the Windows Toast API** — there are zero
  `ToastNotification` / `Windows.UI.Notifications` references in the repo. The notification surfaces
  are `Utils/TrayBalloon.xaml(.cs)` (balloon-style; on `origin/main` it gained an optional action
  button + 10-second countdown, used for the teacher-session "Request unlock" prompt) and
  `Utils/FBMessageBox.xaml(.cs)` (generic modal alert). **Do not port Mac's notification code, and
  do not introduce Toast as part of a port** — use `TrayBalloon` / `FBMessageBox`.
* Mac requires **TCC grants** (Accessibility, Automation/AppleEvents, Calendar) and has a whole
  onboarding flow for them (`accessibilityPermissionVcInfo`, `firefoxPermissionsVcInfo` config
  sections). Windows drives UI Automation directly through `Interop.UIAutomationClient` and needs
  **no user-granted permission at all** — there are no `UIAccess`/accessibility-permission patterns
  in the repo. Any ported screen that explains a macOS permission (`explainingPermissionsVcInfo`,
  `aiPermissionVcInfo`) must be **dropped, not translated** — and the config keys left alone,
  because Mac still uses them.
* Webview token handoff shape is already platform-tagged: `Preferences/Stats.xaml.cs` posts
  `{ access_token, type: "win", lang, font, theme }` via `ExecuteScriptAsync(...loadAccessTokenForStats...)`.
  When a screen goes native, that contract disappears with it — but check `web_dashboard` isn't
  relying on the `type: "win"` tag for anything else first.
* Mac's menu-bar `NSStatusItem` + `NSPopover` menu (`menuView/MenuView.swift`, 60+ `menuView`
  config keys) maps onto Windows' `TrayMenu.xaml`. The `menuView` config section is one of the 21
  missing from the Windows bundle (§4.2).

### 6.9 Process/product risks

* **Moving target.** Focus Start, Focus Session, Block List and Managed Blocking changed in the last
  9 days on Mac. Starting Phase 7 before the Mac integration branch reaches `main-human-verified`
  means porting twice.
* **Two teacher-mode implementations.** §7 Q2. Left unresolved, a later merge will silently pick one.
* **No Windows test culture yet.** Phase 0b is the riskiest phase socially, not technically: it asks
  the team to adopt a test project it has lived without. If it is skipped, every later phase loses
  its verification story and the shared fixtures cannot be enforced.
* **PR backlog.** ~24 open Windows PRs, many labelled `[Blocked UX]` or `[Tested by human]`. Several
  touch files these phases touch (`#1216` inline delete buttons in focus-mode block lists → Phase 1;
  `#1138` block-list action icons → Phase 1; `#1045` to-do list DPI scaling → Phase 4). Reconcile
  before starting, or expect conflicts.

---

### 6.10 🟠 The target framework: .NET Framework 4.7.2

Raised as a side question, answered here because it changes the *sequencing* of everything above.

**Support.** .NET Framework 4.x is a Windows OS component, serviced through Windows Update for the
life of the Windows versions that carry it, and 4.8.1 is the last version that will ever ship. It is
not on a countdown the way .NET Core / 5+ releases are. There is no support cliff to plan around.
**UNVERIFIED:** written against knowledge to May 2026; Microsoft's lifecycle pages move, so confirm
before quoting this to anyone.

**The real exposure is dependency drift, not the framework.** The project uses legacy
`packages.config` (zero `PackageReference`) and pins, among others, `Microsoft.IdentityModel.*`
6.34.0 and a `Microsoft.Extensions.*` mix of 6.0.0/8.0.0. As libraries drop `netstandard2.0` /
`net472` targets, the app stops being able to take updates — including security fixes. Auth0 and
IdentityModel are exactly the family where staying current matters, since they handle tokens.

**The tooling cost is already being paid.** MSBuild only: no `dotnet build`, no `dotnet test`, and
C# 7.3 as the practical language ceiling. It is part of why there is no test project (Phase 0b), and
it is why this plan's own metadata fix (#1569) had to be compile-checked by pushing to CI rather
than built locally — a several-minute loop instead of a several-second one.

**Migration blockers: audited, and thinner than they look.** Counting `using` statements suggests
`System.Web` in 12 files and `System.Runtime.Remoting` in 1. Checking for actual *type* references
finds **none** — every one is a stray using with nothing behind it. Nothing uses
`AppDomain.CreateDomain`, WCF/`ServiceModel`, or `ConfigurationManager`. What remains
(`System.Management`, `Interop.UIAutomationClient`, `System.Drawing`, registry access) all works on
modern .NET on Windows, some via a compatibility package.

```
grep -rn "System\.Web" --include="*.cs" . | grep -v ":using "   # → no hits
```

WPF has run on modern .NET since Core 3.0, so the shape of the work is: SDK-style csproj,
`packages.config` → `PackageReference`, delete the dead usings, fix the fallout. Not a rewrite.

**Why it affects sequencing — revised after Phase 0b landed.** The original argument was that a
test project built against `net472` under MSBuild is a different artifact from one on modern .NET,
so the migration should be decided first. That argument is now much weaker: `FocusBear.Core` targets
`netstandard2.0` alongside `net472` and its tests already run on `net8.0`, so **the fast local loop
exists without migrating anything**. The test project does not need redoing after a migration; it is
already on the modern side of the fence.

What remains is the dependency-drift argument, which is real but slow-moving, and the tooling
friction on the *app* assembly — which matters less every time a phase moves logic into `Core`.

So this is no longer a blocker for Phase 0, and question 8 in §7 is downgraded accordingly: worth
deciding, not worth waiting on.

## 7. Open questions — decide these before work starts

**Q1. Functional parity or visual parity?**
The Windows CHANGELOG repeatedly says "matching the macOS app" / "to match the Mac app" for the
blocking-schedule editor and the PACT invite popup, which suggests the house rule is *visual*
parity. But Phases 2-5 (Stats, Notes, To-Do, Projects) are **webview → native** conversions where
matching the *web* design may matter more than matching Mac's SwiftUI, and Phase 7 is a Figma
redesign that Windows has never had. **Which is it, per phase?** My recommendation: functional +
data parity for 2-6, and treat Phase 7 as a separate product decision, not a port.

**Q2. What happens to `feat/teacher-mode`?**
It is 131 commits behind `origin/main` and contains a second, less mature teacher-mode
implementation than the one `origin/main` already ships (no PACT invite window, no rejoin-after-decline,
no AI-blocking-in-session, no idle/alignment heartbeat). My recommendation: **abandon the branch**,
salvage anything genuinely unique from its 6 commits, and work from `origin/main`. But it is not my
call, and someone may be mid-flight on it.

**Q3. Managed blocking — hold, or land behind a flag?**
Mac PR #2578 says "DO NOT MERGE" and the decision belongs to Manish. Windows PR #1522 is draft and
depends on backend #2370. Options: (a) freeze #1522 as a rebased draft; (b) land the non-enforcing
parts (policy model, evaluator, fixtures, tests) behind an off-by-default flag so it stops rotting;
(c) land nothing. **My recommendation: (b), plus extracting the test project now (Phase 0b)** — but
only if someone confirms no user-visible surface appears.

**Q4. The collaborative project-note editor.**
Mac ships a **bundled TipTap/Yjs web bundle** (`FocusBear/ToDo/ProjectNoteEditorWeb/`, built with
`build.mjs`) inside the app, talking to `/projects/{id}/notes/{id}/doc` and `/realtime-auth`.
Windows could reuse the same JS bundle inside WebView2 (it already has WebView2) — which is
pragmatic but re-introduces a webview right after removing others. Or ship read-only + "open on the
web". **Which?**

**Q5. Does the Windows test project happen?**
Everything in Phases 0c, 1, 4, 5, 6 and 7 cites ported Mac test suites as its verification. If the
answer is "no test project", say so now so the plan can be rewritten around manual QA evidence
instead of pretending.

**Q6. Migrating the AI toggle (§4.1b) — seed, or reset?**
Moving "Use AI" from the schedule to the block list changes behaviour for every existing Windows
user who set it per-schedule. Options: (a) on first run after the update, seed each block list's
`is_ai_enabled` from the schedules that reference it (ambiguous when several schedules with
different values share one list); (b) let it default to on (absent ⇒ on, matching Mac) and accept
that some users silently get AI back; (c) prompt once. **My recommendation: (a) with "any
referencing schedule had it off ⇒ off", which is the conservative reading** — but this is a
product call.

**Q7. Does the backend guarantee `PUT /to-do` metadata preservation the way it does for focus modes?**
Focus-mode `metadata` is replace-wholesale but `categories` is preserve-on-omit (§4.1).
`Habits/DayPlan/DayPlanSync.cs` already works around a similar clobber on `PUT /to-do` (it refuses
to write without a captured `EisenhowerQuadrant`). Before Phase 4 adds a second `PUT /to-do` writer,
**get the actual field-by-field preservation rules from the backend in writing.**

---

8. **Migrate off .NET Framework 4.7.2 first, or port on it?** See §6.10. **Downgraded** now that
   Phase 0b has landed: `FocusBear.Core` already targets `netstandard2.0` and its tests run on
   `net8.0`, so the local loop no longer depends on migrating. Still worth deciding for dependency
   drift, and the blocker audit says the move is cheaper than it looks — but it no longer blocks.

## 8. Known unknowns (flagged rather than invented)

1. **UNVERIFIED** — whether the shared `focus-block.html` page handles both the Mac query-parameter
   shape (`?old_url=…`) and the Windows one (`?old_url=…&block_type=…&block_mode=…&version=…`).
2. **UNVERIFIED** — whether `AppSettings.DownloadConfigFile()` requests `config-de/fr/pl.json` at
   all, or only EN/ES. The assets repo serves five languages.
3. ~~Which Windows call site hooks the AI veto~~ — **resolved**: five consumers gate on
   `ActiveSchedule.bAIBlockingEnabled` today (`Focus/GentleBlock.xaml.cs`,
   `Focus/FocusAlignmentScoreManager.cs`, `Utils/FocusChangedHandler.cs`,
   `Utils/ProcessBrowserURLs.cs`, `TrayMenu.xaml.cs`). All five move in Phase 1 step 2. What remains
   open is the **migration decision** in Phase 1 step 3 (seed block lists from existing schedule
   values, or default to on?).
4. **UNVERIFIED** — whether a background/incognito tab sweep is achievable on Windows via UI
   Automation without a browser extension. This gates managed-blocking enforcement parity.
5. **UNVERIFIED** — the exact endpoint behind Mac's `evaluateBlockRelevanceConversation` and
   `suggestTaskTitleForBlockedItem` (`AccountabilityBuddy/aiAccountabilityBuddy.swift`); the traced
   ones are `POST /user/evaluate-focus-postponement` and `POST /user/task-intention-check`.
6. **UNVERIFIED** — whether `/webview/course` (Mac `timerViewcontroller.swift:792`) has any Windows
   counterpart; no equivalent was found.
7. **UNVERIFIED** — whether a WPF charting dependency is acceptable for Phase 2, or whether charts
   must be hand-drawn. No charting package exists in `packages.config` today.
8. **Not inspected** — the `web_dashboard` repo. Its `src/enums/bridge.ts` is named in Mac's
   `SharedDashboardWebViewManager.swift` as the authority for webview screen tokens; anyone changing
   the webview bridge should read it.

---

## Appendix A — key file map

**Mac (`integration/mac-swiftui-rebuild`, repo root `/Users/s3255727/dev/Mac-App`)**

| Purpose | Path |
|---|---|
| Architecture | `ARCHITECTURE.md`, `TECH_DEBT.md`, `CLAUDE.md` |
| Managed blocking contract | `docs/managed-blocking-native.md`, `plans/classroom-managed-blocking-schedules.md` |
| Forecast contract | `docs/local-time-forecast.md` |
| Backend AI migration plan | `docs/backend-ai-endpoints-plan.md` |
| Shared fixtures | `FocusBearTests/Fixtures/managed-{policy-evaluation,url-normalization,wire-examples}.v1.json` |
| i18n gates | `scripts/check-config-assets-sync.py`, `scripts/check-hardcoded-strings.py`, `scripts/check-managed-blocking-translations.py`, `.github/workflows/{config-assets-sync,hardcoded-strings,unit-tests}.yml` |
| Config (local copy) | `FocusBear/config.json` |

**Windows (`origin/main`, repo root `/Users/s3255727/dev/windows-app-v2`)**

| Purpose | Path |
|---|---|
| Solution / project | `Focus Bear.sln`, `Focus Bear.csproj` (legacy, .NET Framework 4.7.2) |
| CI | `.github/workflows/build.yml` |
| App state singleton | `Utils/AppSettings.cs` |
| API client | `API/FocusBearAPIClient.cs`, `API/Authentication.cs` |
| Settings / credentials | `Models/RegistryManager.cs` |
| Blocking engine | `Utils/FocusChangedHandler.cs`, `Utils/ProcessBrowserURLs.cs`, `Models/AppsEngine.cs` |
| Blocking schedules | `Models/CustomBlockingSchedule.cs`, `Services/BlockingScheduleManager.cs`, `Preferences/BlockingSchedules/` |
| Day Plan | `Habits/DayPlan/` (15 files), `Habits/HabitsDayPlan.xaml(.cs)`, `Habits/HabitsDayPlan.Tasks.cs` |
| Webview token injection | `Services/WebViewTokenSyncService.cs` |
| Teacher mode | `Focus/TeacherSessionManager.cs`, `Focus/TeacherPactInviteWindow.xaml`, `Focus/TeacherUnlockRequestWindow.xaml` |
| Startup | `Models/AppTaskScheduler.cs`, `TaskSchedulerPackage/` |
| Realtime | `Utils/PusherHandler.cs` |
| Config fallbacks | `config.json`, `config-es.json` |

**Assets (`/Users/s3255727/dev/assets`, branch `main`)** — `config/config{,-es,-de,-fr,-pl}.json`.

---

## Appendix B — related pull requests

| Repo | PR | State | Relevance |
|---|---|---|---|
| Mac-App | [#2569] | open | The **only** Mac-App PR; head is `integration/mac-swiftui-rebuild` |
| Mac-App | [#2578] | open, draft, **DO NOT MERGE** | Managed-safety enforcement PoC |
| windows-app-v2 | #1522 | open, draft | Managed school/home blocking on Windows (+5,854) |
| windows-app-v2 | #1507 | open, draft, unbuilt | Notes webview → native WPF |
| windows-app-v2 | #1328 | open | Auth0 config + webview token injection alignment |
| windows-app-v2 | #1216, #1138 | open | Block-list UI changes — conflict risk with Phase 1 |
| windows-app-v2 | #1045 | open | To-do list DPI scaling — conflict risk with Phase 4 |
| backend | #2370 | — | Managed-blocking backend (Phase 8 dependency) |
| backend | #2387 | — | `exclude_completed` for the forecast (Phase 6 dependency) |
| backend | #2400 | — | Focus-mode `categories` column (Phase 1 dependency) |
| assets | #453 | — | Shared managed-blocking strings |

[#2569]: https://github.com/Focus-Bear/Mac-App/pull/2569
[#2578]: https://github.com/Focus-Bear/Mac-App/pull/2578
