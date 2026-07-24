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
        /// <summary>
        /// Offset paired with the gap in the plateau curve. Deliberately the largest tap-rate offset used by any
        /// evaluator (Coordination's 80ms), so the dampener can only ever under-flatten a skill, never invert it.
        /// </summary>
        private const double plateau_offset_ms = 80.0;

        /// <summary>Exponent ceiling. Must stay at or below 1 — that is the no-inversion guarantee.</summary>
        private const double plateau_max_exponent = 1.0;

        /// <summary>A gap wider than this multiple of the local pulse ends a run (rate-invariant).</summary>
        private const double chain_gap_ratio_lo = 1.5;

        private const double chain_gap_ratio_hi = 2.2;
        private const int pulse_window = 16;
        private const double pulse_percentile = 0.5;

        private const double high_speed_nerf = 0.86;
        private const double roll_onset_ms = 260.0;
        private const double period_ramp = 26.0;
        private const int run_cap = 90;
        private const int max_period = 4;
        private const int max_shift = 1;
        private const int narrow_trill_window = 4;

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
        private const double mash_onset_ms = 60.0;
        private const double mash_ramp_lo = 3.0;
        private const double mash_ramp_hi = 9.0;
        private const int mash_run_cap = 64;
        private const double mash_chord_lo = 0.06;
        private const double mash_chord_hi = 0.18;
        private const double mash_cross_lo = 0.10;
        private const double mash_cross_hi = 0.22;

        private const double cross_mash_nerf = 1.0;
        private const double cross_mash_onset_ms = 64.0;
        private const double cross_mash_run_lo = 3.0;
        private const double cross_mash_run_hi = 10.0;
        private const int cross_mash_run_cap = 128;
        private const double cross_mash_cross_lo = 0.14;
        private const double cross_mash_cross_hi = 0.32;
        private const double cross_mash_bigchord_lo = 0.05;
        private const double cross_mash_bigchord_hi = 0.16;

        private const double jumptrill_nerf = 1.0;
        private const double jumptrill_onset_ms = 260.0;
        private const double jumptrill_run_lo = 3.0;
        private const double jumptrill_run_hi = 8.0;
        private const int jumptrill_run_cap = 200;
        private const double jumptrill_cross_lo = 0.06;

        private const double jumptrill_cross_hi = 0.16;

        private const double jumptrill_bridge_jump_lo = 0.05;
        private const double jumptrill_bridge_jump_hi = 0.11;

        private const int jumptrill_jump_window = 32;

        private const double stamina_buff = 0.52;
        private const double stamina_onset_ms = 85.0;
        private const double stamina_run_lo = 6.0;
        private const double stamina_run_hi = 30.0;
        private const int stamina_run_cap = 256;

        private const double vibro_nerf = 1.0;
        private const double vibro_onset_ms = 190.0;
        private const double vibro_run_lo = 2.5;
        private const double vibro_run_hi = 5.0;
        private const int vibro_run_cap = 64;
        private const double vibro_size_lo = 1.6;
        private const double vibro_size_hi = 2.6;

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

            var hands = new Hand[rows.Count];
            bool[] handLocal = new bool[rows.Count];

            for (int i = 0; i < rows.Count; i++)
            {
                hands[i] = handOf(rows[i].Columns, totalColumns);
                handLocal[i] = hands[i] != Hand.Both;
            }

            double[] pulse = localPulses(rows);

            int[] chordPrefix = buildPrefix(rows.Count, i => rows[i].IsChord);
            int[] crossPrefix = buildPrefix(rows.Count, i => !handLocal[i]);
            int[] jumpPrefix = buildPrefix(rows.Count, i => handLocal[i] && rows[i].Size >= 2);
            int[] bigChordPrefix = buildPrefix(rows.Count, i => rows[i].Size >= 3);

            for (int i = 0; i < rows.Count; i++)
            {
                double timeSincePreviousRow = i > 0 ? rows[i].StartTime - rows[i - 1].StartTime : double.PositiveInfinity;

                double manipulationFactor = Math.Min(
                    Math.Min(
                        Math.Min(
                            rollAndPatternFactor(rows, pulse, chordPrefix, i, timeSincePreviousRow),
                            jumptrillFactor(rows, handLocal, hands, pulse, crossPrefix, jumpPrefix, i, timeSincePreviousRow)),
                        mashFactor(rows, handLocal, pulse, chordPrefix, crossPrefix, i, timeSincePreviousRow)),
                    crossMashFactor(rows, pulse, crossPrefix, bigChordPrefix, i, timeSincePreviousRow));

                double staminaFactor = staminaFactorFor(rows, pulse, i, timeSincePreviousRow);

                foreach (var member in rows[i].Objects)
                {
                    // The vibro nerf is per note: only the hammering column is dampened, not the rest of its row.
                    double memberFactor = Math.Min(manipulationFactor, vibroFactor(rows, pulse, i, member.Column, timeSincePreviousRow));

                    if (memberFactor < 1.0)
                        member.ManipulationFactor = memberFactor;

                    if (staminaFactor > 1.0)
                        member.StaminaFactor = staminaFactor;
                }
            }
        }

        private static double vibroFactor(IReadOnlyList<ManiaRow> rows, double[] pulse, int row, int column, double timeSincePreviousRow)
        {
            if (timeSincePreviousRow >= vibro_onset_ms)
                return 1.0;

            int run = 1;
            double sizeSum = rows[row].Size;

            for (int k = row;
                 run < vibro_run_cap && k - 1 >= 0 && rowHasColumn(rows[k - 1], column)
                 && continuesRun(rows[k].StartTime - rows[k - 1].StartTime, pulse[row]) > 0.0;
                 k--)
            {
                run++;
                sizeSum += rows[k - 1].Size;
            }

            for (int k = row;
                 run < vibro_run_cap && k + 1 < rows.Count && rowHasColumn(rows[k + 1], column)
                 && continuesRun(rows[k + 1].StartTime - rows[k].StartTime, pulse[row]) > 0.0;
                 k++)
            {
                run++;
                sizeSum += rows[k + 1].Size;
            }

            double runWeight = DiffUtils.Smoothstep(run, vibro_run_lo, vibro_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            // Small rows -> a single finger hammering (vibro). Full rows -> a chord-jack, which is left alone.
            double sizeGate = 1.0 - DiffUtils.Smoothstep(sizeSum / run, vibro_size_lo, vibro_size_hi);

            return plateauFactor(timeSincePreviousRow, vibro_onset_ms, vibro_nerf * runWeight * sizeGate);
        }

        private static bool rowHasColumn(ManiaRow row, int column)
        {
            foreach (int c in row.Columns)
            {
                if (c == column)
                    return true;
            }

            return false;
        }

        private static double plateauFactor(double gap, double onsetMs, double strength)
        {
            if (strength <= 0.0 || gap >= onsetMs)
                return 1.0;

            double ratio = (gap + plateau_offset_ms) / (onsetMs + plateau_offset_ms);

            return Math.Pow(ratio, Math.Min(strength, plateau_max_exponent));
        }

        private static double[] localPulses(IReadOnlyList<ManiaRow> rows)
        {
            double[] pulse = new double[rows.Count];
            double[] window = new double[2 * pulse_window + 1];

            for (int i = 0; i < rows.Count; i++)
            {
                int lo = Math.Max(1, i - pulse_window);
                int hi = Math.Min(rows.Count - 1, i + pulse_window);
                int length = hi - lo + 1;

                if (length <= 0)
                {
                    pulse[i] = double.PositiveInfinity;
                    continue;
                }

                for (int k = 0; k < length; k++)
                    window[k] = rows[lo + k].StartTime - rows[lo + k - 1].StartTime;

                Array.Sort(window, 0, length);
                pulse[i] = window[(int)(pulse_percentile * (length - 1))];
            }

            return pulse;
        }

        /// <summary>
        /// How much a gap of <paramref name="gap"/> counts as continuing a run whose local pulse is
        /// <paramref name="pulse"/>: 1 while it keeps the pulse, fading to 0 as it widens into a rhythmic break.
        /// Purely a ratio, so it is unchanged by rate.
        /// </summary>
        private static double continuesRun(double gap, double pulse)
        {
            if (!(pulse > 0.0) || double.IsInfinity(pulse))
                return 0.0;

            return 1.0 - DiffUtils.Smoothstep(gap / pulse, chain_gap_ratio_lo, chain_gap_ratio_hi);
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

        private static double rollAndPatternFactor(IReadOnlyList<ManiaRow> rows, double[] pulse, int[] chordPrefix, int row, double timeSincePreviousRow)
        {
            if (timeSincePreviousRow >= roll_onset_ms)
                return 1.0;

            int patternRun = longestRollOrPeriodicRun(rows, row);
            double runWeight = DiffUtils.ReverseLerp(patternRun, 0.0, period_ramp);

            int moveRun = movementRun(rows, pulse, row, out double directionConsistency);

            if (moveRun >= 2)
            {
                double staminaRelief = movement_stamina_relief * DiffUtils.Smoothstep(moveRun, movement_taper_lo, movement_taper_hi);
                double rollGate = DiffUtils.Smoothstep(directionConsistency, movement_dir_lo, movement_dir_hi);

                double moveWeight = DiffUtils.ReverseLerp(moveRun, 0.0, period_ramp) * (1.0 - staminaRelief) * rollGate;
                runWeight = Math.Max(runWeight, moveWeight);
            }

            runWeight *= 1.0 - DiffUtils.Smoothstep(localChordDensity(chordPrefix, row), movement_chord_lo, movement_chord_hi);

            return plateauFactor(timeSincePreviousRow, roll_onset_ms, high_speed_nerf * runWeight);
        }

        private static int longestRollOrPeriodicRun(IReadOnlyList<ManiaRow> rows, int row)
        {
            if (isNarrowTrill(rows, row))
                return 0;

            int run = countRunBackward(row, run_cap, earlier => columnShift(rows[earlier].Columns, rows[earlier + 1].Columns) != 0);

            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRunLength(rows, row, period));

            return run;
        }

        private static bool isNarrowTrill(IReadOnlyList<ManiaRow> rows, int row)
        {
            int colA = -1;
            int colB = -1;
            int counted = 0;

            for (int k = row; k > row - narrow_trill_window && k >= 0; k--)
            {
                if (!rows[k].IsSingleNote)
                    return false;

                int c = rows[k].Columns[0];
                counted++;

                if (c == colA || c == colB)
                    continue;

                if (colA < 0)
                    colA = c;
                else if (colB < 0)
                    colB = c;
                else
                    return false; // a third distinct column -> a spread, not a narrow trill
            }

            // need a full window of single notes using exactly two adjacent columns
            return counted >= narrow_trill_window && colB >= 0 && Math.Abs(colA - colB) == 1;
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

        private static double jumptrillFactor(IReadOnlyList<ManiaRow> rows, bool[] handLocal, Hand[] hands, double[] pulse, int[] crossPrefix, int[] jumpPrefix, int row, double timeSincePreviousRow)
        {
            if (!handLocal[row] || timeSincePreviousRow >= jumptrill_onset_ms)
                return 1.0;

            double run = jumptrillChainLength(rows, handLocal, hands, pulse, row);

            double runWeight = DiffUtils.Smoothstep(run, jumptrill_run_lo, jumptrill_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double crossGate = isStrictJumptrill(rows, row)
                ? 1.0
                : 1.0 - DiffUtils.Smoothstep(localCrossHandDensity(crossPrefix, row), jumptrill_cross_lo, jumptrill_cross_hi);

            double jumpGate = DiffUtils.Smoothstep(localJumpDensity(jumpPrefix, row), jumptrill_bridge_jump_lo, jumptrill_bridge_jump_hi);

            return plateauFactor(timeSincePreviousRow, jumptrill_onset_ms, jumptrill_nerf * runWeight * crossGate * jumpGate);
        }

        private static double jumptrillChainLength(IReadOnlyList<ManiaRow> rows, bool[] handLocal, Hand[] hands, double[] pulse, int row)
        {
            double run = 1.0;

            Step previous = Step.Break;
            double gate = 1.0;

            for (int k = row; run < jumptrill_run_cap && k - 1 >= 0 && handLocal[k - 1]; k--)
            {
                double bridge = continuesRun(rows[k].StartTime - rows[k - 1].StartTime, pulse[row]);

                if (bridge <= 0.0)
                    break;

                Step step = stepKind(rows[k], rows[k - 1], hands[k], hands[k - 1]);

                if (!extendsChain(step, previous))
                    break;

                // The bottleneck weight: once the walk crosses a partly-joined gap, every step beyond it counts for at
                // most that gap's weight, so the far side fades in smoothly rather than snapping onto the chain.
                gate = Math.Min(gate, bridge);
                run += gate;
                previous = step;
            }

            previous = Step.Break;
            gate = 1.0;

            for (int k = row; run < jumptrill_run_cap && k + 1 < rows.Count && handLocal[k + 1]; k++)
            {
                double bridge = continuesRun(rows[k + 1].StartTime - rows[k].StartTime, pulse[row]);

                if (bridge <= 0.0)
                    break;

                Step step = stepKind(rows[k + 1], rows[k], hands[k + 1], hands[k]);

                if (!extendsChain(step, previous))
                    break;

                gate = Math.Min(gate, bridge);
                run += gate;
                previous = step;
            }

            return run;
        }

        private static bool extendsChain(Step step, Step previous)
        {
            switch (step)
            {
                case Step.Roll:
                case Step.JumpSwitch:
                    return true;

                case Step.SingleSwitch:
                    return previous == Step.Roll;

                default:
                    return false;
            }
        }

        private enum Step
        {
            /// <summary>Breaks the chain (a jack, a repeated jump, or a wide jump).</summary>
            Break,

            /// <summary>Same hand, a single-note one-column roll.</summary>
            Roll,

            /// <summary>Opposite hands with a jump — the canonical chord-trill bounce.</summary>
            JumpSwitch,

            /// <summary>Opposite hands, both single notes — a plain hand switch (a handstream step in isolation).</summary>
            SingleSwitch,
        }

        /// <summary>Classifies the step between one-hand rows <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static Step stepKind(ManiaRow a, ManiaRow b, Hand handA, Hand handB)
        {
            if (handA != handB)
                return a.Size >= 2 || b.Size >= 2 ? Step.JumpSwitch : Step.SingleSwitch;

            return a.Size == 1 && b.Size == 1 && Math.Abs(a.Columns[0] - b.Columns[0]) == 1 ? Step.Roll : Step.Break;
        }

        private static double localJumpDensity(int[] jumpPrefix, int row)
            => windowedFraction(jumpPrefix, row, jumptrill_jump_window);

        private static bool isStrictJumptrill(IReadOnlyList<ManiaRow> rows, int row)
        {
            if (!rows[row].IsJump)
                return false;

            bool recursBefore = row - 2 >= 0 && sameColumns(rows[row].Columns, rows[row - 2].Columns) && !sameColumns(rows[row].Columns, rows[row - 1].Columns);
            bool recursAfter = row + 2 < rows.Count && sameColumns(rows[row].Columns, rows[row + 2].Columns) && !sameColumns(rows[row].Columns, rows[row + 1].Columns);

            return recursBefore || recursAfter;
        }

        private static double mashFactor(IReadOnlyList<ManiaRow> rows, bool[] handLocal, double[] pulse, int[] chordPrefix, int[] crossPrefix, int row, double timeSincePreviousRow)
        {
            if (!handLocal[row] || timeSincePreviousRow >= mash_onset_ms)
                return 1.0;

            double run = 0.0;
            double gate = 1.0;

            for (int k = row; run < mash_run_cap && k - 1 >= 0 && handLocal[k - 1]; k--)
            {
                double joins = continuesRun(rows[k].StartTime - rows[k - 1].StartTime, pulse[row]);

                if (joins <= 0.0)
                    break;

                gate = Math.Min(gate, joins);
                run += gate;
            }

            double runWeight = DiffUtils.Smoothstep(run, mash_ramp_lo, mash_ramp_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = DiffUtils.Smoothstep(localChordDensity(chordPrefix, row), mash_chord_lo, mash_chord_hi);
            double crossGate = 1.0 - DiffUtils.Smoothstep(localCrossHandDensity(crossPrefix, row), mash_cross_lo, mash_cross_hi);

            return plateauFactor(timeSincePreviousRow, mash_onset_ms, mash_nerf * runWeight * chordGate * crossGate);
        }

        private static double crossMashFactor(IReadOnlyList<ManiaRow> rows, double[] pulse, int[] crossPrefix, int[] bigChordPrefix, int row, double timeSincePreviousRow)
        {
            if (timeSincePreviousRow >= cross_mash_onset_ms)
                return 1.0;

            double crossGate = DiffUtils.Smoothstep(windowedFraction(crossPrefix, row, movement_chord_window), cross_mash_cross_lo, cross_mash_cross_hi);

            if (crossGate <= 0.0)
                return 1.0;

            double run = 0.0;
            double gate = 1.0;

            for (int k = row; run < cross_mash_run_cap && k - 1 >= 0; k--)
            {
                double joins = continuesRun(rows[k].StartTime - rows[k - 1].StartTime, pulse[row]);

                if (joins <= 0.0)
                    break;

                gate = Math.Min(gate, joins);
                run += gate;
            }

            gate = 1.0;

            for (int k = row; run < cross_mash_run_cap && k + 1 < rows.Count; k++)
            {
                double joins = continuesRun(rows[k + 1].StartTime - rows[k].StartTime, pulse[row]);

                if (joins <= 0.0)
                    break;

                gate = Math.Min(gate, joins);
                run += gate;
            }

            double runWeight = DiffUtils.Smoothstep(run, cross_mash_run_lo, cross_mash_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = 1.0 - DiffUtils.Smoothstep(windowedFraction(bigChordPrefix, row, movement_chord_window), cross_mash_bigchord_lo, cross_mash_bigchord_hi);

            return plateauFactor(timeSincePreviousRow, cross_mash_onset_ms, cross_mash_nerf * runWeight * crossGate * chordGate);
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

        private static double localCrossHandDensity(int[] crossPrefix, int row)
            => windowedFraction(crossPrefix, row, movement_chord_window);

        private static int movementRun(IReadOnlyList<ManiaRow> rows, double[] pulse, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (!rows[row].IsSingleNote)
                return 0;

            int runStart = row;
            while (row - runStart < movement_cap && isFastLateralMove(rows, pulse, row, runStart))
                runStart--;

            int runEnd = row;
            while (runEnd - row < movement_cap && isFastLateralMove(rows, pulse, row, runEnd + 1))
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

        private static bool isFastLateralMove(IReadOnlyList<ManiaRow> rows, double[] pulse, int row, int k)
        {
            return k - 1 >= 0 && k < rows.Count
                              && rows[k].IsSingleNote && rows[k - 1].IsSingleNote
                              && rows[k].Columns[0] != rows[k - 1].Columns[0]
                              && continuesRun(rows[k].StartTime - rows[k - 1].StartTime, pulse[row]) > 0.0;
        }

        private static double localChordDensity(int[] chordPrefix, int row)
            => windowedFraction(chordPrefix, row, movement_chord_window);

        private static double windowedFraction(int[] prefix, int row, int radius)
        {
            int count = prefix.Length - 1;
            int lo = Math.Max(0, row - radius);
            int hi = Math.Min(count - 1, row + radius);

            return (double)(prefix[hi + 1] - prefix[lo]) / (hi - lo + 1);
        }

        /// <summary>Builds the prefix-sum table used by <see cref="windowedFraction"/>.</summary>
        private static int[] buildPrefix(int count, Func<int, bool> predicate)
        {
            int[] prefix = new int[count + 1];

            for (int i = 0; i < count; i++)
                prefix[i + 1] = prefix[i] + (predicate(i) ? 1 : 0);

            return prefix;
        }

        private static double staminaFactorFor(IReadOnlyList<ManiaRow> rows, double[] pulse, int row, double timeSincePreviousRow)
        {
            if (!rows[row].IsJump || timeSincePreviousRow >= stamina_onset_ms)
                return 1.0;

            // Ramps in as the pattern gets faster and then holds — the inverse of the plateau curve, so the buff is
            // monotone in rate and can never shrink a denser map's rating.
            double speedScale = 1.0 - plateauFactor(timeSincePreviousRow, stamina_onset_ms, plateau_max_exponent);

            int run = 1 + countRunBackward(row, stamina_run_cap - 1, earlier =>
                rows[earlier].IsJump && continuesRun(rows[earlier + 1].StartTime - rows[earlier].StartTime, pulse[row]) > 0.0);

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
