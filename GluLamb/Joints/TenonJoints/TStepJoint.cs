using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Step joint (German Versatz): an angled strut (the arm) bearing in a notch in the side of
    /// another beam (the sill), as for braces and struts in timber frames and trusses. In the
    /// plane of the joint, the arm's end is cut to a toe that sits in a notch below the sill
    /// face (or below a shallow seat):
    /// <list type="bullet">
    /// <item>front step (Stirnversatz): the bearing face runs from the front of the arm (where it
    /// meets the sill at the obtuse outside angle) into the sill along the bisector of that
    /// angle, down to StepDepth; the back of the notch runs up to the arm's back edge.</item>
    /// <item>heel step (Fersenversatz): the arm's back (heel) corner sinks HeelDepth into the
    /// sill, with a bearing face square to the arm's axis.</item>
    /// <item>double: both, the heel deeper than the front step.</item>
    /// <item>square front step: the bearing face and the back of the notch at 90° to each other
    /// (two square cuts on the arm), with the notch's long side on the sill face; the depth
    /// sets how the right angle is turned.</item>
    /// </list>
    /// Optionally with a seat (as in glulamb.t-butt, cut square to the sill on the obtuse side)
    /// and a blind tenon along the arm into a mortise. The arm's cut is a StepJoint feature and
    /// the notch a StepJointNotch, after the BTLx processings.
    /// </summary>
    [JointType("glulamb.t-step", Name = "Step joint", Arity = 2, Topology = JointTopology.T,
        Description = "An angled strut bearing in a step notch in the side of another beam, optionally seated and tenoned.")]
    public class TStepJoint : SillJointBase
    {
        [JointParameter(Description = "Step: 0 = front step (Stirnversatz), 1 = heel step (Fersenversatz), 2 = double (both), 3 = square front step (the two notch faces at 90°, easier to cut).")]
        public int StepType { get; set; } = 0;

        [JointParameter(Description = "Depth of the front step below the sill face (or seat), square to it. 0 = StepDepthRatio times the sill depth.", Unit = "length")]
        public double StepDepth { get; set; } = 0;

        [JointParameter(Description = "Depth of the heel below the sill face (or seat), square to it. 0 = the step depth plus 15 for a double step, else the same as StepDepth.", Unit = "length")]
        public double HeelDepth { get; set; } = 0;

        [JointParameter(Description = "With StepDepth 0: the front step depth as a ratio of the sill depth (traditionally 1/6 to 1/4).")]
        public double StepDepthRatio { get; set; } = 0.2;

        [JointParameter(Description = "Depth of a seat for the arm's full section in the sill, mostly for locating it. 0 = none.", Unit = "length")]
        public double SeatDepth { get; set; } = 0;

        [JointParameter(Description = "In a seat, cut the seat wall and the arm's toe square to the sill face on the obtuse side.")]
        public bool SquareObtuseSide { get; set; } = true;

        [JointParameter(Description = "Length of a blind tenon along the arm, measured square to the sill face from the seat (or face). 0 = no tenon.", Unit = "length")]
        public double TenonLength { get; set; } = 0;

        [JointParameter(Description = "Tenon thickness, across the plane of the joint. 0 = a third of the arm.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Shoulder on each side of the tenon in the plane of the joint.", Unit = "length")]
        public double TenonInset { get; set; } = 20;

        [JointParameter(Description = "Clearance on each side of the tenon in the mortise.", Unit = "length")]
        public double Clearance { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of the tenon.", Unit = "length")]
        public double EndClearance { get; set; } = 2.0;

        [JointParameter(Description = "Tool diameter: the tenon edges and mortise corners are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public TStepJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// An arm ending on the side of a sill: above t-butt when the arm is clearly angled
        /// (more than 15° off square), below it otherwise; below t-lap either way.
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (!IsArmOnSill(condition)) return 0;
            var arm = condition.Parts.First(x => JointPartX.IsAtEnd(x.Case)).Direction;
            var sill = condition.Parts.First(x => JointPartX.IsAtMiddle(x.Case)).Direction;
            var angle = Vector3d.VectorAngle(arm, sill);
            if (angle > Math.PI * 0.5) angle = Math.PI - angle;
            return angle < Rhino.RhinoMath.ToRadians(75) ? 0.75 : 0.25;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            if (!SetUp(beams, result)) return;
            var tolerance = context.Tolerance;

            // 2D coordinates in the plane of the joint: x along the sill face towards the front
            // of the arm (the way it leans going in), y out of the sill face
            var d = -ArmFrame.ZAxis;
            // (in the plane of the joint; an arm skewed across the sill is treated as if it were not)
            int obtuse = ObtuseSide();
            var e1 = ArmFrame.XAxis - Towards * (ArmFrame.XAxis * Towards);
            e1.Unitize();
            if (obtuse < 0) e1.Reverse();
            var e2 = Towards;
            var origin = ArmFrame.Origin;
            Point3d P(Point2d p, double y = 0) => origin + e1 * p.X + e2 * p.Y + ArmFrame.YAxis * y;

            var d2 = new Vector2d(d * e1, d * e2);       // into the sill: y < 0
            if (d2.Y > -0.1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arm is almost parallel to the sill face.");
                return;
            }

            var x2 = new Vector2d(ArmFrame.XAxis * e1, ArmFrame.XAxis * e2);
            int frontSide = x2.X >= 0 ? 1 : -1;

            // Where one of the arm's sides (+1 front, -1 back) crosses the line y = level
            Point2d Side(int s, double level)
            {
                var o = new Point2d(x2.X * s * frontSide * ArmWidth * 0.5, x2.Y * s * frontSide * ArmWidth * 0.5);
                var t = (level - o.Y) / d2.Y;
                return o + d2 * t;
            }

            var stepDepth = StepDepth > 0 ? StepDepth : SillDepth * StepDepthRatio;
            var heelDepth = HeelDepth > 0 ? HeelDepth : (StepType == 2 ? stepDepth + 15 : stepDepth);
            bool front = StepType == 0 || StepType == 2 || StepType == 3;
            bool heel = StepType == 1 || StepType == 2;

            var y0 = -SeatDepth;
            bool seated = SeatDepth > tolerance;
            int square = seated && SquareObtuseSide && obtuse != 0 ? 1 : 0;

            var A = Side(1, y0);                       // front edge at the seat bottom (or face)
            var L = Side(-1, y0);                      // back edge
            var Af = square != 0 ? new Point2d(Side(1, 0).X, y0) : A;   // front of the toe, after the square cut

            var profile = new List<Point2d> { new Point2d(L.X - Added, y0), L };
            Point2d B = Point2d.Unset;
            if (front && StepType == 3)
            {
                // Square step: the bearing face and the back of the notch at 90°, so the notch is a
                // right triangle on the seat (or face) between the back edge and the front of the
                // toe; StepDepth turns it. The right angle lies on the circle over that line.
                var mid = new Point2d((L.X + Af.X) * 0.5, y0);
                var r = (Af.X - L.X) * 0.5;
                if (stepDepth >= r)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: a square step can be at most {r:0.#} deep here.");
                    return;
                }
                // Towards the front, so the bearing face is the short one
                B = new Point2d(mid.X + Math.Sqrt(r * r - stepDepth * stepDepth), y0 - stepDepth);
            }
            else if (front)
            {
                // Bisector of the obtuse outside angle at the front: between up the arm and along the face
                var u = new Vector2d(-d2.X, -d2.Y);
                u.Unitize();
                u += new Vector2d(1, 0);
                u.Unitize();
                B = Af - u * (stepDepth / u.Y);
            }

            if (heel)
            {
                var b = -d2.Y;
                var H = L + d2 * (heelDepth / b);
                var n2 = new Vector2d(b, d2.X);         // square to the arm, towards the front, upwards
                n2.Unitize();
                if (n2.Y < 1e-3)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: a heel step needs an angled arm.");
                    return;
                }

                // The heel face rises to the seat (or face), or for a double step to the bottom
                // of the front step, which it then meets along a line parallel to the sill face
                var K = H + n2 * (((front ? B.Y : y0) - H.Y) / n2.Y);
                if (front && (B.Y <= H.Y + tolerance || K.X >= B.X - tolerance))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: the heel must be deeper than the front step, and its face must stay behind it.");
                    return;
                }
                profile.Add(H);
                profile.Add(K);
            }
            if (front) profile.Add(B);
            profile.Add(Af);
            profile.Add(square != 0 ? new Point2d(Af.X, Added) : new Point2d(Af.X + Added, y0));

            // Sanity: the toe has to stay under the arm
            if (profile.Skip(1).Take(profile.Count - 2).Any(p => p.X < L.X - tolerance || p.X > Math.Max(A.X, Af.X) + tolerance))
                result.Messages.Add($"{GetType().Name}: the step runs outside the arm; check StepDepth and HeelDepth.");

            var half = ArmThickness * 0.5;

            // Arm: the profile across the arm (open), or with a tenon, the solid below it minus the tenon
            Brep armCutter;
            Brep tenonPrism = null, mortisePrism = null;
            var deepest = profile.Min(p => p.Y);
            if (TenonLength > 0)
            {
                var tenonEnd = y0 - TenonLength;
                if (tenonEnd > deepest - tolerance)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: the tenon must reach past the step ({-deepest + y0:0.#} below the seat).");
                    return;
                }

                var tw = ArmWidth - TenonInset * 2;
                var tt = TenonThickness > 0 ? TenonThickness : ArmThickness / 3.0;
                if (tw <= 0 || tt <= 0)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: TenonInset leaves no tenon.");
                    return;
                }

                Brep Prism(double grow, double top, double bottom)
                {
                    var rect = new Rectangle3d(ArmFrame, new Interval(-tw * 0.5 - grow, tw * 0.5 + grow), new Interval(-tt * 0.5 - grow, tt * 0.5 + grow)).ToNurbsCurve();
                    var r = Math.Min(ToolDiameter * 0.5, Math.Min(tw, tt) * 0.5 - tolerance);
                    Curve c = r > tolerance ? Curve.CreateFilletCornersCurve(rect, r + grow, tolerance, context.AngleTolerance) ?? rect : rect;
                    // A tube along the arm, cut to the part between the top and bottom planes (both
                    // parallel to the sill face). Brep.Trim keeps the part opposite the plane normal.
                    var span = (top - bottom) / -d2.Y + ArmWidth * 2;
                    var start = c.DuplicateCurve();
                    start.Translate(-d * span);
                    var tube = Surface.CreateExtrusion(start, d * span * 3)?.ToBrep();
                    var kept = tube?.Trim(new Plane(origin + e2 * top, e2), tolerance)?.FirstOrDefault();
                    kept = kept?.Trim(new Plane(origin + e2 * bottom, -e2), tolerance)?.FirstOrDefault();
                    return kept?.CapPlanarHoles(tolerance);
                }

                tenonPrism = Prism(0, Added * 2, tenonEnd);
                mortisePrism = Prism(Clearance, Added, tenonEnd - EndClearance);

                // Wide enough for the tenon, which runs along the arm, sideways as it goes deeper
                var reachX = ArmWidth + (y0 - tenonEnd) * Math.Abs(d2.X / d2.Y);
                var closed = new List<Point2d>(profile)
                {
                    new Point2d(profile.Last().X + reachX, profile.Last().Y),
                    new Point2d(profile.Last().X + reachX, tenonEnd - Added),
                    new Point2d(profile[0].X - reachX, tenonEnd - Added),
                    new Point2d(profile[0].X - reachX, profile[0].Y),
                };
                var below = Extrude(closed, true, half + Added, tolerance);
                var diff = below != null && tenonPrism != null ? Brep.CreateBooleanDifference(Feature.PrepareCutter(below), Feature.PrepareCutter(tenonPrism), tolerance) : null;
                armCutter = diff != null && diff.Length == 1 ? diff[0] : null;
            }
            else
                armCutter = Extrude(profile, false, half + Added, tolerance);

            if (armCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the arm's cut.");
                return;
            }

            var step = new StepJoint { BeamId = Arm.Id, Plane = new Plane(P(Af), e1, ArmFrame.YAxis), Depth = Math.Max(stepDepth, heel ? heelDepth : 0), Width = ArmThickness };
            step.Cutters.Add(armCutter);
            step.Data.Set("StepType", StepType);
            step.Data.Set("StepDepth", front ? stepDepth : 0);
            step.Data.Set("HeelDepth", heel ? heelDepth : 0);
            step.Data.Set("SeatDepth", SeatDepth);
            step.Data.Set("SquareSide", square);
            result.Add(step);

            // Sill: the notch (and seat), closed, as wide as the arm
            var notch = new List<Point2d> { Side(-1, Added) };
            notch.AddRange(profile.Skip(1).Take(profile.Count - 2));
            notch.Add(square != 0 ? new Point2d(Af.X, Added) : Side(1, Added));
            var notchCutter = Extrude(notch, true, half, tolerance);
            if (notchCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the notch.");
                return;
            }

            var sillFeature = new StepJointNotch { BeamId = Sill.Id, Plane = new Plane(P(Af), e1, ArmFrame.YAxis), Depth = Math.Max(stepDepth, heel ? heelDepth : 0) + SeatDepth, Width = ArmThickness };
            sillFeature.Cutters.Add(Feature.PrepareCutter(notchCutter));
            sillFeature.Data.Set("StepType", StepType);
            sillFeature.Data.Set("SeatDepth", SeatDepth);
            result.Add(sillFeature);

            if (mortisePrism != null)
            {
                var mortise = new Mortise { BeamId = Sill.Id, Plane = SillFace, Depth = TenonLength + EndClearance, Width = ArmWidth - TenonInset * 2 + Clearance * 2, Thickness = (TenonThickness > 0 ? TenonThickness : ArmThickness / 3.0) + Clearance * 2 };
                mortise.Cutters.Add(Feature.PrepareCutter(mortisePrism));
                result.Add(mortise);
            }

            // The arm has to reach the bottom of its toe (or tenon)
            var reach = profile.Skip(1).Take(profile.Count - 2).SelectMany(p => new[] { P(p, -half), P(p, half) }).ToList();
            if (TenonLength > 0)
            {
                // The tenon's corners at its end, which lie at different places along the arm
                var toEnd = Transform.ProjectAlong(new Plane(origin + e2 * (y0 - TenonLength), e2), ArmFrame.ZAxis);
                var tt = TenonThickness > 0 ? TenonThickness : ArmThickness / 3.0;
                foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var corner = ArmFrame.PointAt(sx * (ArmWidth * 0.5 - TenonInset), sy * tt * 0.5);
                    corner.Transform(toEnd);
                    reach.Add(corner);
                }
            }
            ExtendToReach(result, Arm, ArmPart, reach);

            // Z: along the arm, the way it goes into the notch
            Position = new Plane(P(Af), e1 - d * (e1 * d), ArmFrame.YAxis);
            if (Position.ZAxis * d < 0) Position = new Plane(Position.Origin, Position.XAxis, -Position.YAxis);
            result.Status = JointStatus.Ok;

            Brep Extrude(List<Point2d> points, bool closed, double halfWidth, double tol)
            {
                var poly = new Polyline(points.Select(p => P(p, -halfWidth)));
                if (closed) poly.Add(poly[0]);
                poly.DeleteShortSegments(tol);
                var srf = Surface.CreateExtrusion(poly.ToNurbsCurve(), ArmFrame.YAxis * halfWidth * 2);
                if (srf == null) return null;
                var brep = srf.ToBrep();
                brep.Faces.SplitKinkyFaces();
                if (closed) brep = brep.CapPlanarHoles(tol);
                return brep;
            }
        }

    }
}
