// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class SpeedProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.05007;

        private static readonly AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
        (
            multiplierAtSS: 1.2,
            multiplierAt99_5: 1.125,
            multiplierAt99: 1.05,
            multiplierAt98: 1.0,
            multiplierAt95: 0.84,
            multiplierAt90: 0.65,
            multiplierAt85: 0.4,
            multiplierAt80: 0.1
        );

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            CurrentStrain += SpeedEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain) => new AccuracyDifficulties(strain, multipliers);
    }
}
