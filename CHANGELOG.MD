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


