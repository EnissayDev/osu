// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    /// <summary>
    /// An unbroken run of rows that a pattern detector walked through, from <see cref="First"/> to
    /// <see cref="Last"/>.
    /// </summary>
    public readonly struct ManiaChain
    {
        public readonly ManiaRow First;
        public readonly ManiaRow Last;

        /// <summary>
        /// How long the chain is. Each step counts for how cleanly it carried on from the one before it, not as a
        /// whole row, so a chain that limps to its end comes out shorter than the number of rows it covers.
        /// </summary>
        public readonly double Length;

        public ManiaChain(ManiaRow first, ManiaRow last, double length)
        {
            First = first;
            Last = last;
            Length = length;
        }

        /// <summary>
        /// How many rows the chain spans, whether or not they all continued it cleanly.
        /// </summary>
        public int RowCount => Last.RowIndex - First.RowIndex + 1;

        public int NoteCount
        {
            get
            {
                int notes = 0;

                foreach (var row in Rows)
                    notes += row.Size;

                return notes;
            }
        }

        public RowRange Rows => First.RowsUpTo(Last);

        public RowRange RowsAfterFirst => new RowRange(First.Next(), Last.RowIndex);

        /// <summary>
        /// The chain covering both this one and <paramref name="other"/>, for joining a walk backwards out of a
        /// row to a walk forwards out of the same row.
        /// </summary>
        public ManiaChain JoinedWith(ManiaChain other)
            => new ManiaChain(
                First.RowIndex <= other.First.RowIndex ? First : other.First,
                Last.RowIndex >= other.Last.RowIndex ? Last : other.Last,
                Length + other.Length);
    }
}
