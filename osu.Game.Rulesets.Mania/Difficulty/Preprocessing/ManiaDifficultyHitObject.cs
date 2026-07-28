// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        private readonly List<DifficultyHitObject>[] perColumnObjects;

        private readonly int columnIndex;

        public readonly int Column;

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        public readonly double ColumnDelta;

        /// <summary>
        /// The row of notes (often a chord) that contains this note.
        /// Subject to a grace period, so notes can belong to the same row even if they are slightly offset in time.
        /// </summary>
        public ManiaRow Row = null!;

        public double ManipulationFactor = 1.0;

        public double EnduranceFactor = 1.0;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnDelta = StartTime - PrevInColumn(0)?.StartTime ?? StartTime;

            if (index > 0)
            {
                ManiaDifficultyHitObject prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                for (int i = 0; i < prevNote.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevNote.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prevNote.Column] = prevNote;
            }
        }

        /// <summary>
        /// The previous object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="backwardsIndex">The number of notes to go back.</param>
        /// <returns>The object in this column <paramref name="backwardsIndex"/> notes back, or null if this is the first note in the column.</returns>
        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        /// <summary>
        /// The next object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="forwardsIndex">The number of notes to go forward.</param>
        /// <returns>The object in this column <paramref name="forwardsIndex"/> notes forward, or null if this is the last note in the column.</returns>
        public ManiaDifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        /// <summary>
        /// The start time of the most recent <see cref="ManiaDifficultyHitObject"/> in <paramref name="column"/> prior to this one.
        /// </summary>
        public double LastStartTimeInColumn(int column) => PreviousHitObjects[column]?.StartTime ?? double.NegativeInfinity;

        /// <summary>
        /// The end time of the most recent <see cref="ManiaDifficultyHitObject"/> in <paramref name="column"/> prior to this one.
        /// </summary>
        public double LastEndTimeInColumn(int column) => PreviousHitObjects[column]?.EndTime ?? double.NegativeInfinity;

        /// <summary>
        /// The number of columns, other than this object's own, that are currently held (i.e. a long note is sustaining through this object's <see cref="DifficultyHitObject.StartTime"/>).
        /// </summary>
        /// <param name="chordTolerance">The time window within which two notes are considered to start simultaneously.</param>
        public int ConcurrentlyHeldColumns(double chordTolerance)
        {
            int heldColumns = 0;

            for (int otherColumn = 0; otherColumn < PreviousHitObjects.Length; otherColumn++)
            {
                if (otherColumn == Column)
                    continue;

                if (Math.Abs(LastStartTimeInColumn(otherColumn) - StartTime) <= chordTolerance)
                    continue;

                if (LastEndTimeInColumn(otherColumn) > StartTime + chordTolerance)
                    heldColumns++;
            }

            return heldColumns;
        }
    }
}
