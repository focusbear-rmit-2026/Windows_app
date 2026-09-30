# Block page "Oops, I actually need this": how the unblock works and how to verify it

## How it works

1. When the app blocks a site it replaces the address bar with its local block page,
   `<data folder>\focus-block.html` (`AppSettings.FocusBlockPagePath`). The data folder is
   `%LOCALAPPDATA%\Focus Bear` for the real app and `%LOCALAPPDATA%\Focus Bear E2E` for the E2E build.
2. The block page URL carries `old_url` (the blocked URL, URL-encoded), the page's own parameters
   (`block_type`, `block_mode`, `focus_mode`, `version`, ...), and, when the page offers the unblock,
   `fb_unblock=<32 hex chars>`: a random token tied to that `old_url` (`BlockPageTokens`).
   - The token lasts 12 hours. At most 500 are kept. They are saved to
     `<data folder>\block-page-tokens.json` after each one is issued, so a block page left open still
     works after the app restarts.
   - A page that doesn't show the Oops button gets no token (`BlockPageUnblock.OffersUnblock`, which mirrors
     the page script's `shouldSkipUnblockPrompt`). That covers `block_mode=grizzly`, `strict_blocking=true`,
     `confirm_super_distracting=true`, `confirm_ai_distracting_url=true`, and `braindump=true` on a
     routine or always-blocked page.
3. The Oops button (hosted script `focus-blocked/js/main.js`) sets
   `window.location.href = window.location.href + "&unblock_it=true"`. The confirmation modal instead adds
   `&unlock_duration_minutes=N&distraction_intention=...&unblock_it=true`. Every parameter already on the page,
   including the token, is kept.
4. The app reads the new address and honours the unblock (`BlockPageUnblock.AuthorisedOldUrl`) only when all
   of these hold:
   - `unblock_it=true` is present, in the query or after a `#fragment`. The page's in-page links
     (`#cuddlyBearMode`, `#`) leave a fragment, and the trigger is then appended after it.
   - The address is this build's own `focus-block.html`, spelled `file:///C:/...`, `C:/...`, with a space
     or `%20`, and with either slash.
   - `fb_unblock` is a live token, and the `old_url` in the address is the URL the token was issued for,
     or has the same host. The host fallback covers an address bar that shows the query un-escaped.
   Then it opens the URL the token was issued for (not the text in the address bar), and the normal rules
   for the session still apply (a site on the session's block list is refused with a balloon, and a
   class session opens the unlock request instead).
5. Anything else is refused and logged, then handled as an ordinary URL. That includes a typed
   `reddit.com/?&unblock_it=true&old_url=...`, a hand-typed block page address with no token or an
   invented one, a token used for another site, an expired token, and a trigger typed onto a grizzly page.
   There is no fallback for a missing token: it would let a typed block-page address unblock anything
   blocked in the last 12 hours.

## Supported browsers

The app reads the address bar in `App.WinEventProc`: Chrome, Edge, Brave, Vivaldi and Comet through
`Chrome_WidgetWin_1`'s accessible value, and Firefox through `MozillaWindowClass` (address bar named
"Search with ..." or named after the block page). Opera and Arc are recognised as browsers elsewhere, but no
URL is read from them, so they never show the block page.

## Manual verification (E2E build, a real browser)

Prerequisites: the E2E build (`bin\E2E`), launched with `FOCUSBEAR_E2E_ENABLE_BLOCKING=1` and signed in to a
test account, **with the user's permission**. It blocks system-wide, and its data folder is
`%LOCALAPPDATA%\Focus Bear E2E`. Test each browser: Chrome, Edge, Firefox, and Brave if installed.

Log: `%LOCALAPPDATA%\Focus Bear E2E\Logs\Debug_Logs.txt`. Before each case, note the line count, or watch it
with `Get-Content -Wait`.

### A. Cuddly unblock works (focus session)
1. Set bear mode to cuddly. Start a focus session on a block list that blocks `reddit.com`.
2. In the browser, open `https://www.reddit.com/r/all`.
3. Expected address:
   `file:///C:/Users/<you>/AppData/Local/Focus%20Bear%20E2E/focus-block.html?old_url=https%3a%2f%2fwww.reddit.com%2fr%2fall&focus_mode_end_time=...&focus_mode=...&focus_mode_intention=...&version=...&cuddly_bear_mode=true&fb_unblock=<32 hex>`.
   Chrome may show it without `file:///` and with spaces.
   Log: `PBU.cs - block page for www.reddit.com, unblock offered`.
4. Click "Oops, I actually need this", then confirm.
5. Expected: the address briefly ends in `&unblock_it=true`, then the tab opens
   `https://www.reddit.com/r/all`. Log: `PBU.cs - ProcessURLForFocusModes - URL = ...&unblock_it=true`,
   with no "unblock refused" line. If reddit is on the session's *blocked URLs*, the balloon
   "You can't unblock this site ..." appears instead, which is correct.
6. Repeat step 4 after first clicking the page's "cuddly bear mode" link (the address gains
   `#cuddlyBearMode`). It must still unblock.

### B. Always-blocked, cuddly and grizzly
1. Turn on "block distracting sites immediately". Start a focus session on a block list that does **not**
   list the site itself, then open a site from the always-blocked (super-distracting) list, such as
   `https://www.youtube.com`. With no session or blocking schedule, the always-blocked list only applies
   during onboarding.
2. Cuddly: the address has `block_type=always-blocked&block_mode=cuddly&...&fb_unblock=...`, and the log reads
   `unblock offered`. Oops reopens the site. Log: `PBU.cs - ProcessCurrentAlwaysBlockedURL - URL = ...`.
3. Grizzly: the address has `block_mode=grizzly` and **no** `fb_unblock`, the log reads `unblock not offered`,
   and the page shows no Oops button. Click in the address bar, append `&unblock_it=true`, and press Enter.
   Expected: the page stays blocked, and the log reads `PBU.cs - unblock refused: ...`.

### C. Typed bypasses never unblock
Type each of these into the address bar and press Enter. Each must be blocked as normal, and the ones that
reach the unblock check log `unblock refused`:
- `https://www.reddit.com/?&unblock_it=true&old_url=https://www.reddit.com`
- `file:///C:/Users/<you>/AppData/Local/Focus%20Bear%20E2E/focus-block.html?old_url=https%3A%2F%2Fwww.reddit.com&unblock_it=true`
  (no token)
- the block page URL from A.3 with `old_url` changed to another blocked site, then `&unblock_it=true`

### D. Restart
1. Leave a cuddly block page from A.3 open. Quit the E2E app (tray, Quit) and start it again, with the same
   environment variable.
2. Confirm that `%LOCALAPPDATA%\Focus Bear E2E\block-page-tokens.json` exists and contains the token from the
   address bar.
3. Click Oops on the old page. It must unblock as in A.5.
4. A token more than 12 hours old must not work, and is dropped from the file at the next start.

### Clean-up
Stop the session, turn off the E2E blocking variable, and delete `block-page-tokens.json` if you want. It holds
only URLs that were blocked.
