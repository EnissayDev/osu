// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class AnchorEvaluator
    {
        private const double anchor_buff = 1.0;
        private const double anchor_window_ms = 400.0;
        private const double anchor_gate_lo = 0.40;
        private const double anchor_gate_hi = 0.85;

        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current)
        {
            int totalColumns = current.Row.TotalColumns;

            if (totalColumns < 2)
                return 1.0;

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

                double ratio = nextUsage / currentUsage;
                double difference = 0.5 - ratio;
                double balanceFactor = 1.0 - 4.0 * difference * difference;

                walkSum += currentUsage * balanceFactor;
                maxWalkSum += currentUsage;
            }

            double anchorValue = maxWalkSum != 0.0 ? walkSum / maxWalkSum : 0.0;

            return 1.0 + anchor_buff * DiffUtils.Smoothstep(anchorValue, anchor_gate_lo, anchor_gate_hi);
        }

        /// <summary>
        /// How much each column is pressed around <paramref name="current"/>, with nearby rows counting for more.
        /// </summary>
        private static double[] columnUsage(ManiaDifficultyHitObject current, int totalColumns)
        {
            double[] usage = new double[totalColumns];
            double center = current.StartTime;

            foreach (var row in current.Row.RowsWithin(anchor_window_ms, center))
            {
                double distance = Math.Abs(row.StartTime - center) / anchor_window_ms;
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
