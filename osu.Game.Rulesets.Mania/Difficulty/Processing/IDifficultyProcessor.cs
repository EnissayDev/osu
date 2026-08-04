// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    /// <summary>
    /// A running strain that the skill owning it advances, one note at a time.
    /// </summary>
    public interface IDifficultyProcessor : IReadonlyDifficultyProcessor
    {
        /// <summary>
        /// Advances the strain onto <paramref name="current"/>.
        /// </summary>
        void ProcessStrainFor(DifficultyHitObject current);
    }
}
