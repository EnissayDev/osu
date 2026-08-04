// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    /// <summary>
    /// A view of a running strain for skills that read another skill's strain without advancing it.
    /// </summary>
    public interface IReadonlyDifficultyProcessor
    {
        /// <summary>
        /// The strain as of the note last processed.
        /// </summary>
        double CurrentStrain { get; }

        /// <summary>
        /// Reads a strain of this skill as the skill needed to hit it at each accuracy.
        /// </summary>
        AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain);
    }
}
