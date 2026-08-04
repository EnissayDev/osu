// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class ReleaseProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.89647;

        private static readonly AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
        (
            multiplierAtSS: 1.55,
            multiplierAt99_5: 1.31,
            multiplierAt99: 1.2,
            multiplierAt98: 1.0,
            multiplierAt95: 0.91,
            multiplierAt90: 0.7,
            multiplierAt85: 0.45,
            multiplierAt80: 0.2
        );

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            CurrentStrain += ReleaseEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain) => new AccuracyDifficulties(strain, multipliers);
    }
}
