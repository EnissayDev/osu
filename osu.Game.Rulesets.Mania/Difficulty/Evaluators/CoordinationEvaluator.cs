// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of placing the current note relative to everything the other fingers are
        /// doing, based on:
        /// <list type="bullet">
        /// <item><description>how recently each neighbouring column was pressed,</description></item>
        /// <item><description>how wide the chord it sits in is,</description></item>
        /// <item><description>and how many long notes are being held while it is hit.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.81659; // sqrt(3.3)

            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateHoldDifficulty(current);

            coordinationDifficulty *= current.ManipulationFactor * current.EnduranceFactor;

            return saturate(coordinationDifficulty * total_weight);
        }

        /// <summary>
        /// Two hands only have so many fingers, so past a point more columns being live at once stops adding
        /// difficulty as fast. Bends the top of the range over without ever capping it outright.
        /// </summary>
        private static double saturate(double strain)
        {
            const double threshold = 13.0;
            const double strength = 0.75;
            const double width = 1.5;

            // A softplus of the excess above the threshold.
            // See https://www.desmos.com/calculator/jgnbehwngr
            double z = (strain - threshold) / width;
            double softExcess = width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));

            return strain - strength * softExcess;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.14529;

            int column = current.Column;
            int totalColumns = current.Row.TotalColumns;
            double total = 0.0;

            if (column > 0)
                total += columnBoundaryPressure(current, column, left: true, totalColumns);

            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, left: false, totalColumns);

            return total * TrillUtils.TrillFactor(current) * boundary_pressure_weight;
        }

        /// <summary>
        /// How much pressure the column on one side of <paramref name="current"/> is putting on the hand, from how
        /// recently it was last pressed.
        /// </summary>
        private static double columnBoundaryPressure(ManiaDifficultyHitObject current, int column, bool left, int totalColumns)
        {
            const double scale_ms = 1300.0;
            const double min_delta_ms = 35.0;

            // Past this the neighbouring column has had time to be forgotten about, and stops sharing the hand.
            const double activity_window_ms = 450.0;

            int adjacentColumn = left ? column - 1 : column + 1;
            double adjacentStartTime = current.LastStartTimeInColumn(adjacentColumn);

            if (double.IsNegativeInfinity(adjacentStartTime))
                return 0.0;

            double adjacentDelta = current.StartTime - adjacentStartTime;

            // The neighbour is part of this same chord, so it is one press rather than two things to place.
            if (adjacentDelta < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            // Boundaries sit between columns, so the left side boundary shares this column's index.
            int boundaryIndex = left ? column : column + 1;

            double intensity = scale_ms / (adjacentDelta + min_delta_ms);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient));
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 0.9;

            if (depthInChord < 2)
                return 0.0;

            // Chordjacks are already paid for by Jack, so the dampening here only targets sustained chord spam.
            bool isChordjack = columnDelta <= JackEvaluator.JACK_WINDOW_MS;

            return load_per_extra_column * (depthInChord - 1) * ChordUtils.ChordRepeatDampen(current, columnDelta)
                   * (isChordjack ? ChordUtils.CHORDJACK_NERF : 1.0) * ChordUtils.ChordSpeedFactor(columnDelta);
        }

        /// <summary>
        /// Long notes held in other columns take fingers out of play, and the less time there is between presses
        /// the more that costs.
        /// </summary>
        private static double calculateHoldDifficulty(ManiaDifficultyHitObject current)
        {
            const double held_long_note_weight = 0.01003;
            const double held_speed_factor_offset = 0.08;

            int heldColumns = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS);
            double heldSpeedFactor = current.DeltaTime >= ChordUtils.CHORD_TOLERANCE_MS ? 1.0 / (current.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;

            return held_long_note_weight * Math.Sqrt(heldColumns) * heldSpeedFactor;
        }
    }
}
