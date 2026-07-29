// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class MinijackEvaluator
    {
        private const double minijack_buff = 2.5;
        private const int minijack_min_chord = 4;
        private const double minijack_fast_ms = 85.0;
        private const double minijack_slow_ms = 110.0;
        private const double minijack_manip_lo = 0.95;
        private const double minijack_manip_hi = 0.99;

        private const int minijack_scan_limit = 32;

        private const double minijack_run_window_scale = 1.5;
        private const double minijack_run_gate_lo = 3.0;
        private const double minijack_run_gate_hi = 4.0;

        private const double minijack_recur_window_scale = 4.0;
        private const double minijack_recur_gate_lo = 1.0;
        private const double minijack_recur_gate_hi = 2.0;

        private const double minijack_size_taper_lo = 2.5;
        private const double minijack_size_taper_hi = 2.9;
        private const int minijack_size_radius = 4;

        private const double minijack_strain_damp = 0.9;
        private const double minijack_strain_lo = 12.0;
        private const double minijack_strain_hi = 15.0;
        private const double minijack_strain_density_lo = 1.6;
        private const double minijack_strain_density_hi = 2.0;

        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current, ManiaDifficultyHitObject? previous, int totalColumns, double columnDelta, double baseStrain)
        {
            int fullChord = Math.Max(minijack_min_chord, totalColumns);

            if (previous == null)
                return 1.0;

            bool sharesChordWithPrevious = current.Row.IsSameRow(previous.Row);

            if (sharesChordWithPrevious || previous.Row.Size < fullChord)
                return 1.0;

            double speedGate = DiffUtils.Smoothstep(minijack_slow_ms - columnDelta, 0.0, minijack_slow_ms - minijack_fast_ms);
            double manipGate = DiffUtils.ReverseLerp(current.ManipulationFactor, minijack_manip_lo, minijack_manip_hi);

            double runWindow = minijack_run_window_scale * columnDelta;
            int runLength = ColumnRunUtils.RunLengthAround(current, runWindow, minijack_scan_limit);

            double runGate = 1.0 - DiffUtils.Smoothstep(runLength, minijack_run_gate_lo, minijack_run_gate_hi);
            double recurGate = fullChordRecurGate(current, fullChord, columnDelta);

            double localSize = localChordSize(current);

            double sizeDampen = 1.0 - DiffUtils.Smoothstep(localSize, minijack_size_taper_lo, minijack_size_taper_hi);

            double strainDensityGate = DiffUtils.Smoothstep(localSize, minijack_strain_density_lo, minijack_strain_density_hi);
            double strainDampen = 1.0 - minijack_strain_damp * DiffUtils.Smoothstep(baseStrain, minijack_strain_lo, minijack_strain_hi) * strainDensityGate;

            return 1.0 + minijack_buff * speedGate * manipGate * runGate * recurGate * sizeDampen * strainDampen;
        }

        /// <summary>
        /// The average notes per row over the rows surrounding <paramref name="current"/>.
        /// </summary>
        private static double localChordSize(ManiaDifficultyHitObject current)
            => current.Row.AverageOfRowsAround(minijack_size_radius, row => row.Size);

        private static double fullChordRecurGate(ManiaDifficultyHitObject current, int fullChord, double columnDelta)
        {
            double window = minijack_recur_window_scale * columnDelta;
            int fullChords = 0;

            for (int i = 0; i < minijack_scan_limit; i++)
            {
                var previous = (ManiaDifficultyHitObject?)current.Previous(i);

                if (previous == null || current.StartTime - previous.StartTime > window)
                    break;

                if (ChordUtils.DepthInChord(previous) == 1 && previous.Row.Size >= fullChord)
                    fullChords++;
            }

            return 1.0 - DiffUtils.Smoothstep(fullChords, minijack_recur_gate_lo, minijack_recur_gate_hi);
        }
    }
}
