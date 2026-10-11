using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// A point in the board's own coordinate space. Core is WPF-free, so it carries its own two-field
    /// point rather than <c>System.Windows.Point</c>; the host converts at the seam.
    /// </summary>
    public struct BoardPoint : IEquatable<BoardPoint>
    {
        public BoardPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool Equals(BoardPoint other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is BoardPoint && Equals((BoardPoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X, Y);
        }
    }

    /// <summary>One column's frame, in the board's own coordinate space.</summary>
    public struct BoardRect : IEquatable<BoardRect>
    {
        public BoardRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }

        public double Area
        {
            get { return Width * Height; }
        }

        /// <summary>
        /// <c>CGRect.contains(_:)</c>: the origin edges are inside and the far edges are NOT — the
        /// interval is half-open on both axes. Two columns laid side by side therefore never both
        /// claim the seam between them, which is the case the Mac's "between columns" test pins.
        /// </summary>
        public bool Contains(BoardPoint point)
        {
            return point.X >= X && point.X < X + Width
                && point.Y >= Y && point.Y < Y + Height;
        }

        public bool Equals(BoardRect other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y)
                && Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        public override bool Equals(object obj)
        {
            return obj is BoardRect && Equals((BoardRect)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Width.GetHashCode();
                return (hash * 397) ^ Height.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}) {2}x{3}", X, Y, Width, Height);
        }
    }

    /// <summary>
    /// The geometry behind dragging a task card onto another column.
    ///
    /// The Mac's board tracks the drag itself — a gesture plus the columns' own frames — rather than
    /// the platform's drag-and-drop, which draws no drag image inside the app's own windows. Windows
    /// has the opposite problem (WPF's <c>DragDrop</c> works, but only against visuals), so what
    /// transfers is not the gesture plumbing but the QUESTION it answers: given a pointer and the
    /// columns' frames, which column is the card over? That answer decides the drop highlight and the
    /// write, and it has to be the same answer on both platforms or the same drag means two things.
    /// </summary>
    public static class ProjectBoardDrag
    {
        /// <summary>
        /// How far the pointer must travel before a press on a card becomes a drag rather than the
        /// click that opens the task editor.
        /// </summary>
        public const double MinimumDistance = 4;

        /// <summary>
        /// The column under <paramref name="point"/>, or null when the pointer is between columns or
        /// off the board entirely.
        ///
        /// The SMALLEST containing frame wins. In list mode a column is a section stacked with the
        /// others and nothing overlaps, so the rule is invisible there; it matters the moment a
        /// column's frame is nested inside another's, where it keeps the answer the column actually
        /// under the pointer rather than whichever happened to be registered first. Ties break on the
        /// column id — ordinally, since ids are opaque — so the highlight never flickers between two
        /// equally sized columns as the frame map is re-enumerated.
        ///
        /// Frames must all be in ONE coordinate space, and the host owns which: AppKit's origin is
        /// bottom-left and WPF's is top-left, and this only ever compares a point against rectangles
        /// given in the same terms as itself.
        /// </summary>
        public static string ColumnId(BoardPoint point, IDictionary<string, BoardRect> frames)
        {
            if (frames == null) return null;
            return frames
                .Where(frame => frame.Value.Contains(point))
                .OrderBy(frame => frame.Value.Area)
                .ThenBy(frame => frame.Key, StringComparer.Ordinal)
                .Select(frame => frame.Key)
                .FirstOrDefault();
        }
    }
}
