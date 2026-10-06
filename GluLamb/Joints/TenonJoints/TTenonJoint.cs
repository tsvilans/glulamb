using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Mortise and tenon: one beam (the arm) ends on the side of another (the sill) with a tenon
    /// in a mortise. By default the tenon is thin across the plane of the joint (so a peg goes
    /// through the sill's sides) and runs through the sill. The tenon's long edges are rounded
    /// to the tool radius so it fits the milled mortise. The arm's cut is a Tenon feature (an open
    /// shoulder-and-tenon surface for Brep.Cut), the sill's a Mortise feature (closed), and the
    /// optional peg a Drilling feature plus a dowel.
    /// </summary>
    [JointType("glulamb.t-tenon", Name = "T mortise and tenon", Arity = 2, Topology = JointTopology.T,
        Description = "A beam ending on the side of another with a tenon in a mortise, optionally pegged.")]
    public class TTenonJoint : SillJointBase
    {
        [JointParameter(Description = "Tenon thickness. 0 = a third of the arm, across the thin direction.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Shoulder on each side of the tenon in the other direction.", Unit = "length")]
        public double TenonInset { get; set; } = 20;

        [JointParameter(Description = "Tenon length into the sill, measured square to the sill face. 0 = through the sill.", Unit = "length")]
        public double TenonLength { get; set; } = 0;

        [JointParameter(Description = "Turn the tenon a quarter turn: thin in the plane of the joint instead of across it.")]
        public bool Rotate { get; set; } = false;

        [JointParameter(Description = "Depth of a housing (seat) for the arm's full section in the sill face, with the shoulder at its bottom. 0 = no housing.", Unit = "length")]
        public double HousingDepth { get; set; } = 0;

        [JointParameter(Description = "On an angled arm in a housing, cut the housing wall and the arm's toe square to the sill face on the obtuse side, so no corner is under 90°.")]
        public bool SquareObtuseSide { get; set; } = true;

        [JointParameter(Description = "Clearance on each side of the tenon in the mortise.", Unit = "length")]
        public double Clearance { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of a blind tenon.", Unit = "length")]
        public double EndClearance { get; set; } = 2.0;

        [JointParameter(Description = "Tool diameter: the mortise corners and tenon edges are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Peg diameter. 0 = no peg.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public TTenonJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => IsArmOnSill(condition) ? 0.5 : 0.0;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            if (!SetUp(beams, result)) return;

            var tolerance = context.Tolerance;
            var armDir = ArmFrame.ZAxis;
            var cos = Math.Abs(armDir * Towards);
            if (cos < 0.1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arm is almost parallel to the sill face.");
                return;
            }

            // Tenon section, square to the arm. Thin across the plane of the joint (ArmFrame Y),
            // or in it when rotated.
            var thinAxis = Rotate ? ArmFrame.XAxis : ArmFrame.YAxis;
            var wideAxis = Rotate ? ArmFrame.YAxis : ArmFrame.XAxis;
            var thinExtent = Rotate ? ArmWidth : ArmThickness;
            var wideExtent = Rotate ? ArmThickness : ArmWidth;

            var thickness = TenonThickness > 0 ? TenonThickness : thinExtent / 3.0;
            var width = wideExtent - TenonInset * 2;
            if (width <= 0 || thickness <= 0)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: TenonInset leaves no tenon.");
                return;
            }

            bool through = TenonLength <= 0;
            var length = through ? SillDepth + Added : TenonLength;
            var radius = Math.Min(ToolDiameter * 0.5, Math.Min(thickness, width) * 0.5 - tolerance);

            var section = new Plane(ArmFrame.Origin, wideAxis, thinAxis);
            Curve Profile(double grow)
            {
                var rect = new Rectangle3d(section,
                    new Interval(-width * 0.5 - grow, width * 0.5 + grow),
                    new Interval(-thickness * 0.5 - grow, thickness * 0.5 + grow)).ToNurbsCurve();
                if (radius <= tolerance) return rect;
                return Curve.CreateFilletCornersCurve(rect, radius + grow, tolerance, context.AngleTolerance) ?? rect;
            }

            // A tube along the arm with the given section, long enough to pass through everything
            var reach = (SillDepth + Added * 4 + length) / cos + ArmWidth + ArmThickness;
            Brep Tube(double grow)
            {
                var start = Profile(grow);
                start.Translate(armDir * reach);
                return Surface.CreateExtrusion(start, -armDir * reach * 2)?.ToBrep();
            }

            // Cut a tube to the part between two planes parallel to the sill face; returns the
            // open side surface, and the end curves on both planes
            Brep Between(Brep tube, Plane outer, Plane inner, out Curve outerCurve, out Curve innerCurve)
            {
                outerCurve = innerCurve = null;
                // Brep.Trim keeps the part opposite the cutter normal
                var kept = tube.Trim(new Plane(outer.Origin, outer.ZAxis), tolerance)?.FirstOrDefault();   // keep the sill side of outer
                kept = kept?.Trim(new Plane(inner.Origin, -inner.ZAxis), tolerance)?.FirstOrDefault();     // keep the arm side of inner
                if (kept == null) return null;

                Curve Section(Plane plane)
                {
                    if (!RX.BrepPlane(tube, plane, tolerance, out Curve[] curves, out _) || curves.Length < 1) return null;
                    return Curve.JoinCurves(curves, tolerance).FirstOrDefault();
                }

                outerCurve = Section(outer);
                innerCurve = Section(inner);
                return outerCurve == null || innerCurve == null ? null : kept;
            }

            // Arm: the shoulder, with the tenon standing out of it towards the sill. The shoulder is
            // at the bottom of the housing, or on the sill face without one
            var shoulderPlane = new Plane(SillFace.Origin - Towards * HousingDepth, Towards);
            if (HousingDepth >= length)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the housing is as deep as the tenon.");
                return;
            }
            var tenonEndPlane = new Plane(SillFace.Origin - Towards * length, Towards);
            var tenonSides = Between(Tube(0), shoulderPlane, tenonEndPlane, out Curve shoulderProfile, out Curve tenonEndProfile);
            // On an angled arm in a seat, the seat wall and the arm's toe are cut square to the sill on the obtuse side
            int square = HousingDepth > tolerance && SquareObtuseSide ? ObtuseSide() : 0;

            Brep tenonCutter = null;
            if (tenonSides != null)
            {
                var faces = new List<Brep> { tenonSides };
                var shoulderFaces = SeatEndFaces(shoulderPlane, square, Added, shoulderProfile, tolerance);
                faces.AddRange(shoulderFaces);
                faces.AddRange(Brep.CreatePlanarBreps(tenonEndProfile, tolerance) ?? new Brep[0]);
                tenonCutter = faces.Count == 2 + shoulderFaces.Count && shoulderFaces.Count == (square != 0 ? 2 : 1) ? Brep.JoinBreps(faces, tolerance)?.FirstOrDefault() : null;
            }

            if (tenonCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the tenon.");
                return;
            }

            var tenon = new Tenon { BeamId = Arm.Id, Plane = new Plane(SillFace.Origin, -Towards), Length = length, Width = width, Thickness = thickness };
            tenon.Cutters.Add(tenonCutter);
            tenon.Data.Set("Shoulder", shoulderProfile);
            tenon.Data.Set("Through", through);
            result.Add(tenon);

            // Sill: the mortise, from just outside the sill face to beyond the tenon end
            var mortiseStart = new Plane(SillFace.Origin + Towards * Added, Towards);
            var mortiseDepth = through ? SillDepth + Added * 2 : length + EndClearance;
            var mortiseEnd = new Plane(SillFace.Origin - Towards * mortiseDepth, Towards);
            var mortiseSides = Between(Tube(Clearance), mortiseStart, mortiseEnd, out Curve mortiseTop, out Curve mortiseBottom);

            Brep mortiseBrep = null;
            if (mortiseSides != null)
            {
                var faces = new List<Brep> { mortiseSides };
                faces.AddRange(Brep.CreatePlanarBreps(mortiseTop, tolerance) ?? new Brep[0]);
                faces.AddRange(Brep.CreatePlanarBreps(mortiseBottom, tolerance) ?? new Brep[0]);
                mortiseBrep = faces.Count == 3 ? Brep.JoinBreps(faces, tolerance)?.FirstOrDefault() : null;
            }

            if (mortiseBrep == null || !mortiseBrep.IsSolid)
            {
                result.Status = JointStatus.Partial;
                result.Messages.Add($"{GetType().Name}: failed to create the mortise.");
            }
            else
            {
                var mortise = new Mortise { BeamId = Sill.Id, Plane = SillFace, Depth = mortiseDepth, Width = width + Clearance * 2, Thickness = thickness + Clearance * 2 };
                mortise.Cutters.Add(Mortise.PrepareCutter(mortiseBrep));
                if (RX.BrepPlane(mortiseBrep, SillFace, tolerance, out Curve[] onFace, out _) && onFace.Length > 0)
                    mortise.Data.Set("Profile", Curve.JoinCurves(onFace, tolerance).First());
                mortise.Data.Set("Through", through);
                result.Add(mortise);
            }

            // Housing: the arm's full section let into the sill face (a seat)
            if (HousingDepth > tolerance)
            {
                var housing = SeatHousing(HousingDepth, 0, square, Added, tolerance);
                if (housing == null)
                {
                    result.Status = JointStatus.Partial;
                    result.Messages.Add($"{GetType().Name}: failed to create the housing.");
                }
                else
                    result.Add(housing);
            }

            // Peg through the sill and the tenon, across the tenon's thin direction, in the middle
            // of the part of the tenon inside the sill
            if (DowelDiameter > 0)
            {
                var inside = Math.Min(length, SillDepth);
                var centre = ArmFrame.Origin - armDir * (inside * 0.5 / cos);
                var sillAcross = Rotate ? SillTangent : JointNormal;
                var sillExtent = Rotate ? double.NaN : SillThickness;

                if (Rotate)
                    result.Messages.Add($"{GetType().Name}: with Rotate the peg would run along the sill; no peg made.");
                else
                {
                    var peg = new Line(centre - sillAcross * sillExtent * 0.5, sillAcross, sillExtent);
                    result.Add(new Drilling(Sill.Id, new Line(peg.From - sillAcross * Added, sillAcross, sillExtent + Added * 2), DowelDiameter));
                    result.Add(new Drilling(Arm.Id, new Line(peg.From - sillAcross * Added, sillAcross, sillExtent + Added * 2), DowelDiameter));
                    result.Hardware.Add(new DowelItem(peg, DowelDiameter, Arm.Id, Sill.Id));
                }
            }

            // The arm has to reach the end of the tenon
            ExtendToReach(result, Arm, ArmPart, new[] { tenonEndProfile.PointAtStart }
                .Concat(Enumerable.Range(0, 8).Select(k => tenonEndProfile.PointAtNormalizedLength(k / 8.0))));

            Position = new Plane(SillFace.Origin, SillTangent, JointNormal);
            result.Status = result.Status == JointStatus.Partial ? JointStatus.Partial : JointStatus.Ok;
        }
    }
}
