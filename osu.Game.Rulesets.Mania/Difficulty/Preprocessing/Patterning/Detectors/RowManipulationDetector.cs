// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// A <see cref="ManipulationDetector"/> whose pattern belongs to the whole row, so every note of the row is
    /// discounted by the same amount.
    /// </summary>
    public abstract class RowManipulationDetector : ManipulationDetector
    {
        /// <summary>
        /// How much of <paramref name="row"/>'s difficulty is left once this pattern is taken into account.
        /// 1 means it was left alone.
        /// </summary>
        public double EvaluateFactorOf(ManiaRow row) => row.GapBefore < Onset ? PlateauOf(row, StrengthOf(row)) : 1.0;

        /// <summary>
        /// How strongly this pattern was detected at <paramref name="row"/>, where 0 is not at all.
        /// </summary>
        protected abstract double StrengthOf(ManiaRow row);
    }
}
