// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        private double totalNoteWeight;

        private readonly List<double> sortedDifficulties;

        private bool isSorted;

        protected int BaseNoteCount { get; private set; }

        protected ManiaSkill(Mod[] mods)
            : base(mods)
        {
            sortedDifficulties = new List<double>();
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            BaseNoteCount++;
            totalNoteWeight += getNoteWeight(current);

            double difficulty = DifficultyAt(current);

            if (difficulty > 0)
            {
                sortedDifficulties.Add(difficulty);
                isSorted = false;
            }

            return difficulty;
        }

        private double getNoteWeight(DifficultyHitObject current)
        {
            const double max_long_note_weight_duration_ms = 1000.0;
            const double long_note_weight_per_200_ms = 0.6;

            double noteWeight = 1;

            // Add additional weight for hold notes, depending on their length.
            if (current.BaseObject is HoldNote holdNote)
            {
                double duration = Math.Min(holdNote.EndTime - holdNote.StartTime, max_long_note_weight_duration_ms);
                noteWeight += long_note_weight_per_200_ms * duration / 200.0;
            }

            return noteWeight;
        }

        protected abstract double DifficultyAt(DifficultyHitObject current);

        public double SustainRatio()
        {
            if (sortedDifficulties.Count == 0)
                return 1.0;

            sortDifficulties();

            double median = strainAtPercentile(0.50);
            double high = strainAtPercentile(0.90);

            return high > 0 ? median / high : 1.0;
        }

        public double CountDifficultStrains()
        {
            if (sortedDifficulties.Count == 0)
                return 0.0;

            sortDifficulties();

            double top = strainAtPercentile(0.93);

            if (top <= 0)
                return sortedDifficulties.Count;

            return sortedDifficulties.Sum(s => DiffUtils.Logistic(s / top, 0.88, 10.0, 1.1));
        }

        /// <summary>
        /// Sorts the recorded difficulties, which every reader below needs and none of them change.
        /// </summary>
        private void sortDifficulties()
        {
            if (isSorted)
                return;

            sortedDifficulties.Sort();
            isSorted = true;
        }

        private double strainAtPercentile(double percentile) => valueAtPercentile(sortedDifficulties, percentile);

        private static double valueAtPercentile(List<double> sortedValues, double percentile)
        {
            int maxIndex = sortedValues.Count - 1;
            int index = Math.Clamp((int)Math.Round(maxIndex * percentile), 0, maxIndex);

            return sortedValues[index];
        }

        public override double DifficultyValue()
        {
            if (sortedDifficulties.Count == 0)
                return 0.0;

            sortDifficulties();

            const int power_mean_exponent = 5;

            double[] highPercentiles = { 0.945, 0.935, 0.925, 0.915 };
            double[] midPercentiles = { 0.845, 0.835, 0.825, 0.815 };

            double highMean = calculatePercentileMean(sortedDifficulties, highPercentiles);
            double midMean = calculatePercentileMean(sortedDifficulties, midPercentiles);
            double powerMean = calculatePowerMean(sortedDifficulties, power_mean_exponent);

            const double high_percentile_weight = 0.25;
            const double high_percentile_scale = 0.88;

            const double mid_percentile_weight = 0.20;
            const double mid_percentile_scale = 0.94;

            const double power_mean_weight = 0.55;

            double rawDifficulty = high_percentile_weight * (high_percentile_scale * highMean)
                                   + mid_percentile_weight * (mid_percentile_scale * midMean)
                                   + power_mean_weight * powerMean;

            const double note_count_offset = 34.64147;
            const double final_scaling = 0.90741;

            return rawDifficulty * (totalNoteWeight / (totalNoteWeight + note_count_offset)) * final_scaling;
        }

        /// <summary>
        /// Calculates the mean of specific percentile positions of <paramref name="sortedValues"/>.
        /// </summary>
        private static double calculatePercentileMean(List<double> sortedValues, double[] percentiles)
        {
            double sum = 0.0;

            foreach (double percentile in percentiles)
                sum += valueAtPercentile(sortedValues, percentile);

            return sum / percentiles.Length;
        }

        private static double calculatePowerMean(List<double> values, int exponent)
        {
            double sum = values.Sum(value => DiffUtils.Pow(value, exponent));
            return DiffUtils.Pow(sum / values.Count, 1.0 / exponent);
        }
    }
}
