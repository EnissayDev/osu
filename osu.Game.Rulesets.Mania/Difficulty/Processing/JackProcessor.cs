// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class JackProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.50497;

        private static readonly AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
        (
            multiplierAtSS: 1.01,
            multiplierAt99_5: 1.0075,
            multiplierAt99: 1.005,
            multiplierAt98: 1.0,
            multiplierAt95: 0.97,
            multiplierAt90: 0.84,
            multiplierAt85: 0.6,
            multiplierAt80: 0.28
        );

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= Math.Pow(strain_decay_base, current.DeltaTime / 1000);

            CurrentStrain += JackEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain) => new AccuracyDifficulties(strain, multipliers);
    }
}
