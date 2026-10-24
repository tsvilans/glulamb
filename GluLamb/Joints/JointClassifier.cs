using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace GluLamb.Joints
{
    /// <summary>
    /// Turns a topology (connected curve pairs, e.g. from Topology.FindCurvePairs) into joint
    /// conditions: each connection is classified as at the end or in the middle of each curve,
    /// nearby connections are merged, and the result is tidied up for the joint types.
    /// </summary>
    public static class JointClassifier
    {
        /// <summary>
        /// Joint conditions for a set of curves and their connections. Each condition's Id is its
        /// index in the returned list.
        /// </summary>
        /// <param name="curves">Curves by element index.</param>
        /// <param name="pairs">Connections between curves (element indices and curve parameters).</param>
        /// <param name="mergeDistance">Connections closer than this are merged into one condition.</param>
        /// <param name="endTolerance">A connection closer than this to a curve end is at that end.</param>
        /// <param name="perpendicularThreshold">Angle threshold between splices, corners and acute joints (see JointX.ClassifyJoint).</param>
        /// <param name="postDirection">Where three or more beams end at one joint, the one most along this direction
        /// (within 45°) is put first, as the post. A zero vector keeps the input order.</param>
        /// <param name="splitDoubleT">Replace TT conditions (two beams ending on a third from opposite sides)
        /// with two T conditions.</param>
        /// <param name="freeEnds">Also make single-beam (E) conditions at curve ends that aren't in any joint.</param>
        public static List<JointX> Classify(IDictionary<int, Curve> curves, IEnumerable<Pair> pairs,
            double mergeDistance = 50, double endTolerance = 10, double perpendicularThreshold = Math.PI * 0.25,
            Vector3d postDirection = default, bool splitDoubleT = true, bool freeEnds = false)
        {
            var joints = new List<JointX>();

            int id = 0;
            foreach (var pair in pairs)
            {
                if (!curves.TryGetValue(pair.A, out var c0) || !curves.TryGetValue(pair.B, out var c1) || c0 == null || c1 == null)
                {
                    id++;
                    continue;
                }

                var jc = Connect(c0, pair.A, pair.tA, c1, pair.B, pair.tB, id, endTolerance, perpendicularThreshold);
                joints.Add(jc);
                id++;
            }

            joints = JointX.MergeJoints(joints, mergeDistance);

            // Post first: where only beam ends meet, the joint can't tell the post from the beams
            if (postDirection.Unitize())
                foreach (var jc in joints.Where(j => j.Parts.Count >= 3 && j.Parts.All(p => JointPartX.IsAtEnd(p.Case))))
                {
                    var post = jc.Parts.OrderByDescending(p => Math.Abs(p.Direction * postDirection) / Math.Max(p.Direction.Length, 1e-9)).First();
                    if (Math.Abs(post.Direction * postDirection) / Math.Max(post.Direction.Length, 1e-9) < Math.Cos(Math.PI * 0.25)) continue;
                    jc.Parts.Remove(post);
                    jc.Parts.Insert(0, post);
                }

            if (splitDoubleT)
                joints = JointX.SplitDoubleT(joints, perpendicularThreshold);

            // Merging adds parts, so frame each condition from all of them
            foreach (var jc in joints)
                jc.Position = JointX.ConditionPlane(jc.Parts, jc.Position.Origin);

            if (freeEnds)
                joints.AddRange(FreeEnds(curves, joints, endTolerance));

            for (int i = 0; i < joints.Count; ++i)
                joints[i].Id = i.ToString();

            return joints;
        }

        /// <summary>
        /// A two-part condition for a connection between two curves. A connection inside an end-to-end
        /// overlap (see JointX.IsEndOverlap) counts as at the end of both curves.
        /// </summary>
        public static JointX Connect(Curve c0, int index0, double t0, Curve c1, int index1, double t1, int jointIndex = -1,
            double endTolerance = 10, double perpendicularThreshold = Math.PI * 0.25)
        {
            JointUtil.ClassifyJointPosition(c0, t0, out int s0, out Vector3d v0, endTolerance);
            JointUtil.ClassifyJointPosition(c1, t1, out int s1, out Vector3d v1, endTolerance);

            if ((JointPartX.IsAtMiddle(s0) || JointPartX.IsAtMiddle(s1)) && JointX.IsEndOverlap(c0, t0, c1, t1, endTolerance, perpendicularThreshold))
            {
                JointUtil.ClassifyJointPosition(c0, t0, out s0, out v0, double.MaxValue);
                JointUtil.ClassifyJointPosition(c1, t1, out s1, out v1, double.MaxValue);
            }

            return new JointX(
                new List<JointPartX>
                {
                    new JointPartX() { Case = s0, ElementIndex = index0, JointIndex = jointIndex, Parameter = t0, Direction = v0 },
                    new JointPartX() { Case = s1, ElementIndex = index1, JointIndex = jointIndex, Parameter = t1, Direction = v1 },
                },
                (c0.PointAt(t0) + c1.PointAt(t1)) * 0.5);
        }

        /// <summary>
        /// Single-beam (E) conditions at the curve ends that no condition in joints uses.
        /// </summary>
        public static List<JointX> FreeEnds(IDictionary<int, Curve> curves, IEnumerable<JointX> joints, double endTolerance = 10)
        {
            var used = new HashSet<(int, bool)>(joints.SelectMany(j => j.Parts)
                .Where(p => JointPartX.IsAtEnd(p.Case))
                .Select(p => (p.ElementIndex, JointPartX.End1(p.Case))));

            var ends = new List<JointX>();
            foreach (var kvp in curves.OrderBy(x => x.Key))
            {
                if (kvp.Value == null) continue;

                foreach (var atEnd1 in new[] { false, true })
                {
                    if (used.Contains((kvp.Key, atEnd1))) continue;

                    var t = atEnd1 ? kvp.Value.Domain.Max : kvp.Value.Domain.Min;
                    JointUtil.ClassifyJointPosition(kvp.Value, t, out int s, out Vector3d v, endTolerance);
                    ends.Add(new JointX(
                        new List<JointPartX> { new JointPartX() { Case = s, ElementIndex = kvp.Key, Parameter = t, Direction = v } },
                        kvp.Value.PointAt(t)));
                }
            }

            return ends;
        }
    }
}
