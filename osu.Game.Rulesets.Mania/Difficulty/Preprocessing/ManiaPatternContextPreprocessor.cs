// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    /// <summary>
    /// Runs every pattern detector over the map and records what they found on each note, so that an evaluator
    /// looking at a single note can still see a pattern that took many rows to form.
    /// </summary>
    public static class ManiaPatternContextPreprocessor
    {
        public static void ProcessAndAssign(ManiaMapData mapData)
        {
            var rowDetectors = new RowManipulationDetector[]
            {
                new RollDetector(),
                new JumptrillDetector(),
                new MashDetector(),
                new CrossHandMashDetector(),
            };

            // Vibro is the one pattern that lives in a single column rather than in the row, so it is read per
            // note. Endurance is read per row, but it rewards as well as discounts, so it is kept apart too.
            var vibroDetector = new VibroDetector();
            var enduranceDetector = new EnduranceDetector();

            foreach (var row in mapData.Rows)
            {
                // The harshest of the patterns the row takes part in wins.
                double manipulation = 1.0;

                foreach (var detector in rowDetectors)
                    manipulation = Math.Min(manipulation, detector.EvaluateFactorOf(row));

                double endurance = enduranceDetector.EvaluateFactorOf(row);

                foreach (var note in row.Objects)
                {
                    double noteManipulation = Math.Min(manipulation, vibroDetector.EvaluateFactorOf(row, note.Column));

                    if (noteManipulation < 1.0)
                        note.ManipulationFactor = noteManipulation;

                    note.EnduranceFactor = endurance;
                }
            }
        }
    }
}
