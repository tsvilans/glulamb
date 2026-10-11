using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Corner butt: two beam ends at a corner, one (the through beam) running on to the outside
    /// of the other, which butts against its inner face, as at the corners of a box or frame.
    /// The butting beam is cut on the through beam's inner face, the through beam flush with the
    /// butting beam's outer face (or past it by Overhang). Both cuts are JackRafterCuts; beams
    /// that stop short are reported for extension. Optional dowels through the through beam into
    /// the end of the butting one. Beam 0 runs through; Flip swaps.
    /// </summary>
    [JointType("glulamb.corner-butt", Name = "Corner butt", Arity = 2, Topology = JointTopology.Corner,
        Description = "Two beam ends at a corner, one butting against the inner face of the other, optionally dowelled.")]
    public class ButtCornerJoint : JointBase
    {
        [JointParameter(Description = "How far the through beam runs on past the outer face of the butting beam. 0 = flush.", Unit = "length")]
        public double Overhang { get; set; } = 0;

        [JointParameter(Description = "Gap between the end of the butting beam and the through beam.", Unit = "length")]
        public double Gap { get; set; } = 0;

        [JointParameter(Description = "Number of dowels, spread across the longer side of the butting beam's section.")]
        public int DowelCount { get; set; } = 0;

        [JointParameter(Description = "Distance between dowels.", Unit = "length")]
        public double DowelSpacing { get; set; } = 80;

        [JointParameter(Description = "Dowel diameter. 0 = no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 12;

        [JointParameter(Description = "How far the dowels go into the butting beam, from its cut end.", Unit = "length")]
        public double DowelEmbedment { get; set; } = 100;

        [JointParameter(Description = "Extra depth drilled beyond the end of the dowel.", Unit = "length")]
        public double DowelDrillExtra { get; set; } = 10;

        [JointParameter(Description = "Extra length added to drillings so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public ButtCornerJoint(JointX condition) : base(condition)
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

            int ti = Flip ? 1 : 0, bi = 1 - ti;

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

            var normal = Vector3d.CrossProduct(inward[0], inward[1]);
            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams are parallel.");
                return;
            }

            // Where the centrelines come closest
            var lines = new[] { new Line(ends[0], ends[0] + inward[0]), new Line(ends[1], ends[1] + inward[1]) };
            var near = (Point3d[])ends.Clone();
            if (Rhino.Geometry.Intersect.Intersection.LineLine(lines[0], lines[1], out double a, out double b, 0, false))
            {
                near[0] = lines[0].PointAt(a);
                near[1] = lines[1].PointAt(b);
            }

            Beam through = beams[ti], butting = beams[bi];

            // Each beam's section framed with Y across it towards the other beam's body
            var throughFrame = AlignSection(through, through.GetPlane(m_parts[ti].Parameter), inward[ti], inward[bi], out _, out double throughDepth);
            var buttingFrame = AlignSection(butting, butting.GetPlane(m_parts[bi].Parameter), inward[bi], inward[ti], out double buttingWidth, out double buttingDepth);
            Vector3d intoButting = throughFrame.YAxis, intoThrough = buttingFrame.YAxis;

            // The butting beam ends on the through beam's inner face (cut normals point to the
            // removed part)
            var innerFace = near[ti] + intoButting * (throughDepth * 0.5 + Gap);
            var buttCut = new Plane(innerFace, -intoButting);

            // The through beam ends at the butting beam's outer face, or past it
            var outerFace = near[bi] - intoThrough * (buttingDepth * 0.5 + Overhang);
            var throughCut = new Plane(outerFace, -intoThrough);

            var cuts = new[] { (Beam: through, Part: m_parts[ti], Frame: throughFrame, Plane: throughCut, Inward: inward[ti], Name: "Through"),
                               (Beam: butting, Part: m_parts[bi], Frame: buttingFrame, Plane: buttCut, Inward: inward[bi], Name: "Butt") };
            foreach (var cut in cuts)
            {
                if (Math.Abs(cut.Inward * cut.Plane.ZAxis) < 0.1)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: {cut.Beam.Id} runs almost along the other beam's face.");
                    return;
                }
                var feature = new JackRafterCut(cut.Beam.Id, cut.Plane);
                feature.Data.Set("Name", cut.Name);
                result.Add(feature);

                // Reach the cut across the whole section
                var project = Transform.ProjectAlong(cut.Plane, cut.Inward);
                var section = cut.Beam.GetPlane(cut.Part.Parameter);
                var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c =>
                {
                    var p = section.PointAt(c.Item1 * cut.Beam.Width * 0.5, c.Item2 * cut.Beam.Height * 0.5);
                    p.Transform(project);
                    return p;
                });
                ExtendToReach(result, cut.Beam, cut.Part, corners);
            }

            // Dowels along the butting beam, from the through beam's outer face into its end
            if (DowelDiameter > 0 && DowelCount > 0)
            {
                var dir = inward[bi];
                var outer = new Plane(near[ti] - intoButting * throughDepth * 0.5, intoButting);
                var toOuter = Transform.ProjectAlong(outer, dir);
                var toEnd = Transform.ProjectAlong(buttCut, dir);
                // Spread across the longer side of the butting beam's section
                var (spread, extent) = buttingWidth >= buttingDepth ? (buttingFrame.XAxis, buttingWidth) : (intoThrough, buttingDepth);
                var inset = (extent - DowelDiameter) * 0.5;

                for (int i = 0; i < DowelCount; ++i)
                {
                    var offset = (i - (DowelCount - 1) * 0.5) * DowelSpacing;
                    if (Math.Abs(offset) > inset)
                        result.Messages.Add($"{GetType().Name}: dowel {i} is outside the butting beam.");

                    var start = near[bi] + spread * offset;
                    var atOuter = start; atOuter.Transform(toOuter);
                    var atEnd = start; atEnd.Transform(toEnd);
                    var tip = atEnd + dir * DowelEmbedment;

                    result.Add(new Drilling(through.Id, new Line(atOuter - dir * Added, atEnd + dir * Added), DowelDiameter));
                    result.Add(new Drilling(butting.Id, new Line(atEnd - dir * Added, tip + dir * DowelDrillExtra), DowelDiameter));
                    result.Hardware.Add(new DowelItem(new Line(atOuter, tip), DowelDiameter, through.Id, butting.Id));
                }
            }

            Position = new Plane(innerFace, intoThrough, normal);
            result.Status = JointStatus.Ok;
        }
    }
}
