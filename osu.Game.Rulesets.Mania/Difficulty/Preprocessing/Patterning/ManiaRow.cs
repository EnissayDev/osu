// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    /// <summary>
    /// A data class that stores mania row information. Includes grace notes (notes offset by at most <see cref="ChordUtils.CHORD_TOLERANCE_MS"/>).
    /// </summary>
    public class ManiaRow
    {
        public int Size => Columns.Length;

        /// <summary>Sorted column indices of every note in this row.</summary>
        public readonly int[] Columns;

        public readonly double StartTime;

        public readonly List<ManiaDifficultyHitObject> Objects;

        /// <summary>
        /// The index of this row out of the list of all rows.
        /// </summary>
        public readonly int RowIndex;

        private readonly ManiaMapData mapData;

        public ManiaRow(int[] columns, double startTime, List<ManiaDifficultyHitObject> objects, int index, ManiaMapData mapData)
        {
            Columns = columns;
            StartTime = startTime;
            Objects = objects;
            RowIndex = index;
            this.mapData = mapData;
        }

        public bool IsChord => Size > 1;

        public bool IsSingleNote => Size == 1;

        public bool IsJump => Size == 2;

        public ManiaRow? Next(int offset = 0) => mapData.RowAt(RowIndex + (offset + 1));
        public ManiaRow? Previous(int offset = 0) => mapData.RowAt(RowIndex - (offset + 1));

        public bool IsSameRow(ManiaRow other) => RowIndex == other.RowIndex;
    }
}
