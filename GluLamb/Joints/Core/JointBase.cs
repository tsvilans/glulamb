using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    /// <summary>
    /// Marks a class as a joint type that the JointRegistry can discover.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class JointTypeAttribute : Attribute
    {
        /// <summary>
        /// Stable identifier, used for lookup and serialisation (e.g. "glulamb.t-lap").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name. Defaults to the class name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number of parts the joint handles. 0 means any.
        /// </summary>
        public int Arity { get; set; }

        public JointTopology Topology { get; set; } = JointTopology.Unknown;

        public int Version { get; set; } = 1;

        public string Description { get; set; } = "";

        public JointTypeAttribute(string id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Marks a public, writable property as a joint parameter that can be listed, set and
    /// serialised generically. The property initializer is the default value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class JointParameterAttribute : Attribute
    {
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "";
    }

    /// <summary>
    /// Convenience base for joints. Copies the parts of the joint condition (so the condition
    /// is never mutated), resolves beams through the context, and turns exceptions thrown
    /// by ConstructCore into a failed result.
    /// </summary>
    public abstract class JointBase : IJoint
    {
        protected List<JointPartX> m_parts;

        public IReadOnlyList<JointPartX> Parts => m_parts;
        public Plane Position { get; protected set; }

        public string TypeId => GetType().GetCustomAttribute<JointTypeAttribute>()?.Id ?? GetType().FullName;

        protected JointBase(JointX condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            var info = GetType().GetCustomAttribute<JointTypeAttribute>();
            if (info != null && info.Arity > 0 && condition.Parts.Count != info.Arity)
                throw new ArgumentException($"{GetType().Name} requires {info.Arity} parts, got {condition.Parts.Count}.");

            m_parts = condition.Parts.Select(x => x.DuplicateJointPart()).ToList();
            Position = condition.Position;
        }

        public JointResult Construct(IJointContext context)
        {
            var beams = new Beam[m_parts.Count];
            for (int i = 0; i < m_parts.Count; ++i)
            {
                beams[i] = context.GetBeam(m_parts[i]);
                if (beams[i] == null)
                    return JointResult.Fail($"{GetType().Name}: no beam found for part {i} " +
                        $"(id '{m_parts[i].BeamId}', index {m_parts[i].ElementIndex}).");

                m_parts[i].BeamId = beams[i].Id;
            }

            var result = new JointResult();
            try
            {
                ConstructCore(beams, context, result);
            }
            catch (Exception e)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: {e.Message}");
            }
            return result;
        }

        /// <summary>
        /// Build the joint's features into result. beams[i] belongs to Parts[i].
        /// </summary>
        protected abstract void ConstructCore(Beam[] beams, IJointContext context, JointResult result);

        public Dictionary<string, object> GetParameters() => JointParameters.Get(this);

        /// <summary>
        /// Set parameters by name. Returns the names that don't match a parameter.
        /// </summary>
        public List<string> SetParameters(IDictionary<string, object> values) => JointParameters.Set(this, values);

        public override string ToString() => $"{GetType().Name} ({TypeId})";
    }

    /// <summary>
    /// Reflection helpers for properties marked with JointParameterAttribute.
    /// </summary>
    public static class JointParameters
    {
        public static IEnumerable<PropertyInfo> GetProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<JointParameterAttribute>() != null);

        public static Dictionary<string, object> Get(object joint) =>
            GetProperties(joint.GetType()).ToDictionary(p => p.Name, p => p.GetValue(joint));

        /// <summary>
        /// Set parameters by name (case-insensitive). Values are converted to the property
        /// type, so numbers can set bools and ints, e.g. from Grasshopper. Returns the names
        /// that don't match a parameter.
        /// </summary>
        public static List<string> Set(object joint, IDictionary<string, object> values)
        {
            var unknown = new List<string>();
            if (values == null) return unknown;

            var props = GetProperties(joint.GetType()).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in values)
            {
                if (!props.TryGetValue(kvp.Key, out var prop))
                {
                    unknown.Add(kvp.Key);
                    continue;
                }

                var value = kvp.Value;
                if (value == null) continue;

                var target = prop.PropertyType;
                if (!target.IsInstanceOfType(value))
                {
                    if (target == typeof(bool))
                        value = value is string s ? bool.Parse(s) : Convert.ToDouble(value) != 0.0;
                    else
                        value = Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
                }

                prop.SetValue(joint, value);
            }

            return unknown;
        }

        /// <summary>
        /// Parse "Name=Value" strings (also "Name:Value") into a parameter dictionary.
        /// Numbers are parsed with the invariant culture; anything else is kept as a string.
        /// </summary>
        public static Dictionary<string, object> Parse(IEnumerable<string> entries)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return values;

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var split = entry.IndexOfAny(new[] { '=', ':' });
                if (split < 1) continue;

                var name = entry.Substring(0, split).Trim();
                var text = entry.Substring(split + 1).Trim();

                if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number))
                    values[name] = number;
                else
                    values[name] = text;
            }

            return values;
        }
    }
}
