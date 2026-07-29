// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public static class ManiaPatternContextPreprocessor
    {
        public static void ProcessAndAssign(ManiaMapData mapData)
        {
            foreach (var row in mapData.Rows)
            {
                // The harshest of the patterns the row takes part in wins.
                double manipulation = Math.Min(
                    Math.Min(RollDetector.EvaluateFactorOf(row), JumptrillDetector.EvaluateFactorOf(row)),
                    Math.Min(MashDetector.EvaluateFactorOf(row), CrossHandMashDetector.EvaluateFactorOf(row)));

                double endurance = EnduranceDetector.EvaluateFactorOf(row);

                foreach (var note in row.Objects)
                {
                    double noteManipulation = Math.Min(manipulation, VibroDetector.EvaluateFactorOf(row, note.Column));

                    if (noteManipulation < 1.0)
                        note.ManipulationFactor = noteManipulation;

                    note.EnduranceFactor = endurance;
                }
            }
        }
    }
}
