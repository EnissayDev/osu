// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class JackProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.3;

        public double CurrentStrain { get; private set; }

        private double accumulatedStrain;

        private double maxChordDifficulty;

        private int currentRowIndex = -1;

        private double strainDecay(double strainDecayBase, double deltaTime) => Math.Pow(strainDecayBase, deltaTime / 1000);

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            if (maniaCurrent.Row.RowIndex > currentRowIndex)
            {
                var prevRow = maniaCurrent.Row.Previous();
                double prevDecay = prevRow is not null ? strainDecay(strain_decay_base, prevRow.GapBefore) : 0;
                accumulatedStrain = maxChordDifficulty * (1 - prevDecay) + accumulatedStrain * prevDecay;

                maxChordDifficulty = 0;

                currentRowIndex = maniaCurrent.Row.RowIndex;
            }

            double jackDifficulty = JackEvaluator.EvaluateDifficultyOf(maniaCurrent);

            double decay = strainDecay(strain_decay_base, maniaCurrent.Row.GapBefore);

            maxChordDifficulty = Math.Max(jackDifficulty, maxChordDifficulty);

            CurrentStrain = jackDifficulty * (1 - decay) + accumulatedStrain * decay;
        }
    }
}
