// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ReleaseEvaluator
    {
        private const double release_weight = 3.056;

        /// <summary>
        /// Holds longer than this are all the same amount of work to keep held, so the duration is capped here
        /// before anything reads it.
        /// </summary>
        private const double max_long_note_duration_ms = 1000.0;

        /// <summary>
        /// Evaluates the difficulty of letting go of the current long note, based on:
        /// <list type="bullet">
        /// <item><description>how long it is held for,</description></item>
        /// <item><description>how close its release lands to a release in another column,</description></item>
        /// <item><description>and how many other long notes are held during its release.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double releaseDifficulty = 0.0;

            if (current.BaseObject is not HoldNote)
                return releaseDifficulty;

            double duration = Math.Min(current.EndTime - current.StartTime, max_long_note_duration_ms);
            double longNoteGate = HoldUtils.HoldGateOf(duration);

            releaseDifficulty += calculateReleaseSpeedBonus(current, longNoteGate);
            releaseDifficulty += calculateReleaseWhileHolds(current, longNoteGate);

            releaseDifficulty *= current.HeldNoteMultiplier;

            return releaseDifficulty * release_weight;
        }

        /// <summary>
        /// Releases very close together are harder to time apart, so the closest release in any other column that
        /// is still being held is paid for here.
        /// </summary>
        private static double calculateReleaseSpeedBonus(ManiaDifficultyHitObject current, double longNoteGate)
        {
            double closestReleaseDelta = double.PositiveInfinity;

            foreach (double? endTime in current.LastConcurrentlyReleasedHolds.Select(o => o?.EndTime))
            {
                if (endTime is null || Math.Abs(endTime.Value - current.EndTime) <= ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                closestReleaseDelta = Math.Min(closestReleaseDelta, Math.Abs(current.EndTime - endTime.Value));
            }

            if (double.IsPositiveInfinity(closestReleaseDelta))
                return 0.0;

            return 0.2 * DiffUtils.Logistic(0.1 * (closestReleaseDelta - 30.0), longNoteGate);
        }

        /// <summary>
        /// Other held notes makes the movement of a release harder, scaling with how many columns are held, and
        /// nerfed if the next note in this column follows too closely.
        /// </summary>
        private static double calculateReleaseWhileHolds(ManiaDifficultyHitObject current, double longNoteGate)
        {
            const double release_long_note_weight = 0.45;
            const double next_note_relief_multiplier = 0.77;

            // Nerf
            double nextNote = 1.0 - next_note_relief_multiplier * nextNoteRelief(current);

            return release_long_note_weight * (current.HeldNoteMultiplier - 1.0) * longNoteGate * nextNote;
        }

        /// <summary>
        /// If the next note starts too close to this release, the release difficulty should be reduced, with a
        /// rebound once the next note is far enough away that the motion is no longer being overlapped.
        /// A note that starts right after a LN doesn't really need the player to think about release.
        /// </summary>
        private static double nextNoteRelief(ManiaDifficultyHitObject current)
        {
            ManiaDifficultyHitObject? nextInColumn = current.NextInColumn(0);

            if (nextInColumn == null)
                return 0.0;

            return DiffUtils.Smoothstep(nextInColumn.StartTime - current.EndTime, 180.0, 110.0);
        }
    }
}
