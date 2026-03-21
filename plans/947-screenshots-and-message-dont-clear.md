# Plan: After Clicking "Share Logs with Focus Bear", the Screenshots and Message Don't Clear (#947)

## Problem Statement

After a user successfully clicks "Share Logs with Focus Bear" (which uploads logs + screenshots + message), the screenshots list and feedback message text field are not cleared. On the next attempt to share logs, the old screenshots and message are still present, which is confusing and may lead to unintentional re-submission of old data.

## Proposed Solution

After a successful log upload in `Preferences/HelpTab.xaml.cs`, clear `appSettings.droppedFiles` and the feedback message text field. The `UploadLogs.UploadLogsToServer()` in `Utils/UploadLogs.cs` returns a `bool` indicating success — use this to trigger a UI reset.

### Files to Change

- `Preferences/HelpTab.xaml.cs` — After `UploadLogs.UploadLogsToServer()` returns `true`, clear the message text and call `RefreshScreenshots()`
- `Utils/AppSettings.cs` — Ensure `droppedFiles` is accessible and can be cleared from `HelpTab.xaml.cs`
- `Preferences/HelpTab.xaml` — Identify the feedback message `TextBox` that needs to be cleared

## Implementation Steps

1. **Reproduce the issue**: 
   - Go to Help tab → enter a feedback message → add screenshots → click "Share logs"
   - After the success dialog dismisses, observe that the message and screenshots are still present
2. **Find the upload handler** in `HelpTab.xaml.cs`:
   - Locate the button click that calls `UploadLogs.UploadLogsToServer(sFeedback)`
   - Check if there is any reset code after the successful upload dialog
3. **Add post-upload cleanup** after `UploadLogsToServer()` returns `true`:
   ```csharp
   appSettings.droppedFiles.Clear();
   RefreshScreenshots();
   FeedbackTextBox.Text = string.Empty; // clear the message field
   ```
4. **Verify `RefreshScreenshots()`** correctly refreshes the UI list to show empty state (line ~272).
5. **Ensure the placeholder panel** reappears when the list is empty (`PlaceholderPanel.Visibility` is already bound to `droppedFiles.Count == 0` — line ~277).
6. **Test** the full flow: add screenshots + message → share → confirm both are cleared on success.

## Testing Checklist

- [ ] Add screenshots + message → share successfully → screenshots list is cleared
- [ ] Add screenshots + message → share successfully → message text field is cleared
- [ ] Placeholder "drag and drop here" text reappears after clear
- [ ] Failed upload does NOT clear the message or screenshots
- [ ] Can add new screenshots and message after clearing
- [ ] Repeat log sharing works correctly (no stale data)

## Acceptance Criteria

- After a successful log share, the screenshots list is empty
- After a successful log share, the feedback message text field is empty
- On upload failure, screenshots and message are retained (so the user can retry)
- The help tab UI resets to its initial state after a successful share

---

📋 Plan by Monk of Modularity (AI) — not a human author
