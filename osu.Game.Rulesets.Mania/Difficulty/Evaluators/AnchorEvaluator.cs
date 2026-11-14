// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class AnchorEvaluator
    {
        private const double uneven = 0.3;
        private const double anchored = 0.9;

        /// <summary>
        /// How many overlapping gates the buff is paid over, each one a step further into the hierarchy than the last.
        /// </summary>
        private const int tier_count = 3;

        /// <summary>
        /// How much more a repeat is worth for sitting in a section that keeps returning to one column.
        /// </summary>
        public static double EvaluateBonusOf(RowLoad load)
        {
            if (HandLayout.FingerCount(load.FingerRates.Length) < 2.0)
                return 0.0;

            double anchorValue = anchorValueOf(load.FingerRates);
            double bonus = 0.0;

            // Each tier walks its gate the same fraction of the way towards a section that never leaves one column,
            // so a merely uneven section only opens the first of them while a running man or a jackhammer pattern opens all
            // of them at once. See https://www.desmos.com/calculator/3ltgc5bycl
            for (int tier = 0; tier < tier_count; tier++)
            {
                double step = (double)tier / tier_count;
                double gateStart = uneven + (anchored - uneven) * step;
                double gateEnd = anchored + (1.0 - anchored) * step;

                // The last tier means that the hand never leaves the column.
                double weight = tier == tier_count - 1 ? tier_count : 1.0;

                bonus += weight * DiffUtils.Smoothstep(anchorValue, gateStart, gateEnd);
            }

            return bonus;
        }

        private static double anchorValueOf(double[] rates)
        {
            double[] usage = (double[])rates.Clone();

            Array.Sort(usage);
            Array.Reverse(usage);

            double walkSum = 0.0;
            double maxWalkSum = 0.0;

            for (int i = 0; i + 1 < usage.Length; i++)
            {
                double currentUsage = usage[i];
                double nextUsage = usage[i + 1];

                if (nextUsage <= 0.0)
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
    }
}
