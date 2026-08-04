// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// A note's difficulty, expressed as the skill needed to hit it at each of
    /// <see cref="AccuracyValueMultipliers.ACCURACY_VALUES"/>.
    /// </summary>
    /// <remarks>
    /// Carrying the whole curve instead of a single number is what lets a map be read at an accuracy rather
    /// than in the abstract: a note far below the player's skill is worth full accuracy no matter how much
    /// further below it goes, so easy filler stops being able to add difficulty.
    /// </remarks>
    public readonly struct AccuracyDifficulties
    {
        /// <summary>
        /// The accuracy the raw strain is taken to describe, as an index into
        /// <see cref="AccuracyValueMultipliers.ACCURACY_VALUES"/>. Every other point is relative to it.
        /// </summary>
        private const int base_difficulty_index = 3; // The difficulty at 98%.

        public double BaseDifficulty { get; }

        public double[] Multipliers { get; }

        public AccuracyDifficulties(double difficulty, AccuracyValueMultipliers multipliers)
        {
            BaseDifficulty = difficulty;
            Multipliers = multipliers.AccuracyMultipliers;
        }

        private AccuracyDifficulties(double baseDifficulty, double[] multipliers)
        {
            BaseDifficulty = baseDifficulty;
            Multipliers = multipliers;
        }

        /// <summary>
        /// The absolute skill this note asks for at a given accuracy index.
        /// </summary>
        private double getDifficulty(int index) => BaseDifficulty * Multipliers[index];

        /// <summary>
        /// Rebuilds a base difficulty and a set of relative multipliers out of absolute skill values, so that
        /// the result of combining two curves is stored the same way as the curves that went into it.
        /// </summary>
        private static AccuracyDifficulties fromDifficultyArray(double[] difficulties)
        {
            double baseDifficulty = difficulties[base_difficulty_index];

            for (int i = 0; i < difficulties.Length; i++)
                difficulties[i] = baseDifficulty != 0 ? difficulties[i] / baseDifficulty : 0;

            return new AccuracyDifficulties(baseDifficulty, difficulties);
        }

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, int exponent)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length - 1; i++)
                newDifficulties[i] = DiffUtils.Pow(difficultiesBase.getDifficulty(i), exponent);

            return fromDifficultyArray(newDifficulties);
        }

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, double exponent)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length - 1; i++)
                newDifficulties[i] = Math.Pow(difficultiesBase.getDifficulty(i), exponent);

            return fromDifficultyArray(newDifficulties);
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length - 1; i++)
                newDifficulties[i] = left.getDifficulty(i) + right.getDifficulty(i);

            return fromDifficultyArray(newDifficulties);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, double right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length - 1; i++)
                newDifficulties[i] = left.getDifficulty(i) * right;

            return fromDifficultyArray(newDifficulties);
        }

        /// <summary>
        /// The accuracy a player of the given skill is expected to hit this note at.
        /// </summary>
        public double AccuracyAt(double skill)
        {
            if (skill >= getDifficulty(0))
                return AccuracyValueMultipliers.ACCURACY_VALUES[0];
            if (skill <= 0)
                return AccuracyValueMultipliers.ACCURACY_VALUES[^1];

            for (int i = 1; i < Multipliers.Length; i++)
            {
                if (getDifficulty(i) > skill)
                    continue;

                double upperSkillBound = getDifficulty(i - 1);
                double lowerSkillBound = getDifficulty(i);

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }

        /// <summary>
        /// The skill needed to hit this note at the given accuracy.
        /// </summary>
        public double DifficultyAt(double accuracy)
        {
            if (accuracy >= AccuracyValueMultipliers.ACCURACY_VALUES[0])
                return getDifficulty(0);
            if (accuracy <= AccuracyValueMultipliers.ACCURACY_VALUES[^1])
                return getDifficulty(Multipliers.Length - 1);

            for (int i = 1; i < Multipliers.Length; i++)
            {
                if (AccuracyValueMultipliers.ACCURACY_VALUES[i] > accuracy)
                    continue;

                double upperSkillBound = getDifficulty(i - 1);
                double lowerSkillBound = getDifficulty(i);

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerSkillBound, upperSkillBound, (accuracy - lowerAccuracyBound) / (upperAccuracyBound - lowerAccuracyBound));
            }

            return 0;
        }
    }
}
