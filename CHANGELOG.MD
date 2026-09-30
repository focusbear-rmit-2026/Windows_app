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

#v3.0.231
- Postponed routines now survive an app restart: if you postpone your morning/evening routine (or the focus summary) and the app is closed and reopened while the postpone is still active, Focus Bear stays postponed for the remaining time instead of immediately re-prompting your habits. The remaining time is honoured exactly (postpone for 2h, restart after 1h45m → 15m left), capped so it never carries past the routine's own window (morning → that evening's shut-off; evening/focus-summary → the next morning). Already-completed habits show as done, and if you'd finished the routine on another device in the meantime the postpone is dropped #1368.
- Day Plan: fixed playing a habit from the postponed/paused state, and finishing a habit that was paused, so the routine continues correctly instead of getting stuck or ending early #1364 #1365.
- Tab Audit: minimized browser windows and every window of a browser are now counted and extracted correctly, and Edge's in-page tab strips are no longer mistaken for browser tabs; sleeping-tab titles are cleaned up. Tab Audit logic was also moved into its own module for maintainability #1367.

#v3.0.232
- Redesigned the tray menu to match the macOS app — a cleaner card/accordion layout with an orange "Start Focus Session" button. Starting a focus session now opens an inline picker right in the menu: type a goal, choose a focus mode, and set the duration with − / + before tapping to start, plus a wrench button to open the full focus screen #1375.
- Day Plan in the tray: the Day Plan card expands in place to show your next habit with a Start button, today's tasks (tap one to tick it off), and a "View full day plan" link — no need to open the main window. While a routine is postponed it shows "Habits postponed until <time>", and Start resumes the routine (finishing any running focus session first the same way the postpone timer expiring would) #1375.
- Breaks in the tray: a Breaks card shows when your next break is due — or why breaks are currently off — and lets you start one on the spot #1375.
- In-app voice command: a mic in the tray header lets you speak a command (e.g. "start a 25 minute focus session"); the popup auto-dismisses once the command is understood #1375.
- The tray menu now themes correctly in dark mode and repaints immediately when you switch light/dark, and all tray text is config-driven (translatable, English + Spanish). The Office / WFH mode switch moved out of the tray into Settings → Breaks (with a description; tooltips unchanged), and the tray tasks list is capped to keep the menu compact #1375.
- Tab limiter: added an enable/disable toggle in the Strictness settings, synced with the macOS app; existing users with a saved tab limit are migrated on automatically #1376.
- Tab Audit: the tab scan is skipped when the limiter isn't in use, and a Firefox "New Tab" (+) button is no longer counted as an open tab #1167 #1168.

#v3.0.233
- Cancel / uncancel a whole routine from the Day Plan (Windows port of the macOS feature). A red ✕ on the active morning/evening routine card asks "Cancel Routine?" and, on confirm, cancels the whole routine for today. The card then shows "You cancelled your … routine" with an "Uncancel my … routine" link — clicking it restores the routine so its habits are fetched and startable again. The evening routine can be uncancelled until your sleep/cutoff time, after which the shut-off screen takes over #1379.
- Day Plan: the shut-off / free-time screen now appears only after your cutoff (sleep) time, not from the evening-routine start — so a finished or cancelled evening routine keeps the Day Plan visible until cutoff. Today's plan also only lists habits scheduled for the current weekday (the "earlier today" list and evening preview no longer show habits set for other days) #1380.

#v3.0.234
- Day Plan: when it's time to wind down, the Day Plan now shows an evening shut-down prompt so you can start your evening routine straight from the plan; once you've finished the evening routine, the Day Plan keeps showing it as done (with the routine's completed habits) instead of hiding it (Windows port of the macOS shut-down flow).
- Tray Breaks card: you can now start a break from the tray even while your schedule is paused, and if a break has no activities set up the card says so clearly instead of appearing empty; spacing/layout on the card was also tidied up.
- Cutoff (sleep time) behaviour: the shut-off window now shows your actual cutoff time, the "Start Focus Session" option is hidden once you're past cutoff, and the Day Plan still opens correctly if you abort the shut-off.
- Fixed: skipping a habit that used allowed-apps / focus-mode now clears its "running" state, so the app no longer behaves as if that habit is still active after you skip it.

#v3.0.235
- AI blocking now works in Grizzly mode and for apps, not just websites in Cuddly mode. When AI blocking is on, Focus Bear runs the AI relevance check on both websites and apps in every mode: in Cuddly mode it shows the gentle popup with the AI's relevance score and reason and lets you set a goal/task, convince the AI, or pick a suggested task; in Grizzly mode an AI-distracting site or app is blocked immediately with no prompt (an AI-approved one is allowed silently). Teacher class sessions always enforce Grizzly behaviour regardless of the student's own mode #1388 #1391.
- The usage-timings CSV export now writes dates in ISO format (yyyy-MM-dd) so they're unambiguous regardless of your Windows locale (previously locales like en-AU/en-GB produced dd-MM-yyyy) #1390.
- Fixed several texts that were stuck in English and didn't translate to Spanish: the "Block distractions / Set schedule" progress labels, the system-tray tour description, the Pomodoro "Which block list", "Edit block lists", "Selected Tasks" and "Show focus status when session ends" texts, and "More Options" on Simple Pomodoro #1177.
- Fixed YouTube pause/resume in micro-break activities: videos loaded directly in the view (not only inside an iframe) now pause and resume in sync with the activity, plus added safety checks so it won't crash if the web view isn't ready yet #1136.

#v3.0.236
- Side menu: hovering a button while the menu is collapsed now shows a tooltip with its name, so you can tell the icons apart without expanding it (works for every menu item, including Day Plan and Tab Audit) #1228.
- Removed the "Deactivate Focus Bear for 4 hours" option — it's redundant now that you can postpone/resume your routine from the Day Plan and pause blocking schedules, and it previously marked your whole routine as done on deactivating #1397.
- Fixed the main window un-maximising itself: when Focus Bear brings its window to the front (e.g. after a break), a window you'd maximised now stays maximised instead of shrinking back to a normal size #1115.
- The To-Do player now reflects your focus-alignment score live during a focus session, stays in sync when you pause/resume, and follows your light/dark theme #1165.
- Teacher Mode: during a class focus session the To-Do player now shows the class checklist (loaded for the session) instead of your personal tasks; your personal tasks are set aside and restored automatically when the session ends #1393.
- Added analytics to better understand sign-in / onboarding drop-off: a logout event with a reason (deliberate vs. involuntary session loss), an onboarding "hold on" screen completion event, and guest-vs-registered state on the sign-in/sign-up screens #1396.

#v3.0.237
- Teacher Mode: while a class focus session is active, the app can no longer be quit via the normal quit reasons — only "I've got an emergency" is allowed — so a student can't trivially bypass the enforced block #1405.
- AuDHD test results: added a `focusbear://` deep link so the web results page can open the gated advanced results directly in a full-screen in-app window (registered on install; works whether the app is already running or launched fresh) #1402.
- Day Plan: finished meetings now drop off today's agenda once they've ended (an in-progress meeting still shows), and the agenda no longer gets clipped on smaller screens or when the side menu is expanded — it shrinks to fit instead of a fixed width #1408 #1407.
- Day Plan: the full-screen clock overlay no longer overlaps the Day Plan's Tasks panel during a routine (the Day Plan already shows its own date/time) #1408.
- Dev tooling: fast/compressed countdown timers are now an opt-in runtime flag (`.env` / env var) instead of being tied to Debug builds, so a Debug build counts down at real-user speed by default #1404.

#v3.0.238
- Teacher Mode: when a student opens a blocked app during a class focus session, the block notification now has a "Request unlock" button (pre-filled with the app name) to ask the teacher to unlock it, and auto-dismisses after a few seconds — instead of the app just being blocked silently #1411.
- Scheduled blocking now keeps running after the evening routine: a blocking schedule (e.g. 4 PM–12 AM) no longer stops when you finish your evening routine and reach the wind-down screen — it stays active until its window ends #932.
- Pausing a focus session now pauses blocking too: while paused, apps, websites and AI checks are no longer blocked (resuming restores it); your always-blocked lists still apply #1274.
- "Free time – no blocking" schedules now block nothing: a blocking schedule set to the Relax focus mode no longer blocks apps like Chrome — nothing is blocked while it's active #1262.
- Fixed the focus-session end screen freezing: the Continue / Break / Extend / Start-another buttons can no longer leave the screen stuck or start two timers at once (e.g. Extend followed by Break); each action now runs once and reliably returns you to the app. The "set a goal/task" block popup also no longer lingers or reappears over the end screen after a session ends #1372 #1373.
- AI blocking popups: fixed duplicate/stacked "set a goal/task" popups when quickly switching tabs, corrected wording that said "site" instead of "app" for AI-checked apps, and skip the AI check when there's no app name #1413.

#v3.0.239
- Fixed the app getting stuck on a loading / "Resume habits" screen with no unlock option after finishing the evening routine before your cutoff time: completing (or skipping the last habit of) the evening routine now returns you to the Day Plan (before cutoff) or the shut-off/unlock screen (after cutoff) instead of the postponed "Resume habits" view. The "Well done finishing your evening routine" prompt now only appears when the routine was actually done, not just because the evening window has passed #1421.
- Fixed the evening Focus Summary rendering blank (and the app hanging afterward) when a focus block had a missing/odd start time — one bad field can no longer blank the whole summary. Related: past your cutoff time the Day Plan no longer lists evening habits that can't be done anymore, showing "No habits left — enjoy your free time" when nothing high-priority remains #1315.
- Login: "Forgot Password" now shows the actual reason when a reset can't be sent (e.g. the account was created with Google) instead of a generic error; and pressing the shortcut/relaunching now brings the login window back to the front when it was minimized #1238.
- Sign-up: the password "special characters" rule now recognises all symbols (e.g. `*`, `-`, `"`, `'`, `:`, `;`, `?`), not just a small set — so valid passwords are no longer rejected #1325.
- AuDHD test results: the in-app advanced-results page now loads correctly (correct host + required app marker so it shows the full profile instead of "Open in the app"); on a cold start it opens after the app has finished loading, and it comes to the front instead of hiding behind other Focus Bear windows #1418.

#v3.0.240
- Fixed the morning routine getting stuck and impossible to start when your calendar is fully booked: if there was no free time around your meetings, Focus Bear showed no routine card at all, leaving the routine "active" (other tools locked) with only a Postpone button that did nothing — the only way out was to skip. The routine card now always appears; on a fully-booked day it starts collapsed with a "Not enough free time today" heading, and expanding it shows your habits with Play / Done / Skip so you can start (or postpone) the routine anyway #1371.
- Fixed the app failing to launch under non-US regional settings: on some Windows locales the app could crash on startup — it now uses a fixed culture internally so it starts reliably regardless of your regional (date/number) settings #1374.
- Fixed "Error when attempting to share logs" when there was no screenshot and no message: sharing logs with a blank description no longer fails (the description now defaults to "No Feedback") #1399.
- Teacher Mode: during a class focus session a student's own "always allowed" apps are now ignored, so they can't be used to bypass the class block; and if a student declines the "join class session" prompt, the tray menu now shows that a class session is running with a button to join it #1427.
- Fixed the gentle-block blur not covering the whole window for apps: the dimmed blur overlay now covers the full app window instead of leaving part of it un-blurred #1426.

#v3.0.241
- Added a Delete Account button to Settings → Account (GDPR): a two-step confirmation (an "are you sure" prompt, then typing "DELETE") permanently deletes your account and all its data, then signs you out. Each step can be cancelled #993.
- Habit checklists: a habit can now show its checklist steps during the activity as tappable items (a local visual aid — ticking them isn't required to finish). If the habit also has a video, a Video / Checklist tab switcher lets you flip between the two #343.
- Fixed side-menu clicks: the whole menu button is now clickable, not just the text, so tapping a menu item reliably switches the view. The side menu also stays a fixed width now and can't be dragged wider #980.
- Fixed a break being prompted immediately after returning from a natural break: confirming a natural break now resets the break timer correctly, so the next break only comes after your full break interval #731.
- Follow-up to the fully-booked-calendar fix: when there's no free time around your meetings, the morning routine's habits now stay together in a single "Not enough free time today" card instead of a meeting splitting the routine into two cards #1371.
- Fixed a crash in the gentle-block popup when the current tab's URL was malformed (e.g. contained spaces or unicode) — the block now handles those URLs without crashing #1095.

#v3.0.242
- Manually adding an app to your blocked/allowed lists now has a "Select File" option to browse for the app's `.exe` (you can pick several at once, shown as removable tags), alongside typing exe names #1140.
- Micro-breaks being off during a focus session is now shown clearly — on the breaks page and in the tray's break section — with a link to resume automatic breaks, so it's obvious why breaks aren't appearing #1215.
- Fixed the Day Plan at the morning→evening handoff: it no longer shows a misleading "Not enough free time today" morning card when you have no meetings, and the evening routine's habits no longer get split across separate cards #1445.
- Blocking schedules and block lists now update without restarting the app: a schedule or block list created/deleted on another device shows up when you reopen Settings → Blocking, and an open schedule editor closes itself if that schedule was deleted elsewhere #1114.
- The break popup now has "Extend break (+5 min)" and "End break early" links. Also, starting a new focus session while a break is running now closes the break popup, and the popup no longer appears a few seconds late #1065.
- Fixed a crash (NullReferenceException) that could occur when a blocking schedule started or stopped while you were switching between apps #1048.
- Past the evening cutoff time, your high-priority habits now start automatically — the reminder auto-closes after a few seconds — instead of waiting for you to click OK #866.
- The password prompt has a new "Type random characters" tab: instead of your password, you can unlock by retyping a generated string of characters #998.
- Internal: added AutomationIds across more controls (tray, side menu, keyboard shortcuts, tab audit, activity checklist) to support end-to-end UI test automation #1439.

#v3.0.243
- Habits with an "allowed app" and a timer no longer show the break controls ("Extend break (+5 min)" / "End break early") — those links only make sense during an actual break, so they're now hidden for allowed-app habits #1453.
- Guest accounts can now be turned into real accounts from Settings → Account: anonymous users see a "Create account" option (email + password) that saves their progress and settings, and a "Leaderboard username" row was added. The "email not verified" banner and its "Resend confirmation" link now only show for real (non-anonymous) accounts and line up with the email address #643.
- Fixed a stale focus intention/task carrying over: when a focus session ends, the running intention and current to-do task are now cleared, so they don't wrongly reappear on the next session or end screen #1264.
- The gentle-block "Report Bad AI Decision" (👎) now also allows the current site/app: since the report means the AI blocked it by mistake, you're no longer immediately re-blocked on the thing you just reported #1290.
- The gentle-block "working on something else" / "just this time" buttons now handle long task names cleanly — the text is trimmed with an ellipsis and the full text shows in a tooltip on hover, instead of being cut off.

#v3.0.244
- The Blocking Schedule editor (Settings → Blocking) has been redesigned to match the macOS app: a draggable time-range slider instead of separate time spinners, Flexible/Moderate/Strict strictness buttons instead of a slider, Weekdays/Weekends/Everyday quick-select day presets, and a collapsible "More Options" section #1462.
- The Blocking Schedule list now shows each schedule as a card with a colored accent, an emoji, and pills for its days/block list/strictness — and you can now pick an emoji for each schedule from the editor, synced with the mac app #1463.
- The onboarding "Set Block Details" screen has been redesigned to match the same new look, and now supports picking a schedule emoji too #1464.
- Fixed the Active Time slider's tick labels always reading up to "6PM" regardless of your selection - the slider itself covered the full 24 hours all along, just the labels were misplaced #1464.
- Fixed blocking-schedule emoji sometimes showing in black-and-white instead of color #1464.
- Fixed the "More Options" section on the blocking-schedule editor not expanding when you clicked directly on its arrow icon #1464.
- Reordered onboarding so distraction blocking is set up before habits (previously the reverse), and Blocked URLs/Apps are set up before Allowed URLs #1465.
- The Create Account screen now explains that Focus Bear uses Auth0 for sign-in #1465.

#v3.0.245
- Fixed the morning/evening routine getting stuck offering an already-completed or already-skipped habit as the "next" one, including when it was finished out of order via the Day Plan #1460.
- Fixed the log-quantity confirmation popup getting stuck open (and the habit never marked complete) when a habit was finished through a shortcut that skips its timer screen #1440.
- Fixed the Day Plan showing, and letting you interact with, more than your trial's 3-habit limit outside of an active routine; also added a card at the top of the Day Plan explaining the limit for trial accounts, with a link to upgrade #1320.
- Fixed a stale "Habit resumes in X minutes" popup reappearing after a routine was already fully finished, when it was completed by marking habits Done/Skip on the Day Plan rather than clicking Play #1101.
- Fixed blocked apps sometimes staying blocked outside your blocking schedule's active hours #1314.

#v3.0.246
- Brain Dump is now backed by the same Notes system as the rest of Focus Bear: your existing entries are moved over automatically the first time you open the app after updating, so they're no longer only stored on this one device #1132.
- Fixed the "Quit Focus Bear?" confirmation popup occasionally freezing and not responding to any button if you clicked "Nope, just quit" right as it appeared #1190.
- Habits with an image and/or note added to their Instructions now show them during the habit, alongside the existing Video/Checklist tabs when more than one is set up.

#v3.0.247
- Fresh installs from the public AuDHD web test now correctly open your results on first launch, even when the deep link that normally carries the retest code is lost during install - the code is picked up from the clipboard instead #1476.
- Added a warning when starting a focus session, or saving/auto-starting a blocking schedule, that uses an Allow List with AI turned off - without AI, every new page on an already-allowed site would otherwise need separate approval #1420.
- Fixed the leaderboard username not loading in Settings → Account - it was being read from the wrong settings key #1485.
- Internal: added PostHog identity-stitching and shown/closed analytics events for the AuDHD results screen #1468 #1461.

#v3.0.248
- Reworked the Allow-List/AI-off warning with clearer copy and a "Make it stricter" option that turns on Grizzly mode, and it's now skipped entirely when Grizzly mode is already on #1420.
- The "postpone" time picker now lets you type your own number of minutes directly instead of only picking from a preset list #1036.
- During a teacher class session, a distracting site is now blocked immediately regardless of your own "immediately block" setting, and requesting an unlock now requires a reason before you can send it.
- Several onboarding screens improved: a "Select / Deselect all" option for the suggested-websites and blocked-websites lists, and default suggested apps to block (News, Movies & TV, Solitaire).
- Fixed several crashes: starting a focus session could fail if the alignment-threshold dropdown had nothing selected, playing focus music could occasionally crash during a rare timing conflict, and changing your Windows theme could crash the login/splash screen.
- Internal: CI/automated test builds no longer send analytics events, keeping test runs out of the real usage data.

#v3.0.249
- Fixed the gentle-block popup blocking you when the AI URL check fails to respond — it now fails open (allows the page) instead of showing a "could not be verified" message, matching how a failed check is already handled outside cuddly mode #1501.
- Redesigned the Teacher Mode class-session invite as a two-screen card matching the Mac app: allowed-site tags, a required decline reason, and a hold-for-15-seconds decline button instead of a plain Yes/No prompt #1502.
- The class-session heartbeat now reports idle time and your live focus-alignment score, so a teacher's report can show Engagement and Site-relevance #1504.
- Fixed the gentle-block analytics event recording every AI check as a "shown" popup, even when the AI silently auto-allowed the page and nothing was ever shown to you - it's now only recorded when the popup genuinely appears.
- Fixed a crash in the background settings sync that could silently stop several settings (including a saved password) from applying whenever the synced data included a blank password field - this was the cause of "pause strict blocking schedule" incorrectly asking you to set a password when one was already set #1483.

#v3.0.250
- Fresh installs from the public AuDHD web test now open your results before the welcome/onboarding screens (onboarding starts once you close them), and the results link goes through the web app's /resume route so a code for a test you haven't taken yet starts the test instead of showing an empty results page #1576 #1520.
- When your settings are changed on another device (e.g. saving a habit on mobile), the Windows app now shows a short "settings changed, restarting" alert before it restarts instead of closing with no warning, which looked like a crash #1509.
- Fixed the micro-break countdown on the Breaks view appearing frozen - it now ticks down every second while the view is open #1030 #1127.
- Fixed the tray menu tour: the first popup image is now in Spanish when the app language is Spanish, and the full 4-step tour (menu overview, Settings gear, Start Focus Session) is restored with screenshots and text updated for the current menu design #489.
- The default "Meetings" focus mode's Zoho entry now matches meeting.zoho.* so Zoho Meeting rooms on any regional domain (.com, .in, .eu, ...) are allowed, instead of only meet.zoho.com which never matched a live call #1511.
- Internal: AutomationIds are now unique per row in checklist/task/checkbox lists and moved off Border/Grid elements so they're reachable by UI Automation for automated testing #1452 #1488.

#v3.1.251
This release brings the Windows app in line with the Mac app. Most screens that used to be web pages inside the app are now built natively, so they load instantly, work offline where they can, and follow your light/dark theme #1588.

**Focus sessions**
- New Start Focus Session screen. Simple mode is a single duration dial with the bear; Advanced mode has collapsible sections for duration, block list, break settings and more. All sections can be collapsed, and the Start button is larger and easier to hit.
- New in-session screen replaces the web To-Do Player: your timer, task list and a Notes panel side by side. Notes typed here stay in step with the Notes screen, and -5 minutes never adds time back when under a minute is left.
- Teacher accounts: the "personal session" checkbox and class picker are gone. A session started from the app is always a personal one; class sessions are started from the dashboard in the browser. A short note on the screen explains this.
- Meetings focus mode: the Zoho entry now matches every regional Zoho Meeting domain.

**Block lists (Blocking)**
- Redesigned to match the Mac: a rail of your lists on the left, a table of entries with suggestions and colours on the right. Saving works exactly as before.
- AI block check-in: when a site is blocked during an AI session, a small check-in replaces the old gentle-block decision popup, with a frosted backdrop and an "Oops, I actually need it" option on app blocks.

**Day Plan**
- Morning-routine habits are slotted into the gaps between your meetings, as on the Mac.
- Shorten or edit a habit from the plan, edit routine timing, hide a meeting, and edit a task's duration inline (typing "1h30" gives 90 minutes).
- Click the date to open a month picker that shows how busy each coming day is.
- Meetings now load for the next 45 days, not just today, and cross-midnight meetings display correctly.
- Re-prioritise ("Reschedule plan") applies every row instead of stopping at the first; each row has an outcome slider and editable duration; tasks assigned to others are left out.

**To-Do and Projects**
- The To-Do list, projects, "What matters", project boards, the Eisenhower matrix, comments, reactions and attachments are all native screens now. Tasks assigned to someone else are hidden.
- Reactions no longer report "Could not add reaction" when the reaction was already there.
- Projects: due-date chip with a calendar picker, and an owner-editable outcome (1–5).

**Notes**
- Personal notes, shared project notes and the archive are native. Shared notes are edited live with your team: several people can type at once and nothing is overwritten.
- Shared-note editor: the scrollbar starts below the formatting toolbar and the stray blue outline is gone; shared notes in the list have a clearer arrow.
- Notes typed on the in-session screen and on the Notes screen stay in sync both ways.

**Stats (formerly Motivation)**
- All tabs are native: Streak, Progress, Goals, Pep Talk, Leaderboard, Habit Stats, Productivity and Focus Log, with charts and a heat map.
- Selected tabs and badges now use white text on orange. Pep Talk no longer loads forever if you switch tabs while it is generating, and the Focus Log's Refresh shows progress and reports a failure.

**Focus Music**
- New layout matching the Mac. YouTube Music plays in an embedded player; Spotify links open the Spotify app.
- Row hover and the "now playing" highlight fill the whole row.

**Late No More (new)**
- Meeting alerts and heads-up timing, calendar selection, working hours, keywords, join-link-only mode and spoken alerts, under TOOLS. Joining a meeting is detected automatically.

**Routines and postponing**
- AI accountability buddy for postponing a routine (off by default, turn it on under Settings → AI). Instead of a minutes picker, a short chat asks what you need the time for; an approved postpone starts a focus session scoped to that goal.
- Settings, Blocking, Edit Habits, Focus Music and Stats stay reachable during a routine. They ask for your password under the same "require password to change settings" rule, so strictness is unchanged.
- The break warning is press-and-hold, with a separate "turn breaks off" window.
- Fixed the Focus Summary's postpone using the skip password rule instead of the postpone one.
- Log Quantity dialogs are keyboard-first: Enter saves, and the slider value can be typed.

**Main window, tray and settings**
- TOOLS rows can be hidden with the hover ✕ and restored from the cog. Stats has a bar-chart icon.
- The top-bar minimise button now appears only in full-screen routines, where the window has no title bar.
- The sign-in and sign-up show-password eye is a keyboard-reachable button and sits beside the box again.
- Report a problem: one form with a note and screenshots.
- Dark theme text contrast improved throughout.

**Voice commands**
- Voice commands (tray microphone and the Focus Button) now go through the Focus Bear backend instead of calling OpenAI directly, and the app no longer contains an API key. Failures show a clear message: not signed in, offline, too long, rate limited, or "didn't catch that" #1600.
- The listening/thinking popup's buttons match the app's orange buttons.

**Reliability and performance**
- Idle CPU is much lower: a hidden splash animation kept the app rendering at 57 fps all session, and several leaked animations, duplicated timers and a too-broad window-event hook were fixed.
- Settings saved on one device no longer undo edits made on another; the app fetches the latest before uploading.
- The offline activity queue no longer discards everything after one failure or doubles durations.
- Brain-dump text is no longer lost when leaving the screen, and notes no longer gain a blank line on every save.
- Logs no longer contain your access token, email or settings; log files roll at 10 MB.
- A crash watchdog relaunches the app if it crashes or is killed; a normal quit is respected.
- Fixed several crashes: the simple-mode duration slider, and a null "metadata" field in synced data.

**Installer**
- The installer now ships FocusBear.Core.dll and the shared-note editor, which the new screens need #1609.
