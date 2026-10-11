using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The frosted backdrop raised behind the block popups (the AI block check-in, the gentle block popup, set
    /// intention) - the Mac's <c>blurWcObj</c> / <c>blockBlurVc</c>. The Mac frosts the blocked app with an
    /// <c>NSVisualEffectView</c> and lays a wash of the window background colour over it at
    /// <see cref="ScrimOpacity"/>, because the blur alone leaves large text readable.
    /// </summary>
    public static class BlockBackdrop
    {
        /// <summary>
        /// Opacity of the wash with no blur under it (Mac <c>BlockBlurScrimView.scrimOpacity</c>): on its own it
        /// has to make the blocked app unreadable.
        /// </summary>
        public const double ScrimOpacity = 0.82;

        /// <summary>
        /// Opacity of the wash over Windows' acrylic blur. Acrylic blurs far harder than the Mac's material, so
        /// the app is already unreadable and a lighter wash lets it bleed through frosted, as the Mac design shows.
        /// </summary>
        public const double ScrimOpacityOverBlur = 0.5;

        /// <summary>The wash to use, depending on whether the system is drawing the blur under it.</summary>
        public static double WashOpacity(bool blurShowing)
        {
            return blurShowing ? ScrimOpacityOverBlur : ScrimOpacity;
        }

        /// <summary>
        /// How long the backdrop may stay up before any block popup has appeared (the popup is shown just after
        /// the backdrop, sometimes after an AI check). Past this with no popup, it goes.
        /// </summary>
        public static readonly TimeSpan PopupGrace = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The backdrop's watchdog. It must never outlive its popup - it swallows clicks, so a stuck one would
        /// lock the user out of the screen. It goes as soon as the popup it was raised for is gone (however
        /// that happened), or when no popup has shown up within <see cref="PopupGrace"/>.
        /// </summary>
        public static bool ShouldDismiss(bool popupVisibleNow, bool popupWasSeen, TimeSpan sinceShown)
        {
            if (popupVisibleNow) return false;
            return popupWasSeen || sinceShown >= PopupGrace;
        }

        /// <summary>How many times one showing of the backdrop fixes the z-order before it stops trying.</summary>
        public const int MaxOrderFixes = 6;

        /// <summary>
        /// What the watchdog does about the z-order. Every block popup (the AI check-in, the gentle block popup, set
        /// intention) must sit ON the backdrop, never under it (block-blur-backdrop, 28 Sep: "AI blocking popups
        /// should appear on top of the blur").
        /// <list type="bullet">
        /// <item>A popup under the backdrop is raised above it - a reused popup shown while another app is in front
        /// can't take the foreground, so Show() leaves it where it was, under the newly shown backdrop. Raising our own
        /// topmost windows needs no foreground rights. If it is still under after <see cref="MaxOrderFixes"/> tries the
        /// backdrop goes: better no frosting than a popup the user can't see or reach.</item>
        /// <item>Another always-on-top window between the popup and the backdrop moves the backdrop back under the popup,
        /// until <see cref="MaxOrderFixes"/> - after that it is left (the popup is still on top).</item>
        /// </list>
        /// </summary>
        public static BackdropOrderFix OrderFix(bool popupBelowBackdrop, bool otherWindowBetween, int attempts)
        {
            if (popupBelowBackdrop)
                return attempts < MaxOrderFixes ? BackdropOrderFix.RaisePopups : BackdropOrderFix.HideBackdrop;
            if (otherWindowBetween && attempts < MaxOrderFixes) return BackdropOrderFix.MoveBackdropUnderPopup;
            return BackdropOrderFix.None;
        }
    }

    /// <summary>A z-order correction for the block backdrop (<see cref="BlockBackdrop.OrderFix"/>).</summary>
    public enum BackdropOrderFix
    {
        None,
        /// <summary>Put the popups back on top of the backdrop.</summary>
        RaisePopups,
        /// <summary>Put the backdrop directly under the top popup.</summary>
        MoveBackdropUnderPopup,
        /// <summary>A popup can't be got above it: take the backdrop down.</summary>
        HideBackdrop,
    }
}
