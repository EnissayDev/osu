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

        /// <summary>
        /// Evaluates the difficulty of hitting the current note with the same finger that just played its column,
        /// based on:
        /// <list type="bullet">
        /// <item><description>how quickly the column comes back to itself,</description></item>
        /// <item><description>the chord the repeat happens inside,</description></item>
        /// <item><description>long notes held in other columns at the time,</description></item>
        /// <item><description>and how much of the repeat the map lets you hit some other way.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            const double tap_rate_offset_ms = 60;
            const double strain_exponent = 1.29407;
            const double jack_multiplier = 0.62159;

            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.19496; // sqrt(1.42793)

            double columnDelta = current.ColumnDelta;

            //TODO: The whole chordjack evaluation could be revamped after all.
            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            int chordDepth = ChordUtils.DepthInChord(current);
            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + tap_rate_offset_ms);

            // How quickly the column comes back to itself, scaled by the chord it repeats inside.
            double jackDifficulty = tapRate * calculateChordJackBonus(current, chordDepth, columnDelta) * calculateSpeedBonus(tapRate);

            jackDifficulty = DiffUtils.Pow(jackDifficulty, strain_exponent);

            jackDifficulty *= calculateChordDepthMultiplier(current, chordDepth, columnDelta);
            jackDifficulty *= calculateConcurrentHoldBonus(current);

            // Repeats that ask for more than their rate suggests.
            jackDifficulty *= FullChordJackEvaluator.EvaluateMultiplierOf(current, columnDelta, jackDifficulty * jack_multiplier);
            jackDifficulty *= current.ManipulationFactor * current.EnduranceFactor * SpeedjackEvaluator.EvaluateMultiplierOf(current) * AnchorEvaluator.EvaluateMultiplierOf(current);

            // Repeats the map lets you hit with something other than a jack motion.
            jackDifficulty *= JackSpacingEvaluator.EvaluateMultiplierOf(current, chordDepth, columnDelta, tapRate);

            return jackDifficulty * jack_multiplier * total_weight;
        }

        /// <summary>
        /// How much the chord this note sits in adds to the repeat, tapering off as the chord repeats.
        /// </summary>
        private static double calculateChordJackBonus(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            return Math.Max(0.1,
                (1.0 + 0.17460 * ChordUtils.ChordSpeedFactor(columnDelta) * (chordDepth - 1))
                * ChordUtils.ChordRepeatDampen(current, columnDelta));
        }

        /// <summary>
        /// Fast repeats cost more than their rate alone suggests, since there is no time to reposition between them.
        /// </summary>
        private static double calculateSpeedBonus(double tapRate)
        {
            // Centred on 5 notes per second, which is roughly a 1/4 note at 150bpm.
            return 1.0 + 0.7 * DiffUtils.Logistic(tapRate, 5.0, 0.5);
        }

        /// <summary>
        /// What the width of the chord does to the repeat. Chord jacks are worth most around 160bpm and less to
        /// either side of it, and past ~190bpm a repeat on a wide chord is a roll or vibro that can be mashed, so
        /// it rolls back off. A repeat that fast on jumps and single notes has to be jacked, and keeps its value.
        /// </summary>
        private static double calculateChordDepthMultiplier(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            const double slow_ms = 140.0;
            const double fast_ms = 100.0;
            const double veryfast_ms = 84.0;

            const double slow_mult = 0.6;
            const double fast_mult = 1.2;
            const double veryfast_mult = 0.75;
            const double veryfast_open_mult = 1.45;

            if (chordDepth < 2)
                return TrillUtils.TrillFactor(current);

            // Ramp up to the peak, then back down past it.
            double bpmScale = DiffUtils.Smoothstep(columnDelta, slow_ms, fast_ms);
            double chordSpeedMultiplier = slow_mult + (fast_mult - slow_mult) * bpmScale;

            // How wide the chords around the repeat are, and how long a run its column plays, together decide
            // whether the repeat can be rolled through instead of jacked.
            double rollable = Math.Max(
                DiffUtils.Smoothstep(ChordUtils.LocalChordSize(current), 1.9, 2.5),
                DiffUtils.Smoothstep(ColumnRunUtils.RunLengthAround(current, 1.5 * columnDelta, 32), 2.5, 4.0));

            // Roll the buff back down past ~160bpm, by as much as the chords around it are wide enough to roll.
            double fastRolloff = DiffUtils.Smoothstep(columnDelta, fast_ms, veryfast_ms);
            double veryfastMultiplier = veryfast_open_mult + (veryfast_mult - veryfast_open_mult) * rollable;

            chordSpeedMultiplier += (veryfastMultiplier - fast_mult) * fastRolloff;

            return ChordUtils.CHORDJACK_NERF * chordSpeedMultiplier;
        }

        /// <summary>
        /// Long notes held in other columns while this note is hit make it harder to place.
        /// </summary>
        private static double calculateConcurrentHoldBonus(ManiaDifficultyHitObject current)
        {
            int totalColumns = current.PreviousHitObjects.Length;

            if (totalColumns == 1)
                return 1.0;

            double heldFraction = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS) / (double)(totalColumns - 1);

            return 1.0 + 0.6 * heldFraction;
        }
    }
}
