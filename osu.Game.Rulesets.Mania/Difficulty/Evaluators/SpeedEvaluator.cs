// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double tap_rate_offset_ms = 30;
        private const double jack_speed_nerf = 0.49996;
        private const double speed_scale = 1.39127;

        private const double total_weight = 1.01112; // sqrt(1.02237)

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            double tapRate = 1000.0 / (hitObject.DeltaTime + tap_rate_offset_ms);

            bool isJack = hitObject.Previous() is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            double patternMultiplier = isJack ? jack_speed_nerf : TrillUtils.TrillFactor(hitObject);

            return tapRate * patternMultiplier * speed_scale * hitObject.ManipulationFactor * hitObject.EnduranceFactor * total_weight;
        }
    }
}
