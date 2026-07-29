// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ColumnRunUtils
    {
        public const int DEFAULT_SCAN_LIMIT = 32;

        /// <summary>
        /// How many notes <paramref name="current"/>'s column plays in an unbroken run of gaps no longer than
        /// <paramref name="windowMs"/>, walking outwards from <paramref name="current"/> in both directions.
        /// </summary>
        public static int RunLengthAround(ManiaDifficultyHitObject current, double windowMs, int scanLimit = DEFAULT_SCAN_LIMIT)
        {
            int run = 1;

            var note = current;

            for (int back = 0; back < scanLimit; back++)
            {
                var previous = current.PrevInColumn(back);

                if (previous == null || note.StartTime - previous.StartTime > windowMs)
                    break;

                run++;
                note = previous;
            }

            note = current;

            for (int forward = 0; forward < scanLimit; forward++)
            {
                var next = current.NextInColumn(forward);

                if (next == null || next.StartTime - note.StartTime > windowMs)
                    break;

                run++;
                note = next;
            }

            return run;
        }
    }
}
