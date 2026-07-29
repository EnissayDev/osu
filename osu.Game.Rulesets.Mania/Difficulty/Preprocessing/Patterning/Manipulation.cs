// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class Manipulation
    {
        private const double plateau_offset_ms = 80.0;
        private const double plateau_max_exponent = 1.0;
        private const double chain_gap_ratio_lo = 1.5;
        private const double chain_gap_ratio_hi = 2.2;

        public static double Plateau(double gap, double onsetMs, double strength)
        {
            if (strength <= 0.0 || gap >= onsetMs)
                return 1.0;

            double ratio = (gap + plateau_offset_ms) / (onsetMs + plateau_offset_ms);

            return Math.Pow(ratio, Math.Min(strength, plateau_max_exponent));
        }

        public static double StepContinuity(double gap, double pulse)
        {
            if (!(pulse > 0.0) || double.IsInfinity(pulse))
                return 0.0;

            return 1.0 - DiffUtils.Smoothstep(gap / pulse, chain_gap_ratio_lo, chain_gap_ratio_hi);
        }

        public static double RunLengthBefore(ManiaRow start, double cap, Func<ManiaRow, ManiaRow, bool>? extendsRun = null)
            => runLength(start, backwards: true, cap, extendsRun);

        public static double RunLengthAfter(ManiaRow start, double cap, Func<ManiaRow, ManiaRow, bool>? extendsRun = null)
            => runLength(start, backwards: false, cap, extendsRun);

        private static double runLength(ManiaRow start, bool backwards, double cap, Func<ManiaRow, ManiaRow, bool>? extendsRun)
        {
            double pulse = start.LocalPulse;
            double length = 0.0;

            // A step never counts for more than the loosest step already walked through.
            double weakestStep = 1.0;

            for (ManiaRow row = start; length < cap;)
            {
                ManiaRow? next = backwards ? row.Previous() : row.Next();

                if (next == null || extendsRun?.Invoke(row, next) == false)
                    break;

                double step = StepContinuity(Math.Abs(next.StartTime - row.StartTime), pulse);

                if (step <= 0.0)
                    break;

                weakestStep = Math.Min(weakestStep, step);
                length += weakestStep;
                row = next;
            }

            return length;
        }
    }
}
