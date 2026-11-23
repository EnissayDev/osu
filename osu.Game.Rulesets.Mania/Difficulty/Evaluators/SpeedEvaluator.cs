// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double speed_weight = 2.2;

        /// <summary>
        /// Evaluates the difficulty of tapping the current note, based on:
        /// <list type="bullet">
        /// <item><description>how long it has been since the previous note,</description></item>
        /// <item><description>whether that note was in the same column,</description></item>
        /// <item><description>and how easily the pattern around it can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            // Every note of a chord is pressed at once, so only the first of them costs a tap.
            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            const double tap_rate_offset_ms = 36;

            // A repeat in the same column is one finger doing the work of two, so it taps slower than its gap suggests.
            const double jack_speed_nerf = 0.5;            const double simple_repetition_relief = 0.95;
            const double technical_repetition_relief = 0.20;
            const double repetition_evenness_exponent = 8.0;

            double tapRate = 1000.0 / (hitObject.DeltaTime + tap_rate_offset_ms);
            var (spike, sustain) = speedGrowthOf(hitObject);

            bool isJack = hitObject.Previous() is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            // Bonus
            double sustainReward = 1.0 + 1.2 * sustain;

            // Nerfs
            double spikeNerf = 1.0 - 0.5 * spike;
            double patternMultiplier = isJack ? jack_speed_nerf : 1.0;
            // Keep the full nerf for almost perfectly even patterns, but fall away quickly as the cycle spreads less evenly.
            double repetitionRelief = technical_repetition_relief + (simple_repetition_relief - technical_repetition_relief) * Math.Pow(hitObject.Row.RepetitionEvenness, repetition_evenness_exponent);
            double settled = 1.0 - repetitionRelief * hitObject.Row.Repetition;
            double previousRow = 1.0 - previous_row_relief_multiplier * previousRowMultiplier(hitObject);
            double sameRow = sameRowMultiplier(hitObject);

            return tapRate * spikeNerf * sustainReward * patternMultiplier * settled * previousRow * sameRow * speed_weight;
        }

        /// <summary>
        /// Similar to previousGapMultiplier in evaluation coordination, it applies a nerf to the note
        /// relative to the gap between the current object and its previous note in column.
        /// </summary>
        private static double previousRowMultiplier(ManiaDifficultyHitObject hitObject)
        {
            return DiffUtils.Smoothstep(hitObject.ColumnDelta / hitObject.DeltaTime, 3.0, 6.0);
        }

        /// <summary>
        /// A lone note is a much smaller share of a wide row than it is of a narrow one, so in higher keymodes
        /// sparse rows should not tap at full speed. Rows of three notes or more are treated as dense enough
        /// to pay in full.
        /// </summary>
        private static double sameRowMultiplier(ManiaDifficultyHitObject hitObject)
        {
            const double single_note_floor = 0.05;
            const double dense_row_size = 3.0;

            int totalColumns = hitObject.Row.TotalColumns;

            if (totalColumns <= 4)
                return 1.0;

            return single_note_floor + (1.0 - single_note_floor) * DiffUtils.Smoothstep(hitObject.Row.Size, 1.0, dense_row_size);
        }

        /// <summary>
        /// Soften growth because a short speed spike does not demand as much as a sustained difficulty:
        /// a row much faster than its previous history gets supressed, while sustained speed at any density passes entirely.
        /// Sustained density additionally earns a reward, except where it is a cycle the hand is already holding.
        /// </summary>
        private static (double spike, double sustain) speedGrowthOf(ManiaDifficultyHitObject hitObject)
        {
            const double window_ms = 250.0;
            const double sustain_window_ms = 1000.0;

            int rows = 0, longRows = 0, longShared = 0;

            for (var p = hitObject.Row.Previous(); p != null && hitObject.StartTime - p.StartTime <= sustain_window_ms; p = p.Previous())
            {
                longRows++;

                if (ColumnPatternUtils.SharesColumn(hitObject.Row.Columns, p.Columns))
                    longShared++;

                if (hitObject.StartTime - p.StartTime <= window_ms)
                    rows++;
            }

            double spike = 0.0;

            if (rows >= 4)
            {
                double rowGap = hitObject.Row.GapBefore;

                if (!double.IsPositiveInfinity(rowGap) && rowGap > 0.0)
                {
                    double ratio = (1000.0 / rowGap) / (rows / (window_ms / 1000.0));
                    spike = DiffUtils.Smoothstep(ratio, 2.0, 4.0);
                }
            }

            double sustain = 0.0;

            if (longRows >= 20)
            {
                double longEven = 1.0 - (double)longShared / longRows;
                sustain = DiffUtils.Smoothstep(longRows, 20.0, 30.0) * DiffUtils.Smoothstep(longEven, 0.6, 0.8) * (1.0 - hitObject.Row.Repetition);
            }

            return (spike, sustain);
        }
    }
}
