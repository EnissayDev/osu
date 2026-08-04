// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds a fast run of rows that one hand covers on its own, dense enough that the hand can mash the columns
    /// instead of hitting each row.
    /// </summary>
    public class MashDetector : RowManipulationDetector
    {
        protected override double Onset => 60;

        protected override bool ContinuesChain(ManiaRow? cameFrom, ManiaRow from, ManiaRow to) => to.IsHandLocal;

        protected override double StrengthOf(ManiaRow row)
        {
            if (!row.IsHandLocal)
                return 0.0;

            double runWeight = DiffUtils.Smoothstep(ChainBefore(row, 64).Length, 3, 9);

            if (runWeight <= 0.0)
                return 0.0;

            // Mashing needs something to mash. A run of single notes this fast is a roll instead.
            double chordGate = DiffUtils.Smoothstep(row.ChordDensity(), 0.06, 0.18);

            // Rows that need both hands have to be hit properly, so a section full of them is not being mashed through.
            double crossHandGate = DiffUtils.Smoothstep(row.CrossHandDensity(), 0.22, 0.10);

            return runWeight * chordGate * crossHandGate;
        }
    }
}
