using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>The widths of one entry row's flexible columns, for a given available row width.</summary>
    public readonly struct BlockListColumnWidths
    {
        public BlockListColumnWidths(double name, double category, double type)
        {
            Name = name;
            Category = category;
            Type = type;
        }

        public double Name { get; }
        public double Category { get; }
        public double Type { get; }

        /// <summary>What the row's columns, avatar and remove button occupy in total, including the gaps.</summary>
        public double TotalRowWidth => Name + Category + Type + BlockListLayout.RowFixedWidth;
    }

    /// <summary>Every width the block-list screen needs, derived from the width it was actually given.</summary>
    public readonly struct BlockListMetrics
    {
        public BlockListMetrics(double railWidth, double panelWidth, double rowWidth, BlockListColumnWidths columns, double titleFontSize)
        {
            RailWidth = railWidth;
            PanelWidth = panelWidth;
            RowWidth = rowWidth;
            Columns = columns;
            TitleFontSize = titleFontSize;
        }

        public double RailWidth { get; }
        public double PanelWidth { get; }
        public double RowWidth { get; }
        public BlockListColumnWidths Columns { get; }
        public double TitleFontSize { get; }

        /// <summary>
        /// The width a row is drawn at. Never below <see cref="BlockListLayout.MinimumRowWidth"/>, so the
        /// name column survives a window too narrow for the design; the table clips its trailing edge instead.
        /// </summary>
        public double LaidOutRowWidth => Math.Max(RowWidth, BlockListLayout.MinimumRowWidth);
    }

    /// <summary>
    /// Pure sizing maths for the block-list screen, ported from the Mac's <c>BlockListLayout</c>.
    ///
    /// Every width the screen uses is derived here from the width it is handed, so the NAME column can
    /// never be squeezed to nothing in a narrow window, and the screen never demands a minimum width of
    /// the window hosting it. Sizing priority when space is tight is NAME, then CATEGORY, then TYPE.
    /// </summary>
    public static class BlockListLayout
    {
        // Left rail
        public const double RailIdealWidth = 340;
        public const double RailMinWidth = 190;
        public const double RailWidthFraction = 0.35;

        public static double RailWidth(double contentWidth)
        {
            double proportional = contentWidth * RailWidthFraction;
            return Math.Min(RailIdealWidth, Math.Max(RailMinWidth, proportional));
        }

        // Entry table columns
        public const double NameMinWidth = 120;
        public const double CategoryIdealWidth = 190;
        public const double CategoryMinWidth = 110;
        public const double TypeIdealWidth = 90;
        public const double TypeMinWidth = 62;
        public const double RemoveButtonWidth = 36;
        public const double AvatarSize = 34;
        public const double ColumnSpacing = 10;
        /// <summary>The entry table's vertical scroll bar sits beside the rows, so a row stops short of it.</summary>
        public const double ScrollerWidth = 16;

        /// <summary>The part of a row that never flexes: the avatar, the remove button and the four gaps.</summary>
        public const double RowFixedWidth = AvatarSize + RemoveButtonWidth + ColumnSpacing * 4;

        /// <summary>The narrowest row that still fits every column at its minimum.</summary>
        public const double MinimumRowWidth = NameMinWidth + CategoryMinWidth + TypeMinWidth + RowFixedWidth;

        /// <summary>
        /// Chooses the three flexible column widths. Category and type start at their minimums and grow
        /// toward their design widths with whatever is spare once the name has been promised
        /// <see cref="NameMinWidth"/>; anything left after both reach their design widths goes to the name.
        /// </summary>
        public static BlockListColumnWidths ColumnWidths(double rowWidth)
        {
            double flexible = Math.Max(rowWidth - RowFixedWidth, 0);
            double secondaryFloor = CategoryMinWidth + TypeMinWidth;
            double categoryHeadroom = CategoryIdealWidth - CategoryMinWidth;
            double typeHeadroom = TypeIdealWidth - TypeMinWidth;
            double secondaryHeadroom = categoryHeadroom + typeHeadroom;

            double secondaryBudget = Math.Max(flexible - NameMinWidth, secondaryFloor);
            double growth = Math.Min(Math.Max(secondaryBudget - secondaryFloor, 0), secondaryHeadroom);
            double share = secondaryHeadroom > 0 ? growth / secondaryHeadroom : 0;

            double category = CategoryMinWidth + categoryHeadroom * share;
            double type = TypeMinWidth + typeHeadroom * share;
            double name = Math.Max(NameMinWidth, flexible - category - type);
            return new BlockListColumnWidths(name, category, type);
        }

        // Panel title: Figma draws a 40pt title in a 1119pt panel; scale by the same ratio.
        public const double TitleFigmaFontSize = 40;
        public const double TitleFigmaPanelWidth = 1119;
        public const double TitleMinFontSize = 18;
        public const double TitleMaxFontSize = 28;

        public static double TitleFontSize(double panelWidth)
        {
            double proportional = panelWidth * TitleFigmaFontSize / TitleFigmaPanelWidth;
            return Math.Min(TitleMaxFontSize, Math.Max(TitleMinFontSize, proportional));
        }

        // Whole screen
        public const double OuterPadding = 16;
        public const double RailPanelGap = 16;
        public const double PanelInset = 25;

        public static BlockListMetrics Metrics(double screenWidth, double outerPadding = OuterPadding)
        {
            double contentWidth = Math.Max(screenWidth - outerPadding * 2, 0);
            double rail = RailWidth(contentWidth);
            double panel = Math.Max(contentWidth - rail - RailPanelGap, 0);
            double row = Math.Max(panel - PanelInset * 2 - ScrollerWidth, 0);
            return new BlockListMetrics(rail, panel, row, ColumnWidths(Math.Max(row, MinimumRowWidth)), TitleFontSize(panel));
        }
    }
}
