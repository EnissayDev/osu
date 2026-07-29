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
        private const int context_radius = 6;

        private const double incidental_nerf = 0.9;
        private const double incidental_rows_lo = 2.2;
        private const double incidental_rows_hi = 3.2;

        private const double incidental_column_delta_lo = 122.0;
        private const double incidental_column_delta_hi = 150.0;

        private const double incidental_single_note_share_lo = 0.72;
        private const double incidental_single_note_share_hi = 0.90;

        private const double handstream_nerf = 0.70;
        private const double handstream_rows_lo = 1.7;
        private const double handstream_rows_hi = 2.6;

        private const double handstream_column_delta_lo = 122.0;
        private const double handstream_column_delta_hi = 155.0;

        private const double handstream_moved_share_lo = 0.55;
        private const double handstream_moved_share_hi = 0.80;

        private const double handstream_pulse_lo = 44.0;
        private const double handstream_pulse_hi = 58.0;

        private const double handstream_chord_share = 0.55;

        private const double stray_nerf = 0.45;
        private const double stray_notes_lo = 1.5;
        private const double stray_notes_hi = 2.2;

        private const double stray_column_delta_lo = 78.0;
        private const double stray_column_delta_hi = 90.0;

        private const double stray_chord_share_lo = 0.50;
        private const double stray_chord_share_hi = 0.78;

        private const double stray_repeat_share_lo = 0.25;
        private const double stray_repeat_share_hi = 0.55;

        private const double stray_run_lo = 3.0;
        private const double stray_run_hi = 6.0;
        private const double stray_run_window_ms = 130.0;

        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            double rowGap = rowGapOf(current);

            if (rowGap <= 1.0)
                return 1.0;

            double rowsBetween = columnDelta / rowGap;

            double rowSpacing = Math.Max(
                evaluateIncidental(current, chordDepth, rowsBetween, columnDelta),
                evaluateHandstream(current, chordDepth, rowsBetween, columnDelta, rowGap));

            return 1.0 - Math.Max(rowSpacing, evaluateStray(current, columnDelta));
        }

        /// <summary>
        /// The stream wrapped around to this column after three rows or more.
        /// </summary>
        private static double evaluateIncidental(ManiaDifficultyHitObject current, int chordDepth, double rowsBetween, double columnDelta)
        {
            if (chordDepth >= 2)
                return 0.0;

            double spacingGate = DiffUtils.Smoothstep(rowsBetween, incidental_rows_lo, incidental_rows_hi);

            if (spacingGate <= 0.0)
                return 0.0;

            double slowGate = DiffUtils.Smoothstep(columnDelta, incidental_column_delta_lo, incidental_column_delta_hi);
            double streamGate = DiffUtils.Smoothstep(singleNoteShare(current), incidental_single_note_share_lo, incidental_single_note_share_hi);

            return incidental_nerf * spacingGate * slowGate * streamGate;
        }

        /// <summary>
        /// The hand moved off this column and came straight back to it.
        /// </summary>
        private static double evaluateHandstream(ManiaDifficultyHitObject current, int chordDepth, double rowsBetween, double columnDelta, double rowGap)
        {
            double spacingGate = DiffUtils.Smoothstep(rowsBetween, handstream_rows_lo, handstream_rows_hi);

            if (spacingGate <= 0.0)
                return 0.0;

            double chordShare = chordDepth >= 2 ? handstream_chord_share : 1.0;
            double slowGate = DiffUtils.Smoothstep(columnDelta, handstream_column_delta_lo, handstream_column_delta_hi);
            double streamGate = DiffUtils.Smoothstep(movedShare(current), handstream_moved_share_lo, handstream_moved_share_hi);
            double pulseGate = DiffUtils.Smoothstep(rowGap, handstream_pulse_lo, handstream_pulse_hi);

            return handstream_nerf * chordShare * spacingGate * slowGate * streamGate * pulseGate;
        }

        private static double evaluateStray(ManiaDifficultyHitObject current, double columnDelta)
        {
            double noteGap = current.DeltaTime;

            if (noteGap <= 1.0)
                return 0.0;

            double notesBetween = columnDelta / noteGap;
            double spacingGate = 1.0 - DiffUtils.Smoothstep(notesBetween, stray_notes_lo, stray_notes_hi);

            if (spacingGate <= 0.0)
                return 0.0;

            double speedGate = DiffUtils.Smoothstep(columnDelta, stray_column_delta_lo, stray_column_delta_hi);

            if (speedGate <= 0.0)
                return 0.0;

            var (chordShare, repeatShare) = chordContext(current);

            double chordSpare = DiffUtils.Smoothstep(chordShare, stray_chord_share_lo, stray_chord_share_hi)
                                * (1.0 - DiffUtils.Smoothstep(repeatShare, stray_repeat_share_lo, stray_repeat_share_hi));

            double anchorSpare = DiffUtils.Smoothstep(ColumnRunUtils.RunLengthAround(current, stray_run_window_ms), stray_run_lo, stray_run_hi);

            return stray_nerf * spacingGate * speedGate * (1.0 - chordSpare) * (1.0 - anchorSpare);
        }

        /// <summary>
        /// The gap from the previous row to this note's row, which every note of a chord shares.
        /// </summary>
        private static double rowGapOf(ManiaDifficultyHitObject current)
        {
            var previousRow = current.Row.Previous();
            return previousRow != null ? current.Row.StartTime - previousRow.StartTime : current.DeltaTime;
        }

        private static double singleNoteShare(ManiaDifficultyHitObject current)
            => current.Row.ShareOfRowsAround(context_radius, row => row.IsSingleNote);

        private static double movedShare(ManiaDifficultyHitObject current)
            => current.Row.ShareOfRowsAround(context_radius, row => row.Previous() is ManiaRow previous
                ? !ColumnPatternUtils.SharesColumn(row.Columns, previous.Columns)
                : null);

        private static (double chordShare, double repeatShare) chordContext(ManiaDifficultyHitObject current)
        {
            double chordShare = current.Row.ShareOfRowsAround(context_radius, row => row.Size >= 3);

            // Rows that are not chords at all are skipped, so this share is out of the chords alone.
            double repeatShare = current.Row.ShareOfRowsAround(context_radius, row => row.Size < 3
                ? null
                : row.Previous() is ManiaRow previous && ColumnPatternUtils.SameColumns(previous.Columns, row.Columns));

            return (chordShare, repeatShare);
        }
    }
}
