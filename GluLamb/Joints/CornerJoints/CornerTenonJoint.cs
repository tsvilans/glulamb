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

        [JointParameter(Description = "Flip the joint normal, swapping which side the tenon is on.")]
        public bool Reverse { get; set; } = false;

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
            if (Reverse)
                normal.Reverse();

            var binormal = v0 + v1;
            binormal.Unitize();

            var plane = new Plane(origin, v0, v1);
            var mirrorPlane = new Plane(origin, binormal, normal);

            var in0 = Vector3d.CrossProduct(v0, normal);
            if (in0 * binormal < 0) in0.Reverse();
            in0.Unitize();

            var in1 = Vector3d.CrossProduct(v1, normal);
            if (in1 * binormal < 0) in1.Reverse();
            in1.Unitize();

            var p0 = new Plane(origin + v0 * BackOffset, normal, in0);
            var p1 = new Plane(origin + v1 * BackOffset, normal, in1);

            result.Debug.Add(new Line(origin, binormal, 400));
            result.Debug.Add(new Line(origin, v0, 300));
            result.Debug.Add(new Line(origin, v1, 300));

            var chamfer = new Plane(
                ((origin - v0 * BackOffset) + (origin - v1 * BackOffset)) * 0.5,
                normal,
                Vector3d.CrossProduct(normal, binormal));

            var pout0 = new Plane(p0.Origin - p0.YAxis * (b0.Width * 0.5 - Inset), p0.ZAxis, normal);
            var pout1 = new Plane(p1.Origin - p1.YAxis * (b1.Width * 0.5 - Inset), p1.ZAxis, normal);

            var pout0Added = new Plane(pout0.Origin - p0.YAxis * 10, pout0.XAxis, pout0.YAxis);

            var pin0 = new Plane(p0.Origin + p0.YAxis * (b0.Width * 0.5 - InsetIn), p0.ZAxis, normal);
            var pin1 = new Plane(p1.Origin + p1.YAxis * (b1.Width * 0.5 - InsetIn), p1.ZAxis, normal);

            var pin0Partial = new Plane(p0.Origin + p0.YAxis * (b0.Width * 0.3 - InsetIn), p0.ZAxis, normal);

            var pout1Added = new Plane(pout1.Origin - p1.YAxis * 10, pout1.XAxis, pout1.YAxis);

            var x0 = (b0.Width * 0.5 - Inset) / Math.Cos(angle);
            var z0 = (b1.Width * 0.5 - Inset * 2) * Math.Tan(Math.PI * 0.5 - angle);

            bool ok = true;

            // Tenon 0
            Point3d[] points = new Point3d[10];
            int idx = 0;

            if (BackOffset < Math.Abs(z0))
            {
                ok &= RX.PlanePlanePlane(plane, p0, pout0Added, out points[idx]); idx++;
                ok &= RX.PlanePlanePlane(plane, p0, pout0, out points[idx]); idx++;
                ok &= RX.PlanePlanePlane(plane, pout0, pout1, out points[idx]); idx++;
                ok &= RX.PlanePlanePlane(plane, pout1, p1, out points[idx]); idx++;

                if (Math.Abs(angle) > AngleLimit)
                    ok &= RX.PlanePlanePlane(plane, pin0, p1, out points[idx]);
                else
                    ok &= RX.PlanePlanePlane(plane, pin0Partial, p1, out points[idx]);
                idx++;
            }
            else
            {
                ok &= RX.PlanePlanePlane(plane, p0, pout0Added, out points[idx]); idx++;
                ok &= RX.PlanePlanePlane(plane, pin1, pout0, out points[idx]); idx++;

                if (BackOffset < Math.Abs(x0))
                {
                    ok &= RX.PlanePlanePlane(plane, pout0, chamfer, out points[idx]); idx++;
                    ok &= RX.PlanePlanePlane(plane, chamfer, pout1, out points[idx]); idx++;
                }
                else
                {
                    ok &= RX.PlanePlanePlane(plane, pout0, pout1, out points[idx]); idx++;
                }

                ok &= RX.PlanePlanePlane(plane, pout1, pin0, out points[idx]); idx++;
            }

            ok &= RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;

            points[0] = points[1] - in0 * 20 + v0 * 20;
            points[idx] = points[idx - 1] + binormal * (InsetIn / Math.Cos(angle * 0.5) + 20); idx++;

            Curve tenon0Outline = new Polyline(points.Take(idx)).ToNurbsCurve();
            tenon0Outline = FilletOutline(tenon0Outline, tolerance, context.AngleTolerance, "tenon", result);

            // Mortise 0
            points = new Point3d[10];
            idx = 0;

            if (BackOffset < Math.Abs(z0))
            {
                ok &= RX.PlanePlanePlane(plane, p1, pout1Added, out points[0]); idx++;
                ok &= RX.PlanePlanePlane(plane, p1, pout1, out points[1]); idx++;
                points[0] = points[1] - in1 * 20 + v1 * 20;

                if (Math.Abs(angle) > AngleLimit)
                    ok &= RX.PlanePlanePlane(plane, p1, pin0, out points[idx]);
                else
                    ok &= RX.PlanePlanePlane(plane, p1, pin0Partial, out points[idx]);
                idx++;

                ok &= RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;
            }
            else
            {
                ok &= RX.PlanePlanePlane(plane, pin0, pout1, out points[1]); idx++;
                points[0] = points[idx] - in1 * 20 + v1 * 20; idx++;

                ok &= RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;
            }

            points[idx] = points[idx - 1] + binormal * (InsetIn / Math.Cos(angle * 0.5) + 20); idx++;

            Curve mortise0Outline = new Polyline(points.Take(idx)).ToNurbsCurve();
            mortise0Outline = FilletOutline(mortise0Outline, tolerance, context.AngleTolerance, "mortise", result);

            if (!ok || tenon0Outline == null || mortise0Outline == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create outlines.");
                return;
            }

            var mirror = Transform.Mirror(mirrorPlane);

            var mortise1Outline = mortise0Outline.DuplicateCurve();
            var tenon1Outline = tenon0Outline.DuplicateCurve();
            mortise1Outline.Transform(mirror);
            tenon1Outline.Transform(mirror);

            Position = new Plane(origin, binormal, Vector3d.CrossProduct(normal, binormal));

            var height = Math.Max(CutterHeight, Math.Max(Math.Max(b0.Width, b0.Height), Math.Max(b1.Width, b1.Height)));

            AddContour(result, b0, mortise1Outline, tenon0Outline, normal, plane, height, tolerance);
            AddContour(result, b1, mortise0Outline, tenon1Outline, -normal, plane, height, tolerance);

            int created = result.Features.Count;
            result.Status = created == 2 ? JointStatus.Ok : created == 1 ? JointStatus.Partial : JointStatus.Failed;
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
        /// Fillet the corners of an outline. If filleting fails or makes the outline
        /// self-intersecting (a fillet on a segment shorter than the radius can flip), the
        /// sharp outline is kept instead and a message is added.
        /// </summary>
        private Curve FilletOutline(Curve outline, double tolerance, double angleTolerance, string name, JointResult result)
        {
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
