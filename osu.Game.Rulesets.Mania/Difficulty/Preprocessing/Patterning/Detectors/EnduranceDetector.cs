// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Rewards sections that hold a high note rate for a while, and penalises sections that only reach that rate
    /// in bursts.
    /// </summary>
    public class EnduranceDetector
    {
        /// <summary>
        /// How far either side of the row the note rate is measured over.
        /// </summary>
        private const double window_ms = 250.0;

        /// <summary>
        /// How much of the row's difficulty is left. This one goes both ways, so the result can land above or
        /// below 1.
        /// </summary>
        public double EvaluateFactorOf(ManiaRow row)
        {
            ManiaRow first = row.FirstWithin(window_ms);
            ManiaRow last = row.LastWithin(window_ms);

            int rowCount = 0;
            int noteCount = 0;
            int revisitingRows = 0;
            int singleNoteRows = 0;

            foreach (var current in first.RowsUpTo(last))
            {
                rowCount++;
                noteCount += current.Size;

                if (revisitsRecentColumn(current))
                    revisitingRows++;

                if (current.IsSingleNote)
                    singleNoteRows++;
            }

            if (rowCount < 3)
                return 1.0;

            double notesPerSecond = noteCount / (2.0 * window_ms / 1000.0);
            double rateGate = DiffUtils.Smoothstep(notesPerSecond, 16, 26);

            if (rateGate <= 0.0)
                return 1.0;

            double streamGate = DiffUtils.Smoothstep(singleNoteRows / (double)rowCount, 0.20, 0.42);

            if (streamGate <= 0.0)
                return 1.0;

            double breadthGate = DiffUtils.Smoothstep(revisitingRows / (double)rowCount, 0.20, 0.32);

            // Past this the section is too dense to be endurance, so the reward fades back out.
            double ceilingFade = DiffUtils.Smoothstep(notesPerSecond, 44, 33);

            double burstShare = burstiness(row, first, last, rowCount);
            double reward = 0.13 * ceilingFade * breadthGate * (1.0 - burstShare);

            return 1.0 + rateGate * streamGate * (reward - 0.10 * burstShare);
        }

        /// <summary>
        /// The share of the section's rows that come faster than the section's own typical gap.
        /// </summary>
        private static double burstiness(ManiaRow row, ManiaRow first, ManiaRow last, int rowCount)
        {
            const double pulse_window_ms = 600.0;
            const int max_gaps = 128;

            Span<double> gaps = stackalloc double[max_gaps];
            int gapCount = 0;

            for (ManiaRow? current = row.Previous(); current != null && gapCount < max_gaps && row.StartTime - current.StartTime <= pulse_window_ms; current = current.Previous())
                gaps[gapCount++] = current.Next()!.GapBefore;

            for (ManiaRow? current = row.Next(); current != null && gapCount < max_gaps && current.StartTime - row.StartTime <= pulse_window_ms; current = current.Next())
                gaps[gapCount++] = current.GapBefore;

            if (gapCount == 0)
                return 0.0;

            // Taken as a median so that a couple of long rests don't make the whole section look bursty.
            gaps = gaps[..gapCount];
            gaps.Sort();

            double burstThreshold = 0.75 * gaps[gapCount / 2];
            int bursts = 0;

            foreach (var current in first.Next()!.RowsUpTo(last))
            {
                if (current.GapBefore < burstThreshold)
                    bursts++;
            }

            return DiffUtils.Smoothstep(bursts / (double)(rowCount - 1), 0.03, 0.14);
        }

        /// <summary>
        /// Whether this row presses a column that one of the last few rows already did.
        /// </summary>
        private static bool revisitsRecentColumn(ManiaRow row)
        {
            for (int back = 1; back <= 2; back++)
            {
                if (row.Offset(-back) is not ManiaRow earlier)
                    return false;

                if (ColumnPatternUtils.SharesColumn(row.Columns, earlier.Columns))
                    return true;
            }

            return false;
        }
    }
}
