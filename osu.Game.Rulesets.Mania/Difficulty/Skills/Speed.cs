// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Speed : ManiaSkill
    {
        private readonly IDifficultyProcessor speedProcessor;

        public Speed(Mod[] mods, IDifficultyProcessor speedProcessor)
            : base(mods)
        {
            this.speedProcessor = speedProcessor;
        }

        protected override AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current)
        {
            speedProcessor.ProcessStrainFor(current);

            return speedProcessor.TransformStrainToAccuracyDifficulties(speedProcessor.CurrentStrain);
        }
    }
}
