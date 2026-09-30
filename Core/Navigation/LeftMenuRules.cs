using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Navigation
{
    /// <summary>
    /// The main window's left-menu entries. Names match the app's <c>LeftButtonType</c> one for one so
    /// the app converts by name; the rules below never need WPF.
    /// </summary>
    public enum LeftMenuItem
    {
        Habits,
        StartFocusSession,
        MicroBreaks,
        ToDoPlayer,
        ToDoList,
        BrainDump,
        FocusMusic,
        Motivation,
        TabAudit,
        LateNoMore,
        Blocking,
        Settings,
        EditHabits,
        Help,
    }

    /// <summary>What currently owns the main window, and so restricts its left menu.</summary>
    public enum MenuLock
    {
        /// <summary>Nothing running: every entry is available.</summary>
        None,
        /// <summary>A routine owns the window (morning/evening habits, focus summary, shut-off time).</summary>
        Routine,
        /// <summary>A micro break is playing.</summary>
        MicroBreak,
        /// <summary>Onboarding / signed out: nothing in the menu applies yet.</summary>
        Everything,
    }

    /// <summary>Which left-menu entries stay clickable while something owns the main window.</summary>
    public static class LeftMenuAvailability
    {
        /// <summary>Open whenever a routine or break runs — none of them can end or skip it.</summary>
        static readonly LeftMenuItem[] AlwaysOpen = { LeftMenuItem.Help, LeftMenuItem.BrainDump, LeftMenuItem.ToDoList };

        /// <summary>
        /// Whether <paramref name="item"/> is clickable under <paramref name="menuLock"/>.
        /// A running routine keeps Settings open, as on the Mac (<c>LeftMenuAvailability.alwaysAvailable</c>):
        /// the user can change a preference without ending the routine, and the Settings entry still asks for
        /// the password when "require password to change settings" is on, so strictness is unchanged. A micro
        /// break does not — the Mac disables Settings for a break too (<c>microBreaksStarted</c>).
        /// </summary>
        public static bool IsEnabled(LeftMenuItem item, MenuLock menuLock)
        {
            switch (menuLock)
            {
                case MenuLock.None: return true;
                case MenuLock.Everything: return false;
                case MenuLock.Routine:
                    // The user asked for the Mac's routine menu: the tools and the editors stay open too. The
                    // editors (Settings, Blocking, Edit Habits) are password-gated in the app, so strictness holds.
                    return item == LeftMenuItem.Habits || item == LeftMenuItem.Settings || item == LeftMenuItem.Blocking
                        || item == LeftMenuItem.EditHabits || item == LeftMenuItem.FocusMusic || item == LeftMenuItem.Motivation
                        // Late No More's settings, as the Mac keeps its Late No More tool open over a routine.
                        || item == LeftMenuItem.LateNoMore
                        || AlwaysOpen.Contains(item);
                case MenuLock.MicroBreak:
                    return item == LeftMenuItem.MicroBreaks || AlwaysOpen.Contains(item);
                default: return true;
            }
        }
    }

    /// <summary>A TOOLS row the user can hide with its hover ✕ and bring back from the TOOLS cog.</summary>
    public enum ToolId
    {
        FocusMusic,
        BrainDump,
        TodoList,
        Stats,
        TabAudit,
        LateNoMore,
    }

    /// <summary>
    /// The hidden-tools list, stored per user on this device as a comma-separated list of the Mac's
    /// <c>ToolID</c> raw values ("focusMusic", "brainDump", …). The Mac keeps the same list in
    /// UserDefaults (<c>kUserdefaultsHiddenToolIds</c> + user id) and does not sync it, so neither do we.
    /// </summary>
    public static class ToolVisibility
    {
        static readonly Dictionary<ToolId, string> Raw = new Dictionary<ToolId, string>
        {
            { ToolId.FocusMusic, "focusMusic" },
            { ToolId.BrainDump, "brainDump" },
            { ToolId.TodoList, "todoList" },
            { ToolId.Stats, "stats" },
            { ToolId.TabAudit, "tabAudit" },
            { ToolId.LateNoMore, "lateNoMore" },
        };

        public static string RawId(ToolId id) => Raw[id];

        /// <summary>The tool behind a menu entry, or null for entries that cannot be hidden.</summary>
        public static ToolId? ForMenuItem(LeftMenuItem item)
        {
            switch (item)
            {
                case LeftMenuItem.FocusMusic: return ToolId.FocusMusic;
                case LeftMenuItem.BrainDump: return ToolId.BrainDump;
                case LeftMenuItem.ToDoList: return ToolId.TodoList;
                case LeftMenuItem.Motivation: return ToolId.Stats;
                case LeftMenuItem.TabAudit: return ToolId.TabAudit;
                case LeftMenuItem.LateNoMore: return ToolId.LateNoMore;
                default: return null;
            }
        }

        /// <summary>Hidden tools in menu order; unknown or repeated ids are dropped.</summary>
        public static IReadOnlyList<ToolId> Parse(string stored)
        {
            var ids = new HashSet<string>((stored ?? "").Split(',').Select(s => s.Trim()), StringComparer.Ordinal);
            return Raw.Where(kv => ids.Contains(kv.Value)).Select(kv => kv.Key).OrderBy(k => (int)k).ToList();
        }

        public static string Serialize(IEnumerable<ToolId> hidden) =>
            string.Join(",", (hidden ?? Enumerable.Empty<ToolId>()).Distinct().OrderBy(k => (int)k).Select(RawId));

        public static string Hide(string stored, ToolId id) => Serialize(Parse(stored).Concat(new[] { id }));

        public static string Restore(string stored, ToolId id) => Serialize(Parse(stored).Where(t => t != id));

        public static bool IsHidden(string stored, ToolId id) => Parse(stored).Contains(id);
    }

    /// <summary>What the main window's top-bar minimise button does.</summary>
    public enum MinimiseAction
    {
        /// <summary>No routine owns the window: minimise it like any other window.</summary>
        MinimiseWindow,
        /// <summary>A routine owns the window: take the same path as its "Exit Fullscreen" / postpone button.</summary>
        ExitRoutine,
        /// <summary>A routine owns the window and is deliberately offering no way out right now.</summary>
        Unavailable,
    }

    public static class MinimisePolicy
    {
        /// <summary>
        /// The Mac's <c>btnMinimizeClicked</c>: while a routine runs, minimise is the routine's own abort /
        /// postpone flow (password rules included) — full screen exists to remove escape hatches, so it must
        /// never silently get the window out of the way. When that routine screen is showing no exit of its
        /// own (e.g. the last habit's "unlock" screen), the minimise does nothing either.
        /// </summary>
        public static MinimiseAction Decide(bool routineOwnsWindow, bool routineOffersExit)
        {
            if (!routineOwnsWindow) return MinimiseAction.MinimiseWindow;
            return routineOffersExit ? MinimiseAction.ExitRoutine : MinimiseAction.Unavailable;
        }
    }
}
