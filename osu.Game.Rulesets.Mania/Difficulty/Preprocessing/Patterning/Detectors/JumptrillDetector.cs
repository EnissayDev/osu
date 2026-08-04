// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds a section bouncing between two hand shapes fast enough that the hands can just alternate as a whole,
    /// instead of each row being hit on its own.
    /// </summary>
    public class JumptrillDetector : RowManipulationDetector
    {
        /// <summary>
        /// How much of the discount a strict jumptrill keeps when the section around it looks cross-handed.
        /// Bouncing between two shapes is the jumptrill itself, not something the section merely allows, so it
        /// should not stop counting just because the rows around it are written across both hands.
        /// </summary>
        private const double strict_jumptrill_floor = 0.55;

        protected override double Onset => 260;

        protected override bool ContinuesChain(ManiaRow? cameFrom, ManiaRow from, ManiaRow to)
        {
            if (!to.IsHandLocal)
                return false;

            switch (stepBetween(from, to))
            {
                case Step.Roll:
                case Step.JumpSwitch:
                    return true;

                // Two single notes swapping hands is only part of a jumptrill if a roll led into it, otherwise
                // it is an ordinary stream crossing over.
                case Step.SingleSwitch:
                    return cameFrom != null && stepBetween(cameFrom, from) == Step.Roll;

                default:
                    return false;
            }
        }

        protected override double StrengthOf(ManiaRow row)
        {
            if (!row.IsHandLocal)
                return 0.0;

            double runWeight = DiffUtils.Smoothstep(chainLength(row), 3, 8);

            if (runWeight <= 0.0)
                return 0.0;

            // Rows that need both hands have to be hit properly, and a section full of them is played as written.
            double crossHandGate = Math.Max(
                DiffUtils.Smoothstep(row.CrossHandDensity(), 0.16, 0.06),
                isStrictJumptrill(row) ? strict_jumptrill_floor : 0.0);

            // A jumptrill needs jumps to bounce between. Both the wider section and the rows right next to this
            // one are checked, so a few jumps scattered through a stream cannot qualify the whole thing.
            double jumpGate = DiffUtils.Smoothstep(row.SingleHandChordDensity(32), 0.05, 0.11)
                              * DiffUtils.Smoothstep(row.SingleHandChordDensity(6), 0.05, 0.20);

            return runWeight * crossHandGate * jumpGate;
        }

        private double chainLength(ManiaRow row) => 1.0 + ChainBefore(row, 200).Length + ChainAfter(row, 200).Length;

        /// <summary>
        /// The ways one row can lead into the next, as far as a jumptrill is concerned.
        /// </summary>
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

        /// <summary>
        /// Whether this row is a jump that bounces off another jump and comes straight back, which is the heart
        /// of a jumptrill rather than something a section merely allows.
        /// </summary>
        private static bool isStrictJumptrill(ManiaRow row)
        {
            if (!row.IsJump)
                return false;

            return recursAt(row, -2) || recursAt(row, 2);
        }

        /// <summary>
        /// Whether the row <paramref name="offset"/> places away plays this row's columns again, with the row
        /// halfway between the two moving off them.
        /// </summary>
        private static bool recursAt(ManiaRow row, int offset)
        {
            return row.Offset(offset) is ManiaRow recurrence
                   && row.Offset(offset / 2) is ManiaRow between
                   && ColumnPatternUtils.IsRecurrence(recurrence.Columns, between.Columns, row.Columns);
        }
    }
}
