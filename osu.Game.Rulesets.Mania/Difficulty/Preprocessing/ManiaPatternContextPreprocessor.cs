// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public static class ManiaPatternContextPreprocessor
    {
        private const double plateau_offset_ms = 80.0;

        private const double plateau_max_exponent = 1.0;

        private const double chain_gap_ratio_lo = 1.5;

        private const double chain_gap_ratio_hi = 2.2;
        private const int pulse_window = 16;
        private const double pulse_percentile = 0.5;

        /// <summary>Radius, in rows, of the window the context gates measure their densities over.</summary>
        private const int context_window = 14;

        private const double roll_nerf = 0.86;
        private const double roll_onset_ms = 260.0;
        private const double period_ramp = 26.0;
        private const int run_cap = 90;
        private const int max_period = 4;
        private const int narrow_trill_window = 4;
        private const int movement_cap = 400;
        private const double movement_dir_lo = 0.42;
        private const double movement_dir_hi = 0.50;
        private const double roll_chord_lo = 0.14;
        private const double roll_chord_hi = 0.34;

        private const double jumptrill_onset_ms = 260.0;
        private const double jumptrill_run_lo = 3.0;
        private const double jumptrill_run_hi = 8.0;
        private const int jumptrill_run_cap = 200;
        private const double jumptrill_cross_lo = 0.06;
        private const double jumptrill_cross_hi = 0.16;
        private const double jumptrill_jump_lo = 0.05;
        private const double jumptrill_jump_hi = 0.11;
        private const int jumptrill_jump_window = 32;

        private const double jumptrill_local_jump_lo = 0.05;
        private const double jumptrill_local_jump_hi = 0.20;
        private const int jumptrill_local_window = 6;

        private const double jumptrill_strict_floor = 0.65;

        private const double mash_onset_ms = 60.0;
        private const double mash_run_lo = 3.0;
        private const double mash_run_hi = 9.0;
        private const int mash_run_cap = 64;
        private const double mash_chord_lo = 0.06;
        private const double mash_chord_hi = 0.18;
        private const double mash_cross_lo = 0.10;
        private const double mash_cross_hi = 0.22;

        private const double cross_mash_onset_ms = 64.0;
        private const double cross_mash_run_lo = 3.0;
        private const double cross_mash_run_hi = 10.0;
        private const int cross_mash_run_cap = 128;
        private const double cross_mash_cross_lo = 0.14;
        private const double cross_mash_cross_hi = 0.32;
        private const double cross_mash_bigchord_lo = 0.05;
        private const double cross_mash_bigchord_hi = 0.16;

        private const double vibro_onset_ms = 190.0;
        private const double vibro_run_lo = 2.5;
        private const double vibro_run_hi = 5.0;
        private const int vibro_run_cap = 64;
        private const double vibro_size_lo = 1.6;
        private const double vibro_size_hi = 2.6;

        /// <summary>Maximum multiplier added where a steady stream of the right breadth clears <see cref="endurance_rate_hi"/>.</summary>
        private const double endurance_buff = 0.13;

        /// <summary>Maximum penalty for a window whose density is delivered entirely in bursts.</summary>
        private const double endurance_burst_nerf = 0.10;

        /// <summary>Radius of the window the note rate and the pattern gates are measured over.</summary>
        private const double endurance_window_ms = 250.0;

        // Notes per second the reward fades in across.
        private const double endurance_rate_lo = 16.0;
        private const double endurance_rate_hi = 26.0;

        private const double endurance_ceiling_lo = 33.0;
        private const double endurance_ceiling_hi = 44.0;

        private const double endurance_pulse_window_ms = 600.0;

        /// <summary>A gap this far inside the surrounding pulse is a burst rather than a step of the stream.</summary>
        private const double endurance_burst_gap_ratio = 0.75;

        private const double endurance_burst_lo = 0.03;
        private const double endurance_burst_hi = 0.14;

        /// <summary>How many rows back a column may reappear in and still count as the stream coming back around.</summary>
        private const int endurance_breadth_reach = 2;

        private const double endurance_breadth_lo = 0.20;
        private const double endurance_breadth_hi = 0.32;

        private const double endurance_single_lo = 0.20;
        private const double endurance_single_hi = 0.42;

        private const int endurance_max_window_rows = 128;

        /// <summary>
        /// Assigns a manipulation factor and an endurance factor to the notes in each row, based on what the pattern
        /// around them does to the difficulty of hitting them.
        /// </summary>
        /// <param name="mapData">The structured data of the map used to calculate the attributes for the notes.</param>
        /// <param name="totalColumns">The number of columns of the beatmap, used to split rows into left/right hands.</param>
        public static void ProcessAndAssign(ManiaMapData mapData, int totalColumns)
        {
            if (mapData.Rows.Count == 0)
                return;

            var map = new MapContext(mapData.Rows, totalColumns);
            var gaps = new List<double>(endurance_max_window_rows);

            for (int i = 0; i < map.Rows.Length; i++)
            {
                double gap = map.GapBefore(i);

                double manipulationFactor = Math.Min(
                    Math.Min(rollFactor(map, i, gap), jumptrillFactor(map, i, gap)),
                    Math.Min(mashFactor(map, i, gap), crossMashFactor(map, i, gap)));

                double endurance = enduranceFactor(map, i, gaps);

                foreach (var member in map.Rows[i].Objects)
                {
                    double memberFactor = Math.Min(manipulationFactor, vibroFactor(map, i, member.Column, gap));

                    if (memberFactor < 1.0)
                        member.ManipulationFactor = memberFactor;

                    member.EnduranceFactor = endurance;
                }
            }
        }

        private readonly struct MapContext
        {
            public readonly ManiaRow[] Rows;

            public readonly double[] StartTimes;

            public readonly Hand[] Hands;

            public readonly bool[] HandLocal;

            public readonly double[] Pulse;

            public readonly int[] ChordPrefix;
            public readonly int[] CrossPrefix;
            public readonly int[] JumpPrefix;
            public readonly int[] BigChordPrefix;

            public MapContext(IReadOnlyList<ManiaRow> rows, int totalColumns)
            {
                int count = rows.Count;

                Rows = new ManiaRow[count];
                StartTimes = new double[count];
                Hands = new Hand[count];
                HandLocal = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    Rows[i] = rows[i];
                    StartTimes[i] = rows[i].StartTime;
                    Hands[i] = handOf(rows[i].Columns, totalColumns);
                    HandLocal[i] = Hands[i] != Hand.Both;
                }

                Pulse = localPulses(StartTimes);

                ManiaRow[] allRows = Rows;
                bool[] handLocal = HandLocal;

                ChordPrefix = buildPrefix(count, i => allRows[i].IsChord);
                CrossPrefix = buildPrefix(count, i => !handLocal[i]);
                JumpPrefix = buildPrefix(count, i => handLocal[i] && allRows[i].Size >= 2);
                BigChordPrefix = buildPrefix(count, i => allRows[i].Size >= 3);
            }

            public double GapBefore(int row) => row > 0 ? StartTimes[row] - StartTimes[row - 1] : double.PositiveInfinity;

            public double Density(int[] prefix, int row, int radius = context_window)
            {
                int lo = Math.Max(0, row - radius);
                int hi = Math.Min(Rows.Length - 1, row + radius);

                return (double)(prefix[hi + 1] - prefix[lo]) / (hi - lo + 1);
            }

            public double ChainLength(int row, int direction, double cap, Func<int, int, bool>? extendsChain = null)
            {
                double run = 0.0;
                double gate = 1.0;

                for (int k = row; run < cap; k += direction)
                {
                    int next = k + direction;

                    if (next < 0 || next >= Rows.Length || extendsChain?.Invoke(k, next) == false)
                        break;

                    double joins = continuesRun(Math.Abs(StartTimes[next] - StartTimes[k]), Pulse[row]);

                    if (joins <= 0.0)
                        break;

                    gate = Math.Min(gate, joins);
                    run += gate;
                }

                return run;
            }
        }

        private static double plateauFactor(double gap, double onsetMs, double strength)
        {
            if (strength <= 0.0 || gap >= onsetMs)
                return 1.0;

            double ratio = (gap + plateau_offset_ms) / (onsetMs + plateau_offset_ms);

            return Math.Pow(ratio, Math.Min(strength, plateau_max_exponent));
        }

        private static double continuesRun(double gap, double pulse)
        {
            if (!(pulse > 0.0) || double.IsInfinity(pulse))
                return 0.0;

            return 1.0 - DiffUtils.Smoothstep(gap / pulse, chain_gap_ratio_lo, chain_gap_ratio_hi);
        }

        private static double[] localPulses(double[] startTimes)
        {
            double[] pulse = new double[startTimes.Length];
            double[] window = new double[2 * pulse_window + 1];

            for (int i = 0; i < startTimes.Length; i++)
            {
                int lo = Math.Max(1, i - pulse_window);
                int hi = Math.Min(startTimes.Length - 1, i + pulse_window);
                int length = hi - lo + 1;

                if (length <= 0)
                {
                    pulse[i] = double.PositiveInfinity;
                    continue;
                }

                for (int k = 0; k < length; k++)
                    window[k] = startTimes[lo + k] - startTimes[lo + k - 1];

                Array.Sort(window, 0, length);
                pulse[i] = window[(int)(pulse_percentile * (length - 1))];
            }

            return pulse;
        }

        private static double rollFactor(in MapContext map, int row, double gap)
        {
            if (gap >= roll_onset_ms)
                return 1.0;

            double runWeight = DiffUtils.ReverseLerp(longestRollOrPeriodicRun(map.Rows, row), 0.0, period_ramp);

            int moveRun = movementRun(map, row, out double directionConsistency);

            if (moveRun >= 2)
            {
                double rollGate = DiffUtils.Smoothstep(directionConsistency, movement_dir_lo, movement_dir_hi);

                runWeight = Math.Max(runWeight, DiffUtils.ReverseLerp(moveRun, 0.0, period_ramp) * rollGate);
            }

            runWeight *= 1.0 - DiffUtils.Smoothstep(map.Density(map.ChordPrefix, row), roll_chord_lo, roll_chord_hi);

            return plateauFactor(gap, roll_onset_ms, roll_nerf * runWeight);
        }

        private static int longestRollOrPeriodicRun(IReadOnlyList<ManiaRow> rows, int row)
        {
            if (isNarrowTrill(rows, row))
                return 0;

            int run = countRunBackward(row, run_cap, earlier => isNeighbourShift(rows[earlier].Columns, rows[earlier + 1].Columns));

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
                    return false;
            }

            // need a full window of single notes using exactly two adjacent columns
            return counted >= narrow_trill_window && colB >= 0 && Math.Abs(colA - colB) == 1;
        }

        /// <summary>How many times the row's exact column set has already repeated at a spacing of <paramref name="period"/> rows.</summary>
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

        private static int movementRun(in MapContext map, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (!map.Rows[row].IsSingleNote)
                return 0;

            int runStart = row;
            while (row - runStart < movement_cap && isFastLateralMove(map, row, runStart))
                runStart--;

            int runEnd = row;
            while (runEnd - row < movement_cap && isFastLateralMove(map, row, runEnd + 1))
                runEnd++;

            int dirPairs = 0;
            int sameDirCount = 0;
            int previousDirection = 0;

            for (int k = runStart + 1; k <= runEnd; k++)
            {
                int direction = Math.Sign(map.Rows[k].Columns[0] - map.Rows[k - 1].Columns[0]);

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

        private static bool isFastLateralMove(in MapContext map, int row, int k)
        {
            return k - 1 >= 0 && k < map.Rows.Length
                              && map.Rows[k].IsSingleNote && map.Rows[k - 1].IsSingleNote
                              && map.Rows[k].Columns[0] != map.Rows[k - 1].Columns[0]
                              && continuesRun(map.Rows[k].StartTime - map.Rows[k - 1].StartTime, map.Pulse[row]) > 0.0;
        }

        private static double jumptrillFactor(in MapContext map, int row, double gap)
        {
            if (!map.HandLocal[row] || gap >= jumptrill_onset_ms)
                return 1.0;

            double runWeight = DiffUtils.Smoothstep(jumptrillChainLength(map, row), jumptrill_run_lo, jumptrill_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double crossGate = Math.Max(
                1.0 - DiffUtils.Smoothstep(map.Density(map.CrossPrefix, row), jumptrill_cross_lo, jumptrill_cross_hi),
                isStrictJumptrill(map.Rows, row) ? jumptrill_strict_floor : 0.0);

            double jumpGate = DiffUtils.Smoothstep(map.Density(map.JumpPrefix, row, jumptrill_jump_window), jumptrill_jump_lo, jumptrill_jump_hi);

            jumpGate *= DiffUtils.Smoothstep(map.Density(map.JumpPrefix, row, jumptrill_local_window), jumptrill_local_jump_lo, jumptrill_local_jump_hi);

            return plateauFactor(gap, jumptrill_onset_ms, runWeight * crossGate * jumpGate);
        }

        /// <summary>The row itself, plus the bounce chain reaching away from it on either side.</summary>
        private static double jumptrillChainLength(MapContext map, int row)
        {
            return 1.0
                   + map.ChainLength(row, -1, jumptrill_run_cap, jumptrillStep(map))
                   + map.ChainLength(row, +1, jumptrill_run_cap, jumptrillStep(map));
        }

        private static Func<int, int, bool> jumptrillStep(MapContext map)
        {
            Step previous = Step.Break;

            return (from, to) =>
            {
                if (!map.HandLocal[to])
                    return false;

                Step step = stepKind(map.Rows[from], map.Rows[to], map.Hands[from], map.Hands[to]);

                switch (step)
                {
                    case Step.Roll:
                    case Step.JumpSwitch:
                        break;

                    case Step.SingleSwitch when previous == Step.Roll:
                        break;

                    default:
                        return false;
                }

                previous = step;
                return true;
            };
        }

        private enum Step
        {
            Break,
            Roll,
            JumpSwitch,
            SingleSwitch,
        }

        private static Step stepKind(ManiaRow a, ManiaRow b, Hand handA, Hand handB)
        {
            if (handA != handB)
                return a.Size >= 2 || b.Size >= 2 ? Step.JumpSwitch : Step.SingleSwitch;

            return a.Size == 1 && b.Size == 1 && Math.Abs(a.Columns[0] - b.Columns[0]) == 1 ? Step.Roll : Step.Break;
        }

        private static bool isStrictJumptrill(IReadOnlyList<ManiaRow> rows, int row)
        {
            if (!rows[row].IsJump)
                return false;

            bool recursBefore = row - 2 >= 0 && sameColumns(rows[row].Columns, rows[row - 2].Columns) && !sameColumns(rows[row].Columns, rows[row - 1].Columns);
            bool recursAfter = row + 2 < rows.Count && sameColumns(rows[row].Columns, rows[row + 2].Columns) && !sameColumns(rows[row].Columns, rows[row + 1].Columns);

            return recursBefore || recursAfter;
        }

        private static double mashFactor(MapContext map, int row, double gap)
        {
            if (!map.HandLocal[row] || gap >= mash_onset_ms)
                return 1.0;

            double run = map.ChainLength(row, -1, mash_run_cap, (_, to) => map.HandLocal[to]);
            double runWeight = DiffUtils.Smoothstep(run, mash_run_lo, mash_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = DiffUtils.Smoothstep(map.Density(map.ChordPrefix, row), mash_chord_lo, mash_chord_hi);
            double crossGate = 1.0 - DiffUtils.Smoothstep(map.Density(map.CrossPrefix, row), mash_cross_lo, mash_cross_hi);

            return plateauFactor(gap, mash_onset_ms, runWeight * chordGate * crossGate);
        }

        private static double crossMashFactor(in MapContext map, int row, double gap)
        {
            if (gap >= cross_mash_onset_ms)
                return 1.0;

            double crossGate = DiffUtils.Smoothstep(map.Density(map.CrossPrefix, row), cross_mash_cross_lo, cross_mash_cross_hi);

            if (crossGate <= 0.0)
                return 1.0;

            double run = map.ChainLength(row, -1, cross_mash_run_cap)
                         + map.ChainLength(row, +1, cross_mash_run_cap);

            double runWeight = DiffUtils.Smoothstep(run, cross_mash_run_lo, cross_mash_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = 1.0 - DiffUtils.Smoothstep(map.Density(map.BigChordPrefix, row), cross_mash_bigchord_lo, cross_mash_bigchord_hi);

            return plateauFactor(gap, cross_mash_onset_ms, runWeight * crossGate * chordGate);
        }

        private static double vibroFactor(in MapContext map, int row, int column, double gap)
        {
            if (gap >= vibro_onset_ms)
                return 1.0;

            int run = 1;
            double sizeSum = map.Rows[row].Size;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                for (int k = row; run < vibro_run_cap; k += direction)
                {
                    int next = k + direction;

                    if (next < 0 || next >= map.Rows.Length || !rowHasColumn(map.Rows[next], column))
                        break;

                    if (continuesRun(Math.Abs(map.Rows[next].StartTime - map.Rows[k].StartTime), map.Pulse[row]) <= 0.0)
                        break;

                    run++;
                    sizeSum += map.Rows[next].Size;
                }
            }

            double runWeight = DiffUtils.Smoothstep(run, vibro_run_lo, vibro_run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double sizeGate = 1.0 - DiffUtils.Smoothstep(sizeSum / run, vibro_size_lo, vibro_size_hi);

            return plateauFactor(gap, vibro_onset_ms, runWeight * sizeGate);
        }

        private static double enduranceFactor(in MapContext map, int row, List<double> gaps)
        {
            double center = map.StartTimes[row];

            int rows = 1;
            int notes = map.Rows[row].Size;
            int revisits = revisitsColumn(map, row) ? 1 : 0;
            int singles = map.Rows[row].IsSingleNote ? 1 : 0;

            for (int k = row - 1; k >= 0 && center - map.StartTimes[k] <= endurance_window_ms; k--)
            {
                rows++;
                notes += map.Rows[k].Size;
                if (revisitsColumn(map, k)) revisits++;
                if (map.Rows[k].IsSingleNote) singles++;
            }

            for (int k = row + 1; k < map.Rows.Length && map.StartTimes[k] - center <= endurance_window_ms; k++)
            {
                rows++;
                notes += map.Rows[k].Size;
                if (revisitsColumn(map, k)) revisits++;
                if (map.Rows[k].IsSingleNote) singles++;
            }

            if (rows < 3)
                return 1.0;

            double notesPerSecond = notes / (2.0 * endurance_window_ms / 1000.0);
            double rateGate = DiffUtils.Smoothstep(notesPerSecond, endurance_rate_lo, endurance_rate_hi);

            if (rateGate <= 0.0)
                return 1.0;

            double streamGate = DiffUtils.Smoothstep(singles / (double)rows, endurance_single_lo, endurance_single_hi);

            if (streamGate <= 0.0)
                return 1.0;

            double breadthGate = DiffUtils.Smoothstep(revisits / (double)rows, endurance_breadth_lo, endurance_breadth_hi);
            double burstiness = burstFraction(map, row, rows, gaps);

            double ceilingFade = 1.0 - DiffUtils.Smoothstep(notesPerSecond, endurance_ceiling_lo, endurance_ceiling_hi);

            return 1.0 + rateGate * streamGate * (endurance_buff * ceilingFade * breadthGate * (1.0 - burstiness) - endurance_burst_nerf * burstiness);
        }

        /// <summary>
        /// How much of the window around <paramref name="row"/> is burst rather than stream: the share of its rows
        /// that arrive well inside the surrounding pulse.
        /// </summary>
        private static double burstFraction(in MapContext map, int row, int rows, List<double> gaps)
        {
            double center = map.StartTimes[row];
            gaps.Clear();

            for (int k = row - 1; k >= 0 && center - map.StartTimes[k] <= endurance_pulse_window_ms && gaps.Count < endurance_max_window_rows; k--)
                gaps.Add(map.StartTimes[k + 1] - map.StartTimes[k]);

            for (int k = row + 1; k < map.StartTimes.Length && map.StartTimes[k] - center <= endurance_pulse_window_ms && gaps.Count < endurance_max_window_rows; k++)
                gaps.Add(map.StartTimes[k] - map.StartTimes[k - 1]);

            if (gaps.Count == 0)
                return 0.0;

            gaps.Sort();

            double burstThreshold = endurance_burst_gap_ratio * gaps[gaps.Count / 2];
            int bursts = 0;

            for (int k = row - 1; k >= 0 && center - map.StartTimes[k] <= endurance_window_ms; k--)
            {
                if (map.StartTimes[k + 1] - map.StartTimes[k] < burstThreshold)
                    bursts++;
            }

            for (int k = row + 1; k < map.StartTimes.Length && map.StartTimes[k] - center <= endurance_window_ms; k++)
            {
                if (map.StartTimes[k] - map.StartTimes[k - 1] < burstThreshold)
                    bursts++;
            }

            return DiffUtils.Smoothstep(bursts / (double)(rows - 1), endurance_burst_lo, endurance_burst_hi);
        }

        private static bool revisitsColumn(in MapContext map, int row)
        {
            for (int back = 1; back <= endurance_breadth_reach; back++)
            {
                if (row - back < 0)
                    return false;

                foreach (int column in map.Rows[row].Columns)
                {
                    foreach (int other in map.Rows[row - back].Columns)
                    {
                        if (column == other)
                            return true;
                    }
                }
            }

            return false;
        }

        private enum Hand
        {
            Left,
            Right,
            Both,
        }

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

        private static int[] buildPrefix(int count, Func<int, bool> predicate)
        {
            int[] prefix = new int[count + 1];

            for (int i = 0; i < count; i++)
                prefix[i + 1] = prefix[i] + (predicate(i) ? 1 : 0);

            return prefix;
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

        private static bool rowHasColumn(ManiaRow row, int column)
        {
            foreach (int c in row.Columns)
            {
                if (c == column)
                    return true;
            }

            return false;
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

        private static bool isNeighbourShift(int[] a, int[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return false;

            int shift = b[0] - a[0];

            if (Math.Abs(shift) != 1)
                return false;

            for (int i = 1; i < a.Length; i++)
            {
                if (b[i] - a[i] != shift)
                    return false;
            }

            return true;
        }
    }
}
