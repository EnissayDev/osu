// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ReleaseEvaluator
    {
        private const double max_long_note_duration_ms = 1000.0;

        private const double long_note_gate_midpoint_ms = 110.90068;
        private const double long_note_gate_slope = 0.07;

        private const double long_note_base_load = 0.42; // WAS 0.2
        private const double long_note_duration_load = 0.9; // WAS 1.10

        private const double long_hold_buff = 1.6;
        private const double long_hold_gate_lo_ms = 500.0;
        private const double long_hold_gate_hi_ms = 680.0;

        // Releases very close together are harder to time apart.
        private const double overlapping_release_slope = 0.1;
        private const double overlapping_release_offset_ms = 30.0;
        private const double overlapping_release_weight = 0.2;

        private const double total_weight = 2.83449;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double releaseDifficulty = 0.0;

            if (current.BaseObject is not HoldNote)
                return releaseDifficulty;

            double duration = Math.Min(current.EndTime - current.StartTime, max_long_note_duration_ms);
            double longNoteGate = DiffUtils.Logistic(duration, long_note_gate_midpoint_ms, long_note_gate_slope);

            releaseDifficulty += calculateLongHoldBonus(duration, longNoteGate);
            releaseDifficulty += calculateReleaseSpeedBonus(current, longNoteGate);

            return releaseDifficulty * total_weight;
        }

        private static double calculateLongHoldBonus(double duration, double longNoteGate)
        {
            double holdLengthFactor = long_hold_buff * DiffUtils.Smoothstep(duration, long_hold_gate_lo_ms, long_hold_gate_hi_ms) * (duration / 1000.0);
            double longHoldBonus = (long_note_base_load + long_note_duration_load * (duration / 1000.0) + holdLengthFactor) * longNoteGate;
            return longHoldBonus;
        }

        private static double calculateReleaseSpeedBonus(ManiaDifficultyHitObject current, double longNoteGate)
        {
            double closestReleaseDelta = double.PositiveInfinity;

            for (int otherColumn = 0; otherColumn < current.PreviousHitObjects.Length; otherColumn++)
            {
                if (otherColumn == current.Column)
                    continue;

                if (Math.Abs(current.LastStartTimeInColumn(otherColumn) - current.StartTime) <= ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                double otherEndTime = current.LastEndTimeInColumn(otherColumn);

                if (otherEndTime > current.StartTime)
                    closestReleaseDelta = Math.Min(closestReleaseDelta, Math.Abs(current.EndTime - otherEndTime));
            }

            double releaseSpeedBonus = overlapping_release_weight * DiffUtils.Logistic(overlapping_release_slope * (closestReleaseDelta - overlapping_release_offset_ms), longNoteGate);

            return releaseSpeedBonus;
        }
    }
}
