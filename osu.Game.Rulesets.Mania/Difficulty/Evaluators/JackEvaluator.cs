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
        private static double heldPressureOf(HandLoad handLoad) => handLoad.HeldShare;

        public static double FingerCost(double rate, double fill)
        {
            const double base_slope = 0.00875;
            const double peak_gain = 0.34;
            const double chord_slope = 0.4;
            const double peak_rate = 7.25;
            const double peak_width = 2.89;
            const double chord_rate = 11.0;
            const double chord_width = 1.4;

            if (rate <= 0)
                return 0.0;

            double bell = DiffUtils.Erf((rate - peak_rate) / peak_width) + DiffUtils.Erf(peak_rate / peak_width);

            return base_slope * rate
                   + peak_gain * peak_width * (Math.Sqrt(Math.PI) / 2.0) * bell
                   + chord_slope * fill * ManiaDiffUtils.Softplus(rate - chord_rate, chord_width);
        }

        private static double chordWidthBonus(HandLoad handLoad, ManiaRow row)
        {
            const double gain = 1.07;
            const double exponent = 1.1;

            return 1.0 + gain * Math.Pow(Math.Max(handLoad.ChordSize - 1.0, 0.0), exponent) * slamReliefOf(handLoad, row);
        }

        private static double slamReliefOf(HandLoad handLoad, ManiaRow row)
        {
            const double relief = 0.4;
            const double locked_lo = 0.92;
            const double locked_hi = 1.0;

            if (row.Size < row.TotalColumns)
                return 1.0;

            return 1.0 - relief * DiffUtils.Smoothstep(handLoad.Occupancy, locked_lo, locked_hi);
        }

        private static double fillOf(HandLoad handLoad)
        {
            const double fill_lo = 0.55;
            const double fill_hi = 1.0;

            return DiffUtils.Smoothstep(handLoad.Fill, fill_lo, fill_hi);
        }

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            const double hand_combine = 1.5;
            const double jack_scale = 139.3;
            const double total_weight = 0.8;

            RowLoad load = current.Row.Load;

            double left = handDifficulty(load, ManiaHand.Left, current.Row);
            double right = handDifficulty(load, ManiaHand.Right, current.Row);

            return DiffUtils.Norm(hand_combine, left, right) * AnchorEvaluator.EvaluateMultiplierOf(load) * jack_scale * total_weight;
        }

        private static double handDifficulty(RowLoad load, ManiaHand hand, ManiaRow row)
        {
            const double hold_gain = 2.60;

            double repetition = row.Repetition;

            HandLoad handLoad = load.Of(hand);

            if (handLoad.Throughput <= 0)
                return 0.0;

            double finger = FingerCost(handLoad.EffectiveRate, fillOf(handLoad));

            if (finger <= 0)
                return 0.0;

            return finger * chordWidthBonus(handLoad, row) * reshapeBonus(handLoad, repetition)
                   * (1.0 + hold_gain * heldPressureOf(handLoad)) * interleaveRelief(handLoad, load);
        }

        private static double interleaveRelief(HandLoad handLoad, RowLoad load)
        {
            const double relief = 0.08351;
            const double gate_lo = 0.6;
            const double gate_hi = 0.95;
            const double settled_lo = 0.55;
            const double settled_hi = 0.8;

            if (load.RowRate <= 0)
                return 1.0;

            double share = handLoad.RowRate / load.RowRate;

            double settled = DiffUtils.Smoothstep(handLoad.Occupancy, settled_lo, settled_hi);

            return 1.0 - relief * settled * DiffUtils.Smoothstep(share, gate_hi, gate_lo);
        }

        private static double reshapeBonus(HandLoad handLoad, double repetition)
        {
            const double churn_gain = 0.3;
            const double settle_discount = 0.61;
            const double settle_gate_lo = 0.14;
            const double settle_gate_hi = 0.16253;

            double chordness = DiffUtils.Smoothstep(handLoad.ChordSize, 1.05, 2.00);
            double reshaping = DiffUtils.Smoothstep(handLoad.Reshaping(repetition), settle_gate_lo, settle_gate_hi);

            double shape = settle_discount + (1.0 + churn_gain - settle_discount) * reshaping;

            return 1.0 + chordness * (shape - 1.0);
        }
    }
}
