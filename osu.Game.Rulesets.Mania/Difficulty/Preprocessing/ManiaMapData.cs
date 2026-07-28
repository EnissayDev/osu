// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaMapData
    {
        public IReadOnlyList<ManiaRow> Rows => rows;
        private readonly List<ManiaRow> rows = new List<ManiaRow>();

        public ManiaRow? RowAt(int index) => index >= 0 && index < rows.Count ? rows[index] : null;

        public ManiaMapData(IReadOnlyList<ManiaDifficultyHitObject> objects)
        {
            groupIntoRows(objects);
        }

        /// <summary>
        /// Groups consecutive hit objects that start within chord tolerance of each other into <see cref="Row"/>s.
        /// </summary>
        private void groupIntoRows(IReadOnlyList<ManiaDifficultyHitObject> objects)
        {
            int i = 0;

            while (i < objects.Count)
            {
                double rowStart = objects[i].StartTime;
                var members = new List<ManiaDifficultyHitObject>();

                while (i < objects.Count && Math.Abs(objects[i].StartTime - rowStart) <= ChordUtils.CHORD_TOLERANCE_MS)
                {
                    members.Add(objects[i]);
                    i++;
                }

                int[] columns = members.Select(m => m.Column).Order().ToArray();

                ManiaRow row = new ManiaRow(columns, rowStart, members, rows.Count, this);

                foreach (var rowMember in members)
                    rowMember.Row = row;

                rows.Add(row);
            }
        }
    }
}
