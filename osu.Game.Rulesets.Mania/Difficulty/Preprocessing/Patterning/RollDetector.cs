// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class RollDetector
    {
        private const double nerf_strength = 0.86;
        private const double onset_ms = 260.0;

        private const double run_ramp = 26.0;

        private const int run_cap = 90;

        private const int max_repeat_period = 4;
        private const int narrow_trill_window = 4;

        private const int lateral_run_cap = 400;

        private const double direction_consistency_lo = 0.42;
        private const double direction_consistency_hi = 0.50;

        private const double chord_density_lo = 0.14;
        private const double chord_density_hi = 0.34;

        public static double EvaluateFactorOf(ManiaRow row)
        {
            double gap = row.GapBefore;

            if (gap >= onset_ms)
                return 1.0;

            double runWeight = DiffUtils.ReverseLerp(shiftOrRepeatRunLength(row), 0.0, run_ramp);

            var lateral = lateralRun(row);

            if (lateral.length >= 2)
            {
                double rollGate = DiffUtils.Smoothstep(lateral.directionConsistency, direction_consistency_lo, direction_consistency_hi);

                runWeight = Math.Max(runWeight, DiffUtils.ReverseLerp(lateral.length, 0.0, run_ramp) * rollGate);
            }

            runWeight *= 1.0 - DiffUtils.Smoothstep(row.ChordDensity(), chord_density_lo, chord_density_hi);

            return Manipulation.Plateau(gap, onset_ms, nerf_strength * runWeight);
        }

        private static int shiftOrRepeatRunLength(ManiaRow row)
        {
            if (isNarrowTrill(row))
                return 0;

            int run = row.RunLengthBack(run_cap, 1, (current, previous) => ColumnPatternUtils.IsRoll(previous.Columns, current.Columns));

            for (int period = 2; period <= max_repeat_period; period++)
                run = Math.Max(run, repeatRunLength(row, period));

            return run;
        }

        /// <summary>
        /// How many times the row's exact column set has already repeated at a spacing of <paramref name="period"/> rows.
        /// </summary>
        private static int repeatRunLength(ManiaRow row, int period)
            => row.RunLengthBack(run_cap, period, (current, earlier) => ColumnPatternUtils.SameColumns(current.Columns, earlier.Columns));

        private static bool isNarrowTrill(ManiaRow row)
        {
            int firstColumn = -1;
            int secondColumn = -1;
            int counted = 0;

            for (ManiaRow? current = row; counted < narrow_trill_window && current != null; current = current.Previous())
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
            return counted >= narrow_trill_window && secondColumn >= 0 && Math.Abs(firstColumn - secondColumn) == 1;
        }

        private static (int length, double directionConsistency) lateralRun(ManiaRow row)
        {
            if (!row.IsSingleNote)
                return (0, 0.0);

            double pulse = row.LocalPulse;

            ManiaRow runStart = row;

            while (row.RowIndex - runStart.RowIndex < lateral_run_cap && isLateralStepInto(runStart, pulse))
                runStart = runStart.Previous()!;

            ManiaRow runEnd = row;

            while (runEnd.RowIndex - row.RowIndex < lateral_run_cap && isLateralStepInto(runEnd.Next(), pulse))
                runEnd = runEnd.Next()!;

            int directedPairs = 0;
            int sameDirectionPairs = 0;
            int previousDirection = 0;

            for (ManiaRow? current = runStart.Next(); current != null && current.RowIndex <= runEnd.RowIndex; current = current.Next())
            {
                int direction = Math.Sign(current.Columns[0] - current.Previous()!.Columns[0]);

                if (previousDirection != 0)
                {
                    directedPairs++;

                    if (direction == previousDirection)
                        sameDirectionPairs++;
                }

                previousDirection = direction;
            }

            double directionConsistency = directedPairs > 0 ? (double)sameDirectionPairs / directedPairs : 0.0;

            return (runEnd.RowIndex - runStart.RowIndex + 1, directionConsistency);
        }

        private static bool isLateralStepInto(ManiaRow? into, double pulse)
        {
            return into?.Previous() is ManiaRow from
                   && into.IsSingleNote && from.IsSingleNote
                   && into.Columns[0] != from.Columns[0]
                   && Manipulation.StepContinuity(into.StartTime - from.StartTime, pulse) > 0.0;
        }
    }
}
