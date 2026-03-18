# Plan: Copy Icon + Remaining Time Not Visible (#975)

## Problem Statement

The copy icon and remaining time display are not visible in the Focus Progress overlay (`Focus/FocusProgress.xaml`). Users cannot see how much time is left in their focus session and cannot copy their intention/session text from the overlay widget.

## Proposed Solution

Investigate the `FocusProgress.xaml` and `FocusProgress.xaml.cs` for visibility conditions that may be hiding these elements, as well as any layout or z-index issues. The `OnUntill` TextBlock shows remaining time; if it's not visible, check whether the Canvas/Viewbox containing it has incorrect sizing or opacity. A copy icon may be missing from the XAML entirely and needs to be added.

### Files to Change

- `Focus/FocusProgress.xaml` — Review the `OnUntill` TextBlock (remaining time) and check if a copy icon element exists or needs to be added
- `Focus/FocusProgress.xaml.cs` — Review the timer tick handler (line ~114–123) that updates `OnUntill.Text` and any Visibility settings
- `Focus/FocusProgress.xaml.cs` — Check if `Opacity` changes hide the widget (the window has `Opacity="0.5"` with `MouseEnter`/`MouseLeave` events — confirm full opacity on hover)

## Implementation Steps

1. **Reproduce the issue**: Start a focus session and check whether the floating progress widget shows remaining time and a copy icon.
2. **Check `OnUntill` TextBlock visibility**:
   - In `FocusProgress.xaml`, the `OnUntill` TextBlock is inside a `Canvas` inside a `Viewbox`. Is the Viewbox sizing correctly? Does it collapse under certain window sizes?
   - In `FocusProgress.xaml.cs`, is `OnUntill.Text` being updated by the timer tick? Are there any conditions that hide it?
3. **Check the timer update code** (line ~114–123):
   - Is `nTimer` being decremented correctly?
   - Is `OnUntill.Text` set in the tick handler?
4. **Identify the copy icon**: 
   - Is there a copy button/icon in `FocusProgress.xaml`? If not, should there be one to copy the focus intention?
   - Check if a copy icon was recently removed or never added.
5. **Fix visibility issues**:
   - If `OnUntill` is hidden due to Visibility binding, fix the condition
   - If the Canvas/Viewbox has a dimension issue (0 width/height), fix the layout
6. **Add copy icon if missing**:
   - Add a small clipboard icon button to the FocusProgress widget
   - On click: copy focus intention text to clipboard using `Clipboard.SetText(appSettings.sRunningFocusModeIntention)`
7. **Verify `MouseEnter` opacity**: Confirm the widget becomes fully visible on hover (`Opacity = 1.0`) so users can interact with it.
8. **Test** at various DPI settings (100%, 125%, 150%).

## Testing Checklist

- [ ] Start focus session → progress widget appears with remaining time visible
- [ ] Remaining time counts down correctly (e.g., "25m", "24m", etc.)
- [ ] Copy icon is present and clickable
- [ ] Clicking copy icon copies the intention text to clipboard
- [ ] Widget is visible on hover (full opacity) and dimmed when not hovered
- [ ] Test at 100%, 125%, 150% DPI scaling
- [ ] Test with short focus sessions (5 min) and long ones (2hr)

## Acceptance Criteria

- Remaining time is clearly visible in the Focus Progress overlay
- Copy icon is present and functional for copying the intention
- No layout regression at different DPI settings
- Widget opacity behaviour (dim at rest, full on hover) is preserved

---

📋 Plan by Monk of Modularity (AI) — not a human author
