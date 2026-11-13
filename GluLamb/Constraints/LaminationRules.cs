/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using GluLamb.Standards;

namespace GluLamb.Constraints
{
    /// <summary>
    /// Names of the quantities that rules limit.
    /// </summary>
    public static class Quantities
    {
        /// <summary>Smallest bending radius over lamella thickness (Min).</summary>
        public const string RadiusRatio = "lamella.radius-ratio";

        /// <summary>Largest lamella thickness, in millimetres (Max).</summary>
        public const string MaxThickness = "lamella.max-thickness";

        /// <summary>Largest strain from twist (Max). Without a rule, the bending strain applies.</summary>
        public const string TwistStrain = "lamella.twist-strain";
    }

    /// <summary>
    /// Names of the context keys that the lamination rules use.
    /// </summary>
    public static class ContextKeys
    {
        /// <summary>Lamella thickness, in millimetres.</summary>
        public const string Thickness = "thickness";

        /// <summary>Species, e.g. "southern-pine".</summary>
        public const string Species = "species";

        /// <summary>Species group: "softwood" or "hardwood".</summary>
        public const string SpeciesGroup = "species-group";

        /// <summary>Process step: "bend" (boards into a curved glulam) or "press" (lamellas into a free-form blank).</summary>
        public const string Step = "step";
    }

    /// <summary>
    /// Rule sets for the lamination standards, and the questions that lamination asks them.
    /// The values are the ones the Standards classes use. They are not checked again against
    /// the standards.
    /// </summary>
    public static class LaminationRules
    {
        /// <summary>No standard: radius over thickness is Glulam.RadiusMultiplier.</summary>
        public static RuleSet None()
        {
            var set = new RuleSet { Name = "None", Description = "No standard. Radius ratio from Glulam.RadiusMultiplier." };
            set.Add("ratio", Quantities.RadiusRatio, Glulam.RadiusMultiplier, LimitKind.Min, "Glulam.RadiusMultiplier");
            return set;
        }

        public static RuleSet Eurocode()
        {
            var set = new RuleSet { Name = "Eurocode", Description = "EN 14080:2013, as in GluLamb.Standards.Eurocode." };
            set.Add("ratio", Quantities.RadiusRatio, 200, LimitKind.Min, "EN 14080:2013 Annex I.5.1");
            set.Add("max-thickness", Quantities.MaxThickness, 80, LimitKind.Max, "GluLamb.Standards.Eurocode");
            return set;
        }

        /// <summary>
        /// ANSI 117-2020, section 3.8. Nominal 2 inch (38 mm) and 1 inch (19 mm) lamellas have
        /// fixed smallest radii. Thinner lamellas have a ratio. Southern pine bends tighter than
        /// other softwoods.
        /// </summary>
        public static RuleSet Ansi()
        {
            const string source = "ANSI 117-2020, 3.8";
            const string pine = "southern-pine";
            const double ThinBelow = 18.99; // thinner than nominal 1 in
            var set = new RuleSet { Name = "APA", Description = "ANSI 117-2020 section 3.8, as in GluLamb.Standards.ANSI." };
            set.Add("max-thickness", Quantities.MaxThickness, 38, LimitKind.Max, source);

            set.Add("2in", Quantities.RadiusRatio, 8400.0 / 38, LimitKind.Min, source + ": 27 ft 6 in for nominal 2 in").IfRange(ContextKeys.Thickness, 38, 38);
            set.Add("1in", Quantities.RadiusRatio, 2800.0 / 19, LimitKind.Min, source + ": 9 ft 4 in for nominal 1 in").IfRange(ContextKeys.Thickness, 19, 19);
            set.Add("thin", Quantities.RadiusRatio, 125, LimitKind.Min, source + ": 125 x thickness").IfRange(ContextKeys.Thickness, 0, ThinBelow);

            set.Add("2in-southern-pine", Quantities.RadiusRatio, 5500.0 / 38, LimitKind.Min, source + ": 18 ft for nominal 2 in").IfRange(ContextKeys.Thickness, 38, 38).If(ContextKeys.Species, pine);
            set.Add("1in-southern-pine", Quantities.RadiusRatio, 2100.0 / 19, LimitKind.Min, source + ": 7 ft for nominal 1 in").IfRange(ContextKeys.Thickness, 19, 19).If(ContextKeys.Species, pine);
            set.Add("thin-southern-pine", Quantities.RadiusRatio, 100, LimitKind.Min, source + ": 100 x thickness").IfRange(ContextKeys.Thickness, 0, ThinBelow).If(ContextKeys.Species, pine);
            return set;
        }

        public static RuleSet For(Standard standard)
        {
            switch (standard)
            {
                case Standard.Eurocode: return Eurocode();
                case Standard.APA: return Ansi();
                default: return None();
            }
        }

        /// <summary>
        /// The thickest lamella that can bend to a radius: the largest thickness whose radius
        /// ratio the radius satisfies, not more than the largest thickness. Lengths in model units.
        /// </summary>
        public static Decision ThickestLamella(this RuleSet rules, double radius, Context context)
        {
            double unit = Globals.FromMillimeters(1);
            var cap = rules.Find(Quantities.MaxThickness, context);
            double radiusMm = radius / unit;

            var decision = rules.LargestWithin(Quantities.RadiusRatio, ContextKeys.Thickness, radiusMm, context);
            if (!decision.Found)
            {
                // No ratio rule: only the cap limits the thickness
                if (cap.Found) cap.Value *= unit;
                return cap;
            }
            if (cap.Found && cap.Value < decision.Value) decision = new Decision { Quantity = cap.Quantity, Value = cap.Value, Rule = cap.Rule };
            decision.Value *= unit;
            return decision;
        }

        /// <summary>
        /// The radius ratio for a lamella of a thickness (model units), with the rule that gives it.
        /// </summary>
        public static Decision RadiusRatio(this RuleSet rules, double thickness, Context context)
        {
            var c = context.Duplicate().With(ContextKeys.Thickness, thickness / Globals.FromMillimeters(1));
            var decision = rules.Find(Quantities.RadiusRatio, c);
            if (decision.Found) return decision;

            // The thickness is between the standard's sizes: the rule for the next size up applies
            Rule next = null;
            double t = thickness / Globals.FromMillimeters(1);
            foreach (var rule in rules.Rules.Where(r => r.Quantity == Quantities.RadiusRatio && r.MatchesExcept(c, ContextKeys.Thickness)))
            {
                double min = rule.ConditionOn(ContextKeys.Thickness)?.Min ?? 0;
                if (min < t) continue;
                double nextMin = next?.ConditionOn(ContextKeys.Thickness)?.Min ?? double.MaxValue;
                if (next == null || min < nextMin || (min == nextMin && rule.Specificity >= next.Specificity)) next = rule;
            }
            return next == null ? decision : new Decision { Quantity = Quantities.RadiusRatio, Value = next.Value, Rule = next };
        }

        /// <summary>
        /// The bending strain at the radius ratio limit, 1 / (2 x ratio): the strain a lamella of
        /// this thickness may take.
        /// </summary>
        public static double AllowedStrain(this RuleSet rules, double thickness, Context context, out Rule rule)
        {
            var ratio = rules.RadiusRatio(thickness, context);
            rule = ratio.Rule;
            return ratio.Found ? 1 / (2 * ratio.Value) : double.PositiveInfinity;
        }

        /// <summary>
        /// The strain a lamella may take from twist: the rule for Quantities.TwistStrain, or
        /// without one the allowed bending strain.
        /// </summary>
        public static double AllowedTwistStrain(this RuleSet rules, double thickness, Context context, out Rule rule)
        {
            var c = context.Duplicate().With(ContextKeys.Thickness, thickness / Globals.FromMillimeters(1));
            var twist = rules.Find(Quantities.TwistStrain, c);
            if (twist.Found) { rule = twist.Rule; return twist.Value; }
            return rules.AllowedStrain(thickness, context, out rule);
        }
    }
}
