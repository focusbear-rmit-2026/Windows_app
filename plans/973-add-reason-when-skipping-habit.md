# Plan: Feature Request — Allow Users to Add a Reason When Skipping or Unable to Complete a Habit (#973)

## Problem Statement

When a user skips a habit or selects "Can't do it today", they cannot provide a custom free-text reason. The current flow (`whySkipHabitVcInfo`) only offers preset buttons ("Already did it", "Can't do it today"). Users want to be able to explain why they're skipping — both for self-reflection and to improve habit data quality.

## Proposed Solution

Add an optional text input to the `GridWhyDoYouWantToSkip` panel in `Habits/HabitWindow.xaml`. When the user selects "Can't do it today" or "Skip", show a text box prompting for an optional reason. Capture this and send it as part of the habit skip event to PostHog and/or the API.

### Files to Change

- `Habits/HabitWindow.xaml` — Add a `TextBox` for "reason" to `GridWhyDoYouWantToSkip`
- `Habits/HabitWindow.xaml.cs` — Read the reason text in `btnCantDoItToday_Click()` and `btnAlreadyDidIt_Click()`; pass it along to PostHog/API
- `config.json` — Add `whySkipHabitVcInfo.reasonPlaceholderText` string (e.g., "Optional: add a reason")
- `config-es.json` — Mirror the new config string
- `API/FocusBearAPIClient.cs` (optional) — If there is an API endpoint to receive skip reasons, update the call

## Implementation Steps

1. **Add a reason TextBox to `HabitWindow.xaml`** in `GridWhyDoYouWantToSkip`:
   ```xml
   <TextBox x:Name="TxtSkipReason" PlaceholderText="{Binding}" MaxLength="250" 
            Margin="10,5,10,5" TextWrapping="Wrap" AcceptsReturn="False"/>
   ```
2. **Update `ShowWhySkipHabitsWindow()`** in `HabitWindow.xaml.cs`:
   - Set placeholder text: `TxtSkipReason.Text = ""; // Clear on show`
   - Set the placeholder from config: `TxtSkipReason.Tag = appSettings.ConfigFileStrings["whySkipHabitVcInfo"]["reasonPlaceholderText"]`
3. **Read reason in skip handlers**:
   - In `btnCantDoItToday_Click()`: capture `string sReason = TxtSkipReason.Text?.Trim();`
   - In `btnAlreadyDidIt_Click()`: same capture
   - Store in `appSettings.sLastSkipReason` or pass directly
4. **Send reason to PostHog**:
   ```csharp
   appSettings.CapturePostHog("skip-habit", $"\"habit-id\":\"{appSettings.sCurrentHabitId}\",\"reason\":\"{sReason}\"");
   ```
5. **Add config strings** to `config.json`:
   ```json
   "whySkipHabitVcInfo": {
     ...existing...,
     "reasonPlaceholderText": "Optional: tell us why (e.g. sick today)"
   }
   ```
6. **Clear reason on window hide**: In `HideWhySkipHabitsWindow()`, clear `TxtSkipReason.Text = ""`.
7. **Test** with and without a reason, check PostHog events.

## Testing Checklist

- [ ] "Why skip?" window shows a text input for reason
- [ ] Reason field is optional — skipping without text still works
- [ ] Reason field clears between habit sessions
- [ ] "Already did it" → reason is captured and sent to PostHog
- [ ] "Can't do it today" → reason is captured and sent to PostHog
- [ ] Reason with special characters (quotes, emojis) is handled safely
- [ ] Long reasons are truncated to 250 characters
- [ ] No regression in the normal skip flow

## Acceptance Criteria

- Users can optionally type a reason when skipping or marking a habit as can't-do-today
- The reason is included in PostHog analytics events
- The text field is clearly optional (has placeholder text, not required)
- The reason field does not block the skip action if left empty

---

📋 Plan by Monk of Modularity (AI) — not a human author
