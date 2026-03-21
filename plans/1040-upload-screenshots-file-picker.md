# Plan: Upload Screenshots for Logs Should Allow Using a File Picker (#1040)

## Problem Statement

The "Share Logs with Focus Bear" feature in the Help tab currently relies primarily on drag-and-drop to add screenshots. Users want a traditional file picker button to select screenshots from their file system. The button `btnSelectScreenshots` exists in the UI but may not be working correctly or is insufficiently discoverable.

## Proposed Solution

Review and fix the file picker button in `Preferences/HelpTab.xaml.cs` (`btnSelectScreenshots_Click` handler). The `OpenFileDialog` implementation is already present but may need fixes for multi-file selection, file type filtering, or the button visibility/functionality.

### Files to Change

- `Preferences/HelpTab.xaml.cs` — Review `btnSelectScreenshots_Click()` (line ~310) and the `OpenFileDialog` setup (lines ~312–328)
- `Preferences/HelpTab.xaml` — Check button visibility, placement, and discoverability of `btnSelectScreenshots`
- `config.json` — Review `helpVcInfo.buttonSelectScreenshots` and `helpVcInfo.titleAddScreenshots` strings for clarity

## Implementation Steps

1. **Reproduce the issue**: Open Help tab → "Share logs with Focus Bear" section → try to add screenshots via the file picker button.
2. **Check `btnSelectScreenshots_Click()`** in `HelpTab.xaml.cs`:
   - Is `OpenFileDialog` configured with correct file filters? (e.g., `*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp`)
   - Is `Multiselect = true` to allow picking multiple files?
   - Does it correctly add files to `appSettings.droppedFiles`?
   - Is `RefreshScreenshots()` called after selection?
3. **Verify the button is visible**: Is `btnSelectScreenshots` visible in the XAML? Is it potentially hidden when no files are present or only shown after drag-and-drop?
4. **Ensure the button works even when `droppedFiles` is empty**: The file picker should be the primary way to add files, not just a fallback for drag-and-drop.
5. **Add or fix file type filter**:
   ```csharp
   dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*";
   dialog.Multiselect = true;
   ```
6. **Update `config.json`** if button label is unclear — e.g., change to "Select Screenshots" or "Browse for Screenshots".
7. **Test** adding screenshots via the button and confirm they appear in the screenshot list.

## Testing Checklist

- [ ] Click "Select Screenshots" button → file picker opens
- [ ] Can select a single image file → file appears in the list
- [ ] Can select multiple image files → all files appear in the list
- [ ] Can select files with `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp` extensions
- [ ] Non-image files are filtered out or handled gracefully
- [ ] Drag-and-drop still works alongside file picker
- [ ] After selecting files, they are included in the log upload
- [ ] Button is visible and accessible (not hidden by default)

## Acceptance Criteria

- Users can add screenshots via a file picker without needing to drag and drop
- Multi-file selection is supported
- Image file types are filtered appropriately
- The button is prominently placed and clearly labelled in the Help tab

---

📋 Plan by Monk of Modularity (AI) — not a human author
