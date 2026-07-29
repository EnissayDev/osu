// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class CrossHandMashDetector
    {
        private const double onset_ms = 64.0;

        private const double run_lo = 3.0;
        private const double run_hi = 10.0;
        private const int run_cap = 128;

        private const double cross_hand_density_lo = 0.14;
        private const double cross_hand_density_hi = 0.32;

        private const double large_chord_density_lo = 0.05;
        private const double large_chord_density_hi = 0.16;

        public static double EvaluateFactorOf(ManiaRow row)
        {
            double gap = row.GapBefore;

            if (gap >= onset_ms)
                return 1.0;

            double crossHandGate = DiffUtils.Smoothstep(row.CrossHandDensity(), cross_hand_density_lo, cross_hand_density_hi);

            if (crossHandGate <= 0.0)
                return 1.0;

            double runLength = Manipulation.RunLengthBefore(row, run_cap) + Manipulation.RunLengthAfter(row, run_cap);
            double runWeight = DiffUtils.Smoothstep(runLength, run_lo, run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double largeChordGate = 1.0 - DiffUtils.Smoothstep(row.LargeChordDensity(), large_chord_density_lo, large_chord_density_hi);

            return Manipulation.Plateau(gap, onset_ms, runWeight * crossHandGate * largeChordGate);
        }
    }
}
