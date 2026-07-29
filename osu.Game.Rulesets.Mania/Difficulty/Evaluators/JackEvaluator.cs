// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        /// <summary>
        /// Column gaps longer than this are too far apart to be jacked at all.
        /// </summary>
        public const double JACK_WINDOW_MS = 350.0;

        private const double total_weight = 1.19496; // sqrt(1.42793)
        private const double jack_multiplier = 0.62159;

        private const double tap_rate_offset_ms = 60;

        private const double strain_exponent = 1.29407;

        private const double speed_bonus_strength = 0.70000;
        private const double speed_bonus_midpoint = 5.0;
        private const double speed_bonus_slope = 0.5;

        private const double chordjack_buff = 0.17460;
        private const double chordjack_bonus_min = 0.1;

        // Chord jacks are worth most around 160bpm and less to either side of it.
        private const double chordjack_slow_ms = 140.0;
        private const double chordjack_fast_ms = 100.0;
        private const double chordjack_veryfast_ms = 84.0;
        private const double chordjack_slow_mult = 0.6;
        private const double chordjack_fast_mult = 1.2;
        private const double chordjack_veryfast_mult = 0.75;

        private const double held_ln_buff = 0.6;

        // Single-column repeats around this tap rate are mashable.
        private const double single_jack_nerf = 0.10;
        private const double single_jack_center = 5.5;
        private const double single_jack_width = 0.7;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double columnDelta = current.ColumnDelta;

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            var previous = (ManiaDifficultyHitObject?)current.Previous();

            int chordDepth = ChordUtils.DepthInChord(current);
            int totalColumns = current.PreviousHitObjects.Length;

            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + tap_rate_offset_ms);

            double jackDifficulty = tapRate * calculateChordJackBonus(current, chordDepth, columnDelta) * calculateSpeedBonus(tapRate);

            jackDifficulty = DiffUtils.Pow(jackDifficulty, strain_exponent);

            jackDifficulty *= calculateChordDepthMultiplier(current, chordDepth, columnDelta);
            jackDifficulty *= calculateConcurrentHoldBonus(current, totalColumns);

            jackDifficulty *= MinijackEvaluator.EvaluateMultiplierOf(current, previous, totalColumns, columnDelta, jackDifficulty * jack_multiplier);

            jackDifficulty *= current.ManipulationFactor * current.EnduranceFactor * SpeedjackEvaluator.EvaluateMultiplierOf(current) * AnchorEvaluator.EvaluateMultiplierOf(current);

            jackDifficulty *= calculateEaseMultiplier(current, chordDepth, tapRate, columnDelta);

            return jackDifficulty * jack_multiplier * total_weight;
        }

        /// <summary>
        /// How much the chord this note sits in adds to the repeat, tapering off as the chord repeats.
        /// </summary>
        private static double calculateChordJackBonus(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            return Math.Max(chordjack_bonus_min,
                (1.0 + chordjack_buff * ChordUtils.ChordSpeedFactor(columnDelta) * (chordDepth - 1))
                * ChordUtils.ChordRepeatDampen(current, columnDelta));
        }

        private static double calculateSpeedBonus(double tapRate) => 1.0 + speed_bonus_strength * DiffUtils.Logistic(tapRate, speed_bonus_midpoint, speed_bonus_slope);

        private static double calculateChordDepthMultiplier(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            if (chordDepth < 2)
                return TrillUtils.TrillFactor(current);

            double bpmScale = DiffUtils.Smoothstep(chordjack_slow_ms - columnDelta, 0.0, chordjack_slow_ms - chordjack_fast_ms);
            double chordSpeedMultiplier = chordjack_slow_mult + (chordjack_fast_mult - chordjack_slow_mult) * bpmScale;

            // Roll the buff back down past ~160bpm.
            double fastRolloff = DiffUtils.Smoothstep(chordjack_fast_ms - columnDelta, 0.0, chordjack_fast_ms - chordjack_veryfast_ms);
            chordSpeedMultiplier += (chordjack_veryfast_mult - chordjack_fast_mult) * fastRolloff;

            return ChordUtils.CHORDJACK_NERF * chordSpeedMultiplier;
        }

        /// <summary>
        /// Long notes held in other columns while this note is hit make it harder to place.
        /// </summary>
        private static double calculateConcurrentHoldBonus(ManiaDifficultyHitObject current, int totalColumns)
        {
            if (totalColumns == 1)
                return 1.0;

            double heldFraction = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS) / (double)(totalColumns - 1);

            return 1.0 + held_ln_buff * heldFraction;
        }

        private static double calculateEaseMultiplier(ManiaDifficultyHitObject current, int chordDepth, double tapRate, double columnDelta)
        {
            double singleJack = calculateSingleJackMultiplier(chordDepth, tapRate);
            double spacing = JackSpacingEvaluator.EvaluateMultiplierOf(current, chordDepth, columnDelta);

            return Math.Min(singleJack, spacing);
        }

        private static double calculateSingleJackMultiplier(int chordDepth, double tapRate)
        {
            if (chordDepth >= 2)
                return 1.0;

            return 1.0 - single_jack_nerf * DiffUtils.SmoothstepBellCurve(tapRate, single_jack_center, single_jack_width);
        }
    }
}
