// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds one column being struck over and over fast enough to be buzzed with two fingers rather than jacked
    /// with one.
    /// </summary>
    /// <remarks>
    /// The only detector whose pattern lives in a column rather than in a row, so it is asked about one column at
    /// a time and its result applies to that note alone.
    /// </remarks>
    public class VibroDetector : ManipulationDetector
    {
        private const int run_cap = 64;

        /// <summary>
        /// The column the chain currently being walked is following.
        /// </summary>
        private int chainColumn;

        protected override double Onset => 190;

        protected override bool ContinuesChain(ManiaRow? cameFrom, ManiaRow from, ManiaRow to) => to.Contains(chainColumn);

        /// <summary>
        /// How much difficulty the note in <paramref name="column"/> of <paramref name="row"/> keeps once vibro
        /// is taken into account. 1 means it was left alone.
        /// </summary>
        public double EvaluateFactorOf(ManiaRow row, int column)
        {
            if (row.GapBefore >= Onset)
                return 1.0;

            chainColumn = column;

            return PlateauOf(row, strengthOf(row));
        }

        private double strengthOf(ManiaRow row)
        {
            ManiaChain back = ChainBefore(row, rowCap: run_cap - 1);
            int remainingRows = run_cap - back.RowCount;

            ManiaChain chain = back.JoinedWith(ChainAfter(row, rowCap: remainingRows));

            double runWeight = DiffUtils.Smoothstep(chain.RowCount, 2.5, 5.0);

            // Putting two fingers on one column is only free while the other columns are quiet enough to spare them.
            double rowSizeGate = DiffUtils.Smoothstep((double)chain.NoteCount / chain.RowCount, 2.6, 1.6);

            return runWeight * rowSizeGate;
        }
    }
}
