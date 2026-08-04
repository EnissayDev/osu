// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ChordUtils
    {
        public const double CHORD_TOLERANCE_MS = 8.0;
        public const double CHORDJACK_NERF = 0.45397;

        public const int DEFAULT_LOCAL_SIZE_RADIUS = 4;

        /// <summary>
        /// A 16th note at 160 BPM, where chord presses stop being fast enough to earn full credit.
        /// </summary>
        private const double chord_speed_threshold_ms = 140.625;

        /// <summary>
        /// Where this note sits within its chord, counting from 1.
        /// </summary>
        public static int DepthInChord(ManiaDifficultyHitObject current) => current.Index - current.Row.Objects[0].Index + 1;

        /// <summary>
        /// The average notes per row over the rows surrounding <paramref name="current"/>.
        /// </summary>
        public static double LocalChordSize(ManiaDifficultyHitObject current, int radius = DEFAULT_LOCAL_SIZE_RADIUS)
        {
            double notes = 0.0;
            int rows = 0;

            foreach (var row in current.Row.RowsAround(radius))
            {
                notes += row.Size;
                rows++;
            }

            return rows > 0 ? notes / rows : 0.0;
        }

        /// <summary>
        /// How much a chord press is worth for the speed it is played at.
        /// </summary>
        public static double ChordSpeedFactor(double columnDelta)
        {
            const double factor_min = 0.1;
            const double factor_max = 2.0;

            if (double.IsPositiveInfinity(columnDelta))
                return 1.0;

            return Math.Clamp(chord_speed_threshold_ms / columnDelta, factor_min, factor_max);
        }

        /// <summary>
        /// How much of a chord press is left after the same wide press keeps coming back. The hand settles into a
        /// shape it never has to leave, so a section of them stops being worth what the first one was.
        /// </summary>
        public static double ChordRepeatDampen(ManiaDifficultyHitObject current, double columnDelta)
        {
            const double full_chord_nerf = 0.50;
            const double full_chord_run_ramp = 2.0;

            const double near_full_chord_nerf = 0.085;
            const double near_full_chord_run_ramp = 12.0;

            int totalColumns = current.Row.TotalColumns;
            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);

            double dampen = chordRunDampen(current, totalColumns, full_chord_nerf * speedScale, full_chord_run_ramp);

            if (totalColumns >= 2)
                dampen *= chordRunDampen(current, totalColumns - 1, near_full_chord_nerf * speedScale, near_full_chord_run_ramp);

            return dampen;
        }

        /// <summary>
        /// How much is left after a run of rows at least <paramref name="minSize"/> wide.
        /// </summary>
        private static double chordRunDampen(ManiaDifficultyHitObject current, int minSize, double ceiling, double runRamp)
        {
            if (ceiling <= 0)
                return 1.0;

            // The run counts the current row, so only the rows before it have to be walked.
            int cap = RunDampenUtils.CapFor(runRamp) - 1;
            int run = 1;

            for (ManiaRow row = current.Row; run <= cap && row.Previous() is ManiaRow earlier && earlier.Size >= minSize; row = earlier)
                run++;

            return RunDampenUtils.Dampen(run, runRamp, ceiling);
        }
    }
}
