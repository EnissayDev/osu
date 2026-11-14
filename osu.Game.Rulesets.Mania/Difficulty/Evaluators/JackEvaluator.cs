// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        private const double jack_multiplier = 111.44;

        /// <summary>
        /// The share of the hand's fingers that are still holding a long note through this row.
        /// </summary>
        private static double heldPressureOf(ManiaDifficultyHitObject current, ManiaHand hand)
        {
            ManiaRow row = current.Row;
            double tolerance = ChordUtils.ToleranceAt(current.ClockRate);
            double heldFingers = 0.0;

            foreach (var previous in row.Objects[0].PreviousHitObjects)
            {
                if (previous is null || previous.Row.StartTime >= row.StartTime - tolerance || previous.EndTime <= row.StartTime + tolerance)
                    continue;

                heldFingers += HandLayout.ShareOf(previous.Column, row.TotalColumns, hand) * HoldUtils.HoldGateOf(previous.EndTime - previous.Row.StartTime);
            }

            return heldFingers / HandLayout.FingerCount(row.TotalColumns);
        }

        public static double FingerCost(double bpm, double fill)
        {
            const double base_slope = 0.00029167;
            const double peak_gain = 1.7413;
            const double chord_slope = 0.013333;

            if (bpm <= 0)
                return 0.0;

            return base_slope * bpm
                   + peak_gain * DiffUtils.Smoothstep(bpm, 90.0, 345.0)
                   + chord_slope * fill * ManiaDiffUtils.Softplus(bpm - 330.0, 42.0);
        }

        private static double chordWidthBonus(double chordSize, double occupancy, ManiaRow row)
        {
            const double slam_relief_multiplier = 0.4;

            double slamRelief = row.Size < row.TotalColumns ? 0.0 : DiffUtils.Smoothstep(occupancy, 0.92, 1.0);

            return Math.Pow(Math.Max(chordSize - 1.0, 0.0), 1.1) * (1.0 - slam_relief_multiplier * slamRelief);
        }

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            const double anchor_multiplier = 0.36527;

            double left = handDifficulty(current, ManiaHand.Left);
            double right = handDifficulty(current, ManiaHand.Right);

            // Bonus
            double anchor = 1.0 + anchor_multiplier * AnchorEvaluator.EvaluateBonusOf(current.Row.Load);

            return DiffUtils.Norm(1.5, left, right) * anchor * jack_multiplier;
        }

        private static double handDifficulty(ManiaDifficultyHitObject current, ManiaHand hand)
        {
            const double chord_width_multiplier = 1.07;
            const double reshape_multiplier = 0.3;
            const double held_pressure_multiplier = 2.60;
            const double settle_relief_multiplier = 0.39;
            const double interleave_relief_multiplier = 0.08351;

            ManiaRow row = current.Row;
            RowLoad load = row.Load;

            double throughput = throughputOf(load.FingerRates, hand);

            if (throughput <= 0)
                return 0.0;

            double handRate = load.HandRates[(int)hand];
            double effectiveRate = secondMomentOf(load.FingerRates, hand) / throughput;
            double chordSize = throughput / handRate;
            double occupancy = effectiveRate / handRate;
            int fingers = HandLayout.FingerCount(row.TotalColumns);

            double fill = fingers > 1 ? DiffUtils.Smoothstep(chordSize / fingers, 0.55, 1.0) : 0.0;
            double finger = FingerCost(DiffUtils.MillisecondsToBPM(1000.0 / effectiveRate, 2), fill);

            // Bonuses
            double chordWidth = 1.0 + chord_width_multiplier * chordWidthBonus(chordSize, occupancy, row);
            double reshape = 1.0 + reshape_multiplier * reshapeBonus(chordSize, occupancy, row.Repetition);
            double heldPressure = 1.0 + held_pressure_multiplier * heldPressureOf(current, hand);

            // Nerfs
            double settle = 1.0 - settle_relief_multiplier * settleRelief(chordSize, occupancy, row.Repetition);
            double interleave = 1.0 - interleave_relief_multiplier * interleaveRelief(occupancy, handRate / load.RowRate);

            return finger * chordWidth * reshape * settle * heldPressure * interleave;
        }

        private static double interleaveRelief(double occupancy, double handShare)
        {
            return DiffUtils.Smoothstep(occupancy, 0.55, 0.8) * DiffUtils.Smoothstep(handShare, 0.95, 0.6);
        }

        private static double reshapeBonus(double chordSize, double occupancy, double repetition)
        {
            return chordnessOf(chordSize) * reshapingOf(occupancy, repetition);
        }

        private static double settleRelief(double chordSize, double occupancy, double repetition)
        {
            return chordnessOf(chordSize) * (1.0 - reshapingOf(occupancy, repetition));
        }

        private static double chordnessOf(double chordSize) => DiffUtils.Smoothstep(chordSize, 1.05, 2.00);

        private static double reshapingOf(double occupancy, double repetition) =>
            DiffUtils.Smoothstep(Math.Clamp(1.0 - occupancy, 0.0, 1.0) * (1.0 - repetition), 0.14, 0.16253);

        private static double throughputOf(double[] fingerRates, ManiaHand hand)
        {
            double throughput = 0.0;

            for (int column = 0; column < fingerRates.Length; column++)
                throughput += HandLayout.ShareOf(column, fingerRates.Length, hand) * fingerRates[column];

            return throughput;
        }

        private static double secondMomentOf(double[] fingerRates, ManiaHand hand)
        {
            double secondMoment = 0.0;

            for (int column = 0; column < fingerRates.Length; column++)
                secondMoment += HandLayout.ShareOf(column, fingerRates.Length, hand) * fingerRates[column] * fingerRates[column];

            return secondMoment;
        }
    }
}
