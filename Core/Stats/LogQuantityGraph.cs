using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>One plotted point: a formatted label, the value, and the day it belongs to.</summary>
    public sealed class GraphPoint
    {
        public GraphPoint(string label, double value, DateTime date)
        {
            Label = label;
            Value = value;
            Date = date;
        }

        public string Label { get; }
        public double Value { get; }
        public DateTime Date { get; }
    }

    /// <summary>
    /// The line chart under a habit's stats. Port of the Mac's <c>LogQuantityGraph</c>, itself the
    /// web's <c>transformSummariesToGraphData</c>.
    /// </summary>
    public static class LogQuantityGraph
    {
        public const string LabelPattern = "MMM dd, yyyy";

        /// <summary>Oldest first; the value is the day's logged summary.</summary>
        public static IReadOnlyList<GraphPoint> Points(IEnumerable<ActivityStatItem> items)
        {
            if (items == null) return new List<GraphPoint>();
            return items
                .Where(item => item != null && item.Date != null)
                .Select(item =>
                {
                    DateTime day = item.Date.Value.UtcDateTime;
                    return new GraphPoint(StatsDates.Format(day, LabelPattern), item.Summary, day);
                })
                .OrderBy(point => point.Date)
                .ToList();
        }
    }
}
