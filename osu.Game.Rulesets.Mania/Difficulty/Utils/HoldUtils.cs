// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class HoldUtils
    {
        private const double long_note_gate_ms = 110.90;
        private const double long_note_gate_slope = 0.07;

        /// <summary>
        /// How much of a hold note this object really is. A hold barely longer than a tap ends in the same
        /// motion that started it, so short durations fade out rather than switching off at a hard length.
        /// </summary>
        public static double HoldGateOf(double duration) => DiffUtils.Logistic(duration, long_note_gate_ms, long_note_gate_slope);
    }
}
