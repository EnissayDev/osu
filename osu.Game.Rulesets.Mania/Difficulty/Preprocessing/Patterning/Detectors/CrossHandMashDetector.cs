// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds a fast section split across both hands, where the rows come close enough together that both hands
    /// can drop at once instead of each row being hit on time.
    /// </summary>
    public class CrossHandMashDetector : RowManipulationDetector
    {
        protected override double Onset => 64;

        protected override double StrengthOf(ManiaRow row)
        {
            const int run_cap = 128;

            // There is nothing to drop together unless the section is actually written across both hands.
            double crossHandGate = DiffUtils.Smoothstep(row.CrossHandDensity(), 0.14, 0.32);

            if (crossHandGate <= 0.0)
                return 0.0;

            // There is no ContinuesChain override here, so the run is just how long the section keeps its pulse.
            double runLength = ChainBefore(row, run_cap).Length + ChainAfter(row, run_cap).Length;
            double runWeight = DiffUtils.Smoothstep(runLength, 3, 10);

            if (runWeight <= 0.0)
                return 0.0;

            // A chord this wide has to be hit properly rather than dropped, so a section full of them is left alone.
            double largeChordGate = DiffUtils.Smoothstep(row.LargeChordDensity(), 0.16, 0.05);

            return runWeight * crossHandGate * largeChordGate;
        }
    }
}
