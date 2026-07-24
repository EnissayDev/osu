// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        private const double boundary_pressure_weight = 1.14529;

        private const double chord_load_per_extra_column = 0.9;
        private const double chordjack_nerf = 0.45397;
        private const double chord_speed_threshold_ms = 140.625;

        private const double held_long_note_weight = 0.01003;
        private const double held_speed_factor_offset = 0.08;

        private const double boundary_scale_ms = 1300.0;
        private const double boundary_min_delta_ms = 35.0;
        private const double boundary_activity_window_ms = 450.0;

        private const double total_weight = 1.81659; // sqrt(3.3)

        private const double saturation_threshold = 13.0;
        private const double saturation_strength = 0.75;
        private const double saturation_width = 1.5;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateHoldDifficulty(current);

            coordinationDifficulty *= current.ManipulationFactor * current.StaminaFactor;

            return saturate(coordinationDifficulty * total_weight);
        }

        private static double saturate(double strain)
        {
            double z = (strain - saturation_threshold) / saturation_width;
            double softExcess = saturation_width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));
            return strain - saturation_strength * softExcess;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// In simple words, calculates how hard it is to tap with two adjacent fingers.
        /// </summary>
        /// <param name="current">The note to calculate for.</param>
        /// <returns>A base difficulty value.</returns>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            int column = current.Column;
            int totalColumns = current.PreviousHitObjects.Length;
            double total = 0.0;

            // If we have a left column
            if (column > 0)
                total += columnBoundaryPressure(current, column, "left", totalColumns);

            // If we have a right column
            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, "right", totalColumns);

            return total * TrillUtils.TrillFactor(current) * boundary_pressure_weight;
        }

        private static double columnBoundaryPressure(ManiaDifficultyHitObject current, int column, string side, int totalColumns)
        {
            int adjacentColumn = side == "left" ? column - 1 : column + 1;
            double adjacentStartTime = current.LastStartTimeInColumn(adjacentColumn);

            if (double.IsNegativeInfinity(adjacentStartTime))
                return 0.0;

            double adjacentDelta = current.StartTime - adjacentStartTime;

            if (adjacentDelta < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            // Since boundaries are between the columns, the left side boundary is also at index column.
            int boundaryIndex = side == "left" ? column : column + 1;

            double intensity = boundary_scale_ms / (adjacentDelta + boundary_min_delta_ms);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= boundary_activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient));
        }

        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            bool isChordjack = depthInChord >= 2 && columnDelta <= JackEvaluator.JACK_WINDOW_MS;
            double chordSpeedFactor = !double.IsPositiveInfinity(columnDelta)
                ? Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0)
                : 1.0;

            // A chordjack repeats a chord on columns it just used (e.g. 4-2-2-4), already paid for by Jack,
            // so the dampening here only targets degenerate sustained full/near-full chord spam.
            double chordLoad = depthInChord >= 2
                ? chord_load_per_extra_column * (depthInChord - 1) * ChordUtils.FullChordDampen(current, current.PreviousHitObjects.Length, columnDelta)
                  * ChordUtils.NearFullChordDampen(current, current.PreviousHitObjects.Length, columnDelta)
                  * (isChordjack ? chordjack_nerf : 1.0) * chordSpeedFactor
                : 0.0;

            return chordLoad;
        }

        private static double calculateHoldDifficulty(ManiaDifficultyHitObject current)
        {
            int heldColumns = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS);
            double heldSpeedFactor = current.DeltaTime >= ChordUtils.CHORD_TOLERANCE_MS ? 1.0 / (current.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;
            double heldNoteLoad = held_long_note_weight * Math.Sqrt(heldColumns) * heldSpeedFactor;

            return heldNoteLoad;
        }
    }
}
