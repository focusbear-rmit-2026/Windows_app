using System;
using System.Globalization;

namespace FocusBear.Core.ToDo
{
    /// <summary>`EISENHOWER_QUADRANT` — the API field is the number 1-4.</summary>
    public enum EisenhowerQuadrant
    {
        DoIt = 1,
        Decide = 2,
        Delegate = 3,
        Delete = 4,
    }

    /// <summary>`SORT_MODE` — the `sort_mode` query param of `GET /to-do`, and the order the list draws in.</summary>
    public enum ToDoSortMode
    {
        Smart,
        Chronological,
        DueDate,
    }

    /// <summary>The wire spellings of <see cref="ToDoSortMode"/>.</summary>
    public static class ToDoSortModeWire
    {
        public const string Smart = "SMART";
        public const string Chronological = "CHRONOLOGICAL";
        public const string DueDate = "DUE_DATE";

        public static string Of(ToDoSortMode mode)
        {
            switch (mode)
            {
                case ToDoSortMode.Smart: return Smart;
                case ToDoSortMode.Chronological: return Chronological;
                case ToDoSortMode.DueDate: return DueDate;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }

    /// <summary>`SubTask` — the backend's canonical <c>{ name, is_completed }</c>.</summary>
    public sealed class ToDoSubtask
    {
        public ToDoSubtask(string name, bool isCompleted, string id = null)
        {
            Name = name;
            IsCompleted = isCompleted;
            Id = id;
        }

        /// <summary>Only old rows carry one; a write never sends it.</summary>
        public string Id { get; }

        public string Name { get; }
        public bool IsCompleted { get; }
    }

    /// <summary>
    /// `TaskAssignee` as returned INSIDE a `GET /to-do` row (<c>{ id, username }</c>). The flat
    /// <c>assignee_id</c> key is absent from those rows entirely (backend#2414), which is why this
    /// relation exists at all — see <see cref="ToDoAssigneeChoices.AssigneeId"/>.
    /// </summary>
    public sealed class ToDoAssignee
    {
        public ToDoAssignee(string id, string username = null, string email = null)
        {
            Id = id;
            Username = username;
            Email = email;
        }

        public string Id { get; }
        public string Username { get; }
        public string Email { get; }
    }

    /// <summary>
    /// The two places the to-do API is looser than its own contract. Both are decoding rules, so they
    /// are functions a caller's JSON layer applies while filling a <see cref="ToDoTask"/> — this
    /// project still knows nothing about the transport.
    ///
    /// The Mac expresses these as a <c>LenientInt</c> Codable and a <c>@DefaultEmpty</c> property
    /// wrapper. C# needs neither wrapper type: a nullable int and a null string already carry
    /// "absent", so only the lenience itself had to come across.
    /// </summary>
    public static class ToDoWire
    {
        /// <summary>Swift <c>LenientInt</c>'s fallback when the value is none of the accepted shapes.</summary>
        public const int UnreadableInt = 0;

        /// <summary>
        /// `duration` comes back as a number on some rows and as a string ("300") on others, so a
        /// reader that insists on one shape drops the estimate on half the list.
        /// </summary>
        public static int LenientInt(object value)
        {
            if (value == null) return UnreadableInt;

            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (value is double doubleValue) return (int)doubleValue;
            if (value is decimal decimalValue) return (int)decimalValue;

            string text = value as string;
            if (text == null) return UnreadableInt;

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
            {
                // Swift truncates towards zero here; "12.9" is 12, not 13.
                return (int)parsedDouble;
            }
            return UnreadableInt;
        }

        /// <summary>
        /// A string the API is supposed to always send but sometimes does not: a completed row has come
        /// back with no `title`, and `PUT /to-do` answers a create with no `created_at`. One such row
        /// must not cost the caller the whole page, so an absent value reads as empty.
        /// </summary>
        public static string TextOrEmpty(string value) => value ?? string.Empty;
    }
}
