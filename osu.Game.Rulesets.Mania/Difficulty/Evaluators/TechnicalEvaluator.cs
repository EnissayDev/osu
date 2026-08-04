// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class TechnicalEvaluator
    {
        /// <summary>
        /// How far apart in time two notes have to be before the passage stops reading as one steady spacing.
        /// Rounding the log of the gap to this base is what buckets gaps into "the same rhythm".
        /// </summary>
        private const double variety_gap_log_base = 1.18;

        /// <summary>
        /// How much this note's spacing differs from the one before it, as a fraction, where 0 is the exact same
        /// spacing and 1 is an unrelated one.
        /// </summary>
        public static double EvaluateRhythmIrregularityOf(ManiaDifficultyHitObject hitObject, double previousDeltaTime)
        {
            if (previousDeltaTime <= ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            double ratio = hitObject.DeltaTime / previousDeltaTime;

            // Read the ratio the same way whether the chart sped up or slowed down.
            if (ratio > 1.0)
                ratio = 1.0 / ratio;

            return 1.0 - ratio;
        }

        /// <summary>
        /// The shape this note makes: the rhythm bucket its gap falls in, together with the direction the hand
        /// moved to reach it. Two notes make the same shape when both agree.
        /// </summary>
        public static (int rhythmClass, int direction) EvaluateShapeOf(ManiaDifficultyHitObject hitObject)
        {
            int rhythmClass = (int)Math.Round(Math.Log(hitObject.DeltaTime) / Math.Log(variety_gap_log_base));
            int direction = hitObject.Previous() is ManiaDifficultyHitObject previous ? Math.Sign(hitObject.Column - previous.Column) : 0;

            return (rhythmClass, direction);
        }

        /// <summary>
        /// How much vocabulary the passage is written in, from the number of distinct shapes it has recently used.
        /// </summary>
        public static double EvaluatePatternVarietyOf(int distinctShapeCount) => DiffUtils.Smoothstep(distinctShapeCount, 2.5, 5.5);

        /// <summary>
        /// Evaluates how hard the current note is to read and place as a pattern, based on:
        /// <list type="bullet">
        /// <item><description>how much its spacing differs from the spacing before it,</description></item>
        /// <item><description>the shape the hand has to make to reach its column,</description></item>
        /// <item><description>how much vocabulary the passage around it is written in,</description></item>
        /// <item><description>and how wide the presses around it are.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject, double rhythmIrregularity, double patternVariety, double windowedIrregularity)
        {
            const double pattern_buff = 0.69740;
            const double technical_scale = 1.49964;

            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.89704; // sqrt(2.49916) * 1.20

            double columnComplexity = evaluateColumnComplexityOf(hitObject);
            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + 0.050);
            double readingPressure = readingPressureOf(hitObject);

            // Mixed rhythm is worth most when it is neither perfectly even nor unreadably loose.
            // See https://www.desmos.com/calculator/2nrishjcm6
            double rhythmAmplifier = 1.0 + 0.9 * readingPressure * DiffUtils.BellCurve(windowedIrregularity, 0.15, 0.085);

            // A shape is a spacing together with the direction the hand moved, so a passage can count up plenty of
            // distinct shapes purely by visiting its columns in a different order while its spacing never changes
            // which is what an ordinary stream does. The floor exists for a passage whose spacings genuinely differ,
            // so it is paid in proportion to how much they actually do.
            double varietyFloor = 1.55 * patternVariety * readingPressure * DiffUtils.Smoothstep(windowedIrregularity, 0.04, 0.12);

            double complexity = Math.Max(rhythmIrregularity + columnComplexity, varietyFloor);

            return pattern_buff * complexity * speedFactor * technical_scale * rhythmAmplifier * chordWidth(hitObject)
                   * hitObject.ManipulationFactor * total_weight;
        }

        /// <summary>
        /// How little time this note leaves to read what the chart is doing, from a comfortable gap up to a stream gap.
        /// </summary>
        /// <remarks>
        /// The readings this scales only count what the chart does, not how fast it does it, and counted alone
        /// they run backwards. An easy chart is written in mixed rhythm and a hard one settles into one spacing,
        /// so a slow chart full of rhythm would read as more technical than a stream that never lets go.
        /// </remarks>
        private static double readingPressureOf(ManiaDifficultyHitObject hitObject) => DiffUtils.Smoothstep(hitObject.DeltaTime, 95, 48);

        /// <summary>
        /// How often the passage around this row plays chords too wide for one hand shape, and how much the hand
        /// has to keep changing shape to hit them. The same wide chord over and over barely counts.
        /// </summary>
        private static double chordWidth(ManiaDifficultyHitObject hitObject)
        {
            var passage = readPassageAround(hitObject);

            double density = DiffUtils.Smoothstep(passage.wideShare, 0.30, 0.60);
            double moves = DiffUtils.Smoothstep(passage.jackShare, 0.90, 0.65);
            double reforms = DiffUtils.Smoothstep(passage.shapeChange, 0.25, 0.65);

            return 1.0 + 1.9 * density * moves * reforms;
        }

        /// <summary>
        /// Looks at the rows around <paramref name="hitObject"/> and reports three things: how many of them are wide
        /// chords, how often a row reuses a column the row before it just played, and how often two wide chords in a
        /// row are actually a different chord.
        /// </summary>
        private static (double wideShare, double jackShare, double shapeChange) readPassageAround(ManiaDifficultyHitObject hitObject)
        {
            // Wide means wide for the keymode being played. Three of four keys is a near-full press while three
            // of seven is an ordinary chord, so this is a share of the columns rather than a key count.
            const double wide_fill = 0.7;

            int wide = 0, rows = 0, shared = 0, steps = 0, wideSteps = 0, wideChanges = 0;

            foreach (var row in hitObject.Row.RowsAround(14))
            {
                rows++;

                bool isWide = row.Size >= wide_fill * row.TotalColumns;

                if (isWide)
                    wide++;

                if (row.Previous() is not ManiaRow previous)
                    continue;

                steps++;

                if (ColumnPatternUtils.SharesColumn(row.Columns, previous.Columns))
                    shared++;

                if (!isWide || previous.Size < wide_fill * previous.TotalColumns)
                    continue;

                wideSteps++;

                if (!ColumnPatternUtils.SameColumns(row.Columns, previous.Columns))
                    wideChanges++;
            }

            return (rows > 0 ? (double)wide / rows : 0.0,
                steps > 0 ? (double)shared / steps : 0.0,
                wideSteps > 0 ? (double)wideChanges / wideSteps : 1.0);
        }

        /// <summary>
        /// How awkward the move to this note's column is. Doubling back on itself is what costs the hand the most,
        /// and a jump costs more the further across the keys it reaches.
        /// </summary>
        private static double evaluateColumnComplexityOf(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.Previous() is not ManiaDifficultyHitObject previous || hitObject.Previous(1) is not ManiaDifficultyHitObject previous2)
                return 0.0;

            double columnComplexity = 0.0;

            int previousDirection = previous.Column - previous2.Column;
            int currentDirection = hitObject.Column - previous.Column;

            if (previousDirection != 0 && currentDirection != 0 && Math.Sign(previousDirection) != Math.Sign(currentDirection))
            {
                double coefficient = CrossColumnUtils.SumBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.Row.TotalColumns);
                columnComplexity += 0.6 + 2.0 * coefficient;
            }

            if (Math.Abs(currentDirection) >= 2)
                columnComplexity += CrossColumnUtils.AverageBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.Row.TotalColumns);

            // Past a certain span the two columns belong to different hands, and the jump between them stops being one hand's problem.
            double spanDamper = 1.0 - 0.60 * DiffUtils.Smoothstep(Math.Abs(currentDirection), 3.0, 5.5);

            return columnComplexity * spanDamper;
        }
    }
}
