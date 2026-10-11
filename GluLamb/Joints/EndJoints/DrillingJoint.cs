using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// A hole drilled into one beam at a point, square to one of its faces: the most basic
    /// single-beam joint, for points placed along a beam (e.g. for a bolt or a peg). The joint
    /// position picks the face: the hole goes in from the face on the side of the position
    /// (from the centreline), at the position's place across that face. A position on the
    /// centreline drills along the section's Y axis.
    /// </summary>
    [JointType("glulamb.drilling", Name = "Drilling", Arity = 1, Topology = JointTopology.Feature,
        Description = "A hole drilled into a beam at a point, square to a face.")]
    public class DrillingJoint : JointBase
    {
        [JointParameter(Description = "Hole diameter.", Unit = "length")]
        public double Diameter { get; set; } = 16;

        [JointParameter(Description = "Hole depth from the face. 0 = through the beam.", Unit = "length")]
        public double Depth { get; set; } = 0;

        [JointParameter(Description = "Which face to drill from: 0 = the face towards the joint position, 1 = along the section X axis, 2 = along the section Y axis. Flip drills from the opposite face.")]
        public int Axis { get; set; } = 0;

        [JointParameter(Description = "Extra length added beyond the beam for through holes.", Unit = "length")]
        public double Added { get; set; } = 10;

        public DrillingJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Single parts: the default along a beam, an alternative at its end.
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 1) return 0;
            return JointPartX.IsAtMiddle(condition.Parts[0].Case) ? 1.0 : 0.5;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam = beams[0];
            var plane = beam.GetPlane(m_parts[0].Parameter);
            var point = Position.IsValid ? Position.Origin : plane.Origin;
            var offset = point - plane.Origin;
            offset -= plane.ZAxis * (offset * plane.ZAxis);

            Vector3d towards;
            switch (Axis)
            {
                case 1: towards = plane.XAxis; break;
                case 2: towards = plane.YAxis; break;
                default:
                    towards = offset.Length > context.Tolerance ? offset : plane.YAxis;
                    break;
            }
            if (Axis != 0 && towards * offset < 0) towards.Reverse();
            if (Flip) towards.Reverse();

            var frame = AlignSection(beam, plane, plane.ZAxis, towards, out double width, out double height);

            // Entry on the face, at the point's place across it (kept inside the face)
            var limit = Math.Max(0, (width - Diameter) * 0.5);
            var across = Math.Max(-limit, Math.Min(limit, offset * frame.XAxis));
            var entry = frame.Origin + frame.XAxis * across + frame.YAxis * height * 0.5;
            var depth = Depth > 0 ? Depth : height + Added;

            result.Add(new Drilling(beam.Id, new Line(entry + frame.YAxis * Added, entry - frame.YAxis * depth), Diameter));
            Position = new Plane(entry, frame.XAxis, frame.ZAxis);
            result.Status = JointStatus.Ok;
        }
    }
}
