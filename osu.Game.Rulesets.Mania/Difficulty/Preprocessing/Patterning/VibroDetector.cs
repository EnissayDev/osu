// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class VibroDetector
    {
        private const double onset_ms = 190.0;

        private const double run_lo = 2.5;
        private const double run_hi = 5.0;
        private const int run_cap = 64;

        private const double row_size_lo = 1.6;
        private const double row_size_hi = 2.6;

        public static double EvaluateFactorOf(ManiaRow row, int column)
        {
            double gap = row.GapBefore;

            if (gap >= onset_ms)
                return 1.0;

            var run = (rows: 1, notes: (double)row.Size);

            run = extendRun(run, row, column, backwards: true);
            run = extendRun(run, row, column, backwards: false);

            double runWeight = DiffUtils.Smoothstep(run.rows, run_lo, run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double rowSizeGate = 1.0 - DiffUtils.Smoothstep(run.notes / run.rows, row_size_lo, row_size_hi);

            return Manipulation.Plateau(gap, onset_ms, runWeight * rowSizeGate);
        }

        private static (int rows, double notes) extendRun((int rows, double notes) run, ManiaRow row, int column, bool backwards)
        {
            double pulse = row.LocalPulse;

            for (ManiaRow current = row; run.rows < run_cap;)
            {
                ManiaRow? next = backwards ? current.Previous() : current.Next();

                if (next == null || !next.Contains(column))
                    break;

                if (Manipulation.StepContinuity(Math.Abs(next.StartTime - current.StartTime), pulse) <= 0.0)
                    break;

                run.rows++;
                run.notes += next.Size;
                current = next;
            }

            return run;
        }
    }
}
