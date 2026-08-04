// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class TrillUtils
    {
        private const double trill_nerf = 0.62864;
        private const double trill_run_ramp = 4.99947;

        /// <summary>
        /// Whether <paramref name="hitObject"/> alternates back into the column it was in two notes ago (1-2-1 pattern).
        /// </summary>
        public static bool IsTrillStep(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.Previous() is not ManiaDifficultyHitObject previous ||
                hitObject.Previous(1) is not ManiaDifficultyHitObject previous2)
                return false;

            return previous.Column != hitObject.Column && previous2.Column == hitObject.Column;
        }

        /// <summary>
        /// Nerfs sustained trill runs, ramping down the longer the trill continues.
        /// </summary>
        public static double TrillFactor(ManiaDifficultyHitObject current)
        {
            if (!IsTrillStep(current))
                return 1.0;

            int cap = RunDampenUtils.CapFor(trill_run_ramp);
            int run = 1;
            var trillStep = current;

            while (run < cap)
            {
                if (trillStep.Previous() is not ManiaDifficultyHitObject previousNote || !IsTrillStep(previousNote))
                    break;

                run++;
                trillStep = previousNote;
            }

            return RunDampenUtils.Dampen(run, trill_run_ramp, 1.0 - trill_nerf);
        }
    }
}
