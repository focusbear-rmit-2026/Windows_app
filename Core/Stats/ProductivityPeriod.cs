using System;

namespace FocusBear.Core.Stats
{
    /// <summary>The bucket size the productivity chart is grouped by.</summary>
    public enum ProductivityPeriod
    {
        Daily,
        Monthly,
        Yearly,
    }

    /// <summary>
    /// Bucketing and label formats for the productivity chart. Port of the Mac's
    /// <c>ProductivityPeriod</c>; the patterns are the web's, so all three clients label a chart
    /// the same way.
    /// </summary>
    public static class ProductivityPeriods
    {
        /// <summary>Chart label pattern (web: 'MMM DD' / 'MMM YYYY' / 'YYYY').</summary>
        public static string LabelPattern(ProductivityPeriod period)
        {
            switch (period)
            {
                case ProductivityPeriod.Daily: return "MMM dd";
                case ProductivityPeriod.Monthly: return "MMM yyyy";
                case ProductivityPeriod.Yearly: return "yyyy";
                default: throw new ArgumentOutOfRangeException("period");
            }
        }

        /// <summary>
        /// Focus-log summary labels. Daily carries the year here and not on the chart, because a
        /// log line is read on its own while a chart axis has its neighbours for context.
        /// </summary>
        public static string FocusLogLabelPattern(ProductivityPeriod period)
        {
            return period == ProductivityPeriod.Daily ? "MMM dd, yyyy" : LabelPattern(period);
        }

        /// <summary>The start of the bucket a date falls in.</summary>
        public static DateTime BucketStart(ProductivityPeriod period, DateTime date)
        {
            switch (period)
            {
                case ProductivityPeriod.Daily: return StatsCalendar.StartOfDay(date);
                case ProductivityPeriod.Monthly: return new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
                case ProductivityPeriod.Yearly: return new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);
                default: throw new ArgumentOutOfRangeException("period");
            }
        }
    }
}
