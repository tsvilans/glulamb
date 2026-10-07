using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// T plate joint: one beam (the arm) ends on the side of another (the sill) and is joined to
    /// it with a slotted-in plate, as in the K plate joint but with a single arm. The plate sits in
    /// the plane of the joint, in a slot through the arm and with a tenon in a slot in the sill,
    /// dowelled through the arm and the sill. Arm end cut as JackRafterCut, slots as Slot, dowel
    /// holes as Drilling, plate and dowels as hardware.
    /// </summary>
    [JointType("glulamb.t-plate", Name = "T plate", Arity = 2, Topology = JointTopology.T,
        Description = "A beam ending on the side of another, joined with a slotted-in plate and dowels.")]
    public class TPlateJoint : SillJointBase
    {
        [JointParameter(Description = "Depth of the plate's tenon into the sill.", Unit = "length")]
        public double PlateDepth { get; set; } = 50.0;

        [JointParameter(Description = "Depth of the tenon slot in the sill.", Unit = "length")]
        public double PlateSlotDepth { get; set; } = 50.0;

        [JointParameter(Description = "Plate thickness.", Unit = "length")]
        public double PlateThickness { get; set; } = 20.0;

        [JointParameter(Description = "Offset of the plate from the arm centreline, across the plane of the joint.", Unit = "length")]
        public double PlateOffset { get; set; } = 0.0;

        [JointParameter(Description = "Extra length of the plate along the arm.", Unit = "length")]
        public double PlateEndOffset { get; set; } = 0.0;

        [JointParameter(Description = "Width of the plate tenon and its slot in the sill, along the sill. 0 = automatic: as wide as the arm plus the tool radius.", Unit = "length")]
        public double TenonWidth { get; set; } = 0;

        [JointParameter(Description = "Shortest length of the plate along each edge of the arm, from the sill face.", Unit = "length")]
        public double MinimumSlotLength { get; set; } = 40;

        [JointParameter(Description = "Tool diameter for slot and plate corner radii.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Distance of the arm dowel from the sill face.", Unit = "length")]
        public double DowelPosition { get; set; } = 40;

        [JointParameter(Description = "Dowel length.", Unit = "length")]
        public double DowelLength { get; set; } = 130;

        [JointParameter(Description = "Dowel diameter.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 5.0;

        [JointParameter(Description = "Clearance on each side of the plate tenon in the sill slot.", Unit = "length")]
        public double ToleranceTenonSide { get; set; } = 0.5;

        [JointParameter(Description = "Clearance at the end of the plate tenon.", Unit = "length")]
        public double ToleranceTenonEnd { get; set; } = 1.5;

        [JointParameter(Description = "Clearance at the end of the plate slot in the arm.", Unit = "length")]
        public double ToleranceSlotEnd { get; set; } = 1.5;

        public TPlateJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => IsArmOnSill(condition) ? 0.5 : 0.0;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            if (!SetUp(beams, result)) return;

            var tolerance = context.Tolerance;
            var armDir = ArmFrame.ZAxis;
            double radius = ToolDiameter * 0.5;

            var platePlane = new Plane(Node + JointNormal * PlateOffset, SillTangent, Towards);
            Position = platePlane;

            var side = new[]
            {
                new Plane(ArmFrame.Origin + ArmFrame.XAxis * ArmWidth * 0.5, ArmFrame.XAxis),
                new Plane(ArmFrame.Origin - ArmFrame.XAxis * ArmWidth * 0.5, ArmFrame.XAxis),
            };

            // Where the arm's edges meet the sill face, as positions along the sill
            var atSill = new Point3d[2];
            for (int i = 0; i < 2; ++i)
                if (!RX.PlanePlanePlane(platePlane, side[i], SillFace, out atSill[i]))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: the arm is parallel to the sill face.");
                    return;
                }

            double SillCoordinate(Point3d p) => (p - Node) * SillTangent;

            // Tenon range along the sill: the arm's footprint, widened so the tool radius fits in
            // the corners between the arm and the sill face
            var range = new double[2];
            for (int i = 0; i < 2; ++i)
            {
                var n = platePlane.Project(side[i].ZAxis); n.Unitize();
                var angle = Vector3d.VectorAngle(n * Towards < 0 ? -n : n, Towards);
                var grow = radius / Math.Tan(Math.Max(angle, 0.01) * 0.5) + radius;
                range[i] = SillCoordinate(atSill[i]) + (SillCoordinate(atSill[i]) >= SillCoordinate(atSill[1 - i]) ? grow : -grow);
            }

            double tMin = Math.Min(range[0], range[1]), tMax = Math.Max(range[0], range[1]);
            if (TenonWidth > 0)
            {
                var middle = (tMin + tMax) * 0.5;
                tMin = middle - TenonWidth * 0.5;
                tMax = middle + TenonWidth * 0.5;
            }

            var tenonSideMin = new Plane(Node + SillTangent * tMin, SillTangent);
            var tenonSideMax = new Plane(Node + SillTangent * tMax, SillTangent);
            var tenonEnd = new Plane(SillFace.Origin - Towards * (PlateDepth - ToleranceTenonEnd), Towards);

            // Plate end along the arm, square to it: past the dowel, and at least
            // MinimumSlotLength along both edges
            var c = ArmFrame.Origin;
            double endDistance = DowelPosition + DowelDiameter * 2 + PlateEndOffset;
            for (int i = 0; i < 2; ++i)
                endDistance = Math.Max(endDistance, (atSill[i] - c) * armDir + MinimumSlotLength);

            var plateEnd = new Plane(c + armDir * endDistance, armDir);

            // Plate outline, going round: arm edge, plate end, other arm edge, sill face, tenon
            int sMin = SillCoordinate(atSill[0]) < SillCoordinate(atSill[1]) ? 0 : 1;
            int sMax = 1 - sMin;

            var planes = new[] { side[sMin], plateEnd, side[sMax], SillFace, tenonSideMax, tenonEnd, tenonSideMin, SillFace };
            var outlinePoints = new List<Point3d>();
            for (int j = 0; j < planes.Length; ++j)
            {
                if (!RX.PlanePlanePlane(platePlane, planes[j], planes[(j + 1) % planes.Length], out Point3d p))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: failed to build the plate outline.");
                    return;
                }
                outlinePoints.Add(p);
            }
            outlinePoints.Add(outlinePoints[0]);

            // Round the inside corners
            Curve outline = new Polyline(outlinePoints).ToNurbsCurve();
            var flags = PlateOutline.InsideCorners(outlinePoints.Take(outlinePoints.Count - 1).ToList(), platePlane.ZAxis);
            var filleted = radius > 0 ? PlateOutline.Round(outlinePoints, flags, radius, tolerance) : null;
            if (filleted != null && RX.CurveSelf(filleted, tolerance).Count == 0)
                outline = filleted;
            if (RX.CurveSelf(outline, tolerance).Count > 0)
                result.Messages.Add($"{GetType().Name}: the plate outline crosses itself; check PlateDepth and TenonWidth.");

            // Dowels: one in the arm, one through the sill and the plate tenon
            var dowelAxes = new List<Line>();

            var armDowelCentre = c + armDir * DowelPosition;
            var armDowel = new Line(armDowelCentre - JointNormal * DowelLength * 0.5, JointNormal, DowelLength);
            result.Add(new Drilling(Arm.Id, new Line(armDowel.From - JointNormal * 10, JointNormal, DowelLength + 20), DowelDiameter));
            result.Hardware.Add(new DowelItem(armDowel, DowelDiameter, Arm.Id));
            dowelAxes.Add(armDowel);

            var sillDowelCentre = Sill.GetPlane(Node + SillTangent * ((tMin + tMax) * 0.5)).Origin;
            var sillDowel = new Line(sillDowelCentre - JointNormal * DowelLength * 0.5, JointNormal, DowelLength);
            result.Add(new Drilling(Sill.Id, new Line(sillDowel.From - JointNormal * 10, JointNormal, DowelLength + 20), DowelDiameter));
            result.Hardware.Add(new DowelItem(sillDowel, DowelDiameter, Sill.Id));
            dowelAxes.Add(sillDowel);

            var neededDepth = Math.Ceiling(SillDepth * 0.5 + DowelDiameter + ToleranceTenonEnd);
            if (PlateDepth < neededDepth || PlateSlotDepth < neededDepth)
                result.Messages.Add($"{GetType().Name}: the plate tenon doesn't reach the sill dowel; " +
                    $"set PlateDepth and PlateSlotDepth to at least {neededDepth} (now {PlateDepth} and {PlateSlotDepth}).");

            // Plate
            var plateGeometry = ExtrudeBetweenFaces(outline, platePlane, PlateThickness, tolerance);
            if (plateGeometry != null)
            {
                var holes = dowelAxes.Select(a => new Cylinder(new Circle(new Plane(a.From, a.Direction), DowelDiameter * 0.5), a.Length).ToBrep(true, true));
                plateGeometry = plateGeometry.Cut(holes, 0.01);
                result.Hardware.Add(new PlateItem
                {
                    Name = "Plate",
                    Thickness = PlateThickness,
                    Plane = platePlane,
                    Outline = outline,
                    Geometry = plateGeometry,
                    BeamIds = new List<string> { Arm.Id, Sill.Id },
                });
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create the plate.");

            // Arm: end cut on the sill face, and the plate slot through its width
            result.Add(new JackRafterCut(Arm.Id, new Plane(SillFace.Origin, -Towards)));

            var slotEnd = new Plane(plateEnd.Origin + armDir * ToleranceSlotEnd, armDir);
            var slotFace = new Plane(SillFace.Origin - Towards * Added, Towards);
            var slotSides = side.Select(s =>
            {
                var outward = (s.Origin - c) * s.ZAxis > 0 ? s.ZAxis : -s.ZAxis;
                return new Plane(s.Origin + outward * Added, s.ZAxis);
            }).ToArray();

            var armSlotOutline = Outline(platePlane, new[] { slotSides[0], slotEnd, slotSides[1], slotFace });
            var armSlot = armSlotOutline == null ? null : ExtrudeBetweenFaces(armSlotOutline, platePlane, PlateThickness, tolerance);
            if (armSlot != null)
            {
                var slot = new Slot { BeamId = Arm.Id, Plane = platePlane, Thickness = PlateThickness, Depth = endDistance + ToleranceSlotEnd };
                slot.Cutters.Add(armSlot);
                slot.Data.Set("PlatePlane", platePlane);
                slot.Data.Set("EndPlane", slotEnd);
                slot.Data.Set("Outline", armSlotOutline);
                result.Add(slot);
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create the plate slot in the arm.");

            // Sill: slot for the plate tenon
            var slotPlane = new Plane(SillFace.Origin + Towards * Added, SillTangent, JointNormal);
            var hw = PlateThickness * 0.5;

            // The slot ends are rounded by the tool, so make the slot longer by the corner radius at
            // each end for the plate's square corners to fit
            var slotRadius = radius > 0 ? Math.Min(radius, hw) : 0;
            var slotMin = tMin - ToleranceTenonSide - slotRadius;
            var slotMax = tMax + ToleranceTenonSide + slotRadius;
            var sillProfile = new Polyline
            {
                slotPlane.PointAt(slotMin - SillCoordinate(slotPlane.Origin), -hw + PlateOffset),
                slotPlane.PointAt(slotMax - SillCoordinate(slotPlane.Origin), -hw + PlateOffset),
                slotPlane.PointAt(slotMax - SillCoordinate(slotPlane.Origin), hw + PlateOffset),
                slotPlane.PointAt(slotMin - SillCoordinate(slotPlane.Origin), hw + PlateOffset),
            };
            sillProfile.Add(sillProfile[0]);

            Curve sillProfileCurve = sillProfile.ToNurbsCurve();
            if (slotRadius > 0)
                sillProfileCurve = Curve.CreateFilletCornersCurve(sillProfileCurve, slotRadius, tolerance, context.AngleTolerance) ?? sillProfileCurve;

            var sillSlot = Surface.CreateExtrusion(sillProfileCurve, -Towards * (PlateSlotDepth + Added))?.ToBrep()?.CapPlanarHoles(tolerance);
            if (sillSlot != null)
            {
                var slot = new Slot { BeamId = Sill.Id, Plane = slotPlane, Thickness = PlateThickness, Depth = PlateSlotDepth };
                slot.Cutters.Add(Slot.PrepareCutter(sillSlot));
                slot.Data.Set("SlotPlane", slotPlane);
                slot.Data.Set("Profile", sillProfileCurve);
                slot.Data.Set("Depth", PlateSlotDepth);
                slot.Data.Set("PlateThickness", PlateThickness);
                result.Add(slot);
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create the slot in the sill.");

            // The arm has to reach the sill face
            ExtendToReach(result, Arm, ArmPart, ArmCornersOn(SillFace));

            result.Status = result.Features.Count == 2 ? JointStatus.Ok : JointStatus.Partial;
        }

        /// <summary>
        /// Closed outline in the plate plane through the intersections of consecutive planes.
        /// </summary>
        private static Curve Outline(Plane plane, Plane[] planes)
        {
            var points = new List<Point3d>();
            for (int j = 0; j < planes.Length; ++j)
            {
                if (!RX.PlanePlanePlane(plane, planes[j], planes[(j + 1) % planes.Length], out Point3d p)) return null;
                points.Add(p);
            }
            points.Add(points[0]);
            return new Polyline(points).ToNurbsCurve();
        }

        /// <summary>
        /// Closed brep from an outline in the plate plane, extruded through the plate thickness.
        /// </summary>
        private static Brep ExtrudeBetweenFaces(Curve outline, Plane plate, double thickness, double tolerance)
        {
            var start = outline.DuplicateCurve();
            start.Translate(-plate.ZAxis * thickness * 0.5);
            var brep = Surface.CreateExtrusion(start, plate.ZAxis * thickness)?.ToBrep()?.CapPlanarHoles(tolerance);
            return brep == null ? null : Slot.PrepareCutter(brep);
        }
    }
}
