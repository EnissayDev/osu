// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds a section written as one shape walking sideways across the columns, which the hand can roll through
    /// in a single motion instead of hitting each note separately.
    /// </summary>
    /// <remarks>
    /// The same motion gets written two ways. Either the columns step by one each row, or a short figure repeats
    /// on a fixed period. Both are read here and the longer run wins.
    /// </remarks>
    public class RollDetector : RowManipulationDetector
    {
        /// <summary>
        /// How long a run has to be to be rolled through completely. The discount fades in over this rather than
        /// switching on, so one stray sideways step is worth almost nothing.
        /// </summary>
        private const double run_ramp = 26.0;

        private const int run_cap = 90;

        protected override double Onset => 260;

        protected override bool ContinuesChain(ManiaRow? cameFrom, ManiaRow from, ManiaRow to)
            => from.IsSingleNote && to.IsSingleNote && from.Columns[0] != to.Columns[0];

        protected override double StrengthOf(ManiaRow row)
        {
            double runWeight = DiffUtils.ReverseLerp(shiftOrRepeatRunLength(row), 0.0, run_ramp);

            ManiaChain lateral = lateralRun(row);

            if (lateral.RowCount >= 2)
                runWeight = Math.Max(runWeight, DiffUtils.ReverseLerp(lateral.RowCount, 0.0, run_ramp) * rollGateOf(row, lateral));

            // A section full of chords is not one shape walking sideways, whatever its single notes are doing.
            runWeight *= DiffUtils.Smoothstep(row.ChordDensity(), 0.34, 0.14);

            return 0.86 * runWeight;
        }

        /// <summary>
        /// How much a run of single notes that never repeats a column is really a roll rather than a stream that
        /// happens not to double back. A roll keeps going the same way and keeps taking the same size of step.
        /// </summary>
        private static double rollGateOf(ManiaRow row, ManiaChain lateral)
        {
            // Pure rolls measure around 0.50 because they run off one edge of the keyboard and start over, while
            // a stream that merely leans one way sits well below that.
            return DiffUtils.Smoothstep(directionConsistencyOf(lateral), 0.42, 0.50)
                   * DiffUtils.Smoothstep(stepRepetitionAround(row), 0.22, 0.78);
        }

        private ManiaChain lateralRun(ManiaRow row) => ChainBefore(row, rowCap: 400).JoinedWith(ChainAfter(row, rowCap: 400));

        /// <summary>
        /// How much the section around <paramref name="row"/> keeps taking the same size of step between columns.
        /// 0 is what stepping at random would look like, 1 is one step size and nothing else.
        /// </summary>
        private static double stepRepetitionAround(ManiaRow row)
        {
            int totalColumns = row.TotalColumns;

            if (totalColumns < 3)
                return 1.0;

            // Steps are counted around the keyboard, so stepping off the last column back onto the first is the
            // same step as any other. That is exactly what a roll does when it starts over.
            int[] stepCounts = new int[totalColumns];
            int steps = 0;

            foreach (var current in row.RowsAround(8))
            {
                if (!current.IsSingleNote || current.Previous() is not ManiaRow previous || !previous.IsSingleNote)
                    continue;

                int step = (current.Columns[0] - previous.Columns[0] + totalColumns) % totalColumns;

                if (step == 0)
                    continue;

                stepCounts[step]++;
                steps++;
            }

            if (steps == 0)
                return 1.0;

            int mostUsed = 0;

            foreach (int count in stepCounts)
                mostUsed = Math.Max(mostUsed, count);

            double chanceShare = 1.0 / (totalColumns - 1);

            return Math.Max(0.0, ((double)mostUsed / steps - chanceShare) / (1.0 - chanceShare));
        }

        /// <summary>
        /// The longer of the two runs a roll can be written as: columns stepping sideways one at a time, or a
        /// short figure repeating on a fixed period.
        /// </summary>
        private static int shiftOrRepeatRunLength(ManiaRow row)
        {
            // Two columns alternating is a trill. It has to be played as written rather than rolled through, and
            // without this check it would look like a figure repeating every two rows.
            if (isNarrowTrill(row))
                return 0;

            int run = shiftRunLength(row);

            for (int period = 2; period <= 4; period++)
                run = Math.Max(run, repeatRunLength(row, period));

            return run;
        }

        /// <summary>
        /// How many rows back to back have shifted this row's whole shape onto neighbouring columns.
        /// </summary>
        private static int shiftRunLength(ManiaRow row)
        {
            int steps = 0;

            for (ManiaRow current = row;
                 steps < run_cap && current.Previous() is ManiaRow previous && ColumnPatternUtils.IsRoll(previous.Columns, current.Columns);
                 current = previous)
            {
                steps++;
            }

            return steps;
        }

        /// <summary>
        /// How many times the row's exact column set has already repeated at a spacing of <paramref name="period"/> rows.
        /// </summary>
        private static int repeatRunLength(ManiaRow row, int period)
        {
            int steps = 0;

            for (ManiaRow current = row;
                 steps < run_cap && current.Offset(-period) is ManiaRow earlier && ColumnPatternUtils.SameColumns(current.Columns, earlier.Columns);
                 current = earlier)
            {
                steps++;
            }

            return steps;
        }

        /// <summary>
        /// Whether the last few rows are single notes using two adjacent columns and nothing else.
        /// </summary>
        private static bool isNarrowTrill(ManiaRow row)
        {
            const int window = 4;

            int firstColumn = -1;
            int secondColumn = -1;
            int counted = 0;

            for (ManiaRow? current = row; counted < window && current != null; current = current.Previous())
            {
                if (!current.IsSingleNote)
                    return false;

                int column = current.Columns[0];
                counted++;

                if (column == firstColumn || column == secondColumn)
                    continue;

                if (firstColumn < 0)
                    firstColumn = column;
                else if (secondColumn < 0)
                    secondColumn = column;
                else
                    return false;
            }

            // A full window of single notes using exactly two adjacent columns is required.
            return counted >= window && secondColumn >= 0 && Math.Abs(firstColumn - secondColumn) == 1;
        }

        /// <summary>
        /// The share of the chain's steps that carry on in the same direction as the step before them.
        /// </summary>
        private static double directionConsistencyOf(ManiaChain lateral)
        {
            int directedPairs = 0;
            int sameDirectionPairs = 0;
            int previousDirection = 0;

            foreach (var row in lateral.RowsAfterFirst)
            {
                int direction = Math.Sign(row.Columns[0] - row.Previous()!.Columns[0]);

                if (previousDirection != 0)
                {
                    directedPairs++;

                    if (direction == previousDirection)
                        sameDirectionPairs++;
                }

                previousDirection = direction;
            }

            return directedPairs > 0 ? (double)sameDirectionPairs / directedPairs : 0.0;
        }
    }
}
