# Plan: App Window Auto-Close After Routine Skip/Postpone — User Setting (#1001)

## Problem Statement

After a user skips or postpones their routine (morning or evening), the app window can optionally auto-close. However, unconditionally auto-closing the window breaks the workflow for users who want the window to stay open so they can immediately start a focus session.

## Solution: User Setting (Revised after @manishjain999 feedback)

Add a boolean user preference that controls whether the app window auto-closes after a routine is skipped or postponed.

- **Setting name:** `AutoCloseAfterRoutineSkipPostpone`
- **Default value:** `false` — window stays open (no surprise for existing users)
- **When `true`:** Window auto-closes after skip/postpone
- **When `false`:** Window stays open, showing the focus session start screen

## Files Changed

### 1. `Utils/AppSettings.cs`
- Added `bool AutoCloseAfterRoutineSkipPostpone { get; set; }` (default `false`)
- Added reading of `kAutoCloseAfterRoutineSkipPostpone` from MacOS local device settings JSON

### 2. `Utils/LocalSettingsManager.cs`
- Added persistence of `kAutoCloseAfterRoutineSkipPostpone` alongside other MacOS settings

### 3. `Preferences/SettingsTab/UI.xaml` + `UI.xaml.cs`
- Added toggle: "Automatically close window after skipping or postponing a routine"
- Placed in the UI settings section (after full-screen mode toggle)
- Handlers: `AutoCloseAfterSkipPostponeCheckbox_Checked/Unchecked`

### 4. `App.xaml.cs`
- `PostponeHabitWork()`: Wrapped `wndFocusBearMainWindow.Hide()` in `if (appSettings.AutoCloseAfterRoutineSkipPostpone)`
- EasySkipPostpone dialog dismiss: Same conditional guard

### 5. `Habits/PostponeHabits.xaml.cs`
- `btnCompletelySkip_Click()`: Conditional guard around `_parentWindow?.Hide()`
- `btnPostpone_Click()`: Conditional guard around `_parentWindow?.Hide()`

### 6. `Focus/FocusActivitySummary.xaml.cs`
- Evening routine postpone path: Conditional guard around `ParentWindow.Hide()`

## Testing Checklist

- [ ] Setting OFF (default): Skip morning routine → window stays open (shows focus session start)
- [ ] Setting OFF: Postpone morning routine → window stays open
- [ ] Setting OFF: Skip evening routine → window stays open
- [ ] Setting OFF: Postpone evening routine → window stays open
- [ ] Setting ON: Skip morning routine → window auto-closes
- [ ] Setting ON: Postpone morning routine → window auto-closes
- [ ] Setting ON: Skip evening routine → window auto-closes
- [ ] Setting ON: Postpone evening routine → window auto-closes
- [ ] Setting persists across app restarts
- [ ] Setting toggle is visible and clearly labelled in Settings > UI section
- [ ] No regression in focus session start flow when window stays open

## Acceptance Criteria

- A new user setting controls auto-close behaviour after routine skip/postpone
- Default is window-stays-open (no surprise for existing users)
- Users who want auto-close can enable it in settings
- Both paths work correctly for morning and evening routines
- Setting persists across sessions

---

📋 Revised plan by Monk of Modularity (AI) — setting-based approach after @manishjain999 feedback.
Implemented by Captain Codebeard (AI).
