// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public static class ManiaManipulationDifficultyPreprocessor
    {
        private const double high_speed_nerf = 0.72;
        private const double period_ramp = 26.0;
        private const int run_cap = 90;
        private const int max_period = 4;
        private const int max_shift = 1;
        private const double speed_hi_ms = 82.0;
        private const double speed_lo_ms = 30.0;

        private const double movement_fast_ms = 60.0;
        private const int movement_cap = 400;
        private const double movement_taper_lo = 70.0;
        private const double movement_taper_hi = 160.0;
        private const double movement_stamina_relief = 0.9;

        private const double movement_dir_lo = 0.42;
        private const double movement_dir_hi = 0.50;

        private const int movement_chord_window = 14;
        private const double movement_chord_lo = 0.14;
        private const double movement_chord_hi = 0.34;

        private const double mash_nerf = 1.0;
        private const double mash_speed_hi_ms = 50.0;
        private const double mash_speed_lo_ms = 33.0;
        private const double mash_ramp_lo = 3.0;
        private const double mash_ramp_hi = 9.0;
        private const int mash_run_cap = 64;
        private const double mash_chord_lo = 0.06;
        private const double mash_chord_hi = 0.18;
        private const double mash_cross_lo = 0.10;
        private const double mash_cross_hi = 0.22;

        private const double jumptrill_nerf = 0.97;
        private const double jumptrill_run_lo = 3.0;
        private const double jumptrill_run_hi = 8.0;
        private const int jumptrill_run_cap = 200;
        private const double jumptrill_speed_hi_ms = 130.0;
        private const double jumptrill_speed_lo_ms = 35.0;
        private const double jumptrill_vfast_hi_ms = 50.0;
        private const double jumptrill_vfast_lo_ms = 36.0;
        private const double jumptrill_vfast_taper = 0.6;
        private const double jumptrill_bridge_ms = 130.0;
        private const double jumptrill_cross_lo = 0.06;
        private const double jumptrill_cross_hi = 0.16;

        private const double stamina_buff = 0.52;
        private const double stamina_speed_hi_ms = 85.0;
        private const double stamina_speed_lo_ms = 62.0;
        private const double stamina_speed_vfast_hi_ms = 62.0;
        private const double stamina_speed_vfast_lo_ms = 48.0;
        private const double stamina_vfast_taper = 0.72;
        private const double stamina_run_lo = 6.0;
        private const double stamina_run_hi = 30.0;
        private const int stamina_run_cap = 256;

        /// <summary>
        /// Groups objects into rows, assigning a manipulation factor to the notes in each row based on how much manipulation affects the difficulty of the note.
        /// </summary>
        /// <param name="mapData">The structured data of the map used to calculate manipulation attributes for the notes.</param>
        /// <param name="totalColumns">The number of columns of the beatmap, used to split rows into left/right hands.</param>
        public static void ProcessAndAssign(ManiaMapData mapData, int totalColumns)
        {
            if (mapData.Rows.Count == 0)
                return;

            var rows = mapData.Rows;

            bool[] handLocal = new bool[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                handLocal[i] = handOf(rows[i].Columns, totalColumns) != Hand.Both;

            for (int i = 0; i < rows.Count; i++)
            {
                double timeSincePreviousRow = i > 0 ? rows[i].StartTime - rows[i - 1].StartTime : double.PositiveInfinity;

                double manipulationFactor = Math.Min(
                    Math.Min(
                        rollAndPatternFactor(rows, i, timeSincePreviousRow),
                        jumptrillFactor(rows, handLocal, i, timeSincePreviousRow, totalColumns)),
                    mashFactor(rows, handLocal, i, timeSincePreviousRow));

                double staminaFactor = staminaFactorFor(rows, i, timeSincePreviousRow);

                foreach (var member in rows[i].Objects)
                {
                    if (manipulationFactor < 1.0)
                        member.ManipulationFactor = manipulationFactor;

                    if (staminaFactor > 1.0)
                        member.StaminaFactor = staminaFactor;
                }
            }
        }

        private static int countRunBackward(int row, int cap, Func<int, bool> condition)
        {
            int run = 0;

            while (run < cap && row - 1 >= 0 && condition(row - 1))
            {
                run++;
                row--;
            }

            return run;
        }

        private static double rollAndPatternFactor(IReadOnlyList<ManiaRow> rows, int row, double timeSincePreviousRow)
        {
            double speedScale = DiffUtils.Smoothstep(speed_hi_ms - timeSincePreviousRow, 0.0, speed_hi_ms - speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int patternRun = longestRollOrPeriodicRun(rows, row);
            double runWeight = DiffUtils.ReverseLerp(patternRun, 0.0, period_ramp);

            int moveRun = movementRun(rows, row, out double directionConsistency);

            if (moveRun >= 2)
            {
                double staminaRelief = movement_stamina_relief * DiffUtils.Smoothstep(moveRun, movement_taper_lo, movement_taper_hi);
                double rollGate = DiffUtils.Smoothstep(directionConsistency, movement_dir_lo, movement_dir_hi);
                double chordGate = 1.0 - DiffUtils.Smoothstep(localChordDensity(rows, row), movement_chord_lo, movement_chord_hi);

                double moveWeight = DiffUtils.ReverseLerp(moveRun, 0.0, period_ramp) * (1.0 - staminaRelief) * rollGate * chordGate;
                runWeight = Math.Max(runWeight, moveWeight);
            }

            if (runWeight <= 0.0)
                return 1.0;

            return 1.0 - high_speed_nerf * runWeight * speedScale;
        }

        private static int longestRollOrPeriodicRun(IReadOnlyList<ManiaRow> rows, int row)
        {
            int run = countRunBackward(row, run_cap, earlier => columnShift(rows[earlier].Columns, rows[earlier + 1].Columns) != 0);

            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRunLength(rows, row, period));

            return run;
        }

        private static int periodRunLength(IReadOnlyList<ManiaRow> rows, int row, int period)
        {
            int run = 0;

            while (run < run_cap && row - period >= 0 && sameColumns(rows[row].Columns, rows[row - period].Columns))
            {
                run++;
                row -= period;
            }

            return run;
        }

        private static double jumptrillFactor(IReadOnlyList<ManiaRow> rows, bool[] handLocal, int row, double timeSincePreviousRow, int totalColumns)
        {
            if (!handLocal[row])
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(jumptrill_speed_hi_ms - timeSincePreviousRow, 0.0, jumptrill_speed_hi_ms - jumptrill_speed_lo_ms)
                                * (1.0 - jumptrill_vfast_taper * DiffUtils.Smoothstep(jumptrill_vfast_hi_ms - timeSincePreviousRow, 0.0, jumptrill_vfast_hi_ms - jumptrill_vfast_lo_ms));

            if (speedScale <= 0.0)
                return 1.0;

            int run = jumptrillChainLength(rows, handLocal, row, totalColumns);

            double runWeight = DiffUtils.Smoothstep(run, jumptrill_run_lo, jumptrill_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double crossGate = isStrictJumptrill(rows, row)
                ? 1.0
                : 1.0 - DiffUtils.Smoothstep(localCrossHandDensity(handLocal, row), jumptrill_cross_lo, jumptrill_cross_hi);

            return 1.0 - jumptrill_nerf * runWeight * speedScale * crossGate;
        }

        private static int jumptrillChainLength(IReadOnlyList<ManiaRow> rows, bool[] handLocal, int row, int totalColumns)
        {
            int run = 1;

            for (int k = row;
                 run < jumptrill_run_cap && k - 1 >= 0 && handLocal[k - 1]
                 && rows[k].StartTime - rows[k - 1].StartTime <= jumptrill_bridge_ms
                 && isMashStep(rows[k], rows[k - 1], totalColumns);
                 k--)
            {
                run++;
            }

            for (int k = row;
                 run < jumptrill_run_cap && k + 1 < rows.Count && handLocal[k + 1]
                 && rows[k + 1].StartTime - rows[k].StartTime <= jumptrill_bridge_ms
                 && isMashStep(rows[k + 1], rows[k], totalColumns);
                 k++)
            {
                run++;
            }

            return run;
        }

        /// <summary>
        /// True when <paramref name="a"/> and <paramref name="b"/> (both one-hand rows) form a mashable jumptrill step:
        /// opposite hands with at least one row a one-hand jump (the note that makes it a chord-trill rather than a plain
        /// handstream), or the same hand as a single-note one-column roll.
        /// </summary>
        private static bool isMashStep(ManiaRow a, ManiaRow b, int totalColumns)
        {
            if (handOf(a.Columns, totalColumns) != handOf(b.Columns, totalColumns))
                return a.Size >= 2 || b.Size >= 2;

            return a.Size == 1 && b.Size == 1 && Math.Abs(a.Columns[0] - b.Columns[0]) == 1;
        }

        private static bool isStrictJumptrill(IReadOnlyList<ManiaRow> rows, int row)
        {
            if (!rows[row].IsJump)
                return false;

            bool recursBefore = row - 2 >= 0 && sameColumns(rows[row].Columns, rows[row - 2].Columns) && !sameColumns(rows[row].Columns, rows[row - 1].Columns);
            bool recursAfter = row + 2 < rows.Count && sameColumns(rows[row].Columns, rows[row + 2].Columns) && !sameColumns(rows[row].Columns, rows[row + 1].Columns);

            return recursBefore || recursAfter;
        }

        private static double mashFactor(IReadOnlyList<ManiaRow> rows, bool[] handLocal, int row, double timeSincePreviousRow)
        {
            if (!handLocal[row])
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(mash_speed_hi_ms - timeSincePreviousRow, 0.0, mash_speed_hi_ms - mash_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int run = 0;

            for (int k = row; run < mash_run_cap && k - 1 >= 0 && handLocal[k - 1] && rows[k].StartTime - rows[k - 1].StartTime <= mash_speed_hi_ms; k--)
                run++;

            double runWeight = DiffUtils.Smoothstep(run, mash_ramp_lo, mash_ramp_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = DiffUtils.Smoothstep(localChordDensity(rows, row), mash_chord_lo, mash_chord_hi);
            double crossGate = 1.0 - DiffUtils.Smoothstep(localCrossHandDensity(handLocal, row), mash_cross_lo, mash_cross_hi);

            return 1.0 - mash_nerf * runWeight * speedScale * chordGate * crossGate;
        }

        private enum Hand
        {
            Left,
            Right,
            Both,
        }

        /// <summary>
        /// Which hand plays the row: <see cref="Hand.Left"/> or <see cref="Hand.Right"/> for a one-hand row
        /// (a middle-only or empty row counts as <see cref="Hand.Left"/>), or <see cref="Hand.Both"/> when it
        /// spans both hands. A row is hand-local exactly when this is not <see cref="Hand.Both"/>.
        /// </summary>
        private static Hand handOf(int[] columns, int totalColumns)
        {
            bool hasLeft = false;
            bool hasRight = false;

            foreach (int c in columns)
            {
                if (c < totalColumns / 2)
                    hasLeft = true;
                else if (c >= (totalColumns + 1) / 2)
                    hasRight = true;
            }

            if (hasLeft && hasRight)
                return Hand.Both;

            return hasRight ? Hand.Right : Hand.Left;
        }

        private static double localCrossHandDensity(bool[] handLocal, int row)
        {
            int lo = Math.Max(0, row - movement_chord_window);
            int hi = Math.Min(handLocal.Length - 1, row + movement_chord_window);

            int crossCount = 0;

            for (int r = lo; r <= hi; r++)
            {
                if (!handLocal[r])
                    crossCount++;
            }

            return (double)crossCount / (hi - lo + 1);
        }

        private static int movementRun(IReadOnlyList<ManiaRow> rows, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (!rows[row].IsSingleNote)
                return 0;

            int runStart = row;
            while (row - runStart < movement_cap && isFastLateralMove(rows, runStart))
                runStart--;

            int runEnd = row;
            while (runEnd - row < movement_cap && isFastLateralMove(rows, runEnd + 1))
                runEnd++;

            int dirPairs = 0;
            int sameDirCount = 0;
            int previousDirection = 0;

            for (int k = runStart + 1; k <= runEnd; k++)
            {
                int direction = Math.Sign(rows[k].Columns[0] - rows[k - 1].Columns[0]);

                if (previousDirection != 0)
                {
                    dirPairs++;
                    if (direction == previousDirection)
                        sameDirCount++;
                }

                previousDirection = direction;
            }

            if (dirPairs > 0)
                directionConsistency = (double)sameDirCount / dirPairs;

            return runEnd - runStart + 1;
        }

        private static bool isFastLateralMove(IReadOnlyList<ManiaRow> rows, int k)
        {
            return k - 1 >= 0 && k < rows.Count
                              && rows[k].IsSingleNote && rows[k - 1].IsSingleNote
                              && rows[k].Columns[0] != rows[k - 1].Columns[0]
                              && rows[k].StartTime - rows[k - 1].StartTime < movement_fast_ms;
        }

        private static double localChordDensity(IReadOnlyList<ManiaRow> rows, int row)
        {
            int lo = Math.Max(0, row - movement_chord_window);
            int hi = Math.Min(rows.Count - 1, row + movement_chord_window);

            int chordCount = 0;

            for (int r = lo; r <= hi; r++)
            {
                if (rows[r].IsChord)
                    chordCount++;
            }

            return (double)chordCount / (hi - lo + 1);
        }

        private static double staminaFactorFor(IReadOnlyList<ManiaRow> rows, int row, double timeSincePreviousRow)
        {
            if (!rows[row].IsJump)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(stamina_speed_hi_ms - timeSincePreviousRow, 0.0, stamina_speed_hi_ms - stamina_speed_lo_ms)
                                * (1.0 - stamina_vfast_taper * DiffUtils.Smoothstep(stamina_speed_vfast_hi_ms - timeSincePreviousRow, 0.0, stamina_speed_vfast_hi_ms - stamina_speed_vfast_lo_ms));

            if (speedScale <= 0.0)
                return 1.0;

            int run = 1 + countRunBackward(row, stamina_run_cap - 1, earlier =>
                rows[earlier].IsJump && rows[earlier + 1].StartTime - rows[earlier].StartTime <= stamina_speed_hi_ms);

            double runWeight = DiffUtils.Smoothstep(run, stamina_run_lo, stamina_run_hi);
            return 1.0 + stamina_buff * speedScale * runWeight;
        }

        private static bool sameColumns(int[] a, int[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// If <paramref name="b"/> is <paramref name="a"/> with every column shifted by the same constant
        /// k (with 0 &lt; |k| &lt;= <see cref="max_shift"/>), returns k; otherwise returns 0.
        /// </summary>
        private static int columnShift(int[] a, int[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            int k = b[0] - a[0];

            if (k == 0 || Math.Abs(k) > max_shift)
                return 0;

            for (int i = 1; i < a.Length; i++)
            {
                if (b[i] - a[i] != k)
                    return 0;
            }

            return k;
        }
    }
}
