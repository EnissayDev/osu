// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceAttributes : PerformanceAttributes
    {
        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }
        [JsonProperty("ss_value")]
        public double ValueSS { get; set; }

        [JsonProperty("99_value")]
        public double Value99 { get; set; }

        [JsonProperty("98_value")]
        public double Value98 { get; set; }

        [JsonProperty("97_value")]
        public double Value97 { get; set; }

        [JsonProperty("96_value")]
        public double Value96 { get; set; }

        [JsonProperty("95_value")]
        public double Value95 { get; set; }

        [JsonProperty("99_scale")]
        public double Scale99 { get; set; }

        [JsonProperty("98_scale")]
        public double Scale98 { get; set; }

        [JsonProperty("97_scale")]
        public double Scale97 { get; set; }

        [JsonProperty("96_scale")]
        public double Scale96 { get; set; }

        [JsonProperty("95_scale")]
        public double Scale95 { get; set; }

        public override IEnumerable<PerformanceDisplayAttribute> GetAttributesForDisplay()
        {
            foreach (var attribute in base.GetAttributesForDisplay())
                yield return attribute;

            yield return new PerformanceDisplayAttribute(nameof(Difficulty), "Difficulty", Difficulty);
        }
    }
}
