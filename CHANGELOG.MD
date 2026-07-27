# v3.0.213

- Implemented AI-powered URL blocking on Windows, matching the functionality available on macOS.

# v3.0.214

- Enhanced blocking popups flow for non AI usage 
- Fixed Postponing evening routine ends active focus session and still prompts routine #984 

#v3.0.215

- Subscription limit popup appears during break despite having only one break activity #1006 
- App window no longer auto-closes after routine is skipped or postponed #1001 
- Fixed an issue where multiple popups could be shown after postponing and then resuming habits.

#v3.0.216
- Enhanced blocking popups flow for non AI usage 
- Manged subscription limit popup should not appear during breaks with ≤3 activities (Closes #1006)
- Added user setting to auto-close window after routine interactions#1116
- Focus Button Device integration - Works for focus modes - start / pause / resume / end, for habits works for triggers from device for start / pause  / resume but start does not work from PC (firmware changes awaited) out of order activities flow managed.

#v3.0.217
- Updated Focus Button device integration for habits and voice commands / Added NAudio dlls

#v3.0.218
- Fixed Duplicate focus modes appear. #945 
- “Looks like you’re on a break” popup appears even when breaks are disabled #935 
- [Windows] Password reset emails not sent from desktop app #1211 
- Recommendation: Don't show pop up when switching to Geek/Simple Mode #623

#v3.0.219
- A new tab for Focus Button is added in settings and it facilitates connecting the device, checking for firmware updates and flashing the firmware on the device. 

#v3.0.220
- Updated build for AI blocking realted enahncements - AI blocking "Set Task" button creates duplicate tasks due to missing success feedback #1291 
- AI blocking related issues #1299
- Dismiss "changed focus?" popup after Set Task #1294 

#v3.0.221
- Render habit emoji in color (Emoji.Wpf)#1293
- Make the full-screen activity window non-resizable#1301
- Fix Focus Button not triggering on startup + focus session window sizing#1296 (FIXES: 1. Focus Button not triggering on startup (the main fix) / 3. Focus session window too big on small / heavily-scaled displays) 
- Separate Postpone password rules from Skip (settings + UI + sync)#1297
- Move postpone password check from EasySkipPostpone to btnPleaseSpareM…#1303 / also added for skipping from late habits confirmation popup
- Updated setup with required signed dlls

#v3.0.222
- Evening routine popup repeatedly retriggers and ignores postpone duration #1232
- Don't cancel an active postpone when the screensaver wakes#1305
- Fixed emoji text color in focus mode end screen
- Replace hardcoded text with config file string for translation #688
- Focus session suggestion defaults to first list item instead of previously used session #1154
- Minor fix for Intention not visible on focus mode end screen and Last focus mode not loading as was loaded too early (related to #1154)

#v3.0.223
- Quick fix for string not loading for Beverly and focus summary screen not showing
- Added missing keys for local configs, fixed Spanish local config not being used as fallback#1284 (Relevant part merged manually -  fixed the local Spanish config file not being used as a fallback despite the language being set to Spanish)
- Blocking schedule cannot be saved when AM/PM is missing and app provides no validation feedback #1249) Enhancement - Moved date check before checking overlapping schedules.
- Fixes Habits can be completely erased #1247 
- Infinite load when resuming habits #1248
- App does not automatically reopen after saving habit changes requiring restart #1212

#v3.0.224
- Fix process-handle leak in GetLastActiveAppName causing app freeze (#1178)#1329 (Possible fix: App crashed #1178)
- Fixed: Win32Exception: Not enough memory — ProcessOfflineData crashes — 27 users #1072

#v3.0.225
- Added "don't require password again for 2 minutes" feature on Windows
- Don't show countdown timer for first 30 seconds #1246
- Add more details to block-distracting-url posthog event #517
- Overnight blocking schedule (spanning midnight) does not activate #1335
- Onboarding: load the revamped routine-suggestion page
- Onboarding: fix evening routine duration (was taken from morning habits); each routine now sums its own habits' durations

#v3.0.226
- Teacher Mode: teacher-controlled class focus sessions #1321. A teacher starts a super-strict allow-list class focus session from the web dashboard; the student gets a join prompt (never auto-starts), the teacher's allow-list is enforced and hot-syncs live, presence is reported via heartbeats, and the session cannot be stopped/paused/quit early — only unlock requests.
- Blocked-URL unlock requests: "Oops I actually need this" opens a pre-filled unlock request instead of unblocking; on teacher approval the site reloads automatically (while on the block page or on returning to that tab).
- Personal routine held back during a class session: a running morning/evening routine is postponed and resumes from where it was left once the session ends (in-progress habit keeps its remaining time); Resume Habits and Start Break are blocked with a notice while a session is active.
- AI URL checks follow the class session's AI setting while it's active.
- Fixed tray balloon-tip text being cut off for long messages (now sizes to the message).

#v3.0.227
- New Day Plan (Windows port of the macOS Day Plan): the "Habits" tab is now "Day Plan", showing an agenda timetable of the day — routine habits grouped by routine, deep-work / break blocks, and the evening-routine transition. Selecting a habit reveals a Play / Done / Skip bar (with new crisp icons).
- Past-day navigation (‹ date › with "Back to today"); past or unlogged habits can be completed retroactively via "I am done". Completed morning habits appear in an "earlier today" accordion (struck-through) once the routine finishes.
- The Day Plan now stays visible between routines and after a routine finishes, instead of a standalone "well done" screen. When a running habit is paused, a back button takes you to the Day Plan; you can resume that habit later from where you left off (its remaining time is preserved) or start a different one instead.
- Tasks panel (right side, resizable): plan tasks for the day and drag them onto a habit or a deep-work sprint to time-box them. Add tasks from your to-do list or from scratch, group by project (with a per-project add button), show/hide completed tasks, and reorder by dragging.
- Task detail popup: edit title, project, duration, importance, notes and sub-tasks; move a task to another day using natural language (today / tomorrow / 2d / next tue / 25 Jul); or remove it.
- Two-way sync with your to-do list: completing a Day Plan task marks its to-do done (and vice-versa); tasks planned or completed on web/mobile reflect in the Day Plan. Projects created on web/mobile appear as suggestions.
- All Day Plan text is config-driven (scheduleViewInfo, with English fallbacks); refreshed the bundled config from the assets repo.
- Fixed: doing habits out of order in the Day Plan now completes the routine at the right time — skipping the second-to-last habit no longer marks the routine complete prematurely, and playing the last remaining habit correctly shows the Unlock / routine-complete screen (previously the app could stay stuck in the routine-active state).
- Fixed: skipped habits now show struck-through and tagged "· skipped" (instead of appearing still-doable) after the routine finishes or the app restarts.
- On the activity-finished screen, "Choose another habit" now opens the Day Plan (the picker) instead of a list that could surface an already-completed habit.

#v3.0.228
- Calendar meetings in the Day Plan: your Google and Microsoft/Outlook events now appear as meeting blocks on the agenda, synced through the backend (no separate sign-in needed — connect the calendar once in the dashboard). New and changed events show up on their own while the Day Plan is open, and refresh immediately when you change the day or reopen it.
- Drag a task onto a meeting to time-box it, the same way you can with habits and deep-work sprints.
- Hide a meeting you don't want on the plan with the ✕ on its card (restore it from "Show hidden meetings" in the tasks settings); hides are remembered per day and sync with the backend.
- Give a meeting its own colour: click the colour dot on a meeting card and pick from the palette (or reset to default). Colours are saved per event and reload on refresh.
- Fixed: the "Set Intention" popup for a blocked site no longer flickers away instantly when a background app (e.g. Visual Studio, Teams) is running — it now stays until you actually switch to another app.

#v3.0.229
- New Keyboard Shortcuts settings tab: set global shortcuts that work from anywhere (even when Focus Bear isn't focused) to open Brain Dump, Focus Music or your Task List, and to start a focus session. Click a box and press a key combination (Ctrl / Alt / Shift / Win + a key); clear one with the ✕.
- Teacher Mode: a teacher can now start a class focus session straight from the Focus button instead of the web dashboard. Pressing Focus starts a super-strict class session for the chosen class (with a class picker when you administer more than one), reusing your selected focus mode's allowed apps/URLs and the button's duration. A "Start a personal session instead" option is there when you just want a personal session, and non-teachers are unaffected.
- Teacher Mode: if a class session can't be started (e.g. outside the school's allowed hours, or one is already running for the class), Focus Bear now tells you with a clear message instead of silently starting a personal session.
- New Tab Audit tab (Tools): see how many browser tabs you have open per browser and export them all to a CSV so you can decide what to keep and safely close the rest. Extracting also offers to set a max tab limit so you don't get overwhelmed again. (Tab titles are captured for every tab; the currently-active tab also includes its URL.)
- Fixed the app freezing for several seconds on startup when a problematic (e.g. ghost/Bluetooth) COM port was present — Focus Button COM-port detection now runs in the background so the app loads straight to the main menu #1231. The Focus Button settings tab now shows "Connecting…" and updates to "Connected" once the device is ready.
- Fixed block-list strings ("Select a block list", "Edit block lists", "Add block list") not translating to Spanish on the Pomodoro and Focus preferences screens — these now come from the config file instead of hardcoded English #1223.
- Set a default work-to-break ratio (e.g. 5:1) in the Pomodoro settings tab, and the break duration on the Pomodoro start screen is now derived from it automatically (you can still override it, and an "Edit" link jumps to the setting). Applies to both the normal and Simple Pomodoro start screens; the ratio persists across restarts #1182.

#v3.0.230
- Tab Audit now captures the URL of *every* open tab, not just the last-viewed one, across Firefox, Edge, Chrome, Brave and Comet. Clicking "Extract" shows a full-screen "please wait" overlay with progress while Focus Bear steps through your browser tabs and reads each address, then restores your original windows and tabs (Vivaldi stays title-only for now). Tab counting and extraction now also cover minimized windows and every window of a browser — not just one #1360.
- Tab Audit polish: a small spinner + "Getting tab count…" shows while the count is read (with the Extract button disabled until it's ready), and all Tab Audit text is now config-driven and translated (English + Spanish) #1360.
- Day Plan: the Task Details popup's text boxes are now theme-aware — dark inputs in dark mode instead of glaring white (and the native white look in light mode). A task's sub-task "done/total" count is now readable on coloured task rows too (it follows the row's contrast colour) instead of an invisible green #1361.
- Embedded dashboard screens (To-Do list, Stats, Edit Habits and the other in-app web views) now keep their sign-in token fresh: Focus Bear proactively refreshes and pushes a new token before it expires — including after the machine wakes from a long sleep — and also refreshes on demand when a screen reports its token has gone stale, so those screens no longer get stuck on an expired session #1318.
- Focus Button: adopted the newer device firmware protocol (v1.6.0–v1.8.1). Syncing your habits to the device is now checksum-verified and re-sent if the serial link drops bytes, the device reliably receives the full habit list, and your focus session's intention/goal now shows on the device and updates if you change it mid-session. Completion-based (no-timer) habits count up on the device instead of showing a bogus countdown, and device crash/reset reasons are now logged for diagnostics. Older firmware keeps its existing behaviour unchanged #1342.
