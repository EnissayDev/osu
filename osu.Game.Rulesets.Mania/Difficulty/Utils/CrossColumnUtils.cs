// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class CrossColumnUtils
    {
        // Multipliers for the boundaries between columns, for key counts 1K to 10K.
        private static readonly double[][] boundary_multipliers_per_column =
        {
            new[] { 0.075, 0.075 }, // 1K
            new[] { 0.125, 0.05, 0.125 }, // 2K
            new[] { 0.125, 0.125, 0.125, 0.125 }, // 3K
            new[] { 0.175, 0.25, 0.05, 0.25, 0.175 }, // 4K
            new[] { 0.175, 0.25, 0.175, 0.175, 0.25, 0.175 }, // 5K
            new[] { 0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225 }, // 6K
            new[] { 0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225 }, // 7K
            new[] { 0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275 }, // 8K
            new[] { 0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275 }, // 9K
            new[] { 0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325 } // 10K
        };

        public static double SumBoundaryMultipliersBetween(int columnA, int columnB, int totalColumns)
        {
            double[] coefficients = totalColumns >= 1 && totalColumns <= boundary_multipliers_per_column.Length
                ? boundary_multipliers_per_column[totalColumns - 1]
                : fallbackBoundaryMultipliers(totalColumns);

            int lowColumn = Math.Min(columnA, columnB);
            int highColumn = Math.Max(columnA, columnB);
            double sum = 0.0;

            for (int boundary = lowColumn + 1; boundary <= highColumn && boundary < coefficients.Length; boundary++)
                sum += coefficients[boundary];

            return sum;
        }

        public static double ColumnBoundaryMultiplier(int boundaryIndex, int totalColumns)
        {
            double[] coefficients = totalColumns >= 1 && totalColumns <= boundary_multipliers_per_column.Length
                ? boundary_multipliers_per_column[totalColumns - 1]
                : fallbackBoundaryMultipliers(totalColumns);

            return boundaryIndex >= 0 && boundaryIndex < coefficients.Length ? coefficients[boundaryIndex] : 0.0;
        }

        public static double AverageBoundaryMultipliersBetween(int columnA, int columnB, int totalColumns)
        {
            int span = Math.Abs(columnA - columnB);

            if (span <= 0)
                return 0.0;

            return SumBoundaryMultipliersBetween(columnA, columnB, totalColumns) / span * Math.Sqrt(span);
        }

        private static double[] fallbackBoundaryMultipliers(int keyCount)
        {
            double[] fallback = new double[keyCount + 1];

            for (int i = 0; i < fallback.Length; i++)
                fallback[i] = 1.0 / fallback.Length;

            return fallback;
        }
    }
}
