// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class MashDetector
    {
        private const double onset_ms = 60.0;

        private const double run_lo = 3.0;
        private const double run_hi = 9.0;
        private const int run_cap = 64;

        private const double chord_density_lo = 0.06;
        private const double chord_density_hi = 0.18;

        private const double cross_hand_density_lo = 0.10;
        private const double cross_hand_density_hi = 0.22;

        public static double EvaluateFactorOf(ManiaRow row)
        {
            double gap = row.GapBefore;

            if (!row.IsHandLocal || gap >= onset_ms)
                return 1.0;

            double runLength = Manipulation.RunLengthBefore(row, run_cap, (_, to) => to.IsHandLocal);
            double runWeight = DiffUtils.Smoothstep(runLength, run_lo, run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = DiffUtils.Smoothstep(row.ChordDensity(), chord_density_lo, chord_density_hi);
            double crossHandGate = 1.0 - DiffUtils.Smoothstep(row.CrossHandDensity(), cross_hand_density_lo, cross_hand_density_hi);

            return Manipulation.Plateau(gap, onset_ms, runWeight * chordGate * crossHandGate);
        }
    }
}
