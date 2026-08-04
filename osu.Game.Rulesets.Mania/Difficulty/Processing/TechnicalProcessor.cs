// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class TechnicalProcessor : IDifficultyProcessor
    {
        private static readonly AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
        (
            multiplierAtSS: 1.46,
            multiplierAt99_5: 1.38,
            multiplierAt99: 1.25,
            multiplierAt98: 1.1,
            multiplierAt95: 0.88,
            multiplierAt90: 0.7,
            multiplierAt85: 0.55,
            multiplierAt80: 0.25
        );

        public double CurrentStrain { get; private set; }

        private const double strain_decay_base = 0.06696;

        /// <summary>
        /// How many notes back the rolling irregularity is averaged over.
        /// </summary>
        private const int rhythm_window = 10;

        /// <summary>
        /// How many notes back distinct shapes are counted over.
        /// </summary>
        private const int variety_window = 8;

        private readonly Queue<double> recentIrregularities = new Queue<double>();
        private double irregularitySum;

        private readonly Queue<(int rhythmClass, int direction)> recentShapes = new Queue<(int, int)>();
        private readonly (int rhythmClass, int direction)[] shapeBuffer = new (int, int)[variety_window];

        private double previousDeltaTime = -1.0;

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            var hitObject = (ManiaDifficultyHitObject)current;

            // Every note of a chord is pressed at once, so the pattern only steps forward on the first of them.
            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return;

            double rhythmIrregularity = TechnicalEvaluator.EvaluateRhythmIrregularityOf(hitObject, previousDeltaTime);
            previousDeltaTime = hitObject.DeltaTime;

            CurrentStrain += TechnicalEvaluator.EvaluateDifficultyOf(hitObject, rhythmIrregularity, patternVariety(hitObject), windowedIrregularity(rhythmIrregularity));
        }

        /// <summary>
        /// The mean irregularity over the last few notes. A single spacing change is a rhythm the player reads
        /// once, while a passage of them is a rhythm the player has to keep reading.
        /// </summary>
        private double windowedIrregularity(double rhythmIrregularity)
        {
            recentIrregularities.Enqueue(rhythmIrregularity);
            irregularitySum += rhythmIrregularity;

            while (recentIrregularities.Count > rhythm_window)
                irregularitySum -= recentIrregularities.Dequeue();

            return irregularitySum / recentIrregularities.Count;
        }

        private double patternVariety(ManiaDifficultyHitObject hitObject)
        {
            recentShapes.Enqueue(TechnicalEvaluator.EvaluateShapeOf(hitObject));

            while (recentShapes.Count > variety_window)
                recentShapes.Dequeue();

            return TechnicalEvaluator.EvaluatePatternVarietyOf(distinctShapeCount());
        }

        /// <summary>
        /// How many of the recent shapes differ from each other. The window is small enough that comparing every
        /// pair is cheaper than keeping a set.
        /// </summary>
        private int distinctShapeCount()
        {
            recentShapes.CopyTo(shapeBuffer, 0);

            int count = recentShapes.Count;
            int distinct = 0;

            for (int i = 0; i < count; i++)
            {
                bool seen = false;

                for (int j = 0; j < i; j++)
                {
                    if (shapeBuffer[j] == shapeBuffer[i])
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    distinct++;
            }

            return distinct;
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain) => new AccuracyDifficulties(strain, multipliers);
    }
}
