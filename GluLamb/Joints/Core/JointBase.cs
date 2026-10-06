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

        /// <summary>
        /// Inverts the joint's automatic choice of which beam is on top (or which side the
        /// joint is cut from). The automatic choice puts the beam whose centreline is higher
        /// on top; if the centrelines are within tolerance of each other, the first part is on top.
        /// </summary>
        [JointParameter(Description = "Invert the automatic choice of which beam is on top.")]
        public bool Flip { get; set; } = false;

        [JointParameter(Description = "Extra length added to the beam extensions this joint reports.", Unit = "length")]
        public double ExtensionTolerance { get; set; } = 10.0;

        /// <summary>
        /// Decide which of two parts is on top along an up direction: 0 or 1. The higher origin
        /// is on top; if the offset is within tolerance, part a. Flip inverts the result.
        /// </summary>
        protected int TopPart(Point3d originA, Point3d originB, Vector3d up, double tolerance)
        {
            var offset = (originB - originA) * up;
            int top = Math.Abs(offset) > tolerance && offset > 0 ? 1 : 0;
            return Flip ? 1 - top : top;
        }

        /// <summary>
        /// The segment of the line through point along direction that spans all the given
        /// beam sections, each given by its centre and its size along direction. Used for the
        /// actual length of dowels, as opposed to their (longer) drilling cutters.
        /// </summary>
        protected static Line SpanThrough(Point3d point, Vector3d direction, IEnumerable<(Point3d Centre, double Size)> sections)
        {
            direction.Unitize();
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var (centre, size) in sections)
            {
                var d = (centre - point) * direction;
                lo = Math.Min(lo, d - size * 0.5);
                hi = Math.Max(hi, d + size * 0.5);
            }
            return lo > hi ? Line.Unset : new Line(point + direction * lo, point + direction * hi);
        }

        /// <summary>
        /// Report how far the beam of a part at a beam end needs extending so that its end
        /// reaches past all the given points, plus ExtensionTolerance. Does nothing for parts
        /// in the middle of a beam.
        /// </summary>
        protected void ExtendToReach(JointResult result, Beam beam, JointPartX part, IEnumerable<Point3d> points)
        {
            if (!JointPartX.IsAtEnd(part.Case)) return;

            bool atStart = JointPartX.End0(part.Case);
            var curve = beam.Centreline;
            var end = atStart ? curve.PointAtStart : curve.PointAtEnd;
            var outward = atStart ? -curve.TangentAtStart : curve.TangentAtEnd;

            double reach = double.MinValue;
            foreach (var pt in points)
                reach = Math.Max(reach, (pt - end) * outward);

            if (reach == double.MinValue) return;
            result.Extend(beam.Id, atStart, reach + ExtensionTolerance);
        }

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
