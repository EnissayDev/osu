// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    /// <summary>
    /// This skill is processed last, to ensure that the rest of the skills are able to process the current note in each <see cref="IReadonlyDifficultyProcessor"/>.
    /// </summary>
    public class Total : ManiaSkill
    {
        private readonly IReadonlyDifficultyProcessor coordinationProcessor;
        private readonly IReadonlyDifficultyProcessor jackProcessor;
        private readonly IReadonlyDifficultyProcessor releaseProcessor;
        private readonly IReadonlyDifficultyProcessor speedProcessor;
        private readonly IReadonlyDifficultyProcessor technicalProcessor;

        private const double release_weight = 0.72;

        public Total(Mod[] mods,
                     IReadonlyDifficultyProcessor coordinationProcessor,
                     IReadonlyDifficultyProcessor jackProcessor,
                     IReadonlyDifficultyProcessor releaseProcessor,
                     IReadonlyDifficultyProcessor speedProcessor,
                     IReadonlyDifficultyProcessor technicalProcessor)
            : base(mods)
        {
            this.coordinationProcessor = coordinationProcessor;
            this.jackProcessor = jackProcessor;
            this.releaseProcessor = releaseProcessor;
            this.speedProcessor = speedProcessor;
            this.technicalProcessor = technicalProcessor;
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            double coordinationDifficulty = coordinationProcessor.CurrentStrain;
            double releaseDifficulty = releaseProcessor.CurrentStrain;
            double speedDifficulty = speedProcessor.CurrentStrain;
            double jackDifficulty = jackProcessor.CurrentStrain;
            double technicalDifficulty = technicalProcessor.CurrentStrain;

            const int combine_lambda = 2;

            double powerSum = DiffUtils.Pow(speedDifficulty, combine_lambda)
                              + DiffUtils.Pow(jackDifficulty, combine_lambda)
                              + DiffUtils.Pow(coordinationDifficulty, combine_lambda)
                              + DiffUtils.Pow(technicalDifficulty, combine_lambda);

            double tapDifficulty = powerSum > 0 ? DiffUtils.Pow(powerSum, 1.0 / combine_lambda) : 0.0;

            return tapDifficulty + release_weight * releaseDifficulty;
        }
    }
}
