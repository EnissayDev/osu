// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class AnchorEvaluator
    {
        /// <summary>
        /// How far either side of the current note the column hierarchy is read from.
        /// </summary>
        private const double window_ms = 400.0;

        /// <summary>
        /// How many overlapping gates the buff is paid over, each one a step further into the hierarchy than the last.
        /// </summary>
        private const int tier_count = 3;

        /// <summary>
        /// How much more a repeat is worth for sitting in a section that keeps returning to one column.
        /// </summary>
        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current)
        {
            const double anchor_buff = 1.0;

            // The anchor values a merely uneven section and an unambiguous one measure at.
            const double uneven = 0.40;
            const double anchored = 0.85;

            int totalColumns = current.Row.TotalColumns;

            if (totalColumns < 2)
                return 1.0;

            double anchorValue = anchorValueOf(current, totalColumns);
            double multiplier = 1.0;

            // Each tier walks its gate the same fraction of the way towards a section that never leaves one column,
            // so a merely uneven section only opens the first of them while a running man or a jackhammer pattern opens all
            // of them at once. See https://www.desmos.com/calculator/3ltgc5bycl
            for (int tier = 0; tier < tier_count; tier++)
            {
                double step = (double)tier / tier_count;
                double gateStart = uneven + (anchored - uneven) * step;
                double gateEnd = anchored + (1.0 - anchored) * step;

                // The last tier means that the hand never leaves the column.
                double buff = tier == tier_count - 1 ? anchor_buff * tier_count : anchor_buff;

                multiplier += buff * DiffUtils.Smoothstep(anchorValue, gateStart, gateEnd);
            }

            return multiplier;
        }

        /// <summary>
        /// How much of an anchor the section around <paramref name="current"/> is, from how its columns are used relative to each other.
        /// </summary>
        private static double anchorValueOf(ManiaDifficultyHitObject current, int totalColumns)
        {
            double[] usage = columnUsage(current, totalColumns);

            Array.Sort(usage);
            Array.Reverse(usage);

            double walkSum = 0.0;
            double maxWalkSum = 0.0;

            for (int i = 0; i + 1 < totalColumns; i++)
            {
                double currentUsage = usage[i];
                double nextUsage = usage[i + 1];

                if (nextUsage == 0.0)
                    break;

                // A downwards parabola peaking at a ratio of 1:2 between one column and the next.
                // See https://www.desmos.com/calculator/njwfgiyfmm
                double ratio = nextUsage / currentUsage;
                double difference = 0.5 - ratio;
                double balanceFactor = 1.0 - 4.0 * difference * difference;

                walkSum += currentUsage * balanceFactor;
                maxWalkSum += currentUsage;
            }

            return maxWalkSum != 0.0 ? walkSum / maxWalkSum : 0.0;
        }

        /// <summary>
        /// How much each column is pressed around <paramref name="current"/>, with nearby rows counting for more.
        /// </summary>
        private static double[] columnUsage(ManiaDifficultyHitObject current, int totalColumns)
        {
            double[] usage = new double[totalColumns];
            double center = current.StartTime;

            foreach (var row in current.Row.RowsWithin(window_ms, center))
            {
                // Falls off as a parabola over the window, reaching zero exactly at its edge.
                double distance = Math.Abs(row.StartTime - center) / window_ms;
                double weight = 1.0 - distance * distance;

                if (weight <= 0.0)
                    continue;

                foreach (int column in row.Columns)
                {
                    if (column >= 0 && column < totalColumns)
                        usage[column] += weight;
                }
            }

            return usage;
        }
    }
}
