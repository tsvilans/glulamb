using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Branch split: two beams that start together and fork apart, like the branches of a split
    /// glulam. Near the fork their sections run into each other; both are cut along one plane
    /// between them, from the fork to where they have separated, so they sit side by side.
    /// Port of BranchJointSimple, with the split direction taken from how the beams diverge
    /// rather than from the section axes, and its hard-coded extension as a parameter. Each
    /// beam gets a LongitudinalCut.
    /// </summary>
    [JointType("glulamb.branch-split", Name = "Branch split", Arity = 2, Topology = JointTopology.Acute,
        Description = "Two beams forking from the same point, split apart along a plane between them.")]
    public class BranchSplitJoint : JointBase
    {
        [JointParameter(Description = "How far the split runs on past the point where the beams no longer touch.", Unit = "length")]
        public double ExtraLength { get; set; } = 0;

        [JointParameter(Description = "Offset of the split plane towards beam 1.", Unit = "length")]
        public double Offset { get; set; } = 0;

        [JointParameter(Description = "Extra size added to the cutting surface so it clears the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Largest angle between the branches that counts as a fork.", Unit = "radians")]
        public double MaximumAngle { get; set; } = Rhino.RhinoMath.ToRadians(30.0);

        public BranchSplitJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Two beam ends meeting and pointing the same way, at a small angle. Scores above the
        /// corner lap there, which makes little sense between nearly parallel beams.
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 2 || !condition.Parts.All(x => JointPartX.IsAtEnd(x.Case))) return 0;
            var angle = Vector3d.VectorAngle(-condition.Parts[0].Direction, -condition.Parts[1].Direction);
            return angle < Rhino.RhinoMath.ToRadians(30.0) ? 1.5 : 0.0;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var tolerance = context.Tolerance;
            var node = Position.IsValid ? Position.Origin : (beams[0].GetPlane(m_parts[0].Parameter).Origin + beams[1].GetPlane(m_parts[1].Parameter).Origin) * 0.5;

            // Directions from the fork into each beam
            var dirs = new Vector3d[2];
            for (int i = 0; i < 2; ++i)
            {
                var plane = beams[i].GetPlane(m_parts[i].Parameter);
                var into = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - node;
                dirs[i] = plane.ZAxis * into < 0 ? -plane.ZAxis : plane.ZAxis;
            }

            var angle = Vector3d.VectorAngle(dirs[0], dirs[1]);
            if (angle > MaximumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams diverge by {Rhino.RhinoMath.ToDegrees(angle):0.0}°, more than MaximumAngle.");
                return;
            }

            var along = dirs[0] + dirs[1];
            along.Unitize();
            // Split normal, from beam 0 towards beam 1
            var normal = dirs[1] - dirs[0];
            normal -= along * (normal * along);
            if (!normal.Unitize())
            {
                // Same direction: split across the first beam's section, towards the second beam's centreline
                var plane0 = beams[0].GetPlane(m_parts[0].Parameter);
                var towards = beams[1].Centreline.PointAt(beams[1].Centreline.Domain.Mid) - beams[0].Centreline.PointAt(beams[0].Centreline.Domain.Mid);
                normal = NearestSectionAxis(plane0, towards);
                if (normal * towards < 0) normal.Reverse();
                normal -= along * (normal * along);
                normal.Unitize();
                result.Messages.Add($"{GetType().Name}: the beams are parallel; splitting across beam 0's section.");
            }
            var across = Vector3d.CrossProduct(along, normal);

            var size = new[] { Math.Max(beams[0].Width, beams[0].Height), Math.Max(beams[1].Width, beams[1].Height) };
            var maxSize = Math.Max(size[0], size[1]);

            // Walk along beam 0 until it is clear of beam 1 by half their sizes
            var c0 = beams[0].Centreline;
            var c1 = beams[1].Centreline;
            var total = c0.GetLength();
            var clear = (size[0] + size[1]) * 0.5;
            var length = total;
            for (double s = 0; s <= total; s += Math.Max(total / 200, tolerance * 10))
            {
                var t = JointPartX.End0(m_parts[0].Case) ? s : total - s;
                if (!c0.LengthParameter(t, out double ct)) break;
                var p = c0.PointAt(ct);
                c1.ClosestPoint(p, out double ct1);
                if (p.DistanceTo(c1.PointAt(ct1)) >= clear)
                {
                    length = (p - node) * along;
                    break;
                }
            }
            length += ExtraLength;

            var origin = node + normal * Offset;
            var splitPlane = new Plane(origin, along, across);
            var half = maxSize + Added;
            var surface = Brep.CreateFromCornerPoints(
                splitPlane.PointAt(-maxSize, -half), splitPlane.PointAt(length + Added, -half),
                splitPlane.PointAt(length + Added, half), splitPlane.PointAt(-maxSize, half), tolerance);
            if (surface == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the split surface.");
                return;
            }

            for (int i = 0; i < 2; ++i)
            {
                // The removed side is towards the other beam
                var n = i == 0 ? normal : -normal;
                result.Add(new LongitudinalCut
                {
                    BeamId = beams[i].Id,
                    Plane = new Plane(origin, n),
                    Length = length,
                    Cutters = new List<Brep> { surface.DuplicateBrep() },
                });
            }

            Position = new Plane(origin, along, -across);   // Z along the split normal
            result.Status = JointStatus.Ok;
        }
    }
}
