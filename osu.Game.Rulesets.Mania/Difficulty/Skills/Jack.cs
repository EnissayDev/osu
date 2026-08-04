// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Jack : ManiaSkill
    {
        private readonly IDifficultyProcessor jackProcessor;

        public Jack(Mod[] mods, IDifficultyProcessor jackProcessor)
            : base(mods)
        {
            this.jackProcessor = jackProcessor;
        }

        protected override AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current)
        {
            jackProcessor.ProcessStrainFor(current);

            return jackProcessor.TransformStrainToAccuracyDifficulties(jackProcessor.CurrentStrain);
        }
    }
}
