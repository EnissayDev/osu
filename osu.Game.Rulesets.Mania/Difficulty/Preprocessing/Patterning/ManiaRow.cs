// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    /// <summary>
    /// Every hit object that starts close enough together to be hit at the same time. Patterns in
    /// osu!mania are shapes built out of rows rather than out of single notes, so the detectors read the map
    /// through these instead of through <see cref="ManiaDifficultyHitObject"/>s.
    /// </summary>
    public class ManiaRow
    {
        /// <summary>
        /// How many notes this row presses at once.
        /// </summary>
        public int Size => Columns.Length;

        /// <summary>
        /// Sorted column indices of every note in this row.
        /// </summary>
        public readonly int[] Columns;

        public readonly double StartTime;

        public readonly List<ManiaDifficultyHitObject> Objects;

        /// <summary>
        /// This row's index in the map's list of rows.
        /// </summary>
        public readonly int RowIndex;

        /// <summary>
        /// The hand, or hands, this row is played with.
        /// </summary>
        public readonly ManiaHand Hand;

        private readonly ManiaMapData mapData;

        public ManiaRow(int[] columns, double startTime, List<ManiaDifficultyHitObject> objects, int index, ManiaMapData mapData)
        {
            Columns = columns;
            StartTime = startTime;
            Objects = objects;
            RowIndex = index;
            Hand = handOf(columns, mapData.TotalColumns);
            this.mapData = mapData;
        }

        public bool IsChord => Size > 1;

        public bool IsSingleNote => Size == 1;

        public bool IsJump => Size == 2;

        /// <summary>
        /// Whether this row is played entirely with one hand.
        /// </summary>
        public bool IsHandLocal => Hand != ManiaHand.Both;

        /// <summary>
        /// How long it has been since the previous row, or infinity if this is the first row of the map.
        /// </summary>
        public double GapBefore => Previous() is ManiaRow previous ? StartTime - previous.StartTime : double.PositiveInfinity;

        public int TotalColumns => mapData.TotalColumns;
        public double LocalPulse => mapData.LocalPulseAt(RowIndex);
        public double ChordDensity(int radius = ManiaMapData.DEFAULT_DENSITY_RADIUS) => mapData.ChordDensity(RowIndex, radius);
        public double CrossHandDensity(int radius = ManiaMapData.DEFAULT_DENSITY_RADIUS) => mapData.CrossHandDensity(RowIndex, radius);
        public double SingleHandChordDensity(int radius = ManiaMapData.DEFAULT_DENSITY_RADIUS) => mapData.SingleHandChordDensity(RowIndex, radius);
        public double LargeChordDensity(int radius = ManiaMapData.DEFAULT_DENSITY_RADIUS) => mapData.LargeChordDensity(RowIndex, radius);

        /// <summary>
        /// The row <paramref name="offset"/> places after this one, negative for earlier rows.
        /// </summary>
        public ManiaRow? Offset(int offset) => mapData.RowAt(RowIndex + offset);

        public ManiaRow? Next(int skip = 0) => Offset(skip + 1);

        public ManiaRow? Previous(int skip = 0) => Offset(-(skip + 1));

        /// <summary>
        /// This row together with the rows up to <paramref name="radius"/> places on either side of it.
        /// </summary>
        public RowRange RowsAround(int radius)
        {
            ManiaRow first = this;

            while (RowIndex - first.RowIndex < radius && first.Previous() is ManiaRow earlier)
                first = earlier;

            return new RowRange(first, RowIndex + radius);
        }

        public RowRange RowsUpTo(ManiaRow last) => new RowRange(this, last.RowIndex);

        /// <summary>
        /// This row first, then the rows before and after it that start within <paramref name="radiusMs"/> of
        /// <paramref name="center"/>.
        /// </summary>
        public IEnumerable<ManiaRow> RowsWithin(double radiusMs, double center)
        {
            yield return this;

            for (ManiaRow? earlier = Previous(); earlier != null && center - earlier.StartTime <= radiusMs; earlier = earlier.Previous())
                yield return earlier;

            for (ManiaRow? later = Next(); later != null && later.StartTime - center <= radiusMs; later = later.Next())
                yield return later;
        }

        /// <summary>
        /// The earliest row starting within <paramref name="radiusMs"/> before this one.
        /// </summary>
        public ManiaRow FirstWithin(double radiusMs)
        {
            ManiaRow first = this;

            while (first.Previous() is ManiaRow earlier && StartTime - earlier.StartTime <= radiusMs)
                first = earlier;

            return first;
        }

        /// <summary>
        /// The latest row starting within <paramref name="radiusMs"/> after this one.
        /// </summary>
        public ManiaRow LastWithin(double radiusMs)
        {
            ManiaRow last = this;

            while (last.Next() is ManiaRow later && later.StartTime - StartTime <= radiusMs)
                last = later;

            return last;
        }

        public bool Contains(int column)
        {
            foreach (int c in Columns)
            {
                if (c == column)
                    return true;
            }

            return false;
        }

        public bool IsSameRow(ManiaRow other) => RowIndex == other.RowIndex;

        /// <summary>
        /// Which hand plays a row, taking the columns to be split evenly down the middle. On odd keymodes the
        /// middle column belongs to neither side, so a row using only it is not a cross-hand row.
        /// </summary>
        private static ManiaHand handOf(int[] columns, int totalColumns)
        {
            bool hasLeft = false;
            bool hasRight = false;

            foreach (int column in columns)
            {
                if (column < totalColumns / 2)
                    hasLeft = true;
                else if (column >= (totalColumns + 1) / 2)
                    hasRight = true;
            }

            if (hasLeft && hasRight)
                return ManiaHand.Both;

            return hasRight ? ManiaHand.Right : ManiaHand.Left;
        }
    }
}
