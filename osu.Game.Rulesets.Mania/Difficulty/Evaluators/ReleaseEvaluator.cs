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
        /// <summary>
        /// Holds longer than this are all the same amount of work to keep held, so the duration is capped here
        /// before anything reads it.
        /// </summary>
        private const double max_long_note_duration_ms = 1000.0;

        /// <summary>
        /// Evaluates the difficulty of letting go of the current long note, based on:
        /// <list type="bullet">
        /// <item><description>how long it is held for,</description></item>
        /// <item><description>and how close its release lands to a release in another column.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double releaseDifficulty = 0.0;

            if (current.BaseObject is not HoldNote)
                return releaseDifficulty;

            // Release is not combined in quadrature with the tap skills, so it carries its whole weight here.
            const double total_weight = 2.83449;

            double duration = Math.Min(current.EndTime - current.StartTime, max_long_note_duration_ms);
            double longNoteGate = longNoteGateOf(duration);

            releaseDifficulty += calculateLongHoldBonus(duration, longNoteGate);
            releaseDifficulty += calculateReleaseSpeedBonus(current, longNoteGate);

            return releaseDifficulty * total_weight;
        }

        /// <summary>
        /// How much of a hold note this object really is. A hold barely longer than a tap ends in the same
        /// motion that started it, so short durations fade out rather than switching off at a hard length.
        /// </summary>
        private static double longNoteGateOf(double duration) => DiffUtils.Logistic(duration, 110.90068, 0.07);

        /// <summary>
        /// The work of holding the note down, growing with its duration and growing faster once the hold is long
        /// enough that it has to be tracked rather than just ridden out.
        /// </summary>
        private static double calculateLongHoldBonus(double duration, double longNoteGate)
        {
            double seconds = duration / 1000.0;
            double holdLengthFactor = 1.6 * DiffUtils.Smoothstep(duration, 500, 680) * seconds;

            return (0.42 + 0.9 * seconds + holdLengthFactor) * longNoteGate;
        }

        /// <summary>
        /// Releases very close together are harder to time apart, so the closest release in any other column that
        /// is still being held is paid for here.
        /// </summary>
        private static double calculateReleaseSpeedBonus(ManiaDifficultyHitObject current, double longNoteGate)
        {
            const double slope = 0.1;
            const double offset_ms = 30.0;
            const double weight = 0.2;

            double closestReleaseDelta = double.PositiveInfinity;

            for (int otherColumn = 0; otherColumn < current.Row.TotalColumns; otherColumn++)
            {
                if (otherColumn == current.Column)
                    continue;

                // A hold starting in the same chord is one press, not a second thing to track.
                if (Math.Abs(current.LastStartTimeInColumn(otherColumn) - current.StartTime) <= ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                double otherEndTime = current.LastEndTimeInColumn(otherColumn);

                if (otherEndTime > current.StartTime)
                    closestReleaseDelta = Math.Min(closestReleaseDelta, Math.Abs(current.EndTime - otherEndTime));
            }

            return weight * DiffUtils.Logistic(slope * (closestReleaseDelta - offset_ms), longNoteGate);
        }
    }
}
