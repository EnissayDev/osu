// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Coordination : ManiaSkill
    {
        private readonly IDifficultyProcessor coordinationProcessor;

        public Coordination(Mod[] mods, IDifficultyProcessor coordinationProcessor)
            : base(mods)
        {
            this.coordinationProcessor = coordinationProcessor;
        }

        protected override AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current)
        {
            coordinationProcessor.ProcessStrainFor(current);

            return coordinationProcessor.TransformStrainToAccuracyDifficulties(coordinationProcessor.CurrentStrain);
        }
    }
}
