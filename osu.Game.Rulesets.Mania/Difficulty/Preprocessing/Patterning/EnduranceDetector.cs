// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class EnduranceDetector
    {
        private const double steady_buff = 0.13;

        private const double burst_nerf = 0.10;

        private const double window_ms = 250.0;

        private const double rate_lo = 16.0;
        private const double rate_hi = 26.0;

        private const double rate_ceiling_lo = 33.0;
        private const double rate_ceiling_hi = 44.0;

        private const double stream_share_lo = 0.20;
        private const double stream_share_hi = 0.42;

        private const int breadth_reach = 2;

        private const double breadth_share_lo = 0.20;
        private const double breadth_share_hi = 0.32;

        private const double pulse_window_ms = 600.0;

        private const double burst_gap_ratio = 0.75;

        private const double burst_share_lo = 0.03;
        private const double burst_share_hi = 0.14;

        private const int max_pulse_window_gaps = 128;

        public static double EvaluateFactorOf(ManiaRow row)
        {
            ManiaRow first = row.FirstWithin(window_ms);
            ManiaRow last = row.LastWithin(window_ms);

            int rowCount = 0;
            int noteCount = 0;
            int revisitingRows = 0;
            int singleNoteRows = 0;

            for (ManiaRow? current = first; current != null && current.RowIndex <= last.RowIndex; current = current.Next())
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
            double rateGate = DiffUtils.Smoothstep(notesPerSecond, rate_lo, rate_hi);

            if (rateGate <= 0.0)
                return 1.0;

            double streamGate = DiffUtils.Smoothstep(singleNoteRows / (double)rowCount, stream_share_lo, stream_share_hi);

            if (streamGate <= 0.0)
                return 1.0;

            double breadthGate = DiffUtils.Smoothstep(revisitingRows / (double)rowCount, breadth_share_lo, breadth_share_hi);
            double ceilingFade = 1.0 - DiffUtils.Smoothstep(notesPerSecond, rate_ceiling_lo, rate_ceiling_hi);
            double burstShare = burstiness(row, first, last, rowCount);

            double reward = steady_buff * ceilingFade * breadthGate * (1.0 - burstShare);

            return 1.0 + rateGate * streamGate * (reward - burst_nerf * burstShare);
        }

        private static double burstiness(ManiaRow row, ManiaRow first, ManiaRow last, int rowCount)
        {
            Span<double> gaps = stackalloc double[max_pulse_window_gaps];
            int gapCount = 0;

            for (ManiaRow? current = row.Previous(); current != null && gapCount < max_pulse_window_gaps && row.StartTime - current.StartTime <= pulse_window_ms; current = current.Previous())
                gaps[gapCount++] = current.Next()!.GapBefore;

            for (ManiaRow? current = row.Next(); current != null && gapCount < max_pulse_window_gaps && current.StartTime - row.StartTime <= pulse_window_ms; current = current.Next())
                gaps[gapCount++] = current.GapBefore;

            if (gapCount == 0)
                return 0.0;

            gaps = gaps[..gapCount];
            gaps.Sort();

            double burstThreshold = burst_gap_ratio * gaps[gapCount / 2];
            int bursts = 0;

            for (ManiaRow? current = first.Next(); current != null && current.RowIndex <= last.RowIndex; current = current.Next())
            {
                if (current.StartTime - current.Previous()!.StartTime < burstThreshold)
                    bursts++;
            }

            return DiffUtils.Smoothstep(bursts / (double)(rowCount - 1), burst_share_lo, burst_share_hi);
        }

        /// <summary>
        /// Whether this row presses a column that one of the last few rows already did.
        /// </summary>
        private static bool revisitsRecentColumn(ManiaRow row)
        {
            for (int back = 1; back <= breadth_reach; back++)
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
