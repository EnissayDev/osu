// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Total : ManiaSkill
    {
        private readonly IReadonlyDifficultyProcessor coordinationProcessor;
        private readonly IReadonlyDifficultyProcessor jackProcessor;
        private readonly IReadonlyDifficultyProcessor releaseProcessor;
        private readonly IReadonlyDifficultyProcessor speedProcessor;
        private readonly IReadonlyDifficultyProcessor technicalProcessor;

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

        protected override AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current)
        {
            const double release_weight = 0.72;
            const int combine_lambda = 2;

            AccuracyDifficulties coordinationDifficulty = coordinationProcessor.TransformStrainToAccuracyDifficulties(coordinationProcessor.CurrentStrain);
            AccuracyDifficulties releaseDifficulty = releaseProcessor.TransformStrainToAccuracyDifficulties(releaseProcessor.CurrentStrain);
            AccuracyDifficulties speedDifficulty = speedProcessor.TransformStrainToAccuracyDifficulties(speedProcessor.CurrentStrain);
            AccuracyDifficulties jackDifficulty = jackProcessor.TransformStrainToAccuracyDifficulties(jackProcessor.CurrentStrain);
            AccuracyDifficulties technicalDifficulty = technicalProcessor.TransformStrainToAccuracyDifficulties(technicalProcessor.CurrentStrain);

            AccuracyDifficulties powerSum = AccuracyDifficulties.Pow(speedDifficulty, combine_lambda)
                                            + AccuracyDifficulties.Pow(jackDifficulty, combine_lambda)
                                            + AccuracyDifficulties.Pow(coordinationDifficulty, combine_lambda)
                                            + AccuracyDifficulties.Pow(technicalDifficulty, combine_lambda);

            AccuracyDifficulties tapDifficulty = powerSum.BaseDifficulty > 0 ? AccuracyDifficulties.Pow(powerSum, 1.0 / combine_lambda) : powerSum;

            return tapDifficulty + releaseDifficulty * release_weight;
        }
    }
}
