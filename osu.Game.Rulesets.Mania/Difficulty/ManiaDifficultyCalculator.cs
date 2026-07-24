// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double overall_multiplier = 0.360643;
        private const double power_exponent = 0.52899;

        // Strain-length bonus. Every map starts nerfed by strain_length_base_nerf; the bonus then lifts it back toward its full
        // rating in proportion to how much sustained difficulty it carries (difficulty-weighted strain count, taiko's
        // length-bonus measure). A full-length map earns the whole bonus and lands exactly on its un-nerfed rating; a
        // short map earns little and stays down. Long holds add content the note count can't see, so they count toward
        // "full length".
        private const double strain_length_base_nerf = 0.195;
        private const double strain_length_full_strains = 420.0;
        private const double strain_length_max = strain_length_base_nerf / (1.0 - strain_length_base_nerf); // restores a full-length map to exactly 1.0
        private const double strain_length_hold_lo = 250.0;
        private const double strain_length_hold_hi = 450.0;

        private const double consistency_base_nerf = 0.18;
        private const double consistency_bonus_max = consistency_base_nerf / (1.0 - consistency_base_nerf); // restores a fully consistent map to exactly 1.0
        private const double consistency_ratio_lo = 0.24;
        private const double consistency_ratio_hi = 0.50;

        private const double jack_breadth_buff = 0.05;
        private const double jack_breadth_jack_lo = 5.6;
        private const double jack_breadth_jack_hi = 6.0;
        private const double jack_breadth_speed_lo = 3.0;
        private const double jack_breadth_speed_hi = 3.5;
        private const double jack_breadth_dominance_lo = 0.78;
        private const double jack_breadth_dominance_hi = 0.85;
        private const double jack_breadth_fade_lo = 9.6;
        private const double jack_breadth_fade_hi = 11.0;

        private const double od_weight = 0.188;

        private readonly bool isForCurrentRuleset;

        public override int Version => 20241007;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            var totalSkill = skills.OfType<Total>().Single();
            var speedSkill = skills.OfType<Speed>().Single();
            var technicalSkill = skills.OfType<Technical>().Single();
            var jackSkill = skills.OfType<Jack>().Single();
            var coordinationSkill = skills.OfType<Coordination>().Single();
            var releaseSkill = skills.OfType<Release>().Single();

            HitWindows hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            // Hard rock and ez don't apply directly to od, so we manually scale the hit windows.
            double windowScale = 1.0;
            if (mods.Any(m => m is ManiaModHardRock))
                windowScale = 1.0 / ManiaModHardRock.HIT_WINDOW_DIFFICULTY_MULTIPLIER;
            else if (mods.Any(m => m is ManiaModEasy))
                windowScale = 1.0 / ManiaModEasy.HIT_WINDOW_DIFFICULTY_MULTIPLIER;

            double greatHitWindow = hitWindows.WindowFor(HitResult.Great) * windowScale;
            double odMult = hitWindowMultiplier(greatHitWindow);

            int totalNotes = beatmap.HitObjects.Count;
            int holdNotes = beatmap.HitObjects.Count(h => h is HoldNote);

            double lnRatio = totalNotes > 0 ? (double)holdNotes / totalNotes : 0.0;

            double meanHoldMs = holdNotes > 0 ? beatmap.HitObjects.OfType<HoldNote>().Average(h => h.Duration) : 0.0;
            double lengthBonus = strainLengthBonus(totalSkill.CountDifficultStrains(), meanHoldMs);
            double consistencyMult = consistencyBonus(totalSkill.SustainRatio());

            double totalDifficulty = totalSkill.DifficultyValue();
            double coordinationDifficulty = coordinationSkill.DifficultyValue();

            double speedStarRating = scaleToStarRating(speedSkill.DifficultyValue()) * odMult;
            double technicalStarRating = scaleToStarRating(technicalSkill.DifficultyValue()) * odMult;
            double jackStarRating = scaleToStarRating(jackSkill.DifficultyValue()) * odMult;
            double coordinationStarRating = scaleToStarRating(coordinationDifficulty) * odMult;
            double releaseStarRating = scaleToStarRating(releaseSkill.DifficultyValue()) * odMult;

            double starRating = scaleToStarRating(totalDifficulty * consistencyMult)
                                * odMult
                                * lengthBonus;
            starRating *= jackBreadthBuff(jackStarRating, speedStarRating, starRating);

            return new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
                SpeedDifficulty = speedStarRating,
                TechnicalDifficulty = technicalStarRating,
                JackDifficulty = jackStarRating,
                CoordinationDifficulty = coordinationStarRating,
                ReleaseDifficulty = releaseStarRating,
                Variety = participationRatio(speedStarRating, technicalStarRating, jackStarRating, coordinationStarRating, releaseStarRating),
                LnRatio = lnRatio,
                GreatHitWindow = greatHitWindow,
                MeanManipulation = meanManipulation,
                NoteCount = totalNotes,
                HoldNoteCount = holdNotes,
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty
            };
        }

        private static double participationRatio(params double[] difficulties)
        {
            double sum = 0;
            double sumSquares = 0;

            foreach (double d in difficulties)
            {
                sum += d;
                sumSquares += d * d;
            }

            return sumSquares > 0 ? sum * sum / sumSquares : 1.0;
        }

        private static double strainLengthBonus(double difficultStrains, double meanHoldMs)
        {
            double lengthFraction = Math.Clamp(difficultStrains / strain_length_full_strains, 0.0, 1.0);

            // Long holds add sustained content the note count can't see, so treat them as filling out the length.
            double holdProtection = DiffUtils.Smoothstep(meanHoldMs, strain_length_hold_lo, strain_length_hold_hi);
            lengthFraction = 1.0 - (1.0 - lengthFraction) * (1.0 - holdProtection);

            return (1.0 - strain_length_base_nerf) * (1.0 + strain_length_max * lengthFraction);
        }

        private static double jackBreadthBuff(double jackStarRating, double speedStarRating, double starRating)
        {
            double jackGate = DiffUtils.Smoothstep(jackStarRating, jack_breadth_jack_lo, jack_breadth_jack_hi);
            double speedGate = DiffUtils.Smoothstep(speedStarRating, jack_breadth_speed_lo, jack_breadth_speed_hi);

            double dominance = starRating > 0.0 ? jackStarRating / starRating : 0.0;
            double dominanceGate = DiffUtils.Smoothstep(dominance, jack_breadth_dominance_lo, jack_breadth_dominance_hi);

            double highEndFade = 1.0 - DiffUtils.Smoothstep(starRating, jack_breadth_fade_lo, jack_breadth_fade_hi);

            return 1.0 + jack_breadth_buff * jackGate * speedGate * dominanceGate * highEndFade;
        }

        private static double consistencyBonus(double sustainRatio)
        {
            double consistency = DiffUtils.Smoothstep(sustainRatio, consistency_ratio_lo, consistency_ratio_hi);
            return (1.0 - consistency_base_nerf) * (1.0 + consistency_bonus_max * consistency);
        }

        private static double scaleToStarRating(double aggregatedDifficulty)
        {
            if (aggregatedDifficulty <= 0)
                return 0.0;

            return overall_multiplier * DiffUtils.Pow(aggregatedDifficulty, power_exponent);
        }

        private static double hitLeniency(double greatHitWindow) => 0.6 * (greatHitWindow - 90) + 90;

        private double hitWindowMultiplier(double greatHitWindow)
        {
            const double od8_great_window = 40.0;

            // Our hit window multiplier is scaled around a base value of od8 (40ms)
            double raw = hitLeniency(od8_great_window) / hitLeniency(greatHitWindow);
            return 1.0 + od_weight * (raw - 1.0);
        }

        private int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var sortedObjects = beatmap.HitObjects.ToList();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            sortedObjects.Sort(Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Count; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            ManiaMapData mapData = new ManiaMapData(objects.Cast<ManiaDifficultyHitObject>().ToList());
            ManiaManipulationDifficultyPreprocessor.ProcessAndAssign(mapData, totalColumns);

            meanManipulation = objects.Count > 0
                ? objects.Cast<ManiaDifficultyHitObject>().Average(o => o.ManipulationFactor)
                : 1.0;

            return objects;
        }

        private double meanManipulation = 1.0;

        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            SpeedProcessor speedProcessor = new SpeedProcessor();
            TechnicalProcessor technicalProcessor = new TechnicalProcessor();
            JackProcessor jackProcessor = new JackProcessor();
            CoordinationProcessor coordinationProcessor = new CoordinationProcessor();
            ReleaseProcessor releaseProcessor = new ReleaseProcessor();

            return new Skill[]
            {
                new Speed(mods, speedProcessor),
                new Technical(mods, technicalProcessor),
                new Jack(mods, jackProcessor),
                new Coordination(mods, coordinationProcessor),
                new Release(mods, releaseProcessor),
                new Total(mods, coordinationProcessor, jackProcessor, releaseProcessor, speedProcessor, technicalProcessor),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),
                };

                if (isForCurrentRuleset)
                    return mods;

                return mods.Concat(new Mod[]
                {
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                }).ToArray();
            }
        }
    }
}
