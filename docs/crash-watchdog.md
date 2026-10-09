# Crash watchdog ("Focus Bear Watchdog" scheduled task)

Brings Focus Bear back after it is killed (Task Manager "End task", ended from Task Scheduler, or a crash) without
undoing a deliberate quit. Code: `Models/AppWatchdog.cs`, `Models/AppTaskScheduler.EnsureWatchdogTask`,
`Core/Startup/WatchdogBackoff.cs`, `Core/Startup/StartupTaskPolicy.cs`.

## How it works

- On every start, `App.SetAtWindowsStartup()` registers (or refreshes) a scheduled task named **`Focus Bear Watchdog`**
  in the Task Scheduler root folder. Every minute it runs `Focus Bear.exe /watchdog`.
- That launch is a probe. It exits from `App`'s static constructor, before settings, logs or windows are created,
  unless Focus Bear is **not** running **and** its last exit was not a clean one.
- Every graceful exit (quit, restart, update, sign-out, Windows shutting down) goes through `Application_Exit`, which
  writes `WATCHDOG_CLEAN_EXIT = "1"`. A kill never gets there, so the next probe starts the app for real.

All markers live under `HKCU\Software\Focus Bear` (the E2E and test-host builds use their own keys).

| Value | Meaning |
|---|---|
| `WATCHDOG_CLEAN_EXIT` | `"1"` after a graceful exit, `"0"` while the app runs |
| `WATCHDOG_FAILED_STARTS` | Watchdog relaunches in a row that have not reached the tray icon yet |
| `WATCHDOG_LAST_RELAUNCH` | When the watchdog last relaunched the app (UTC ticks) |
| `WATCHDOG_DISABLED` | `"1"` switches the watchdog off for this Windows account (see below) |

## Who gets it

| Build / user | Watchdog? |
|---|---|
| Release build, administrator (elevated) | Yes. The task runs elevated, like the `Focus Bear` logon task. |
| Release build, administrator running unelevated, or a standard user | Yes, as a non-elevated task for that user, provided Windows lets the user create scheduled tasks (the default). A non-elevated run never replaces an elevated task that is already there. |
| Standard user where policy blocks task creation for non-admins | **No.** `RegisterTaskDefinition` throws, the log shows `EnsureWatchdogTask ex - …` on every start, and the app runs without a watchdog. Known limitation. |
| Debug build | No. It never registers the task, and a `/watchdog` launch of a Debug build exits at once (stopping the debugger is a kill, and the relaunched exe locked `bin\Debug`). |
| E2E test build | No. It never touches Task Scheduler. |
| Second Windows account on the same PC | Only one of them. The task name is per machine: an admin's registration replaces the other account's task, and a standard user cannot replace an admin's. The task runs as the account that registered it. Known limitation; a per-user task name would need installer and migration changes that can't be tested without a Windows box. |

## A build that dies during start-up (back-off)

Each watchdog relaunch counts as a failed start until that run creates its tray icon, the point from which it can be
quit. Reaching the tray resets the count. After **3** failed starts in a row, the watchdog tries only once every
**15 minutes** (`WatchdogBackoff`) instead of every minute. An app killed *after* its tray icon appeared is relaunched
at the next probe, as before, so this does not make it easier to escape a focus session by killing the app.

## Switching it off (support)

For one Windows account, in a Command Prompt as that user:

```bat
reg add "HKCU\Software\Focus Bear" /v WATCHDOG_DISABLED /t REG_SZ /d 1 /f
```

From then on probes exit at once, and the next start of Focus Bear removes the task (if the account is allowed to;
an elevated task needs an elevated Focus Bear start, or the `schtasks` command below). To switch it back on, delete
the value and restart Focus Bear:

```bat
reg delete "HKCU\Software\Focus Bear" /v WATCHDOG_DISABLED /f
```

## Removing the task by hand

The installer removes the task before it installs, and the uninstaller removes it too. To remove it by hand
(elevated prompt if the task was registered elevated):

```bat
schtasks /Delete /TN "Focus Bear Watchdog" /F
```

**Rolling back to a build without the watchdog (anything before v3.1.251) needs this.** The older installer does not know
the task, and the older exe does not understand `/watchdog`: while it runs, each probe takes the second-instance path
and pops its tray menu, and after a quit the probe starts it again. Delete the task as above after installing the
older build.
