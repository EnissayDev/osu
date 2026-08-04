// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class JackSpacingEvaluator
    {
        /// <summary>
        /// How many rows either side of the current one the passage is read from.
        /// </summary>
        private const int context_radius = 6;

        /// <summary>
        /// How much of the repeat is given back because the map lets it be hit with something other than a
        /// jack motion. The nerfs read different spacings of the same repeat, so the strongest one wins
        /// rather than compounding.
        /// </summary>
        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current, int chordDepth, double columnDelta, double tapRate)
        {
            double single = evaluateSingle(chordDepth, tapRate);
            double rowGap = rowGapOf(current);

            if (rowGap <= 1.0)
                return single;

            // How many rows the chart got through before coming back to this column. This is what separates a
            // jack from a stream that wrapped around, and every nerf below is keyed on it.
            double rowsBetween = columnDelta / rowGap;

            double rowSpacing = Math.Max(
                evaluateIncidental(current, chordDepth, rowsBetween, columnDelta),
                evaluateHandstream(current, chordDepth, rowsBetween, columnDelta, rowGap));

            return Math.Min(single, 1.0 - Math.Max(rowSpacing, evaluateStray(current, columnDelta)));
        }

        /// <summary>
        /// A lone column repeating at mashable speed, with no other column between the presses.
        /// </summary>
        private static double evaluateSingle(int chordDepth, double tapRate)
        {
            if (chordDepth >= 2)
                return 1.0;

            // Single-column repeats around this tap rate are mashable, and slower or faster ones are not.
            return 1.0 - 0.10 * DiffUtils.SmoothstepBellCurve(tapRate, 5.5, 0.7);
        }

        /// <summary>
        /// The stream wrapped around to this column after three rows or more.
        /// </summary>
        private static double evaluateIncidental(ManiaDifficultyHitObject current, int chordDepth, double rowsBetween, double columnDelta)
        {
            if (chordDepth >= 2)
                return 0.0;

            double spacingGate = DiffUtils.Smoothstep(rowsBetween, 2.2, 3.2);

            if (spacingGate <= 0.0)
                return 0.0;

            double slowGate = DiffUtils.Smoothstep(columnDelta, 122, 150);
            double streamGate = DiffUtils.Smoothstep(singleNoteShare(current), 0.72, 0.90);

            return 0.9 * spacingGate * slowGate * streamGate;
        }

        /// <summary>
        /// The hand moved off this column and came straight back to it.
        /// </summary>
        private static double evaluateHandstream(ManiaDifficultyHitObject current, int chordDepth, double rowsBetween, double columnDelta, double rowGap)
        {
            double spacingGate = DiffUtils.Smoothstep(rowsBetween, 1.7, 2.6);

            if (spacingGate <= 0.0)
                return 0.0;

            // A chord coming back to the same column is more of a jack than a single note is, so it keeps more.
            double chordShare = chordDepth >= 2 ? 0.55 : 1.0;

            double slowGate = DiffUtils.Smoothstep(columnDelta, 122, 155);
            double streamGate = DiffUtils.Smoothstep(movedShare(current), 0.55, 0.80);

            double pulseGate = DiffUtils.Smoothstep(rowGap, 44, 58);

            return 0.70 * chordShare * spacingGate * slowGate * streamGate * pulseGate;
        }

        /// <summary>
        /// The repeat came back before the chart had time to play another note, so it is a stray press inside a
        /// fast section rather than a repeat that was aimed for. Spared when the section is actually chordjack,
        /// or when the column is holding a long anchor run.
        /// </summary>
        private static double evaluateStray(ManiaDifficultyHitObject current, double columnDelta)
        {
            double noteGap = current.DeltaTime;

            if (noteGap <= 1.0)
                return 0.0;

            double notesBetween = columnDelta / noteGap;
            double spacingGate = DiffUtils.Smoothstep(notesBetween, 2.2, 1.5);

            if (spacingGate <= 0.0)
                return 0.0;

            double speedGate = DiffUtils.Smoothstep(columnDelta, 78, 90);

            if (speedGate <= 0.0)
                return 0.0;

            var (chordShare, repeatShare) = chordContext(current);

            double chordSpare = DiffUtils.Smoothstep(chordShare, 0.50, 0.78) * DiffUtils.Smoothstep(repeatShare, 0.55, 0.25);
            double anchorSpare = DiffUtils.Smoothstep(ColumnRunUtils.RunLengthAround(current, 130), 3, 6);

            return 0.45 * spacingGate * speedGate * (1.0 - chordSpare) * (1.0 - anchorSpare);
        }

        /// <summary>
        /// The gap from the previous row to this note's row, which every note of a chord shares.
        /// </summary>
        private static double rowGapOf(ManiaDifficultyHitObject current)
        {
            var previousRow = current.Row.Previous();
            return previousRow != null ? current.Row.StartTime - previousRow.StartTime : current.DeltaTime;
        }

        /// <summary>
        /// The share of the rows around <paramref name="current"/> that are a single note.
        /// </summary>
        private static double singleNoteShare(ManiaDifficultyHitObject current)
        {
            int singleNoteRows = 0;
            int rows = 0;

            foreach (var row in current.Row.RowsAround(context_radius))
            {
                rows++;

                if (row.IsSingleNote)
                    singleNoteRows++;
            }

            return rows > 0 ? (double)singleNoteRows / rows : 0.0;
        }

        /// <summary>
        /// The share of the row steps around <paramref name="current"/> that leave every column they were on.
        /// </summary>
        private static double movedShare(ManiaDifficultyHitObject current)
        {
            int movedRows = 0;
            int rows = 0;

            foreach (var row in current.Row.RowsAround(context_radius))
            {
                if (row.Previous() is not ManiaRow previous)
                    continue;

                rows++;

                if (!ColumnPatternUtils.SharesColumn(row.Columns, previous.Columns))
                    movedRows++;
            }

            return rows > 0 ? (double)movedRows / rows : 0.0;
        }

        /// <summary>
        /// The share of the rows around <paramref name="current"/> that are chords, and the share of those chords
        /// that repeat the chord before them.
        /// </summary>
        private static (double chordShare, double repeatShare) chordContext(ManiaDifficultyHitObject current)
        {
            int rows = 0;
            int chordRows = 0;
            int repeatedChordRows = 0;

            foreach (var row in current.Row.RowsAround(context_radius))
            {
                rows++;

                if (row.Size < 3)
                    continue;

                chordRows++;

                if (row.Previous() is ManiaRow previous && ColumnPatternUtils.SameColumns(previous.Columns, row.Columns))
                    repeatedChordRows++;
            }

            double chordShare = rows > 0 ? (double)chordRows / rows : 0.0;

            // Rows that are not chords at all are skipped, so this share is out of the chords alone.
            double repeatShare = chordRows > 0 ? (double)repeatedChordRows / chordRows : 0.0;

            return (chordShare, repeatShare);
        }
    }
}
