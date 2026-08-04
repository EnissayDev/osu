// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class RunDampenUtils
    {
        /// <summary>
        /// How far a run has to be counted before the nerf stops deepening. Anything past this is wasted work.
        /// </summary>
        public static int CapFor(double runRamp) => (int)Math.Ceiling(Math.Max(1.0, runRamp)) + 1;

        /// <summary>
        /// How much of the difficulty a run keeps, bottoming out at one minus the ceiling.
        /// </summary>
        public static double Dampen(int run, double runRamp, double ceiling)
            => 1.0 - ceiling * DiffUtils.ReverseLerp(run - 1, 0.0, Math.Max(1.0, runRamp));
    }
}
