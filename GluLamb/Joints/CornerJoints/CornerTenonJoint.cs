using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Corner tenon (open mortise and tenon, or corner bridle): two beam ends meeting at a
    /// corner, the middle third of one beam (across the plane of the joint) running as a tongue
    /// into a slot in the end of the other, like the tenon splice turned round a corner. Both
    /// ends run out flush with the other beam's outer side. Each beam is cut by one open
    /// surface (a Slot on the slotted beam, a Tenon on the other). Beam 1 has the tenon; Flip
    /// gives it to beam 0.
    /// </summary>
    [JointType("glulamb.corner-tenon", Name = "Corner tenon", Arity = 2,
        Description = "Two beam ends meeting at a corner, one with a through tenon in a slot in the other.")]
    public class CornerTenonJoint : JointBase
    {
        [JointParameter(Description = "Tenon thickness, across the plane of the joint. 0 = a third of the thinner beam.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Offset of the tenon from the middle, along the joint normal.", Unit = "length")]
        public double TenonOffset { get; set; } = 0;

        [JointParameter(Description = "Clearance on each side of the tenon in the slot.", Unit = "length")]
        public double Clearance { get; set; } = 0;

        [JointParameter(Description = "Diameter of a peg through the joint, along the joint normal. 0 = no peg.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public CornerTenonJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => CornerLapJoint.Score(condition, context) * 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var tolerance = context.Tolerance;
            var planes = new[] { beams[0].GetPlane(m_parts[0].Parameter), beams[1].GetPlane(m_parts[1].Parameter) };
            var dirs = new[] { m_parts[0].Direction, m_parts[1].Direction };

            var normal = Vector3d.CrossProduct(dirs[0], dirs[1]);
            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are parallel.");
                return;
            }
            if (normal * NearestSectionAxis(planes[0], normal) < 0) normal.Reverse();

            int ti = Flip ? 0 : 1;   // tenon
            int si = 1 - ti;         // slot

            var frames = new Plane[2];
            var widths = new double[2];
            var heights = new double[2];
            for (int i = 0; i < 2; ++i)
                frames[i] = AlignSection(beams[i], planes[i], -dirs[i], normal, out widths[i], out heights[i]);

            var thickness = TenonThickness > 0 ? TenonThickness : Math.Min(heights[0], heights[1]) / 3.0;
            var centre = (frames[0].Origin + frames[1].Origin) * 0.5 + normal * TenonOffset;
            var half = Math.Max(heights[0], heights[1]) * 0.5 + Math.Abs(TenonOffset) + Added;

            Plane Layer(double z) => new Plane(centre + normal * z, normal);

            // Side planes of a beam: outer is away from the other beam's body
            Plane SidePlane(int i, bool outer, double grow)
            {
                var d = frames[i].XAxis;
                if ((d * dirs[1 - i] > 0) != outer) d.Reverse();
                return new Plane(frames[i].Origin + d * (widths[i] * 0.5 + grow), d);
            }

            bool ok = true;
            Point3d X(Plane a, Plane b, Plane c)
            {
                ok &= RX.PlanePlanePlane(a, b, c, out Point3d p);
                return p;
            }

            // A profile across the other beam (on its side planes, at the given heights),
            // lofted across this beam between its sides pushed out by Added
            Brep Cutter(int self, (bool outer, double z)[] profile)
            {
                var other = 1 - self;
                var curves = new[] { true, false }.Select(sideOuter =>
                {
                    var side = SidePlane(self, sideOuter, Added);
                    return new Polyline(profile.Select(p => X(Layer(p.z), SidePlane(other, p.outer, 0), side))).ToNurbsCurve();
                }).ToArray();
                if (!ok) return null;
                var loft = Brep.CreateFromLoft(curves, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (loft == null || loft.Length < 1) return null;
                loft[0].Faces.SplitKinkyFaces();
                return loft[0];
            }

            var slotHalf = thickness * 0.5 + Clearance;
            var tenonHalf = thickness * 0.5;

            // Slotted beam: ends at the tenon beam's outer side, with the slot back to its inner side
            var slotCutter = Cutter(si, new[]
            {
                (true, half), (true, slotHalf), (false, slotHalf), (false, -slotHalf), (true, -slotHalf), (true, -half),
            });
            // Tenon beam: cheeks off from the slotted beam's inner side, ends at its outer side
            var tenonCutter = Cutter(ti, new[]
            {
                (false, half), (false, tenonHalf), (true, tenonHalf), (true, -tenonHalf), (false, -tenonHalf), (false, -half),
            });

            if (!ok || slotCutter == null || tenonCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the cutters.");
                return;
            }

            var outerCorner = X(Layer(0), SidePlane(0, true, 0), SidePlane(1, true, 0));
            var innerCorner = X(Layer(0), SidePlane(0, false, 0), SidePlane(1, false, 0));
            var corner0 = X(Layer(0), SidePlane(0, true, 0), SidePlane(1, false, 0));
            var corner1 = X(Layer(0), SidePlane(0, false, 0), SidePlane(1, true, 0));
            var middle = (outerCorner + innerCorner) * 0.5;

            Position = new Plane(middle, frames[si].ZAxis, frames[ti].ZAxis);

            var slot = new Slot { BeamId = beams[si].Id, Plane = new Plane(middle, normal), Thickness = slotHalf * 2 };
            slot.Cutters.Add(slotCutter);
            result.Add(slot);

            var tenon = new Tenon { BeamId = beams[ti].Id, Plane = new Plane(middle, frames[ti].XAxis, normal), Thickness = thickness, Width = widths[si] };
            tenon.Cutters.Add(tenonCutter);
            tenon.Data.Set("Through", true);
            result.Add(tenon);

            var overlap = new[] { outerCorner, corner0, innerCorner, corner1 };
            ExtendToReach(result, beams[0], m_parts[0], overlap);
            ExtendToReach(result, beams[1], m_parts[1], overlap);

            if (DowelDiameter > 0)
            {
                var dowel = SpanThrough(middle, normal, new[] { (frames[0].Origin, heights[0]), (frames[1].Origin, heights[1]) });
                var hole = new Line(dowel.From - normal * Added, dowel.To + normal * Added);
                result.Add(new Drilling(beams[0].Id, hole, DowelDiameter));
                result.Add(new Drilling(beams[1].Id, hole, DowelDiameter));
                result.Hardware.Add(new DowelItem(dowel, DowelDiameter, beams[0].Id, beams[1].Id));
            }

            result.Status = JointStatus.Ok;
        }
    }
}
