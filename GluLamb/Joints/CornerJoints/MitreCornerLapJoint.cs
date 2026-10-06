using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Mitred corner lap: a corner half-lap whose top face shows a mitre. Where the beams
    /// overlap, the top layer is split along the diagonal from the outer to the inner corner,
    /// each beam taking the half next to it; the bottom layer is all the bottom beam's. The top
    /// beam drops in from above. Replaces CornerJoint2X, which aimed at this but never produced
    /// geometry. Each beam is cut by one open surface: a shoulder below the lap face, the lap
    /// face, and the mitre wall above it. The higher beam goes on top (beam 0 when they are
    /// level); Flip inverts that.
    /// </summary>
    [JointType("glulamb.corner-lap-mitre", Name = "Mitred corner lap", Arity = 2,
        Description = "Two beam ends half-lapped at a corner, with a mitre on the top face.")]
    public class MitreCornerLapJoint : JointBase
    {
        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Diameter of a dowel through the lap. 0 = no dowel.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0.0;

        public MitreCornerLapJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// As the corner lap, but not for beams within 30° of each other (forks), where a mitre
        /// across the overlap makes no sense.
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            var score = CornerLapJoint.Score(condition, context) * 0.5;
            if (score <= 0) return 0;
            var angle = Vector3d.VectorAngle(condition.Parts[0].Direction, condition.Parts[1].Direction);
            return angle < Rhino.RhinoMath.ToRadians(30) ? 0 : score;
        }

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

            int ti = TopPart(planes[0].Origin, planes[1].Origin, normal, tolerance);
            int bi = 1 - ti;

            // Sections relative to the lap: Y along the normal, X across the beam in the lap plane
            var frames = new Plane[2];
            var widths = new double[2];
            var heights = new double[2];
            for (int i = 0; i < 2; ++i)
                frames[i] = AlignSection(beams[i], planes[i], -dirs[i], normal, out widths[i], out heights[i]);

            var lapOrigin = Interpolation.Lerp(frames[bi].Origin, frames[ti].Origin, heights[bi] / (heights[bi] + heights[ti]));
            var lap = new Plane(lapOrigin, normal);

            // Side planes of a beam: outer is away from the other beam's body (which lies back
            // along the other's direction)
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

            // Overlap corners: outer, inner
            var outerCorner = X(lap, SidePlane(bi, true, 0), SidePlane(ti, true, 0));
            var innerCorner = X(lap, SidePlane(bi, false, 0), SidePlane(ti, false, 0));
            var topCorner = X(lap, SidePlane(bi, false, 0), SidePlane(ti, true, 0));      // on the top beam's side of the mitre
            var bottomCorner = X(lap, SidePlane(bi, true, 0), SidePlane(ti, false, 0));
            if (!ok || outerCorner.DistanceTo(innerCorner) < tolerance)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams' sides don't intersect.");
                return;
            }

            var mitre = new Plane(outerCorner, innerCorner - outerCorner, normal);
            var depth = heights[0] + heights[1] + Added;

            // One beam's cutting surface: a shoulder on the vertical plane s below the lap face,
            // the lap face over the top beam's half of the overlap, and the mitre wall above,
            // meeting at the pivot (where s crosses the mitre). The lap face runs out past the
            // beam's side towards the outside of the corner (far), and the shoulder and wall
            // past the other side (near).
            Brep Cutter(Plane s, Point3d pivot, Plane far, Plane near)
            {
                var a = X(lap, s, far);
                var b = X(lap, mitre, far);
                var c = X(lap, s, near);
                var d = X(lap, mitre, near);
                var down = -normal * depth;
                var up = normal * depth;

                var faces = new[]
                {
                    Brep.CreateFromCornerPoints(pivot, a, b, tolerance),
                    Brep.CreateFromCornerPoints(c, pivot, pivot + down, c + down, tolerance),
                    Brep.CreateFromCornerPoints(pivot, a, a + down, pivot + down, tolerance),
                    Brep.CreateFromCornerPoints(d, pivot, pivot + up, d + up, tolerance),
                    Brep.CreateFromCornerPoints(pivot, b, b + up, pivot + up, tolerance),
                };
                if (faces.Any(x => x == null)) return null;
                var joined = Brep.JoinBreps(faces, tolerance);
                return joined != null && joined.Length == 1 ? joined[0] : null;
            }

            // Top beam: its bottom half stops at the bottom beam's inner side; pivot at the inner corner
            var topCutter = Cutter(SidePlane(bi, false, 0), innerCorner, SidePlane(ti, true, Added), SidePlane(ti, false, Added));
            // Bottom beam: ends at the top beam's outer side; pivot at the outer corner
            var bottomCutter = Cutter(SidePlane(ti, true, 0), outerCorner, SidePlane(bi, false, Added), SidePlane(bi, true, Added));

            if (!ok || topCutter == null || bottomCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the cutters.");
                return;
            }

            Position = new Plane(lapOrigin, outerCorner - innerCorner, Vector3d.CrossProduct(normal, outerCorner - innerCorner));
            foreach (var (i, cutter) in new[] { (ti, topCutter), (bi, bottomCutter) })
            {
                var feature = new Lap { BeamId = beams[i].Id, Plane = new Plane(lapOrigin, frames[i].XAxis, frames[i].ZAxis), Cutters = new List<Brep> { cutter } };
                feature.Data.Set("Name", "MitreLap");
                result.Add(feature);
            }

            // Both beams reach across the overlap
            var overlap = new[] { outerCorner, topCorner, innerCorner, bottomCorner };
            ExtendToReach(result, beams[0], m_parts[0], overlap);
            ExtendToReach(result, beams[1], m_parts[1], overlap);

            // Dowel through the middle of the top beam's half, where both layers are solid
            if (DowelDiameter > 0)
            {
                var centre = (outerCorner + innerCorner + topCorner) / 3;
                var dowel = SpanThrough(centre, normal, new[] { (frames[0].Origin, heights[0]), (frames[1].Origin, heights[1]) });
                var hole = new Line(dowel.From - normal * Added, dowel.To + normal * Added);
                result.Add(new Drilling(beams[0].Id, hole, DowelDiameter));
                result.Add(new Drilling(beams[1].Id, hole, DowelDiameter));
                result.Hardware.Add(new DowelItem(dowel, DowelDiameter, beams[0].Id, beams[1].Id));
            }

            result.Status = JointStatus.Ok;
        }
    }
}
