using System;
using System.Collections.Generic;
using System.Globalization;

namespace FocusBear.Core.ToDo
{
    /// <summary>One row of the outcome picker: "Not set", a /5 step, or a legacy odd wire value kept as it is.</summary>
    public sealed class OutcomeOption
    {
        public OutcomeOption(int? wire, int? step, bool isLegacy)
        {
            Wire = wire;
            Step = step;
            IsLegacy = isLegacy;
        }

        /// <summary>What goes on the wire as <c>outcome</c> (the server's /10 scale); null = not set.</summary>
        public int? Wire { get; }

        /// <summary>The displayed /5 step; null for "Not set" and for a legacy row.</summary>
        public int? Step { get; }

        public bool IsLegacy { get; }
    }

    /// <summary>
    /// The Mac's project outcome rating (<c>ForecastOutcomePicker</c> and the project page's outcome chip).
    /// Projects carry the same <c>outcome</c> a task does — a /10 wire value shown as 1–5 — and legacy
    /// <c>importance</c> (1–4) is the server's own portfolio input, never shown or edited here.
    /// </summary>
    public static class ProjectOutcomeEdit
    {
        /// <summary>
        /// The /5 step the chip names for a wire value, as the Mac's chip computes it: whole-number
        /// division by the wire factor, clamped to 1–5 (so a legacy 7 reads as 3, and 1 as 1).
        /// </summary>
        public static int? ChipStep(int? wire)
        {
            if (!wire.HasValue) return null;
            return Math.Min(ToDoScoring.OutcomeSteps, Math.Max(1, wire.Value / ToDoScoring.OutcomeWireFactor));
        }

        /// <summary>
        /// Not set · 1/5 … 5/5 (wire 2, 4 … 10), then — only when the project holds an odd legacy value
        /// (set on the web's old 1–10 scale) — a row for that value, so opening the picker never
        /// silently changes it.
        /// </summary>
        public static IList<OutcomeOption> Options(int? current)
        {
            var options = new List<OutcomeOption> { new OutcomeOption(null, null, false) };
            for (int step = 1; step <= ToDoScoring.OutcomeSteps; step++)
                options.Add(new OutcomeOption(step * ToDoScoring.OutcomeWireFactor, step, false));
            if (IsLegacy(current)) options.Add(new OutcomeOption(current, null, true));
            return options;
        }

        /// <summary>A wire value that is not one of the five steps.</summary>
        public static bool IsLegacy(int? wire) => wire.HasValue && wire.Value % ToDoScoring.OutcomeWireFactor != 0;

        /// <summary>Mac <c>ForecastText.outcomeValue</c>: a wire value as "3.5 / 5", clamped to 1–5.</summary>
        public static string ValueText(int wire, CultureInfo culture = null)
        {
            double shown = Math.Min(ToDoScoring.OutcomeSteps, Math.Max(1, wire / (double)ToDoScoring.OutcomeWireFactor));
            return shown.ToString("0.#", culture ?? CultureInfo.CurrentCulture) + " / " + ToDoScoring.OutcomeSteps;
        }
    }
}
