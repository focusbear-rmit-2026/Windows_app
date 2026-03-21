# Plan: Tab Limiter Feature on Windows (#377)

## Problem Statement

Focus Bear already has the config strings (`postBrowsersTabAuditVcInfo.titleSetTabLimit`, `alertMaxTabLimitVcInfo`) and UI concepts for a tab limiter feature (visible in the Upgrade to Pro screen), but the actual tab limiting functionality is not implemented on Windows. Users cannot set a maximum number of browser tabs, and the app does not alert or enforce a tab limit.

## Proposed Solution

Implement the Tab Limiter feature on Windows:
1. Add a tab limit setting in the Preferences UI (post-tab-audit flow or in the Advanced tab)
2. Periodically check the number of open browser tabs
3. When the limit is exceeded, show an alert (`alertMaxTabLimitVcInfo`)
4. Store the setting persistently

### Files to Change

- `Utils/AppSettings.cs` — Add `nMaxTabLimit` (int, 0 = disabled)
- `Utils/RegistryManager.cs` — Add `GetMaxTabLimit()` and `SaveMaxTabLimit()`
- `App.xaml.cs` — Add tab count checking in `dispatcherTimer_Background()` using `Process.GetProcessesByName("chrome")` and counting tabs via the existing Chrome process inspection code
- `Preferences/SettingsTab/AdvancedTab.xaml` + `.cs` — Add "Tab limit" numeric input
- A new `AlertMaxTabLimitWindow.xaml` + `.cs` — Alert window using `alertMaxTabLimitVcInfo` strings
- `config.json` — `alertMaxTabLimitVcInfo` and `postBrowsersTabAuditVcInfo` already have the required strings

## Implementation Steps

1. **Add setting storage**:
   - `AppSettings.cs`: `public int nMaxTabLimit = 0; // 0 = disabled`
   - `RegistryManager.cs`: Save/load `MaxTabLimit` from registry
2. **Add setting UI in `AdvancedTab.xaml`**:
   - Numeric input: "Max browser tab limit (0 = disabled)"
   - Text from `postBrowsersTabAuditVcInfo.titleTabLimitCount`
3. **Add tab counting logic in `App.xaml.cs`**:
   - In `dispatcherTimer_Background()` (runs every ~30s):
     ```csharp
     if (appSettings.nMaxTabLimit > 0)
     {
         int nTabCount = GetBrowserTabCount(); // new helper method
         if (nTabCount > appSettings.nMaxTabLimit)
             ShowMaxTabLimitAlert(nTabCount);
     }
     ```
4. **Implement `GetBrowserTabCount()`**:
   - Use `Process.GetProcessesByName("chrome")` + count main windows
   - Similar approach for Firefox, Edge, Brave
5. **Create `AlertMaxTabLimitWindow.xaml`**:
   - Show `alertMaxTabLimitVcInfo.title` (replace `<COUNT>` with actual count)
   - Show `alertMaxTabLimitVcInfo.titleTabsCount` + current count
   - "Close excess tabs" / "Dismiss" buttons
6. **Prevent repeated alerts**: Only show alert once per threshold breach, not every background tick.
7. **Test** with Chrome, Edge, Firefox.

## Testing Checklist

- [ ] Tab limit setting is visible in Advanced Preferences tab
- [ ] Setting to 0 disables the feature
- [ ] Setting to 5: when 6+ tabs are open → alert appears
- [ ] Alert shows current tab count and the limit
- [ ] Alert does not spam (only shows once per breach event)
- [ ] Works with Chrome, Edge, Firefox, Brave
- [ ] Setting persists after app restart
- [ ] Tab count decreases after closing tabs → no alert on next check

## Acceptance Criteria

- Users can set a maximum tab limit in Preferences
- When the limit is exceeded, a non-intrusive alert is shown
- The alert uses `alertMaxTabLimitVcInfo` config strings (no hardcoded text)
- The feature works with the main supported browsers (Chrome, Edge, Firefox, Brave)
- The feature is a Pro tier feature (check subscription before enforcing)

---

📋 Plan by Monk of Modularity (AI) — not a human author
