using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Mitre: two beam ends cut on the plane that bisects the angle between them, so their end
    /// faces meet, as two rafters at a ridge. The plane goes through where the centrelines come
    /// closest, square to the plane both beams lie in. Each cut is a JackRafterCut; the beams are
    /// reported for extension if they stop short of the plane. Optional gap between the faces and
    /// a dowel square to the mitre.
    /// </summary>
    [JointType("glulamb.corner-mitre", Name = "Mitre", Arity = 2, Topology = JointTopology.Corner,
        Description = "Two beam ends cut on the angle bisector so their end faces meet.")]
    public class MitreCornerJoint : JointBase
    {
        [JointParameter(Description = "Gap between the two end faces, half taken from each beam.", Unit = "length")]
        public double Gap { get; set; } = 0;

        [JointParameter(Description = "Diameter of a dowel across the mitre, square to it. 0 = no dowel.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0;

        [JointParameter(Description = "Length of the dowel, centred on the mitre.", Unit = "length")]
        public double DowelLength { get; set; } = 120;

        [JointParameter(Description = "Extra depth drilled beyond each end of the dowel.", Unit = "length")]
        public double DowelDrillExtra { get; set; } = 5;

        public MitreCornerJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Two beam ends at a corner or an acute angle, as the corner lap but less preferred.
        /// </summary>
        public static double Score(JointX condition, IJointContext context) =>
            CornerLapJoint.Score(condition, context) * 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            if (!JointPartX.IsAtEnd(m_parts[0].Case) || !JointPartX.IsAtEnd(m_parts[1].Case))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: both beams have to end at the joint.");
                return;
            }

            // Each beam's end, and its direction into the beam from there
            var ends = new Point3d[2];
            var inward = new Vector3d[2];
            for (int i = 0; i < 2; ++i)
            {
                var curve = beams[i].Centreline;
                bool atStart = JointPartX.End0(m_parts[i].Case);
                ends[i] = atStart ? curve.PointAtStart : curve.PointAtEnd;
                inward[i] = atStart ? curve.TangentAtStart : -curve.TangentAtEnd;
                inward[i].Unitize();
            }

            // The bisecting plane; its normal points into beam 0
            var normal = inward[0] - inward[1];
            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams run back along each other.");
                return;
            }

            // Where the centrelines come closest, or between the ends if they are in line
            var origin = 0.5 * (ends[0] + ends[1]);
            var line0 = new Line(ends[0], ends[0] + inward[0]);
            var line1 = new Line(ends[1], ends[1] + inward[1]);
            if (Vector3d.CrossProduct(inward[0], inward[1]).Length > 1e-3
                && Rhino.Geometry.Intersect.Intersection.LineLine(line0, line1, out double a, out double b, 0, false))
                origin = 0.5 * (line0.PointAt(a) + line1.PointAt(b));

            var mitre = new Plane(origin, normal);
            var across = Vector3d.CrossProduct(inward[0], inward[1]);
            if (across.Unitize())
                mitre = new Plane(origin, across, Vector3d.CrossProduct(normal, across));
            Position = mitre;

            // Each beam's cut: its normal points away from the beam, to the removed part
            for (int i = 0; i < 2; ++i)
            {
                var sign = i == 0 ? 1 : -1;
                var cut = new Plane(origin + normal * sign * Gap * 0.5, -normal * sign);
                var feature = new JackRafterCut(beams[i].Id, cut);
                feature.Data.Set("Name", "Mitre");
                result.Add(feature);

                // Reach the cut across the whole section
                var section = beams[i].GetPlane(m_parts[i].Parameter);
                var project = Transform.ProjectAlong(cut, inward[i]);
                var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c =>
                {
                    var p = section.PointAt(c.Item1 * beams[i].Width * 0.5, c.Item2 * beams[i].Height * 0.5);
                    p.Transform(project);
                    return p;
                });
                ExtendToReach(result, beams[i], m_parts[i], corners);
            }

            if (DowelDiameter > 0)
            {
                var dowel = new Line(origin - normal * DowelLength * 0.5, origin + normal * DowelLength * 0.5);
                var hole = new Line(dowel.From - normal * DowelDrillExtra, dowel.To + normal * DowelDrillExtra);
                result.Add(new Drilling(beams[0].Id, hole, DowelDiameter));
                result.Add(new Drilling(beams[1].Id, hole, DowelDiameter));
                result.Hardware.Add(new DowelItem(dowel, DowelDiameter, beams[0].Id, beams[1].Id));
            }

            result.Status = JointStatus.Ok;
        }
    }

    /// <summary>
    /// Butt splice: two beam ends in line (or nearly), each cut square, or on the bisector of a
    /// slight angle between them, meeting face to face. The mitre for splices.
    /// </summary>
    [JointType("glulamb.splice-butt", Name = "Butt splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two beam ends in line, cut on the bisector of their angle and butted together.")]
    public class ButtSpliceJoint : MitreCornerJoint
    {
        public ButtSpliceJoint(JointX condition) : base(condition)
        {
        }

        public static new double Score(JointX condition, IJointContext context) =>
            condition.Parts.Count == 2 && condition.Parts.All(p => JointPartX.IsAtEnd(p.Case))
            && JointRegistry.Classify(condition, JointX.PerpendicularThreshold) == JointTopology.Splice ? 0.5 : 0;
    }
}
