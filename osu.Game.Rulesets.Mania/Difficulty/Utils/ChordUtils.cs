// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ChordUtils
    {
        public const double CHORD_TOLERANCE_MS = 8.0;

        /// <summary>
        /// Notes of a chord past the first come for free with the press, so a chord repeated on columns it just
        /// used only asks for part of what its size suggests.
        /// </summary>
        public const double CHORDJACK_NERF = 0.45397;

        private const double full_chord_nerf = 0.50;
        private const double full_chord_run_ramp = 2.0;

        private const double near_full_chord_nerf = 0.575;
        private const double near_full_chord_run_ramp = 55.0;

        // 16th note at 160 BPM - the crossover where chord presses earn full credit.
        private const double chord_speed_threshold_ms = 140.625;

        private const double chord_speed_factor_min = 0.1;
        private const double chord_speed_factor_max = 2.0;

        public static int DepthInChord(ManiaDifficultyHitObject current) => current.Index - current.Row.Objects[0].Index + 1;

        public static double ChordSpeedFactor(double columnDelta)
        {
            if (double.IsPositiveInfinity(columnDelta))
                return 1.0;

            return Math.Clamp(chord_speed_threshold_ms / columnDelta, chord_speed_factor_min, chord_speed_factor_max);
        }

        public static double ChordRepeatDampen(ManiaDifficultyHitObject current, double columnDelta)
        {
            int totalColumns = current.Row.TotalColumns;
            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);

            double dampen = chordRunDampen(current, totalColumns, full_chord_nerf * speedScale, full_chord_run_ramp);

            if (totalColumns >= 2)
                dampen *= chordRunDampen(current, totalColumns - 1, near_full_chord_nerf * speedScale, near_full_chord_run_ramp);

            return dampen;
        }

        private static double chordRunDampen(ManiaDifficultyHitObject current, int minSize, double ceiling, double runRamp)
        {
            if (ceiling <= 0)
                return 1.0;

            // The run counts the current row, so only the rows before it have to be walked.
            int run = 1 + current.Row.RunLengthBack(RunDampenUtils.CapFor(runRamp) - 1, 1, minSize, (_, earlier, size) => earlier.Size >= size);

            return RunDampenUtils.Dampen(run, runRamp, ceiling);
        }
    }
}
