// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class SpeedjackEvaluator
    {
        private const double speedjack_buff = 0.35;
        private const double speedjack_speed_hi_ms = 110.0;
        private const double speedjack_speed_lo_ms = 70.0;
        private const double speedjack_single_gate = 0.5;
        private const double speedjack_chord_taper = 0.8;
        private const int speedjack_clean_window = 6;

        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current)
        {
            ManiaRow row = current.Row;
            ManiaRow? previous = row.Previous();
            ManiaRow? previous2 = row.Previous(1);

            if (previous == null || previous2 == null)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(speedjack_speed_hi_ms - row.GapBefore, 0.0, speedjack_speed_hi_ms - speedjack_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            bool isFullRepeat = ColumnPatternUtils.SameColumns(row.Columns, previous.Columns) || ColumnPatternUtils.SameColumns(row.Columns, previous2.Columns);
            bool isRoll = ColumnPatternUtils.IsRoll(previous.Columns, row.Columns);
            bool sharesJack = ColumnPatternUtils.SharesColumn(row.Columns, previous.Columns) || ColumnPatternUtils.SharesColumn(row.Columns, previous2.Columns);

            if (isFullRepeat || isRoll || !sharesJack)
                return 1.0;

            double clean = 1.0 - localJumptrillRollDensity(row);

            if (clean <= 0.0)
                return 1.0;

            double sizeGate = row.Size <= 1
                ? speedjack_single_gate
                : 1.0 - speedjack_chord_taper * DiffUtils.Smoothstep(row.Size, 2.0, 4.0);

            return 1.0 + speedjack_buff * speedScale * sizeGate * clean;
        }

        private static double localJumptrillRollDensity(ManiaRow row)
        {
            int window = 0;
            int manipulable = 0;

            for (ManiaRow? current = row; current != null && window < speedjack_clean_window; current = current.Previous())
            {
                window++;

                ManiaRow? previous = current.Previous();
                ManiaRow? previous2 = current.Previous(1);

                if (previous == null || previous2 == null)
                    continue;

                if (current.GapBefore > speedjack_speed_hi_ms)
                    continue;

                bool isJumptrill = ColumnPatternUtils.IsRecurrence(previous2.Columns, previous.Columns, current.Columns);
                bool isRoll = ColumnPatternUtils.IsRoll(previous.Columns, current.Columns);

                if (isJumptrill || isRoll)
                    manipulable++;
            }

            return window > 0 ? (double)manipulable / window : 0.0;
        }
    }
}
