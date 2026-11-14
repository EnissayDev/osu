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
        private const double technical_weight = 4.88;

        /// <summary>
        /// How many rows to look back when deciding whether the current passage is a chordjack.
        /// </summary>
        private const int chordjack_window_rows = 8;

        /// <summary>
        /// A repeat only counts as chordjack context when it is at most this many times slower than the
        /// minijack being evaluated. A minijack that is faster than the notes around it still stands out.
        /// </summary>
        private const double chordjack_rate_margin = 1.1;

        /// <summary>
        /// Evaluates how hard the current note is to read and place as a pattern, based on:
        /// <list type="bullet">
        /// <item><description>how much its spacing differs from the spacing before it,</description></item>
        /// <item><description>the shape the hand has to make to reach its column,</description></item>
        /// <item><description>how much vocabulary the passage around it is written in,</description></item>
        /// <item><description>how wide the presses around it are,</description></item>
        /// <item><description>and how sharply a column repeats itself mid-flow.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject, double rhythmIrregularity, double patternVariety, double windowedIrregularity)
        {
            const double rhythm_multiplier = 0.9;
            const double chord_width_multiplier = 1.9;
            const double minijack_multiplier = 4.5;

            double columnComplexity = evaluateColumnComplexityOf(hitObject);
            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + 0.060);
            double readingPressure = readingPressureOf(hitObject);

            // Mixed rhythm is worth most when it is neither perfectly even nor unreadably loose.
            // See https://www.desmos.com/calculator/2nrishjcm6
            double rhythmAmplifier = 1.0 + rhythm_multiplier * readingPressure * DiffUtils.BellCurve(windowedIrregularity, 0.15, 0.085);

            // A shape is a spacing together with the direction the hand moved, so a passage can count up plenty of
            // distinct shapes purely by visiting its columns in a different order while its spacing never changes
            // which is what an ordinary stream does. The floor exists for a passage whose spacings genuinely differ,
            // so it is paid in proportion to how much they actually do.
            double varietyFloor = 1.45 * patternVariety * readingPressure * DiffUtils.ReverseLerp(windowedIrregularity, 0.04, 0.5);

            // A gap too short to read leaves nothing for a change of rhythm to register against, so irregularity counts for less the tighter the note is.
            double rhythmShare = DiffUtils.Smoothstep(hitObject.DeltaTime, 10.0, 40.0);

            double spacingComplexity = rhythmIrregularity * rhythmShare + columnComplexity;

            if (varietyFloor > spacingComplexity)
                rhythmAmplifier = 1.0;

            double complexity = Math.Max(spacingComplexity, varietyFloor);

            // Bonuses
            double chordWidth = 1.0 + chord_width_multiplier * chordWidthBonus(hitObject);
            double minijack = 1.0 + minijack_multiplier * minijackPressure(hitObject);

            double difficulty = complexity * speedFactor * rhythmAmplifier * chordWidth * minijack;

            return difficulty * technical_weight;
        }

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
        /// More columns write more shapes at the same density, so the window scales with
        /// keymode: 4K reads exactly the old thresholds, higher keys need a bigger
        /// vocabulary to max out. Capped so the top stays reachable in an 8-shape window.
        /// </summary>
        public static double EvaluatePatternVarietyOf(int distinctShapeCount, int totalColumns)
        {
            double keyScale = Math.Min(1.45, 1.0 + 0.125 * Math.Max(0, totalColumns - 4));
            return DiffUtils.Smoothstep(distinctShapeCount, 2.5 * keyScale, 5.5 * keyScale);
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
        private static double chordWidthBonus(ManiaDifficultyHitObject hitObject)
        {
            var passage = readPassageAround(hitObject);

            double density = DiffUtils.Smoothstep(passage.wideShare, 0.30, 0.60);
            double moves = DiffUtils.Smoothstep(passage.jackShare, 0.90, 0.65);
            double reforms = DiffUtils.Smoothstep(passage.shapeChange, 0.25, 0.65);

            return density * moves * reforms;
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
            if (hitObject.Row.Previous() is not ManiaRow previousRow || previousRow.Previous() is not ManiaRow previous2Row)
                return 0.0;

            int handSplit = hitObject.Row.TotalColumns / 2;

            int previousColumn = closestColumn(previousRow, hitObject.Column, handSplit, hitObject.Column);
            int previous2Column = closestColumn(previous2Row, previousColumn);

            if ((previousColumn < handSplit) != (hitObject.Column < handSplit))
                return 0.0;

            double columnComplexity = 0.0;

            int previousDirection = previousColumn - previous2Column;
            int currentDirection = hitObject.Column - previousColumn;

            // Past a hand's span the move is not something one hand does, so it stops costing anything more.
            int span = Math.Min(Math.Abs(currentDirection), handSplit);

            if (previousDirection != 0 && currentDirection != 0 && Math.Sign(previousDirection) != Math.Sign(currentDirection))
            {
                double coefficient = CrossColumnUtils.SumBoundaryMultipliersBetween(previousColumn, hitObject.Column, hitObject.Row.TotalColumns);
                columnComplexity += 0.45 + 2.0 * coefficient;
            }

            if (span >= 2)
                columnComplexity += CrossColumnUtils.AverageBoundaryMultipliersBetween(previousColumn, hitObject.Column, hitObject.Row.TotalColumns);

            // Past a certain span the two columns belong to different hands, and the jump between them stops being one hand's problem.
            double spanDamper = 1.0 - 0.75 * DiffUtils.Smoothstep(Math.Abs(currentDirection), 3.0, 5.5);

            return columnComplexity * spanDamper;
        }

        /// <summary>
        /// The column in <paramref name="row"/> closest to <paramref name="target"/>.
        /// </summary>
        private static int closestColumn(ManiaRow row, int target)
            => closestColumn(row, target, -1, -1);

        /// <summary>
        /// The column in <paramref name="row"/> closest to <paramref name="target"/>, preferring a column on
        /// the same hand as <paramref name="handColumn"/> when two columns are equally close.
        /// </summary>
        private static int closestColumn(ManiaRow row, int target, int handSplit, int handColumn)
        {
            int column = row.Columns[0];
            int distance = Math.Abs(column - target);
            bool hasSameHand = handSplit >= 0 && (column < handSplit) == (handColumn < handSplit);
            int sameHandColumn = column;

            for (int i = 1; i < row.Columns.Length; i++)
            {
                int candidate = row.Columns[i];
                int candidateDistance = Math.Abs(candidate - target);

                if (candidateDistance < distance)
                {
                    column = candidate;
                    distance = candidateDistance;
                    sameHandColumn = candidate;
                    hasSameHand = handSplit >= 0 && (candidate < handSplit) == (handColumn < handSplit);
                }
                else if (candidateDistance == distance && handSplit >= 0 && (candidate < handSplit) == (handColumn < handSplit))
                {
                    sameHandColumn = candidate;
                    hasSameHand = true;
                }
            }

            return hasSameHand ? sameHandColumn : column;
        }

        /// <summary>
        /// How much a same-column repeat in the middle of a flow adds to the pattern's technical demand.
        /// The repeat has to be fast, dense, and a genuine speed-up over the column's previous gap; when
        /// other columns are already jacking at the same rate, the repeat is less of a standalone spike.
        /// </summary>
        private static double minijackPressure(ManiaDifficultyHitObject hitObject)
        {
            // Technical difficulty is evaluated once per row, so a minijack in any of the row's other
            // columns would otherwise be missed.
            double best = 0.0;

            foreach (var rowObject in hitObject.Row.Objects)
                best = Math.Max(best, minijackPressureOfNote(rowObject));

            return best;
        }

        /// <summary>
        /// How much of a minijack the surrounding passage still reads as. Repeated columns are a
        /// standalone technical spike in a stream, but a wall of chordjacks is already covered by the
        /// jack and coordination skills.
        /// </summary>
        private static double minijackIsolation(ManiaDifficultyHitObject hitObject, double jackDelta)
        {
            double repeatedNotes = 0;
            int totalNotes = 0;
            var row = hitObject.Row;

            for (int i = 0; i < chordjack_window_rows && row != null; i++, row = row.Previous())
            {
                foreach (var note in row.Objects)
                {
                    totalNotes++;

                    if (note.PrevInColumn(0) is not { } previousNote)
                        continue;

                    double noteDelta = note.StartTime - previousNote.StartTime;

                    if (noteDelta <= 0.0)
                        continue;

                    // Only repeats at a comparable rate make this minijack part of the jack texture.
                    // A much slower surrounding repeat leaves the minijack as the faster, distinct note.
                    repeatedNotes += DiffUtils.Smoothstep(noteDelta, jackDelta * chordjack_rate_margin, jackDelta);
                }
            }

            if (totalNotes == 0)
                return 1.0;

            double density = repeatedNotes / totalNotes;

            return DiffUtils.Smoothstep(density, 0.50, 0.30);
        }

        /// <summary>
        /// The minijack pressure of one note in a row.
        /// </summary>
        private static double minijackPressureOfNote(ManiaDifficultyHitObject hitObject)
        {
            // A minijack is worth less when other columns are jacking just as hard.
            const double other_jack_penalty = 0.75;
            const double other_jack_margin = 1.2;
            const double minijack_mult_floor = 0.02;

            if (hitObject.PrevInColumn(0) is not { } previousNote)
                return 0.0;

            if (isHandAlternation(hitObject, previousNote))
                return 0.0;

            double jackDelta = hitObject.StartTime - previousNote.StartTime;

            double multiplier = DiffUtils.Smoothstep(jackDelta, 140.0, 75.0);

            if (multiplier <= 0.0)
                return 0.0;

            double isolation = minijackIsolation(hitObject, jackDelta);

            if (isolation <= 0.0)
                return 0.0;

            // A minijack only reads as one when the hand is already moving through a dense passage.
            int rowCount = 0;

            for (var row = hitObject.Row; row != null && hitObject.StartTime - row.StartTime <= jackDelta * 5.0; row = row.Previous())
                rowCount++;

            multiplier *= DiffUtils.Smoothstep(rowCount, 4.0, 5.5);

            if (multiplier <= 0.0)
                return 0.0;

            // Reward a repeat that is faster than the gap the column had before it.
            double acceleration = 1.0;

            if (previousNote.PrevInColumn(0) is { } beforePrevious)
            {
                acceleration = (previousNote.StartTime - beforePrevious.StartTime) / Math.Max(jackDelta, 1.0);
                acceleration = DiffUtils.Smoothstep(acceleration, 1.0, 2.5);
            }

            // If other columns are already repeating at a similar rate, this is not a fresh minijack.
            double otherJackMultiplier = 1.0;

            for (int column = 0; column < hitObject.PreviousHitObjects.Length; column++)
            {
                if (column == hitObject.Column)
                    continue;

                if (hitObject.PreviousHitObjects[column] is not { } neighbour
                    || hitObject.StartTime - neighbour.StartTime > jackDelta * 5.0
                    || neighbour.PrevInColumn(0) is not { } before)
                    continue;

                double otherDelta = neighbour.StartTime - before.StartTime;

                if (otherDelta > 0.0 && otherDelta <= jackDelta * other_jack_margin)
                    otherJackMultiplier *= other_jack_penalty;
            }

            multiplier *= Math.Max(otherJackMultiplier, minijack_mult_floor);

            return multiplier * acceleration * isolation;
        }

        private static bool isHandAlternation(ManiaDifficultyHitObject hitObject, ManiaDifficultyHitObject previousNote)
        {
            int totalColumns = hitObject.Row.TotalColumns;
            var hand = HandLayout.HandOf(hitObject.Column, totalColumns);
            bool anyBetween = false;

            for (var row = hitObject.Row.Previous(); row != null && row.RowIndex > previousNote.Row.RowIndex; row = row.Previous())
            {
                foreach (int column in row.Columns)
                {
                    if (HandLayout.HandOf(column, totalColumns) == hand)
                        return false;
                }

                anyBetween = true;
            }

            return anyBetween;
        }
    }
}
