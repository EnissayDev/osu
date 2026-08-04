// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// A group of notes of near-identical difficulty, read as one note weighted by how many fell into it.
    /// </summary>
    public class Bin
    {
        public double BaseDifficulty;

        public double Count;

        public readonly double[] MeanAccuracyMultipliers = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

        /// <summary>
        /// Folds a note into this bin's running mean, so that no note ever has to be revisited.
        /// </summary>
        public void Add(AccuracyDifficulties difficulty, double weight)
        {
            for (int i = 0; i < MeanAccuracyMultipliers.Length; i++)
                MeanAccuracyMultipliers[i] = (MeanAccuracyMultipliers[i] * Count + difficulty.Multipliers[i] * weight) / (Count + weight);

            Count += weight;
        }

        public double AccuracyAt(double skill)
        {
            if (skill >= BaseDifficulty * MeanAccuracyMultipliers[0])
                return AccuracyValueMultipliers.ACCURACY_VALUES[0];
            if (skill <= 0)
                return AccuracyValueMultipliers.ACCURACY_VALUES[^1];

            for (int i = 1; i < MeanAccuracyMultipliers.Length; i++)
            {
                if (BaseDifficulty * MeanAccuracyMultipliers[i] > skill)
                    continue;

                double upperSkillBound = BaseDifficulty * MeanAccuracyMultipliers[i - 1];
                double lowerSkillBound = BaseDifficulty * MeanAccuracyMultipliers[i];

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }
    }

    /// <summary>
    /// A sparse, geometrically spaced histogram of <see cref="AccuracyDifficulties"/> that notes are folded
    /// into one at a time.
    /// </summary>
    /// <remarks>
    /// The star rating is solved for by asking a map's accuracy at many skill levels, which would otherwise
    /// cost a pass over every note each time. Binning on a log scale keeps that cost proportional to the
    /// spread of difficulties rather than to the length of the map, and never needs the maximum difficulty
    /// to be known up front.
    /// </remarks>
    public class BinnedDifficulties
    {
        /// <summary>
        /// The ratio between neighbouring bin edges. Smaller reads the map more finely, at more bins.
        /// </summary>
        private const double bin_ratio = 1.05;

        private static readonly double log_ratio = Math.Log(bin_ratio);

        /// <summary>
        /// The difficulty below which a note is read as free rather than placed on the log scale.
        /// </summary>
        /// <remarks>
        /// A strain decaying over a long stretch of nothing runs off the bottom of the scale entirely - far
        /// enough that its bin edges are no longer separable - and a note that far below any skill level is
        /// hit perfectly anyway.
        /// </remarks>
        private const double min_binned_difficulty = 1e-10;

        private readonly Dictionary<int, Bin> bins = new Dictionary<int, Bin>();

        /// <summary>
        /// Notes of no difficulty at all have no place on a log scale, but they still count towards the map's
        /// accuracy - they are the filler this whole reading exists to handle.
        /// </summary>
        private readonly Bin zeroBin = new Bin { BaseDifficulty = 0 };

        private Bin[]? flattenedBins;

        /// <summary>
        /// Every bin holding at least one note. Rebuilt only when the histogram has changed, since the star
        /// rating walks this many times over once the map is done.
        /// </summary>
        public Bin[] Bins => flattenedBins ??= bins.Values.Append(zeroBin).ToArray();

        public void Add(AccuracyDifficulties difficulty)
        {
            flattenedBins = null;

            if (difficulty.BaseDifficulty <= min_binned_difficulty)
            {
                zeroBin.Add(difficulty, 1);
                return;
            }

            int index = (int)Math.Floor(Math.Log(difficulty.BaseDifficulty) / log_ratio);

            double lowerEdge = Math.Pow(bin_ratio, index);
            double upperEdge = Math.Pow(bin_ratio, index + 1);

            // Splitting a note between the two edges it sits between keeps the histogram continuous, so that
            // a note drifting across a bin boundary can't move the star rating.
            double upperProportion = DiffUtils.ReverseLerp(difficulty.BaseDifficulty, lowerEdge, upperEdge);
            double lowerProportion = 1 - upperProportion;

            if (lowerProportion > 0)
                getOrCreate(index, lowerEdge).Add(difficulty, lowerProportion);
            if (upperProportion > 0)
                getOrCreate(index + 1, upperEdge).Add(difficulty, upperProportion);
        }

        private Bin getOrCreate(int index, double edgeDifficulty)
        {
            if (!bins.TryGetValue(index, out var bin))
                bins[index] = bin = new Bin { BaseDifficulty = edgeDifficulty };

            return bin;
        }
    }
}
