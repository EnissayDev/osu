// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    /// <summary>
    /// The beatmap grouped into <see cref="ManiaRow"/>s, along with the readings around each row that would be
    /// too expensive to work out again for every note.
    /// </summary>
    public class ManiaMapData
    {
        public const int DEFAULT_DENSITY_RADIUS = 14;

        public readonly int TotalColumns;

        public IReadOnlyList<ManiaRow> Rows => rows;

        private readonly List<ManiaRow> rows = new List<ManiaRow>();

        private readonly double[] localPulses;

        /// <summary>
        /// Running totals of how many rows of each <see cref="RowKind"/> came before each index, so that the
        /// density over any window can be read as one subtraction.
        /// </summary>
        private readonly int[][] rowKindCounts;

        public ManiaMapData(IReadOnlyList<ManiaDifficultyHitObject> objects, int totalColumns)
        {
            TotalColumns = totalColumns;

            groupIntoRows(objects);

            localPulses = calculateLocalPulses();
            rowKindCounts = new[]
            {
                countRows(RowKind.Chord),
                countRows(RowKind.CrossHand),
                countRows(RowKind.SingleHandChord),
                countRows(RowKind.LargeChord),
            };
        }

        public ManiaRow? RowAt(int index) => index >= 0 && index < rows.Count ? rows[index] : null;

        /// <summary>
        /// The typical gap between rows around <paramref name="index"/>, as a median so that one break in the
        /// middle of a stream does not move it.
        /// </summary>
        public double LocalPulseAt(int index) => localPulses[index];

        public double ChordDensity(int index, int radius = DEFAULT_DENSITY_RADIUS) => density(RowKind.Chord, index, radius);
        public double CrossHandDensity(int index, int radius = DEFAULT_DENSITY_RADIUS) => density(RowKind.CrossHand, index, radius);
        public double SingleHandChordDensity(int index, int radius = DEFAULT_DENSITY_RADIUS) => density(RowKind.SingleHandChord, index, radius);
        public double LargeChordDensity(int index, int radius = DEFAULT_DENSITY_RADIUS) => density(RowKind.LargeChord, index, radius);

        /// <summary>
        /// Groups consecutive hit objects that start within chord tolerance of each other into <see cref="ManiaRow"/>s.
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

        /// <summary>
        /// The median row gap around each row. Taken as a median rather than a mean so that a single long gap in
        /// the middle of a stream reads as one break rather than as a slower stream.
        /// </summary>
        private double[] calculateLocalPulses()
        {
            const int pulse_radius = 16;

            double[] pulses = new double[rows.Count];
            double[] window = new double[2 * pulse_radius + 1];

            for (int i = 0; i < rows.Count; i++)
            {
                // The first row has no gap before it, so the window can never start earlier than the second.
                int lo = Math.Max(1, i - pulse_radius);
                int hi = Math.Min(rows.Count - 1, i + pulse_radius);
                int length = hi - lo + 1;

                if (length <= 0)
                {
                    pulses[i] = double.PositiveInfinity;
                    continue;
                }

                for (int k = 0; k < length; k++)
                    window[k] = rows[lo + k].StartTime - rows[lo + k - 1].StartTime;

                Array.Sort(window, 0, length);
                pulses[i] = window[(length - 1) / 2];
            }

            return pulses;
        }

        private int[] countRows(RowKind kind)
        {
            int[] counts = new int[rows.Count + 1];

            for (int i = 0; i < rows.Count; i++)
                counts[i + 1] = counts[i] + (isKind(rows[i], kind) ? 1 : 0);

            return counts;
        }

        private static bool isKind(ManiaRow row, RowKind kind)
        {
            switch (kind)
            {
                case RowKind.Chord:
                    return row.IsChord;

                case RowKind.CrossHand:
                    return row.Hand == ManiaHand.Both;

                case RowKind.SingleHandChord:
                    return row.Hand != ManiaHand.Both && row.Size >= 2;

                case RowKind.LargeChord:
                    return row.Size >= 3;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The share of the rows within <paramref name="radius"/> of <paramref name="index"/> that are of
        /// <paramref name="kind"/>.
        /// </summary>
        private double density(RowKind kind, int index, int radius)
        {
            int[] counts = rowKindCounts[(int)kind];

            int lo = Math.Max(0, index - radius);
            int hi = Math.Min(rows.Count - 1, index + radius);

            return (double)(counts[hi + 1] - counts[lo]) / (hi - lo + 1);
        }

        /// <summary>
        /// The kinds of row whose density the pattern detectors read. The order here indexes
        /// <see cref="rowKindCounts"/>.
        /// </summary>
        private enum RowKind
        {
            Chord,
            CrossHand,
            SingleHandChord,
            LargeChord,
        }
    }
}
