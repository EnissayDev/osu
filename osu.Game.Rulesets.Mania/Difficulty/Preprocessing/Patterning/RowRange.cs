// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    /// <summary>
    /// A run of consecutive <see cref="ManiaRow"/>s, walked by following each row to the next rather than by
    /// indexing. Kept as a struct with its own enumerator because the pattern detectors walk these per note.
    /// </summary>
    public readonly struct RowRange
    {
        private readonly ManiaRow? first;
        private readonly int lastIndex;

        public RowRange(ManiaRow? first, int lastIndex)
        {
            this.first = first;
            this.lastIndex = lastIndex;
        }

        public Enumerator GetEnumerator() => new Enumerator(first, lastIndex);

        public struct Enumerator
        {
            private readonly int lastIndex;
            private ManiaRow? upcoming;

            public Enumerator(ManiaRow? first, int lastIndex)
            {
                this.lastIndex = lastIndex;
                upcoming = first;
                Current = null!;
            }

            public ManiaRow Current { get; private set; }

            public bool MoveNext()
            {
                if (upcoming == null || upcoming.RowIndex > lastIndex)
                    return false;

                Current = upcoming;
                upcoming = upcoming.Next();

                return true;
            }
        }
    }
}
