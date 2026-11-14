// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        private const double coordination_multiplier = 2.22;

        /// <summary>
        /// Evaluates the difficulty of placing the current note relative to everything the other fingers are
        /// doing, based on:
        /// <list type="bullet">
        /// <item><description>how recently each neighbouring column was pressed,</description></item>
        /// <item><description>how wide the chord it sits in is,</description></item>
        /// <item><description>and how many long notes are being held when it is hit.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateShieldDifficulty(current);
            coordinationDifficulty += calculateNoteWhileHolds(current);

            return coordinationDifficulty * coordination_multiplier;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.09;

            int column = current.Column;
            int totalColumns = current.Row.TotalColumns;
            double total = 0.0;

            if (column > 0)
                total += columnBoundaryPressure(current, column, left: true, totalColumns);

            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, left: false, totalColumns);

            return total * boundary_pressure_weight;
        }

        /// <summary>
        /// How much pressure the column on one side of <paramref name="current"/> is putting on the hand, from how
        /// recently it was last pressed.
        /// </summary>
        private static double columnBoundaryPressure(ManiaDifficultyHitObject current, int column, bool left, int totalColumns)
        {
            // Past this the neighbouring column has had time to be forgotten about.
            const double activity_window_ms = 450.0;

            int adjacentColumn = left ? column - 1 : column + 1;
            if (Array.IndexOf(current.Row.Columns, adjacentColumn) >= 0)
                return 0.0;

            double adjacentStartTime = current.LastStartTimeInColumn(adjacentColumn);
            if (double.IsNegativeInfinity(adjacentStartTime))
                return 0.0;

            double adjacentDelta = current.StartTime - adjacentStartTime;
            if (adjacentDelta < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            // Boundaries sit between columns, so the left side boundary shares this column's index.
            int boundaryIndex = left ? column : column + 1;

            // Capping intensity to nerf graces
            double intensity = Math.Min(1300.0 / (adjacentDelta + 35.0), 20.0);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient)) * previousGapMultiplier(current, adjacentDelta, intensity);
        }

        /// <summary>
        /// How far back this column itself was last pressed, compared to how far back the neighbour was.
        /// A neighbour that is fresher than this column means the finger landing here is the one that has
        /// been resting, so the placement costs the hand less.
        /// </summary>
        private static double previousGapMultiplier(ManiaDifficultyHitObject current, double adjacentDelta, double intensity)
        {
            const double min_penality = 0.2;
            // How long this column has been idle, in comparison to the adjacent one.
            double ratio = current.ColumnDelta / adjacentDelta;

            double gapPenalty = Math.Pow(DiffUtils.ReverseLerp(ratio, 3.0, 7.0), 1.5);

            return Math.Max(1.0 - gapPenalty * DiffUtils.ReverseLerp(intensity, 6.0, 12.0), min_penality);
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 2.8;

            if (depthInChord < 2)
                return 0.0;

            double chordjackValue = calculateChordjackValue(current);

            // A series of chords with equal density in each rows but few shared columns
            // require a LOT more control than uneven chordjacks, example (O = note):
            //   left is much harder than right for the same density
            //          OOXOXXO                  XOXOXXX
            //          XOOXOOX                  OOOOOXO
            //          OXOOXOX                  XXXXOXO
            //          OOXXOXO                  OOXOOOO
            double balanceRatio = current.Row.Previous() is { } previous
                ? (double)Math.Min(previous.Size, current.Row.Size) / Math.Max(previous.Size, current.Row.Size)
                : 1.0;

            bool isJackNote = current.Row.Previous() is { } previousRow && Array.IndexOf(previousRow.Columns, current.Column) >= 0;
            double speedFactor = ChordUtils.ChordSpeedFactor(columnDelta);

            if (isJackNote)
                speedFactor = Math.Min(speedFactor, 1.0);

            double difficulty = load_per_extra_column * (depthInChord - 1) * balanceRatio * ChordUtils.ChordRepeatNerf(current, columnDelta) * speedFactor;

            return isJackNote ? difficulty * (1.0 - chordjackValue) : difficulty;
        }

        /// <summary>
        /// How much the current chord repeats the one before it.
        /// 0 for a chord with nothing shared (or no previous row), 1 for the same chord played again.
        /// </summary>
        private static double calculateChordjackValue(ManiaDifficultyHitObject current)
        {
            if (current.Row.Previous() is not { } previous)
                return 0.0;

            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);
            double averageSize = (previous.Size + current.Row.Size) / 2.0;

            return averageSize > 0.0 ? shared / averageSize : 0.0;
        }

        /// <summary>
        /// A long note with a jack previously is a shield. If the long note needs to be held for an extended period of time,
        /// the hand has to fight against bouncing the finger up from the jack.
        /// </summary>
        ///         Hard shield  -->  O O===========|
        ///
        ///         Easy shields -->  O   O=======|
        ///                           O O===|
        private static double calculateShieldDifficulty(ManiaDifficultyHitObject current)
        {
            // Only a note that is actually being held can be a shield.
            double holdLength = current.EndTime - current.StartTime;

            if (holdLength <= 0.0)
                return 0.0;

            if (current.PrevInColumn(0) is null)
                return 0.0;

            double intensity = Math.Min(320000.0 / (current.ColumnDelta * (current.ColumnDelta + 120.0)), 20.0);

            return intensity * DiffUtils.ReverseLerp(holdLength, 150.0, 600.0);
        }

        /// <summary>
        /// Other held notes makes the movement of a hit harder, the hand is already comitted to a note
        /// and spends more energy managing other patterns.
        /// </summary>
        private static double calculateNoteWhileHolds(ManiaDifficultyHitObject current)
        {
            const double held_note_weight = 1.5;

            return held_note_weight * (current.HeldNoteMultiplier - 1.0);
        }
    }
}
