using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Corner joint with an interlocking tenon and mortise profile, for two beams meeting at
    /// their ends at any angle. Port of CornerJoint3X to the IJoint system: same geometry,
    /// output as FreeContour features carrying the tenon and mortise outlines.
    /// </summary>
    [JointType("glulamb.corner-tenon", Name = "Corner tenon", Arity = 2,
        Description = "Two beam ends meeting at an angle, with an interlocking tenon and mortise.")]
    public class CornerTenonJoint : JointBase
    {
        [JointParameter(Description = "Inset of the outer faces of the tenon.", Unit = "length")]
        public double Inset { get; set; } = 3;

        [JointParameter(Description = "Inset of the inner faces of the tenon.", Unit = "length")]
        public double InsetIn { get; set; } = 15;

        [JointParameter(Description = "Below this angle, the tenon is only cut partway into the inner side.", Unit = "radians")]
        public double AngleLimit { get; set; } = 0.1;

        [JointParameter(Description = "Fillet radius of the outline corners, e.g. the tool radius.", Unit = "length")]
        public double FilletRadius { get; set; } = 11;

        [JointParameter(Description = "Length of the tenon back from the joint position.", Unit = "length")]
        public double BackOffset { get; set; } = 100;

        [JointParameter(Description = "Minimum height of the cutters above and below the joint plane. They are always at least as tall as the deepest beam.", Unit = "length")]
        public double CutterHeight { get; set; } = 120;

        public CornerTenonJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Handles two beam ends meeting at an angle (corner or acute topology). Scores below
        /// CornerLapJoint, so it is only used when asked for by type id.
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 2) return 0;
            if (!JointPartX.IsAtEnd(condition.Parts[0].Case) || !JointPartX.IsAtEnd(condition.Parts[1].Case)) return 0;

            var topology = JointRegistry.Classify(condition, JointX.PerpendicularThreshold);
            return topology == JointTopology.Corner || topology == JointTopology.Acute ? 0.5 : 0.0;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var b0 = beams[0];
            var b1 = beams[1];
            var tolerance = context.Tolerance;

            var v0 = -m_parts[0].Direction;
            var v1 = -m_parts[1].Direction;
            v0.Unitize();
            v1.Unitize();

            var origin = Position.Origin;
            var angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, v0 * -v1)));

            var normal = Vector3d.CrossProduct(v0, v1);
            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are parallel.");
                return;
            }
            // Beam 0 is on top along -normal. Orient the normal with beam 0's up axis, then reverse
            // it if beam 0 should be on top (higher or level, inverted by Flip). Reversing mirrors
            // the joint through its plane.
            if (normal * NearestSectionAxis(b0.GetPlane(m_parts[0].Parameter), normal) < 0)
                normal.Reverse();
            if (TopPart(b0.GetPlane(m_parts[0].Parameter).Origin, b1.GetPlane(m_parts[1].Parameter).Origin, normal, tolerance) == 0)
                normal.Reverse();

            // In-plane widths of the beams, whichever side of each section faces the joint
            AlignSection(b0, b0.GetPlane(m_parts[0].Parameter), v0, normal, out double w0, out _);
            AlignSection(b1, b1.GetPlane(m_parts[1].Parameter), v1, normal, out double w1, out _);

            var binormal = v0 + v1;
            binormal.Unitize();

            var plane = new Plane(origin, v0, v1);

            var in0 = Vector3d.CrossProduct(v0, normal);
            if (in0 * binormal < 0) in0.Reverse();
            in0.Unitize();

            var in1 = Vector3d.CrossProduct(v1, normal);
            if (in1 * binormal < 0) in1.Reverse();
            in1.Unitize();

            result.Debug.Add(new Line(origin, binormal, 400));
            result.Debug.Add(new Line(origin, v0, 300));
            result.Debug.Add(new Line(origin, v1, 300));

            var chamfer = new Plane(
                ((origin - v0 * BackOffset) + (origin - v1 * BackOffset)) * 0.5,
                normal,
                Vector3d.CrossProduct(normal, binormal));

            var side0 = MakeSide(origin, v0, in0, w0, normal);
            var side1 = MakeSide(origin, v1, in1, w1, normal);

            // Which outline shape each beam's end gets. It depends on the other beam's width,
            // and the tenon of a beam and the mortise it receives must use the same shape so
            // that their ends meet.
            bool backBranch0 = BackOffset < Math.Abs((w1 * 0.5 - Inset * 2) * Math.Tan(Math.PI * 0.5 - angle));
            bool backBranch1 = BackOffset < Math.Abs((w0 * 0.5 - Inset * 2) * Math.Tan(Math.PI * 0.5 - angle));

            // Each beam's tenon and the mortise it makes in the other beam, from the actual widths
            // of both. (The original computed one pair and mirrored it across the bisector, which
            // only fits when the beams have the same width.)
            bool ok = true;
            ok &= Outlines(side0, side1, backBranch0, backBranch1, plane, chamfer, binormal, angle, out var tenon0Pts, out var mortise1Pts);
            ok &= Outlines(side1, side0, backBranch1, backBranch0, plane, chamfer, binormal, angle, out var tenon1Pts, out var mortise0Pts);

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create outlines.");
                return;
            }

            var tenon0Outline = FilletOutline(new Polyline(tenon0Pts).ToNurbsCurve(), tolerance, context.AngleTolerance, "tenon 0", result);
            var tenon1Outline = FilletOutline(new Polyline(tenon1Pts).ToNurbsCurve(), tolerance, context.AngleTolerance, "tenon 1", result);
            var mortise0Outline = FilletOutline(new Polyline(mortise0Pts).ToNurbsCurve(), tolerance, context.AngleTolerance, "mortise 0", result);
            var mortise1Outline = FilletOutline(new Polyline(mortise1Pts).ToNurbsCurve(), tolerance, context.AngleTolerance, "mortise 1", result);

            // Both beams have to cover the whole overlap of the two cross-sections
            var overlap = new List<Point3d>();
            foreach (var s0 in new[] { 1, -1 })
                foreach (var s1 in new[] { 1, -1 })
                {
                    var face0 = new Plane(origin + in0 * w0 * 0.5 * s0, in0);
                    var face1 = new Plane(origin + in1 * w1 * 0.5 * s1, in1);
                    if (RX.PlanePlanePlane(plane, face0, face1, out Point3d corner))
                        overlap.Add(corner);
                }

            ExtendToReach(result, b0, m_parts[0], overlap);
            ExtendToReach(result, b1, m_parts[1], overlap);

            Position = new Plane(origin, binormal, Vector3d.CrossProduct(normal, binormal));

            var height = Math.Max(CutterHeight, Math.Max(Math.Max(b0.Width, b0.Height), Math.Max(b1.Width, b1.Height)));

            // Each beam: its own tenon on one side of the joint plane, the mortise for the other
            // beam's tenon on the other side
            AddContour(result, b0, mortise0Outline, tenon0Outline, normal, plane, height, tolerance);
            AddContour(result, b1, mortise1Outline, tenon1Outline, -normal, plane, height, tolerance);

            int created = result.Features.Count;
            result.Status = created == 2 ? JointStatus.Ok : created == 1 ? JointStatus.Partial : JointStatus.Failed;
        }

        /// <summary>
        /// The planes describing one beam's end at the joint.
        /// </summary>
        private struct Side
        {
            public Vector3d V;      // into the beam
            public Vector3d In;     // towards the inside of the corner
            public Plane P;         // across the beam at BackOffset from the joint
            public Plane Pout;      // outer face, inset by Inset
            public Plane PoutAdded; // 10 beyond the outer face
            public Plane Pin;       // inner face, inset by InsetIn
            public Plane PinPartial;
        }

        private Side MakeSide(Point3d origin, Vector3d v, Vector3d inward, double width, Vector3d normal)
        {
            var p = new Plane(origin + v * BackOffset, normal, inward);
            var pout = new Plane(p.Origin - p.YAxis * (width * 0.5 - Inset), p.ZAxis, normal);
            return new Side
            {
                V = v,
                In = inward,
                P = p,
                Pout = pout,
                PoutAdded = new Plane(pout.Origin - p.YAxis * 10, pout.XAxis, pout.YAxis),
                Pin = new Plane(p.Origin + p.YAxis * (width * 0.5 - InsetIn), p.ZAxis, normal),
                PinPartial = new Plane(p.Origin + p.YAxis * (width * 0.3 - InsetIn), p.ZAxis, normal),
            };
        }

        /// <summary>
        /// Outline of beam a's tenon, and of the mortise it makes in beam b, in the joint plane.
        /// backA and backB pick the outline shape at each beam's end; the mortise in b must use
        /// the same shape as b's own tenon so that the two outlines of beam b meet.
        /// </summary>
        private bool Outlines(Side a, Side b, bool backA, bool backB, Plane plane, Plane chamfer, Vector3d binormal, double angle,
            out Point3d[] tenon, out Point3d[] mortise)
        {
            bool ok = true;
            var extension = binormal * (InsetIn / Math.Cos(angle * 0.5) + 20);
            var inner = Math.Abs(angle) > AngleLimit ? a.Pin : a.PinPartial;

            // Tenon of a
            var points = new List<Point3d>();
            Point3d pt;
            if (backA)
            {
                ok &= RX.PlanePlanePlane(plane, a.P, a.Pout, out pt); points.Add(pt);
                ok &= RX.PlanePlanePlane(plane, a.Pout, b.Pout, out pt); points.Add(pt);
                ok &= RX.PlanePlanePlane(plane, b.Pout, b.P, out pt); points.Add(pt);
                ok &= RX.PlanePlanePlane(plane, inner, b.P, out pt); points.Add(pt);
            }
            else
            {
                ok &= RX.PlanePlanePlane(plane, b.Pin, a.Pout, out pt); points.Add(pt);

                // Only chamfer the outer corner if the chamfer actually cuts it
                ok &= RX.PlanePlanePlane(plane, a.Pout, b.Pout, out Point3d outerCorner);
                ok &= RX.PlanePlanePlane(plane, b.Pout, a.Pin, out Point3d outerEnd);

                if (RX.PlanePlanePlane(plane, a.Pout, chamfer, out Point3d chamfer0) &&
                    RX.PlanePlanePlane(plane, chamfer, b.Pout, out Point3d chamfer1) &&
                    IsBetween(chamfer0, points[points.Count - 1], outerCorner) &&
                    IsBetween(chamfer1, outerCorner, outerEnd) &&
                    chamfer0.DistanceTo(chamfer1) > FilletRadius * 2)
                {
                    points.Add(chamfer0);
                    points.Add(chamfer1);
                }
                else
                    points.Add(outerCorner);

                points.Add(outerEnd);
            }

            ok &= RX.PlanePlanePlane(plane, a.Pin, b.Pin, out pt); points.Add(pt);
            points.Insert(0, points[0] - a.In * 20 + a.V * 20);
            points.Add(points[points.Count - 1] + extension);
            tenon = points.ToArray();

            // Mortise in b
            points = new List<Point3d>();
            if (backB)
            {
                ok &= RX.PlanePlanePlane(plane, b.P, b.Pout, out pt); points.Add(pt);
                ok &= RX.PlanePlanePlane(plane, b.P, inner, out pt); points.Add(pt);
            }
            else
            {
                ok &= RX.PlanePlanePlane(plane, a.Pin, b.Pout, out pt); points.Add(pt);
            }

            ok &= RX.PlanePlanePlane(plane, a.Pin, b.Pin, out pt); points.Add(pt);
            points.Insert(0, points[0] - b.In * 20 + b.V * 20);
            points.Add(points[points.Count - 1] + extension);
            mortise = points.ToArray();

            return ok;
        }

        private void AddContour(JointResult result, Beam beam, Curve mortise, Curve tenon, Vector3d up, Plane plane, double height, double tolerance)
        {
            var cutters = ConstructGeometry(mortise, tenon, up, height, tolerance);
            if (cutters == null || cutters.Length < 1)
            {
                result.Messages.Add($"{GetType().Name}: failed to create cutter for beam {beam.Id}.");
                return;
            }

            result.Add(new FreeContour
            {
                BeamId = beam.Id,
                Plane = plane,
                Cutters = cutters.ToList(),
                Contours = new List<Curve> { tenon.DuplicateCurve(), mortise.DuplicateCurve() }
            });
        }

        /// <summary>
        /// True if pt lies strictly between a and b, measured along the line from a to b.
        /// </summary>
        private static bool IsBetween(Point3d pt, Point3d a, Point3d b)
        {
            var ab = b - a;
            var lengthSquared = ab.SquareLength;
            if (lengthSquared < 1e-12) return false;
            var t = ((pt - a) * ab) / lengthSquared;
            return t > 1e-6 && t < 1 - 1e-6;
        }

        /// <summary>
        /// Fillet the corners of an outline. If filleting fails or makes the outline
        /// self-intersecting (a fillet on a segment shorter than the radius can flip), the
        /// sharp outline is kept instead and a message is added.
        /// </summary>
        private Curve FilletOutline(Curve outline, double tolerance, double angleTolerance, string name, JointResult result)
        {
            result.Debug.Add(outline.DuplicateCurve());

            if (RX.CurveSelf(outline, tolerance).Count > 0)
                result.Messages.Add($"{GetType().Name}: {name} outline is self-intersecting before filleting; check Inset, InsetIn and BackOffset.");

            if (FilletRadius <= 0) return outline;

            var filleted = Curve.CreateFilletCornersCurve(outline, FilletRadius, tolerance, angleTolerance);
            if (filleted == null)
            {
                result.Messages.Add($"{GetType().Name}: filleting the {name} outline failed; using sharp corners.");
                return outline;
            }

            if (RX.CurveSelf(filleted, tolerance).Count > 0)
            {
                result.Messages.Add($"{GetType().Name}: filleting made the {name} outline self-intersect; using sharp corners.");
                return outline;
            }

            return filleted;
        }

        private static Brep[] ConstructGeometry(Curve c0, Curve c1, Vector3d up, double height, double tolerance)
        {
            var curves = new Curve[] { c0, c1 };
            var trims = new Curve[2];

            var res = RX.CurveCurve(curves[0], curves[1], tolerance, tolerance);

            var spans = new List<Interval>[] { new List<Interval>(), new List<Interval>() };
            for (int i = 0; i < res.Count; ++i)
            {
                spans[0].Add(res[i].OverlapA);
                spans[1].Add(res[i].OverlapB);
            }

            for (int i = 0; i < 2; ++i)
            {
                var subs = Utility.SpanSubtract(curves[i].Domain, spans[i]);
                if (subs.Count < 1)
                {
                    trims[i] = curves[i].DuplicateCurve();
                    continue;
                }

                var longest = subs.OrderBy(x => x.Length).Last();
                trims[i] = curves[i].Trim(longest);
            }

            var faces = new List<Brep>();

            var middle = Brep.CreatePlanarBreps(trims, tolerance);
            if (middle != null && middle.Length > 0)
                faces.AddRange(middle);

            faces.Add(Extrusion.CreateExtrusion(curves[0], up * height).ToBrep());
            faces.Add(Extrusion.CreateExtrusion(curves[1], up * -height).ToBrep());

            return Brep.JoinBreps(faces, tolerance);
        }
    }
}
