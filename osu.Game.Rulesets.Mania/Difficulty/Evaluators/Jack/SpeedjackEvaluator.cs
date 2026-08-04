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
        /// <summary>
        /// Above this gap the repeat is slow enough to be placed deliberately, and none of this applies.
        /// </summary>
        private const double slowest_speedjack_ms = 110.0;

        /// <summary>
        /// How much more a fast repeat is worth for landing in a passage that keeps moving. A repeat that fast is
        /// only a speedjack while the rows around it are varied: the same shape struck twice, a roll, or a row that
        /// shares no column at all are all something else, and are left alone.
        /// </summary>
        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current)
        {
            ManiaRow row = current.Row;
            ManiaRow? previous = row.Previous();
            ManiaRow? previous2 = row.Previous(1);

            if (previous == null || previous2 == null)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(row.GapBefore, slowest_speedjack_ms, 70);

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

            // A lone note repeating is a plain jack, so it earns part of the buff but a wide chord repeating is a
            // chordjack, which Jack already pays for, so it earns almost none.
            double sizeGate = row.Size <= 1 ? 0.5 : 1.0 - 0.8 * DiffUtils.Smoothstep(row.Size, 2.0, 4.0);

            return 1.0 + 0.35 * speedScale * sizeGate * clean;
        }

        /// <summary>
        /// The share of the recent rows that step in a way the hand can mash through rather than jack.
        /// </summary>
        private static double localJumptrillRollDensity(ManiaRow row)
        {
            int window = 0;
            int manipulable = 0;

            for (ManiaRow? current = row; current != null && window < 6; current = current.Previous())
            {
                window++;

                ManiaRow? previous = current.Previous();
                ManiaRow? previous2 = current.Previous(1);

                if (previous == null || previous2 == null)
                    continue;

                if (current.GapBefore > slowest_speedjack_ms)
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
