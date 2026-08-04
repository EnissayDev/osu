// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning.Detectors
{
    /// <summary>
    /// Finds a pattern that can be played with an easier motion than the one it was written as, and says how much
    /// difficulty is left once the player takes that shortcut.
    /// </summary>
    /// <remarks>
    /// Every detector works the same way. Walk outwards from a row for as long as the pattern continues, then
    /// turn how far it got into a discount through <see cref="PlateauOf"/>.
    /// </remarks>
    public abstract class ManipulationDetector
    {
        /// <summary>
        /// The row gap at which this pattern first becomes playable by the easier motion. Slower than this and
        /// there is time to play it as written, so nothing is discounted at all.
        /// </summary>
        protected abstract double Onset { get; }

        /// <summary>
        /// Whether the chain should carry on from the row it currently ends at onto the next one.
        /// </summary>
        /// <param name="cameFrom">The row the walk was at one step earlier, or null if it has only taken one step so far.
        /// Only needed by patterns that decide using the step before this one.</param>
        /// <param name="from">The row the chain currently ends at.</param>
        /// <param name="to">The row being considered next. Note that this is the <em>earlier</em> row when walking backwards.</param>
        protected virtual bool ContinuesChain(ManiaRow? cameFrom, ManiaRow from, ManiaRow to) => true;

        protected ManiaChain ChainBefore(ManiaRow seed, double lengthCap = double.PositiveInfinity, int rowCap = int.MaxValue)
            => walk(seed, backwards: true, lengthCap, rowCap);

        protected ManiaChain ChainAfter(ManiaRow seed, double lengthCap = double.PositiveInfinity, int rowCap = int.MaxValue)
            => walk(seed, backwards: false, lengthCap, rowCap);

        /// <summary>
        /// Turns how strongly a pattern was detected into what is left of the row's difficulty.
        /// </summary>
        /// <remarks>
        /// The discount is an exponent on how far below the onset the row sits, not a flat multiplier, and that
        /// is what makes it a plateau: a pattern that can be manipulated stops getting harder as its gaps shrink,
        /// but it never comes out easier than the same pattern written with wider gaps.
        /// See https://www.desmos.com/calculator/s1lkzakgpy
        /// </remarks>
        protected double PlateauOf(ManiaRow row, double strength)
        {
            const double offset_ms = 80.0;

            double gap = row.GapBefore;

            if (strength <= 0.0 || gap >= Onset)
                return 1.0;

            double ratio = (gap + offset_ms) / (Onset + offset_ms);

            return Math.Pow(ratio, Math.Min(strength, 1.0));
        }

        /// <summary>
        /// Walks out of <paramref name="seed"/> for as long as the pattern continues, and returns how far it got.
        /// </summary>
        private ManiaChain walk(ManiaRow seed, bool backwards, double lengthCap, int rowCap)
        {
            double pulse = seed.LocalPulse;
            double length = 0.0;
            int steps = 0;

            // A step never counts for more than the loosest step already walked through.
            double weakestStep = 1.0;

            ManiaRow? cameFrom = null;
            ManiaRow end = seed;

            while (length < lengthCap && steps < rowCap)
            {
                ManiaRow? next = backwards ? end.Previous() : end.Next();

                if (next == null || !ContinuesChain(cameFrom, end, next))
                    break;

                double step = stepContinuity(Math.Abs(next.StartTime - end.StartTime), pulse);

                if (step <= 0.0)
                    break;

                weakestStep = Math.Min(weakestStep, step);
                length += weakestStep;
                steps++;
                cameFrom = end;
                end = next;
            }

            return backwards ? new ManiaChain(end, seed, length) : new ManiaChain(seed, end, length);
        }

        /// <summary>
        /// How much a step counts for, based on how far its gap strays from the pulse of the surrounding section.
        /// A gap of twice the pulse is a rest that breaks the pattern, while anything near the pulse is the
        /// pattern carrying on.
        /// </summary>
        private static double stepContinuity(double gap, double pulse)
        {
            if (!(pulse > 0.0) || double.IsInfinity(pulse))
                return 0.0;

            return DiffUtils.Smoothstep(gap / pulse, 2.2, 1.5);
        }
    }
}
