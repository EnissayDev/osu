// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class TechnicalEvaluator
    {
        private const double speed_factor_offset = 0.050;

        private const double reversal_base_complexity = 0.6;
        private const double reversal_coefficient_multiplier = 2.0;

        private const double wide_jump_nerf = 0.60;
        private const double wide_jump_span_lo = 3.0;
        private const double wide_jump_span_hi = 5.5;

        private const double pattern_buff = 0.69740;
        private const double technical_scale = 1.49964;

        private const double rhythm_tech_buff = 0.9;
        private const double rhythm_tech_center = 0.15;
        private const double rhythm_tech_width = 0.085;

        private const double variety_floor = 1.55;
        private const double variety_lo = 2.5;
        private const double variety_hi = 5.5;
        private const double variety_gap_log_base = 1.18;

        private const double total_weight = 1.58087; // sqrt(2.49916)

        public static double EvaluateRhythmIrregularityOf(ManiaDifficultyHitObject hitObject, double previousDeltaTime)
        {
            if (previousDeltaTime <= ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            double ratio = hitObject.DeltaTime / previousDeltaTime;

            if (ratio > 1.0)
                ratio = 1.0 / ratio;

            return 1.0 - ratio;
        }

        /// <summary>
        /// The (rhythm class, movement direction) shape of this note, used by the pattern-variety window.
        /// </summary>
        public static (int rhythmClass, int direction) EvaluateShapeOf(ManiaDifficultyHitObject hitObject)
        {
            int rhythmClass = (int)Math.Round(Math.Log(hitObject.DeltaTime) / Math.Log(variety_gap_log_base));
            int direction = hitObject.Previous() is ManiaDifficultyHitObject previous ? Math.Sign(hitObject.Column - previous.Column) : 0;

            return (rhythmClass, direction);
        }

        public static double EvaluatePatternVarietyOf(int distinctShapeCount)
            => DiffUtils.Smoothstep(distinctShapeCount, variety_lo, variety_hi);

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject, double rhythmIrregularity, double patternVariety, double windowedIrregularity)
        {
            double columnComplexity = evaluateColumnComplexityOf(hitObject);
            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + speed_factor_offset);
            double complexity = Math.Max(rhythmIrregularity + columnComplexity, variety_floor * patternVariety);
            double rhythmAmplifier = 1.0 + rhythm_tech_buff * DiffUtils.BellCurve(windowedIrregularity, rhythm_tech_center, rhythm_tech_width);

            return pattern_buff * complexity * speedFactor * technical_scale * rhythmAmplifier * hitObject.ManipulationFactor * total_weight;
        }

        private static double evaluateColumnComplexityOf(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.Previous() is not ManiaDifficultyHitObject previous || hitObject.Previous(1) is not ManiaDifficultyHitObject previous2)
                return 0.0;

            double columnComplexity = 0.0;

            int previousDirection = previous.Column - previous2.Column;
            int currentDirection = hitObject.Column - previous.Column;

            if (previousDirection != 0 && currentDirection != 0 && Math.Sign(previousDirection) != Math.Sign(currentDirection))
            {
                double coefficient = CrossColumnUtils.SumBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length);
                columnComplexity += reversal_base_complexity + reversal_coefficient_multiplier * coefficient;
            }

            if (Math.Abs(currentDirection) >= 2)
                columnComplexity += CrossColumnUtils.AverageBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length); // wide jump, averaged path scaled by sqrt(span)

            double spanDamper = 1.0 - wide_jump_nerf * DiffUtils.Smoothstep(Math.Abs(currentDirection), wide_jump_span_lo, wide_jump_span_hi);
            return columnComplexity * spanDamper;
        }
    }
}
