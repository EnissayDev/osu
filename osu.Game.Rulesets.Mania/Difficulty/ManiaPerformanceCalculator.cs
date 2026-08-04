// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double base_coefficient = 4.243 * 0.8;
        private const double base_sr_offset = 0.15;
        private const double base_exponent = 2.470;

        private const double low_end_coefficient = 7.7;
        private const double low_end_exponent = 1.63;

        private const double variety_floor = 0.88;
        private const double variety_cap = 1.10;

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
            isLegacyScore = score.Mods.Any(m => m is ManiaModClassic) && (totalHits + 0.1) > maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount;

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
            double accuracyScale = computeAccuracyScale(calculateCustomAccuracy(), maniaAttributes);
            double varietyMultiplier = this.varietyMultiplier(maniaAttributes.Variety);
            double lengthMultiplier = this.lengthMultiplier(totalHits, maniaAttributes.StarRating);
            double totalValue = difficultyValue * accuracyScale * varietyMultiplier * lengthMultiplier * multiplier;
            double valueSS = difficultyValue * varietyMultiplier * lengthMultiplier * multiplier;
            double value99 = valueSS * computeAccuracyScale(0.99, maniaAttributes);
            double value98 = valueSS * computeAccuracyScale(0.98, maniaAttributes);
            double value97 = valueSS * computeAccuracyScale(0.97, maniaAttributes);
            double value96 = valueSS * computeAccuracyScale(0.96, maniaAttributes);
            double value95 = valueSS * computeAccuracyScale(0.95, maniaAttributes);

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                //EstimatedUnstableRate = accuracyImpliedDeviation,
                Total = totalValue,
                ValueSS = valueSS,
                Value99 = value99,
                Value98 = value98,
                Value97 = value97,
                Value96 = value96,
                Value95 = value95,
                Scale99 = value99 / valueSS,
                Scale98 = value98 / value99,
                Scale97 = value97 / value98,
                Scale96 = value96 / value97,
                Scale95 = value95 / value96
            };
        }

        private double varietyMultiplier(double variety)
        {
            return variety_floor + DiffUtils.Logistic(variety, 3.7987, 2.0, variety_cap - variety_floor);
        }

        private double lengthMultiplier(double totalNotes, double starRating)
        {
            if (totalNotes <= 0)
                return 1.0;

            return 1.1 / (1.0 + Math.Sqrt(starRating / (2.0 * totalNotes)));
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            double baseValue = base_coefficient * DiffUtils.Pow(Math.Max(attributes.StarRatingSS - base_sr_offset, 0.05), base_exponent);

            double lowEndBonus = low_end_coefficient * DiffUtils.Pow(attributes.StarRating, low_end_exponent)
                                                     * DiffUtils.Smoothstep(attributes.StarRating, 7.0, 2.5);

            return (baseValue + lowEndBonus) * denseFastMultiplier(attributes);
        }

        private double computeAccuracyScale(double accuracy, ManiaDifficultyAttributes attributes)
        {
            if (accuracyImpliedDeviation == null)
                return 0;

            double scoreLoss = 1 - accuracy;

            return Math.Pow(1 -
                            PolynomialPenaltyUtils.GetPenaltyAt(new PolynomialPenaltyUtils.QuarticCoefficients(
                                attributes.ScoreLossCoefficientA,
                                attributes.ScoreLossCoefficientB,
                                attributes.ScoreLossCoefficientC,
                                attributes.ScoreLossCoefficientD), Math.Log(scoreLoss + 1))
                , base_exponent * ManiaDifficultyCalculator.STAR_RATING_EXPONENT);
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
            double coGate = DiffUtils.Smoothstep(coActivation, 3.01761, 5.02934);
            double releaseGate = DiffUtils.Smoothstep(attributes.ReleaseDifficulty, 5.20566, 2.60283);
            double srTaper = DiffUtils.Smoothstep(attributes.StarRating, 13.0, 9.5);

            return 1.0 + 0.18 * coGate * releaseGate * srTaper;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => totalHits - countMiss;
    }
}
