using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// End cut: the end of a beam cut off with a plane, optionally tilted, and optionally tapered
    /// (chamfered) on one or two opposite sides. For single-part conditions at a beam end.
    /// Each cut is a JackRafterCut feature.
    /// </summary>
    [JointType("glulamb.end-cut", Name = "End cut", Arity = 1, Topology = JointTopology.End,
        Description = "Cuts off the end of a beam with a plane, optionally tilted or tapered.")]
    public class EndCutJoint : JointBase
    {
        [JointParameter(Description = "Where the cut is, measured back from the end of the centreline. Negative cuts beyond the end (the beam is extended).", Unit = "length")]
        public double Offset { get; set; } = 0;

        [JointParameter(Description = "Tilt of the cut about the section's X axis (the end leans along Y).", Unit = "radians")]
        public double TiltX { get; set; } = 0;

        [JointParameter(Description = "Tilt of the cut about the section's Y axis (the end leans along X).", Unit = "radians")]
        public double TiltY { get; set; } = 0;

        [JointParameter(Description = "Length of the taper along the beam. 0 = no taper.", Unit = "length")]
        public double TaperLength { get; set; } = 0;

        [JointParameter(Description = "How much the taper takes off the side at the end of the beam.", Unit = "length")]
        public double TaperDepth { get; set; } = 0;

        [JointParameter(Description = "Taper both opposite sides instead of one.")]
        public bool TaperBothSides { get; set; } = false;

        [JointParameter(Description = "Taper the sides along X instead of along Y.")]
        public bool TaperAlongX { get; set; } = false;

        public EndCutJoint(JointX condition) : base(condition)
        {
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam = beams[0];
            var part = m_parts[0];

            if (!JointPartX.IsAtEnd(part.Case))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the condition is not at the end of the beam.");
                return;
            }

            bool atStart = JointPartX.End0(part.Case);
            var curve = beam.Centreline;
            var t = atStart ? curve.Domain.Min : curve.Domain.Max;
            var section = beam.GetPlane(t);

            // Outward along the beam at its end
            var outward = atStart ? -section.ZAxis : section.ZAxis;
            var endFrame = new Plane(section.Origin, section.XAxis, section.YAxis);
            if (endFrame.ZAxis * outward < 0)
                endFrame = new Plane(section.Origin, -section.XAxis, section.YAxis);

            // The cut plane, Offset back from the end, tilted about the section axes; its normal
            // points to the removed end
            var cut = new Plane(endFrame.Origin - outward * Offset, endFrame.XAxis, endFrame.YAxis);
            cut.Rotate(TiltX, cut.XAxis, cut.Origin);
            cut.Rotate(TiltY, cut.YAxis, cut.Origin);

            var endCut = new JackRafterCut(beam.Id, cut);
            endCut.Data.Set("Name", "EndCut");
            result.Add(endCut);
            Position = cut;

            if (Offset < 0 || TiltX != 0 || TiltY != 0)
            {
                // Reach the cut across the whole section
                var project = Transform.ProjectAlong(cut, outward);
                var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c =>
                {
                    var p = endFrame.PointAt(c.Item1 * beam.Width * 0.5, c.Item2 * beam.Height * 0.5);
                    p.Transform(project);
                    return p;
                });
                ExtendToReach(result, beam, part, corners);
            }

            // Tapers: a plane from TaperDepth in from the side at the cut, back to the side at
            // TaperLength from the cut
            if (TaperLength > 0 && TaperDepth > 0)
            {
                var axis = TaperAlongX ? cut.XAxis : cut.YAxis;
                var half = (TaperAlongX ? beam.Width : beam.Height) * 0.5;
                var sides = Flip ? new[] { -1 } : new[] { 1 };
                if (TaperBothSides) sides = new[] { 1, -1 };

                foreach (var s in sides)
                {
                    var side = axis * s;
                    var atCut = cut.Origin + side * (half - TaperDepth);
                    var back = cut.Origin - outward * TaperLength + side * half;
                    var along = atCut - back; along.Unitize();
                    var across = Vector3d.CrossProduct(side, outward);

                    // Normal points away from the beam, to the removed wedge
                    var normal = Vector3d.CrossProduct(along, across);
                    if (normal * side < 0) normal.Reverse();

                    var taper = new JackRafterCut(beam.Id, new Plane(atCut, normal));
                    taper.Data.Set("Name", s > 0 ? "Taper+" : "Taper-");
                    result.Add(taper);
                }
            }

            result.Status = JointStatus.Ok;
        }
    }
}
