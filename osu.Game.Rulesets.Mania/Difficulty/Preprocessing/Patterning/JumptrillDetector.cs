// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class JumptrillDetector
    {
        private const double onset_ms = 260.0;

        private const double run_lo = 3.0;
        private const double run_hi = 8.0;
        private const int run_cap = 200;

        private const double cross_hand_density_lo = 0.06;
        private const double cross_hand_density_hi = 0.16;

        private const double jump_density_lo = 0.05;
        private const double jump_density_hi = 0.11;
        private const int jump_density_radius = 32;

        private const double local_jump_density_lo = 0.05;
        private const double local_jump_density_hi = 0.20;
        private const int local_jump_density_radius = 6;

        private const double strict_jumptrill_floor = 0.65;

        public static double EvaluateFactorOf(ManiaRow row)
        {
            double gap = row.GapBefore;

            if (!row.IsHandLocal || gap >= onset_ms)
                return 1.0;

            double runWeight = DiffUtils.Smoothstep(chainLength(row), run_lo, run_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double crossHandGate = Math.Max(
                1.0 - DiffUtils.Smoothstep(row.CrossHandDensity(), cross_hand_density_lo, cross_hand_density_hi),
                isStrictJumptrill(row) ? strict_jumptrill_floor : 0.0);

            double jumpGate = DiffUtils.Smoothstep(row.SingleHandChordDensity(jump_density_radius), jump_density_lo, jump_density_hi)
                              * DiffUtils.Smoothstep(row.SingleHandChordDensity(local_jump_density_radius), local_jump_density_lo, local_jump_density_hi);

            return Manipulation.Plateau(gap, onset_ms, runWeight * crossHandGate * jumpGate);
        }

        private static double chainLength(ManiaRow row)
        {
            return 1.0
                   + Manipulation.RunLengthBefore(row, run_cap, trillStep())
                   + Manipulation.RunLengthAfter(row, run_cap, trillStep());
        }

        private static Func<ManiaRow, ManiaRow, bool> trillStep()
        {
            Step previous = Step.Break;

            return (from, to) =>
            {
                if (!to.IsHandLocal)
                    return false;

                Step step = stepBetween(from, to);

                switch (step)
                {
                    case Step.Roll:
                    case Step.JumpSwitch:
                        break;

                    case Step.SingleSwitch when previous == Step.Roll:
                        break;

                    default:
                        return false;
                }

                previous = step;
                return true;
            };
        }

        private enum Step
        {
            /// <summary>Anything the chain cannot be continued through.</summary>
            Break,

            /// <summary>Two single notes on neighbouring columns of the same hand.</summary>
            Roll,

            /// <summary>A hand swap where at least one of the two rows is a chord.</summary>
            JumpSwitch,

            /// <summary>A hand swap between two single notes.</summary>
            SingleSwitch,
        }

        private static Step stepBetween(ManiaRow from, ManiaRow to)
        {
            if (from.Hand != to.Hand)
                return from.Size >= 2 || to.Size >= 2 ? Step.JumpSwitch : Step.SingleSwitch;

            return from.IsSingleNote && to.IsSingleNote && Math.Abs(from.Columns[0] - to.Columns[0]) == 1 ? Step.Roll : Step.Break;
        }

        private static bool isStrictJumptrill(ManiaRow row)
        {
            if (!row.IsJump)
                return false;

            return recursAt(row, -2) || recursAt(row, 2);
        }

        private static bool recursAt(ManiaRow row, int offset)
        {
            return row.Offset(offset) is ManiaRow recurrence
                   && row.Offset(offset / 2) is ManiaRow between
                   && ColumnPatternUtils.IsRecurrence(recurrence.Columns, between.Columns, row.Columns);
        }
    }
}
