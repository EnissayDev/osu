// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class RunDampenUtils
    {
        public static int CapFor(double runRamp) => (int)Math.Ceiling(Math.Max(1.0, runRamp)) + 1;

        public static double Dampen(int run, double runRamp, double ceiling)
            => 1.0 - ceiling * DiffUtils.ReverseLerp(run - 1, 0.0, Math.Max(1.0, runRamp));
    }
}
