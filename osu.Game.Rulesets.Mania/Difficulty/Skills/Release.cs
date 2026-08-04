// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Release : ManiaSkill
    {
        private readonly IDifficultyProcessor releaseProcessor;

        public Release(Mod[] mods, IDifficultyProcessor releaseProcessor)
            : base(mods)
        {
            this.releaseProcessor = releaseProcessor;
        }

        protected override AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current)
        {
            releaseProcessor.ProcessStrainFor(current);

            return releaseProcessor.TransformStrainToAccuracyDifficulties(releaseProcessor.CurrentStrain);
        }
    }
}
