// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double base_coefficient = 4.243;
        private const double base_sr_offset = 0.15;
        private const double base_exponent = 2.470;

        private const double accuracy_sr_lo = 6.0;
        private const double accuracy_sr_hi = 11.0;
        private const double accuracy_shift_easy = 155.0;
        private const double accuracy_shift_hard = 155.0;
        private const double accuracy_exp_easy = 3.4;
        private const double accuracy_exp_hard = 2.2;
        private const double accuracy_min = 0.55;
        private const double accuracy_max = 1.13;
        private const double accuracy_max_hard = 1.20;
        private const double accuracy_ceiling_sr_lo = 10.0;
        private const double accuracy_ceiling_sr_hi = 11.5;

        private const double low_acc_fade_lo = 0.83;
        private const double low_acc_fade_hi = 0.925;

        private const double release_reward_strength = 0.38;
        private const double release_reward_lo = 1.0;
        private const double release_reward_hi = 3.0;

        private const double variety_floor = 0.88;
        private const double variety_cap = 1.10;
        private const double variety_midpoint = 3.7;
        private const double variety_steepness = 2.0;

        private const double dense_buff = 0.18;
        private const double dense_coact_lo = 3.0;
        private const double dense_coact_hi = 5.0;
        private const double dense_release_lo = 1.5;
        private const double dense_release_hi = 3.0;
        private const double dense_sr_taper_lo = 9.5;
        private const double dense_sr_taper_hi = 13.0;

        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;
        private bool isLegacyScore;

        private double? accuracyImpliedDeviation;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            isLegacyScore = score.isLegacyScore;

            double[] hitWindows = isLegacyScore
                ? getLegacyHitWindows(score.Mods, false, maniaAttributes.OverallDifficulty)
                : getLazerHitWindows(score.Mods, maniaAttributes.OverallDifficulty);

            accuracyImpliedDeviation = totalSuccessfulHits == 0
                ? null
                : deviationFromCustomAccuracy(calculateCustomAccuracy(), hitWindows) * 10.0;

            double multiplier = 1.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes);
            double accuracyScale = computeAccuracyScale(maniaAttributes);
            double varietyMultiplier = this.varietyMultiplier(maniaAttributes.Variety);
            double lengthMultiplier = this.lengthMultiplier(totalHits, maniaAttributes.StarRating);
            double totalValue = difficultyValue * accuracyScale * varietyMultiplier * lengthMultiplier * multiplier;

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                //EstimatedUnstableRate = accuracyImpliedDeviation,
                Total = totalValue
            };
        }

        private double varietyMultiplier(double variety)
        {
            const double range = variety_cap - variety_floor;
            return variety_floor + DiffUtils.Logistic(variety, variety_midpoint, variety_steepness, range);
        }

        private double lengthMultiplier(double totalNotes, double starRating)
        {
            if (totalNotes <= 0)
                return 1.0;

            return 1.1 / (1.0 + Math.Sqrt(starRating / (2.0 * totalNotes)));
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            return base_coefficient * DiffUtils.Pow(Math.Max(attributes.StarRating - base_sr_offset, 0.05), base_exponent) * denseFastMultiplier(attributes);
        }

        private double computeAccuracyScale(ManiaDifficultyAttributes attributes)
        {
            if (accuracyImpliedDeviation == null)
                return 0;

            double customAccuracy = calculateCustomAccuracy();
            double lowAccFade = DiffUtils.Smoothstep(customAccuracy, low_acc_fade_lo, low_acc_fade_hi);

            // Discount the implied deviation on release-heavy (LN) charts so that LN plays are rewarded.
            double adjustedDeviation = accuracyImpliedDeviation.Value / (1.0 + release_reward_strength * DiffUtils.Smoothstep(attributes.ReleaseDifficulty, release_reward_lo, release_reward_hi));

            return lowAccFade * accuracyScaling(adjustedDeviation, attributes.StarRating);
        }

        private static double accuracyScaling(double deviation, double starRating)
        {
            double hardness = DiffUtils.Smoothstep(starRating, accuracy_sr_lo, accuracy_sr_hi);
            double shift = accuracy_shift_easy + (accuracy_shift_hard - accuracy_shift_easy) * hardness;
            double exponent = accuracy_exp_easy + (accuracy_exp_hard - accuracy_exp_easy) * hardness;

            double precision = DiffUtils.Pow(DiffUtils.Erf(shift / (DiffUtils.SQRT2 * Math.Max(deviation, 1e-6))), exponent);

            double ceiling = accuracy_max + (accuracy_max_hard - accuracy_max) * DiffUtils.Smoothstep(starRating, accuracy_ceiling_sr_lo, accuracy_ceiling_sr_hi);

            return accuracy_min + (ceiling - accuracy_min) * precision;
        }

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * 320 + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * 320);
        }

        #region Custom-accuracy -> deviation

        /// <summary>
        /// The custom accuracy a player of the given timing deviation is expected to achieve on a map with the given
        /// hit windows, assuming a zero-centred normal hit distribution. Monotonically decreasing in the deviation.
        /// </summary>
        private static double expectedCustomAccuracy(double deviation, double[] hitWindows)
        {
            double within(double window) => DiffUtils.Erf(window / (deviation * DiffUtils.SQRT2));

            double belowPerfect = within(hitWindows[0]);
            double belowGreat = within(hitWindows[1]);
            double belowGood = within(hitWindows[2]);
            double belowOk = within(hitWindows[3]);
            double belowMeh = within(hitWindows[4]);

            return (320 * belowPerfect
                    + 300 * (belowGreat - belowPerfect)
                    + 200 * (belowGood - belowGreat)
                    + 100 * (belowOk - belowGood)
                    + 50 * (belowMeh - belowOk)) / 320.0;
        }

        /// <summary>
        /// Recovers the timing deviation that yields the score's custom accuracy on the map's hit windows, by
        /// bisecting the (monotonic) <see cref="expectedCustomAccuracy"/>.
        /// </summary>
        private static double deviationFromCustomAccuracy(double customAccuracy, double[] hitWindows)
        {
            double lo = 0.05;
            double hi = 400.0;

            for (int i = 0; i < 90; i++)
            {
                double mid = 0.5 * (lo + hi);

                if (expectedCustomAccuracy(mid, hitWindows) > customAccuracy)
                    lo = mid;
                else
                    hi = mid;
            }

            return 0.5 * (lo + hi);
        }

        #endregion

        #region Hit windows

        private static double[] getLegacyHitWindows(Mod[] mods, bool isConvert, double overallDifficulty)
        {
            double[] legacyHitWindows = new double[5];

            double greatWindowLeniency = 0;
            double goodWindowLeniency = 0;

            // When converting beatmaps to osu!mania in stable, the resulting hit window sizes are dependent on whether the beatmap's OD is above or below 4.
            if (isConvert)
            {
                overallDifficulty = 10;

                if (overallDifficulty <= 4)
                {
                    greatWindowLeniency = 13;
                    goodWindowLeniency = 10;
                }
            }

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            legacyHitWindows[0] = Math.Floor(16 * windowMultiplier);
            legacyHitWindows[1] = Math.Floor((64 - 3 * overallDifficulty + greatWindowLeniency) * windowMultiplier);
            legacyHitWindows[2] = Math.Floor((97 - 3 * overallDifficulty + goodWindowLeniency) * windowMultiplier);
            legacyHitWindows[3] = Math.Floor((127 - 3 * overallDifficulty) * windowMultiplier);
            legacyHitWindows[4] = Math.Floor((151 - 3 * overallDifficulty) * windowMultiplier);

            return legacyHitWindows;
        }

        private static double[] getLazerHitWindows(Mod[] mods, double overallDifficulty)
        {
            double[] lazerHitWindows = new double[5];

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            if (overallDifficulty < 5)
                lazerHitWindows[0] = (22.4 - 0.6 * overallDifficulty) * windowMultiplier;
            else
                lazerHitWindows[0] = (24.9 - 1.1 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[1] = (64 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[2] = (97 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[3] = (127 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[4] = (151 - 3 * overallDifficulty) * windowMultiplier;

            return lazerHitWindows;
        }

        #endregion

        private static double denseFastMultiplier(ManiaDifficultyAttributes attributes)
        {
            double coActivation = Math.Min(attributes.SpeedDifficulty, attributes.JackDifficulty);
            double coGate = DiffUtils.Smoothstep(coActivation, dense_coact_lo, dense_coact_hi);
            double releaseGate = 1.0 - DiffUtils.Smoothstep(attributes.ReleaseDifficulty, dense_release_lo, dense_release_hi);
            double srTaper = 1.0 - DiffUtils.Smoothstep(attributes.StarRating, dense_sr_taper_lo, dense_sr_taper_hi);

            return 1.0 + dense_buff * coGate * releaseGate * srTaper;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => totalHits - countMiss;
    }
}
