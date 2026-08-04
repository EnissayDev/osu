// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    internal static class FullChordJackEvaluator
    {
        /// <summary>
        /// The narrowest press that counts as a full chord.
        /// </summary>
        private const int min_chord = 4;

        /// <summary>
        /// How many notes back the scans here are willing to walk before giving up.
        /// </summary>
        private const int scan_limit = 32;

        /// <summary>
        /// How much more the repeat is worth than its rate suggests, for landing right after a full-width chord.
        /// Dropping off a whole chord onto one column is what gets paid for here, so the gates below take the
        /// value back whenever the chord is part of something that can be rolled or mashed through instead.
        /// </summary>
        public static double EvaluateMultiplierOf(ManiaDifficultyHitObject current, double columnDelta, double baseStrain)
        {
            var previous = (ManiaDifficultyHitObject?)current.Previous();

            if (previous == null)
                return 1.0;

            int fullChord = Math.Max(min_chord, current.PreviousHitObjects.Length);

            if (current.Row.IsSameRow(previous.Row) || previous.Row.Size < fullChord)
                return 1.0;

            double speedGate = DiffUtils.Smoothstep(columnDelta, 100, 82);

            // Anything the pattern preprocessor already called manipulable is not being jacked at all.
            double manipGate = DiffUtils.ReverseLerp(current.ManipulationFactor, 0.95, 0.99);

            // A long run in this column is a jackhammer the chord happens to sit on, not a drop off the chord.
            double runGate = DiffUtils.Smoothstep(ColumnRunUtils.RunLengthAround(current, 1.5 * columnDelta, scan_limit), 4, 3);

            double recurGate = fullChordRecurGate(current, fullChord, columnDelta);

            // A band rather than a threshold: the texture has to be thick enough to drop off and thin enough not
            // to be chordstream. See https://www.desmos.com/calculator/edme0ehdom
            double localSize = ChordUtils.LocalChordSize(current, 4);
            double sizeDampen = DiffUtils.Smoothstep(localSize, 1.90, 2.25) * DiffUtils.Smoothstep(localSize, 2.9, 2.5);

            // The repeats that are already hard do not need the whole buff on top, once the texture is dense enough that the
            // strain is coming from the section rather than this one drop.
            double strainDampen = 1.0 - 0.9 * DiffUtils.Smoothstep(baseStrain, 12, 15) * DiffUtils.Smoothstep(localSize, 1.6, 2.0);

            // A section that jacks freely between its narrow rows was always going to jack here too.
            double looseDampen = 1.0 - 0.11 * DiffUtils.Smoothstep(chordlessJackTexture(current, fullChord), 0.45, 0.05);

            return 1.0 + 2.5 * speedGate * manipGate * runGate * recurGate * sizeDampen * strainDampen * looseDampen;
        }

        /// <summary>
        /// The share of the row steps around <paramref name="current"/> that repeat a column, counting only the
        /// steps between two rows narrower than <paramref name="fullChord"/>. Counted in rows rather than
        /// milliseconds so it reads the same at every rate.
        /// </summary>
        private static double chordlessJackTexture(ManiaDifficultyHitObject current, int fullChord)
        {
            int jackSteps = 0;
            int steps = 0;

            foreach (var row in current.Row.RowsAround(8))
            {
                if (row.Previous() is not ManiaRow previous || row.Size >= fullChord || previous.Size >= fullChord)
                    continue;

                steps++;

                if (ColumnPatternUtils.SharesColumn(row.Columns, previous.Columns))
                    jackSteps++;
            }

            return steps > 0 ? (double)jackSteps / steps : 0.0;
        }

        /// <summary>
        /// Fades the buff out when the full chord keeps coming back, since a chord that recurs is a jumptrill to
        /// bounce off rather than a single wall to drop away from.
        /// </summary>
        private static double fullChordRecurGate(ManiaDifficultyHitObject current, int fullChord, double columnDelta)
        {
            double window = 4.0 * columnDelta;
            int fullChords = 0;

            for (int i = 0; i < scan_limit; i++)
            {
                var previous = (ManiaDifficultyHitObject?)current.Previous(i);

                if (previous == null || current.StartTime - previous.StartTime > window)
                    break;

                // Count each chord once, from its first note, so a wide chord is not counted once per column.
                if (ChordUtils.DepthInChord(previous) == 1 && previous.Row.Size >= fullChord)
                    fullChords++;
            }

            return DiffUtils.Smoothstep(fullChords, 2, 1);
        }
    }
}
