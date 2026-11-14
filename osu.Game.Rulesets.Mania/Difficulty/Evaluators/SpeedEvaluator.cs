// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double speed_weight = 2.4;

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
            const double jack_speed_nerf = 0.5;

            double tapRate = 1000.0 / (hitObject.DeltaTime + tap_rate_offset_ms);
            tapRate *= speedGrowth(hitObject);

            bool isJack = hitObject.Previous() is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            double patternMultiplier = isJack ? jack_speed_nerf : 1.0;

            const double repetition_relief = 0.95;
            double settled = 1.0 - repetition_relief * hitObject.Row.Repetition;

            return tapRate * patternMultiplier * settled * previousRowMultiplier(hitObject) * speed_weight;
        }

        /// <summary>
        /// A softer take on coordination's previous-row check. A column that has been idle far longer than the
        /// flow around it means the finger landing here has been resting, so the tap asks a little less. This is
        /// kept mild (unlike coordination's, which shapes chording) so it only trims isolated refreshes out of a
        /// stream rather than reshaping the stream itself.
        /// </summary>
        private static double previousRowMultiplier(ManiaDifficultyHitObject hitObject)
        {
            const double min_share = 0.85;
            const double idle_ratio = 3.0;
            const double idle_ratio_full = 6.0;

            double flow = hitObject.DeltaTime;

            if (flow <= 0.0)
                return 1.0;

            double ratio = hitObject.ColumnDelta / flow;

            return 1.0 - DiffUtils.Smoothstep(ratio, idle_ratio, idle_ratio_full) * (1 - min_share);
        }

        /// <summary>
        /// Soften growth because a short speed spike does not demand as much as a sustained difficulty:
        /// a row much faster than its previous history gets supressed, while sustained speed at any density passes entirely.
        /// Sustained density additionally earns a reward, except where it is a cycle the hand is already holding.
        /// </summary>
        private static double speedGrowth(ManiaDifficultyHitObject hitObject)
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

            double spikeNerf = 1.0;

            if (rows >= 4)
            {
                double rowGap = hitObject.Row.GapBefore;

                if (!double.IsPositiveInfinity(rowGap) && rowGap > 0.0)
                {
                    double ratio = (1000.0 / rowGap) / (rows / (window_ms / 1000.0));
                    spikeNerf = 1.0 - 0.5 * DiffUtils.Smoothstep(ratio, 2.0, 4.0);
                }
            }

            double sustainReward = 0.0;

            if (longRows >= 20)
            {
                double longEven = 1.0 - (double)longShared / longRows;
                sustainReward = 0.6 * DiffUtils.Smoothstep(longRows, 20.0, 30.0) * DiffUtils.Smoothstep(longEven, 0.6, 0.8) * (1.0 - hitObject.Row.Repetition);
            }

            return spikeNerf * (1.0 + sustainReward);
        }
    }
}
