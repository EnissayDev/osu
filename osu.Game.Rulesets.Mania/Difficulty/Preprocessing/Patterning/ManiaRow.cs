// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
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

        public readonly int TotalColumns;

        public readonly int ColumnMask;

        public RowLoad Load = null!;

        public double Repetition;

        /// <summary>
        /// How evenly the repeated cycle spreads its notes across the columns it actually uses.
        /// </summary>
        public double RepetitionEvenness;

        private readonly IReadOnlyList<ManiaRow> rows;

        public ManiaRow(int[] columns, double startTime, List<ManiaDifficultyHitObject> objects, int index, int totalColumns, IReadOnlyList<ManiaRow> rows)
        {
            Columns = columns;
            StartTime = startTime;
            Objects = objects;
            RowIndex = index;
            TotalColumns = totalColumns;
            this.rows = rows;

            foreach (int column in columns)
                ColumnMask |= 1 << column;
        }

        /// <summary>
        /// How long it has been since the previous row, or infinity if this is the first row of the map.
        /// </summary>
        public double GapBefore => Previous() is ManiaRow previous ? StartTime - previous.StartTime : double.PositiveInfinity;

        /// <summary>
        /// The row <paramref name="offset"/> places after this one, negative for earlier rows.
        /// </summary>
        public ManiaRow? Offset(int offset)
        {
            int index = RowIndex + offset;
            return index >= 0 && index < rows.Count ? rows[index] : null;
        }

        public ManiaRow? Previous(int skip = 0) => Offset(-(skip + 1));

        /// <summary>
        /// This row together with the rows up to <paramref name="radius"/> places on either side of it.
        /// </summary>
        public IEnumerable<ManiaRow> RowsAround(int radius)
        {
            int first = Math.Max(RowIndex - radius, 0);
            int last = Math.Min(RowIndex + radius, rows.Count - 1);

            for (int index = first; index <= last; index++)
                yield return rows[index];
        }

        public double ChordSizeOn(ManiaHand hand)
        {
            double size = 0.0;

            foreach (int column in Columns)
                size += HandLayout.ShareOf(column, TotalColumns, hand);

            return size;
        }
    }
}
