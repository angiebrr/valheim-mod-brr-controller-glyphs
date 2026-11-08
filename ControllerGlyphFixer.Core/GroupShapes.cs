// ------------------------------------------------------------------------------------------------
// GroupShapes.cs: decides how a prefix group's slots are laid out, to pick its line (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// How a prefix group's slots are laid out, which decides how its line is drawn.
    /// </summary>
    public enum GroupShape
    {
        /// <summary>Side by side: a line across, from the prefix to the last slot.</summary>
        Row,

        /// <summary>Stacked: a line down the column's left side.</summary>
        Column,

        /// <summary>Several rows and columns: an outline around the slots.</summary>
        Area,
    }

    /// <summary>
    /// Picks a prefix group's <see cref="GroupShape"/> from its slots' positions.
    /// </summary>
    public static class GroupShapes
    {
        /// <summary>How far apart (UI units) positions can be and still count as the same row or column.</summary>
        private const float Tolerance = 1f;

        /// <summary>
        /// Returns the shape of a group's slots.
        /// </summary>
        /// <param name="positions">Each slot's position (x, y), in the same space.</param>
        /// <returns>Row when all share a y (also for one or no slots), Column when all share an x,
        /// otherwise Area.</returns>
        public static GroupShape Of(IReadOnlyList<(float X, float Y)> positions)
        {
            if (positions.Count < 2 || Spread(positions.Select(p => p.Y)) <= Tolerance)
            {
                return GroupShape.Row;
            }

            return Spread(positions.Select(p => p.X)) <= Tolerance ? GroupShape.Column : GroupShape.Area;
        }

        /// <summary>
        /// Distance between the smallest and largest value.
        /// </summary>
        /// <param name="values">At least one value.</param>
        /// <returns>Max minus min.</returns>
        private static float Spread(IEnumerable<float> values)
        {
            float[] all = values.ToArray();
            return Math.Abs(all.Max() - all.Min());
        }
    }
}
