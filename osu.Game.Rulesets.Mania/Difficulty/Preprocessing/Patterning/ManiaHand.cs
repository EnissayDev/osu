// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    /// <summary>
    /// Which hand a <see cref="ManiaRow"/> falls under, taking the columns to be split evenly down the middle.
    /// </summary>
    public enum ManiaHand
    {
        Left,
        Right,
        Both,
    }
}
