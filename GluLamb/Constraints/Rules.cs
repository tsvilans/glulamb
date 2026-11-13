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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GluLamb.Constraints
{
    /// <summary>
    /// The facts a rule is tested against: text values (species, step) and numbers (thickness).
    /// Keys and text values ignore case.
    /// </summary>
    public class Context
    {
        readonly Dictionary<string, string> m_text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, double> m_numbers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public Context With(string key, string value) { m_text[key] = value; m_numbers.Remove(key); return this; }
        public Context With(string key, double value) { m_numbers[key] = value; m_text.Remove(key); return this; }

        public bool TryGetText(string key, out string value) => m_text.TryGetValue(key, out value);
        public bool TryGetNumber(string key, out double value) => m_numbers.TryGetValue(key, out value);

        public Context Duplicate()
        {
            var c = new Context();
            foreach (var kv in m_text) c.m_text[kv.Key] = kv.Value;
            foreach (var kv in m_numbers) c.m_numbers[kv.Key] = kv.Value;
            return c;
        }

        /// <summary>
        /// Reads "key=value" items. A value that is a number becomes a number.
        /// </summary>
        public static Context Parse(IEnumerable<string> items)
        {
            var c = new Context();
            foreach (var item in items ?? Array.Empty<string>())
            {
                int i = item?.IndexOf('=') ?? -1;
                if (i < 1) continue;
                string key = item.Substring(0, i).Trim(), value = item.Substring(i + 1).Trim();
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) c.With(key, number);
                else c.With(key, value);
            }
            return c;
        }

        public override string ToString() => string.Join(", ",
            m_text.Select(kv => $"{kv.Key}={kv.Value}").Concat(m_numbers.Select(kv => $"{kv.Key}={kv.Value.ToString("0.###", CultureInfo.InvariantCulture)}")));
    }

    /// <summary>
    /// One test on the context. Text: the key has this value. Number: the key is in [Min, Max].
    /// A key that the context does not have fails the test.
    /// </summary>
    public class Condition
    {
        public string Key { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Is { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Min { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Max { get; set; }

        [JsonIgnore]
        public bool IsRange => Is == null;

        public bool Matches(Context context)
        {
            if (!IsRange)
                return context.TryGetText(Key, out string text) && string.Equals(text, Is, StringComparison.OrdinalIgnoreCase);
            return context.TryGetNumber(Key, out double number) && number >= (Min ?? double.MinValue) && number <= (Max ?? double.MaxValue);
        }

        public override string ToString() => IsRange
            ? $"{Key} in [{Min?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-inf"}, {Max?.ToString("0.###", CultureInfo.InvariantCulture) ?? "inf"}]"
            : $"{Key} is {Is}";
    }

    public enum LimitKind
    {
        /// <summary>The actual value must not be more than the limit.</summary>
        Max,

        /// <summary>The actual value must not be less than the limit.</summary>
        Min
    }

    /// <summary>
    /// A limit on one quantity, valid when all its conditions match.
    /// </summary>
    public class Rule
    {
        /// <summary>Name of the rule, for reports.</summary>
        public string Id { get; set; }

        /// <summary>The quantity the rule limits. See Quantities.</summary>
        public string Quantity { get; set; }

        public double Value { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LimitKind Kind { get; set; } = LimitKind.Max;

        /// <summary>Where the rule comes from: standard and clause, or the production reason.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Source { get; set; }

        public List<Condition> When { get; set; } = new List<Condition>();

        public bool Matches(Context context) => When.All(c => c.Matches(context));

        /// <summary>Matches, with the conditions on one key left out.</summary>
        public bool MatchesExcept(Context context, string key) =>
            When.Where(c => !string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).All(c => c.Matches(context));

        public Condition ConditionOn(string key) => When.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>More conditions make a rule more specific.</summary>
        [JsonIgnore]
        public int Specificity => When.Count;

        public Rule If(string key, string value) { When.Add(new Condition { Key = key, Is = value }); return this; }
        public Rule IfRange(string key, double? min, double? max) { When.Add(new Condition { Key = key, Min = min, Max = max }); return this; }

        public override string ToString()
        {
            string when = When.Count > 0 ? " when " + string.Join(" and ", When) : "";
            string source = string.IsNullOrEmpty(Source) ? "" : $" ({Source})";
            return $"{Id}: {Quantity} {(Kind == LimitKind.Max ? "<=" : ">=")} {Value.ToString("0.#####", CultureInfo.InvariantCulture)}{when}{source}";
        }
    }

    /// <summary>
    /// The answer to a query: the value and the rule that gave it.
    /// </summary>
    public class Decision
    {
        public string Quantity;
        public double Value = double.NaN;
        public Rule Rule;
        public bool Found => Rule != null;

        public override string ToString() => Found ? Rule.ToString() : $"No rule for {Quantity}.";
    }

    /// <summary>
    /// An actual value tested against a rule.
    /// </summary>
    public class Check
    {
        public string Quantity;
        public double Actual, Limit = double.NaN;
        public Rule Rule;

        /// <summary>True when there is no rule: nothing forbids the value.</summary>
        public bool Passed = true;

        /// <summary>Actual over limit (Max) or limit over actual (Min). At most 1 passes.</summary>
        public double Utilisation;

        public override string ToString() => Rule == null
            ? $"{Quantity} = {Actual:0.#####}: no rule."
            : $"{(Passed ? "OK" : "FAIL")} {Quantity} = {Actual:0.#####} ({Utilisation:0.##} of the limit). {Rule}";
    }

    /// <summary>
    /// A named list of rules (a standard, a factory, a machine). A query gives the matching rule
    /// with the most conditions. Of equals, the later rule wins. A set with no matching rule asks
    /// its parent.
    /// </summary>
    public class RuleSet
    {
        public string Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        public List<Rule> Rules { get; set; } = new List<Rule>();

        /// <summary>Asked when this set has no matching rule, e.g. general rules under a factory's.</summary>
        [JsonIgnore]
        public RuleSet Parent { get; set; }

        public RuleSet Add(Rule rule) { Rules.Add(rule); return this; }

        public Rule Add(string id, string quantity, double value, LimitKind kind = LimitKind.Max, string source = null)
        {
            var rule = new Rule { Id = id, Quantity = quantity, Value = value, Kind = kind, Source = source };
            Rules.Add(rule);
            return rule;
        }

        IEnumerable<Rule> For(string quantity) => Rules.Where(r => string.Equals(r.Quantity, quantity, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The limit on a quantity in this context.
        /// </summary>
        public Decision Find(string quantity, Context context)
        {
            Rule best = null;
            foreach (var rule in For(quantity))
                if (rule.Matches(context) && (best == null || rule.Specificity >= best.Specificity))
                    best = rule;

            if (best != null) return new Decision { Quantity = quantity, Value = best.Value, Rule = best };
            return Parent?.Find(quantity, context) ?? new Decision { Quantity = quantity };
        }

        /// <summary>
        /// Tests an actual value against the limit on a quantity in this context.
        /// </summary>
        public Check Check(string quantity, Context context, double actual)
        {
            var decision = Find(quantity, context);
            var check = new Check { Quantity = quantity, Actual = actual, Rule = decision.Rule };
            if (!decision.Found) return check;

            check.Limit = decision.Value;
            bool max = decision.Rule.Kind == LimitKind.Max;
            check.Passed = max ? actual <= decision.Value * (1 + 1e-9) : actual >= decision.Value * (1 - 1e-9);
            check.Utilisation = max ? actual / decision.Value : decision.Value / actual;
            return check;
        }

        /// <summary>
        /// The largest value of a context number (e.g. thickness) for which
        /// number x ratio is at most a given amount (e.g. radius), where the ratio is the limit
        /// on a quantity and can change with that number. Each matching rule gives a candidate
        /// inside its own range of the number. For rules with the same range, the most specific
        /// one counts.
        /// </summary>
        /// <param name="ratioQuantity">The quantity that holds the ratio, e.g. Quantities.RadiusRatio.</param>
        /// <param name="key">The context number to solve for, e.g. ContextKeys.Thickness.</param>
        /// <param name="amount">The amount the product must not exceed, e.g. the radius.</param>
        public Decision LargestWithin(string ratioQuantity, string key, double amount, Context context)
        {
            var result = new Decision { Quantity = ratioQuantity };
            var candidates = For(ratioQuantity).Where(r => r.MatchesExcept(context, key)).ToList();

            foreach (var rule in candidates)
            {
                var range = rule.ConditionOn(key);
                double min = range?.Min ?? 0, max = range?.Max ?? double.MaxValue;

                // A more specific rule for the same range replaces this one
                if (candidates.Any(o => o != rule && o.Specificity > rule.Specificity && SameRange(o.ConditionOn(key), range))) continue;

                double value = Math.Min(amount / rule.Value, max);
                if (value < min || value <= 0) continue;
                if (!result.Found || value > result.Value) { result.Value = value; result.Rule = rule; }
            }

            if (!result.Found && Parent != null) return Parent.LargestWithin(ratioQuantity, key, amount, context);
            return result;
        }

        static bool SameRange(Condition a, Condition b) =>
            (a == null && b == null) || (a != null && b != null && Nullable.Equals(a.Min, b.Min) && Nullable.Equals(a.Max, b.Max));

        static readonly JsonSerializerOptions s_json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public string ToJson() => JsonSerializer.Serialize(this, s_json);

        public static RuleSet FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json, s_json);

        public override string ToString() => $"RuleSet {Name} ({Rules.Count} rules{(Parent != null ? ", then " + Parent.Name : "")})";
    }
}
