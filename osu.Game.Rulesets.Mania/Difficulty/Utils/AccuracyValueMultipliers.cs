// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// How far above or below a note's difficulty a player has to be to hit it at a given accuracy, as a
    /// multiplier of that difficulty.
    /// </summary>
    /// <remarks>
    /// A skill exactly equal to a note's difficulty scores the accuracy at <see cref="ACCURACY_VALUES"/>[3];
    /// every other entry says how much more or less skill the same note asks for to be hit better or worse.
    /// Each skill reads this differently: a release is far more forgiving to almost-hit than a jack is.
    /// </remarks>
    public readonly struct AccuracyValueMultipliers
    {
        /// <summary>
        /// The accuracies the multipliers are given at, descending. The last one is the accuracy a note is
        /// assumed to be worth when it is completely out of reach.
        /// </summary>
        public static readonly double[] ACCURACY_VALUES = { 1.00, 0.995, 0.99, 0.98, 0.95, 0.90, 0.85, 0.80, 0.75 };

        public readonly double[] AccuracyMultipliers;

        public AccuracyValueMultipliers(
            double multiplierAtSS,
            double multiplierAt99_5,
            double multiplierAt99,
            double multiplierAt98,
            double multiplierAt95,
            double multiplierAt90,
            double multiplierAt85,
            double multiplierAt80)
        {
            AccuracyMultipliers = new[]
            {
                multiplierAtSS,
                multiplierAt99_5,
                multiplierAt99,
                multiplierAt98,
                multiplierAt95,
                multiplierAt90,
                multiplierAt85,
                multiplierAt80,
                0.0 // A note asks for no skill at all to be worth the floor accuracy.
            };
        }
    }
}
