using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using Feature = GluLamb.Features.Feature;

namespace GluLamb.Joints
{
    /// <summary>
    /// Topology of a joint condition, matching the letters returned by JointX.ClassifyJoint.
    /// </summary>
    public enum JointTopology
    {
        Unknown,
        /// <summary>One part, at the end of a beam (E).</summary>
        End,
        /// <summary>One part, along a beam (F).</summary>
        Feature,
        /// <summary>Two ends, roughly aligned (S).</summary>
        Splice,
        /// <summary>Two ends, at an angle (L).</summary>
        Corner,
        /// <summary>Two ends, folded back on each other (V).</summary>
        Acute,
        /// <summary>One end meeting the middle of another beam (T).</summary>
        T,
        /// <summary>Two beams crossing in their middles (X).</summary>
        Cross,
        /// <summary>Three or more parts.</summary>
        Node
    }

    /// <summary>
    /// A joint between one or more beams. Joints refer to beams only by identifier and
    /// produce features; they never modify beams or other joints.
    /// </summary>
    public interface IJoint
    {
        /// <summary>
        /// Stable identifier of the joint type, from its JointTypeAttribute.
        /// </summary>
        string TypeId { get; }

        IReadOnlyList<JointPartX> Parts { get; }

        Plane Position { get; }

        /// <summary>
        /// Build the joint. Implementations should report geometric failure through the
        /// result rather than by throwing.
        /// </summary>
        JointResult Construct(IJointContext context);
    }

    public enum JointStatus
    {
        Ok,
        /// <summary>Some features were created, but not all.</summary>
        Partial,
        /// <summary>The joint chose not to build anything for this condition.</summary>
        Skipped,
        Failed
    }

    /// <summary>
    /// Output of IJoint.Construct: status, messages, and the features per beam.
    /// </summary>
    public class JointResult
    {
        public JointStatus Status = JointStatus.Ok;
        public List<string> Messages = new List<string>();

        /// <summary>
        /// Features keyed by Beam.Id.
        /// </summary>
        public Dictionary<string, List<Feature>> Features = new Dictionary<string, List<Feature>>();

        /// <summary>
        /// Debug objects, fresh for each Construct call.
        /// </summary>
        public List<object> Debug = new List<object>();

        public bool Success => Status == JointStatus.Ok || Status == JointStatus.Partial;

        public void Add(Feature feature)
        {
            if (feature == null) return;
            if (string.IsNullOrEmpty(feature.BeamId))
                throw new ArgumentException("Feature has no BeamId.");

            if (!Features.TryGetValue(feature.BeamId, out var list))
            {
                list = new List<Feature>();
                Features[feature.BeamId] = list;
            }
            list.Add(feature);
        }

        public IEnumerable<Feature> AllFeatures => Features.Values.SelectMany(x => x);

        public static JointResult Fail(string message)
        {
            var result = new JointResult { Status = JointStatus.Failed };
            result.Messages.Add(message);
            return result;
        }

        public static JointResult Skip(string message)
        {
            var result = new JointResult { Status = JointStatus.Skipped };
            result.Messages.Add(message);
            return result;
        }
    }
}
