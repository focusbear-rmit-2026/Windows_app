# Plan: Feature Request — Option to Disable Notification Sounds (#743)

## Problem Statement

Focus Bear plays notification sounds (routine start, habit complete, focus mode start/end) but provides no way for users to disable them. Users working in quiet environments, open offices, or who simply prefer a silent experience have no option to turn off these sounds.

## Proposed Solution

Add a "Disable notification sounds" toggle to the Settings tab (`Preferences/SettingsTab/Settings.xaml`). Store the preference in `AppSettings` and check it in `Utils/Sounds.cs` before playing any sound.

### Files to Change

- `Preferences/SettingsTab/Settings.xaml` — Add a toggle/checkbox for "Disable notification sounds"
- `Preferences/SettingsTab/Settings.xaml.cs` — Save/load the setting
- `Utils/AppSettings.cs` — Add `bDisableNotificationSounds` boolean property with registry persistence
- `Utils/Sounds.cs` — Add guard at the top of each play method: `if (appSettings.bDisableNotificationSounds) return;`
- `Utils/RegistryManager.cs` — Add `GetDisableNotificationSounds()` and `SaveDisableNotificationSounds()`
- `config.json` — Add `settingsNew.disableNotificationSoundsText` string
- `config-es.json` — Mirror new string

## Implementation Steps

1. **Add `bDisableNotificationSounds`** to `AppSettings.cs`:
   ```csharp
   public bool bDisableNotificationSounds = false;
   ```
2. **Add registry persistence** in `RegistryManager.cs`:
   ```csharp
   public static void SaveDisableNotificationSounds(bool bDisable)
   {
       RegistryKey key = Registry.CurrentUser.OpenSubKey(sRegKeyPath, true);
       key.SetValue("DisableNotificationSounds", bDisable ? 1 : 0);
   }
   public static bool GetDisableNotificationSounds()
   {
       RegistryKey key = Registry.CurrentUser.OpenSubKey(sRegKeyPath);
       return key != null && (int)(key.GetValue("DisableNotificationSounds", 0)) == 1;
   }
   ```
3. **Load setting on startup** in `AppSettings.cs` or `App.xaml.cs`.
4. **Add toggle to `Settings.xaml`**:
   ```xml
   <CheckBox x:Name="chkDisableSounds" Content="{Binding}" Click="chkDisableSounds_Click"/>
   ```
5. **Handle in `Settings.xaml.cs`**:
   ```csharp
   private void chkDisableSounds_Click(object sender, RoutedEventArgs e)
   {
       appSettings.bDisableNotificationSounds = chkDisableSounds.IsChecked == true;
       RegistryManager.SaveDisableNotificationSounds(appSettings.bDisableNotificationSounds);
   }
   ```
6. **Add guard to `Sounds.cs`** — at the top of each `Play*` method:
   ```csharp
   static AppSettings appSettings = AppSettings.GetInstance;
   if (appSettings.bDisableNotificationSounds) return;
   ```
7. **Add config string**:
   ```json
   "settingsNew": {
     ...existing...,
     "disableNotificationSoundsText": "Disable notification sounds"
   }
   ```
8. **Test** by toggling the setting and triggering each sound.

## Testing Checklist

- [ ] Setting toggle is visible in Settings tab
- [ ] Enabling "Disable notification sounds" → no sounds play
- [ ] Disabling (re-enabling sounds) → sounds play again
- [ ] Setting persists across app restarts
- [ ] All sound types are silenced: routine start, habit complete, focus mode start/end, evening routine complete
- [ ] Focus music (FocusMusic) is NOT affected (that's a separate feature)
- [ ] Setting label text comes from config (not hardcoded)

## Acceptance Criteria

- Users can toggle notification sounds on/off from Settings
- The setting persists after app restart
- When sounds are disabled, all notification sounds (habit, focus mode, routine) are silent
- Focus music is not affected by this setting

---

📋 Plan by Monk of Modularity (AI) — not a human author
