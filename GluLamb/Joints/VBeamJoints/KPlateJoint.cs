using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Collections;
using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// K joint: two arm beams end on the side of a sill beam and are joined by a slotted-in steel
    /// plate, dowelled through the arms and the sill. The plate's tenon sits in a slot in the sill.
    /// Port of KJoint_Plate6 to the IJoint system: same geometry; end and seam cuts as
    /// JackRafterCut features, plate slots as Slot features, dowel holes as Drilling features,
    /// and the plate and dowels as hardware.
    /// </summary>
    [JointType("glulamb.k-plate", Name = "K plate", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beams ending on the side of a third, joined with a slotted-in plate and dowels.")]
    public class KPlateJoint : JointBase
    {
        [JointParameter(Description = "Depth of the plate's tenon into the sill.", Unit = "length")]
        public double PlateDepth { get; set; } = 50.0;

        [JointParameter(Description = "Depth of the tenon slot in the sill.", Unit = "length")]
        public double PlateSlotDepth { get; set; } = 50.0;

        [JointParameter(Description = "Plate thickness.", Unit = "length")]
        public double PlateThickness { get; set; } = 20.0;

        [JointParameter(Description = "Offset of the plate from the arm centrelines, along the plate normal.", Unit = "length")]
        public double PlateOffset { get; set; } = 0.0;

        [JointParameter(Description = "Extra length of the plate along the arms.", Unit = "length")]
        public double PlateEndOffset { get; set; } = 0.0;

        [JointParameter(Description = "Tool diameter for slot and plate corner radii.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Distance of the arm dowels from the sill face.", Unit = "length")]
        public double DowelPosition { get; set; } = 40;

        [JointParameter(Description = "Dowel length.", Unit = "length")]
        public double DowelLength { get; set; } = 130;

        [JointParameter(Description = "Depth of the drilled dowel holes in the arms.", Unit = "length")]
        public double DowelDrillDepth { get; set; } = 270;

        [JointParameter(Description = "Dowel diameter.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 5.0;

        [JointParameter(Description = "Clearance on each side of the plate tenon in the sill slot.", Unit = "length")]
        public double ToleranceTenonSide { get; set; } = 0.5;

        [JointParameter(Description = "Clearance at the end of the plate tenon.", Unit = "length")]
        public double ToleranceTenonEnd { get; set; } = 1.5;

        [JointParameter(Description = "Clearance at the end of the plate slots in the arms.", Unit = "length")]
        public double ToleranceSlotEnd { get; set; } = 1.5;

        [JointParameter(Description = "Width of the plate tenon and its slot in the sill, along the sill. 0 = automatic: just wide enough for the arms and the tool radius. (PlateWidth in KJoint_Plate4.)", Unit = "length")]
        public double TenonWidth { get; set; } = 0;

        [JointParameter(Description = "Shortest length of the plate and its slots along each edge of an arm, measured from the sill face.", Unit = "length")]
        public double MinimumSlotLength { get; set; } = 40;

        [JointParameter(Description = "Insert the plate from one direction (slot ends parallel), instead of along each arm.")]
        public bool SingleInsertionDirection { get; set; } = true;

        [JointParameter(Description = "How the arms meet: 0 split down the seam, -1 arm 0 into the side of arm 1, 1 arm 1 into arm 0.")]
        public int Mode { get; set; } = 0;

        // State for one Construct call. Index 0 and 1 are the arms, 2 is the sill.
        protected Beam[] Beams;
        protected JointPartX[] KParts;
        protected Vector3d[] BeamDirections;
        protected double[] ArmWidths;
        protected double[] ArmHeights;
        protected Plane[] BeamPlanes;
        protected Plane KPlane;
        protected Vector3d VSum;
        protected Vector3d InsertionVector;
        protected Plane SillPlane;
        protected Plane SillPlatePlane;
        protected Plane PlatePlane;
        protected Plane[] PlateFacePlanes;
        protected Plane[] SeamPlanes;
        protected Plane[] OutsidePlanes;
        protected Plane[] EndPlanes;
        protected Plane[] TenonSidePlanes;
        protected Plane[] DowelOffsetPlanes;
        protected List<Line> PlateDowelAxes;
        protected List<object> Debug;

        public KPlateJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Two parts at beam ends (the arms) and one in the middle of a beam (the sill).
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 3) return 0;
            var middle = condition.Parts.Where(x => JointPartX.IsAtMiddle(x.Case)).ToList();
            if (middle.Count != 1) return 0.0;
            // The arms must be in one plane with the sill; otherwise it is a post (glulamb.post-beams)
            var ends = condition.Parts.Where(x => JointPartX.IsAtEnd(x.Case)).ToList();
            return PostBeamsJoint.ArmsInPlane(middle[0].Direction, ends[0].Direction, ends[1].Direction) ? 1.0 : 0.0;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int sill = Enumerable.Range(0, 3).FirstOrDefault(i => JointPartX.IsAtMiddle(m_parts[i].Case));
            var arms = Enumerable.Range(0, 3).Where(i => i != sill).ToArray();
            if (!JointPartX.IsAtMiddle(m_parts[sill].Case) || arms.Any(i => !JointPartX.IsAtEnd(m_parts[i].Case)))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: needs two beams ending on the side of a third.");
                return;
            }

            ConstructK(new[] { beams[arms[0]], beams[arms[1]], beams[sill] },
                new[] { m_parts[arms[0]], m_parts[arms[1]], m_parts[sill] }, context, result);
        }

        /// <summary>
        /// Build the K joint for arms 0 and 1 and the sill (index 2).
        /// </summary>
        protected void ConstructK(Beam[] beams, JointPartX[] parts, IJointContext context, JointResult result)
        {
            Debug = result.Debug;
            Beams = beams.ToArray();
            KParts = parts.ToArray();
            PlateDowelAxes = new List<Line>();

            KPlane = Beams[2].GetPlane(KParts[2].Parameter);
            BeamDirections = new Vector3d[2];
            BeamPlanes = new Plane[2];

            // Arm directions, pointing from the node into each arm
            for (int i = 0; i < 2; ++i)
            {
                var d0 = Beams[i].Centreline.PointAt(Beams[i].Centreline.Domain.Mid) - KPlane.Origin;
                BeamPlanes[i] = Beams[i].GetPlane(KParts[i].Parameter);
                BeamDirections[i] = BeamPlanes[i].ZAxis * d0 < 0 ? -BeamPlanes[i].ZAxis : BeamPlanes[i].ZAxis;
            }

            VSum = BeamDirections[0] + BeamDirections[1];
            VSum.Unitize();

            // Frame the sill and the arms relative to the joint, not to their own section
            // orientations: Y is the section axis closest to the normal of the plane of the joint,
            // so the joint works whichever side of each section faces the others.
            var kNormal = Vector3d.CrossProduct(KPlane.ZAxis, VSum);
            if (!kNormal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arms are parallel to the sill.");
                return;
            }

            KPlane = AlignSection(Beams[2], KPlane, KPlane.ZAxis, kNormal, out double width, out _);

            ArmWidths = new double[2];
            ArmHeights = new double[2];
            for (int i = 0; i < 2; ++i)
                BeamPlanes[i] = AlignSection(Beams[i], BeamPlanes[i], BeamDirections[i], KPlane.YAxis, out ArmWidths[i], out ArmHeights[i]);

            InsertionVector = -KPlane.Project(VSum);
            InsertionVector.Unitize();

            OrderArms();

            // Plate plane axis: the sill's cross-section axis towards the arms
            var xaxis = KPlane.XAxis * VSum < 0 ? -KPlane.XAxis : KPlane.XAxis;

            Position = new Plane(KPlane.Origin, xaxis, KPlane.ZAxis);

            // SillPlane is the face of the sill where the arms meet it
            SillPlane = new Plane(KPlane.Origin + xaxis * width * 0.5, KPlane.ZAxis, KPlane.YAxis);
            if (SillPlane.ZAxis * VSum < 0)
                SillPlane = new Plane(SillPlane.Origin, -SillPlane.XAxis, SillPlane.YAxis);

            CreatePlatePlanes(KPlane.Origin, InsertionVector, BeamDirections[0]);

            // SillPlatePlane: on the sill face, aligned with the plate normal
            SillPlatePlane = new Plane(SillPlane.Origin, SillPlane.Project(PlatePlane.ZAxis), SillPlane.XAxis);
            if (SillPlatePlane.ZAxis * VSum < 0)
                SillPlatePlane = new Plane(SillPlatePlane.Origin, -SillPlatePlane.XAxis, SillPlatePlane.YAxis);

            if (RX.PlanePlanePlane(SillPlane, PlatePlane, KPlane, out Point3d sillPlateOrigin))
                SillPlatePlane.Origin = sillPlateOrigin;

            // Dowel offset planes, on which the arm dowels lie
            DowelOffsetPlanes = new Plane[2];
            double[] ratios, dowelPlaneOffsets;
            switch (Mode)
            {
                case -1:
                    ratios = new[] { 1.0, 0.0 };
                    dowelPlaneOffsets = new[] { DowelPosition + DowelDiameter * 1.5, DowelPosition };
                    break;
                case 1:
                    ratios = new[] { 0.0, 1.0 };
                    dowelPlaneOffsets = new[] { DowelPosition, DowelPosition + DowelDiameter * 1.5 };
                    break;
                default:
                    ratios = new[] { 0.0, 0.0 };
                    dowelPlaneOffsets = new[] { DowelPosition, DowelPosition };
                    break;
            }

            for (int i = 0; i < 2; ++i)
                DowelOffsetPlanes[i] = new Plane(SillPlane.Origin + xaxis * dowelPlaneOffsets[i], SillPlane.XAxis, SillPlane.YAxis);

            // End cuts of the arms on the sill face
            for (int i = 0; i < 2; ++i)
            {
                var res = RX.CurvePlane(Beams[i].Centreline, SillPlane, 0.01);
                var xpt = res != null && res.Count > 0 ? res[0].PointA : SillPlane.ClosestPoint(BeamPlanes[i].Origin);
                var cutPlane = new Plane(xpt, SillPlane.XAxis, SillPlane.YAxis);

                AddEndCut(result, i, cutPlane, $"EndCut_{Short(Beams[2])}");

                // The arm has to reach the sill face across its whole section
                var project = Transform.ProjectAlong(SillPlane, BeamDirections[i]);
                var corners = new[] { new Point2d(-1, -1), new Point2d(1, -1), new Point2d(1, 1), new Point2d(-1, 1) }
                    .Select(c => BeamPlanes[i].PointAt(c.X * ArmWidths[i] * 0.5, c.Y * ArmHeights[i] * 0.5))
                    .Select(p => { p.Transform(project); return p; });
                ExtendToReach(result, Beams[i], KParts[i], corners);
            }

            // Seam and outside planes of the arms
            SeamPlanes = new[]
            {
                new Plane(BeamPlanes[0].Origin + BeamPlanes[0].XAxis * ArmWidths[0] * 0.5, BeamPlanes[0].ZAxis, BeamPlanes[0].YAxis),
                new Plane(BeamPlanes[1].Origin - BeamPlanes[1].XAxis * ArmWidths[1] * 0.5, BeamPlanes[1].ZAxis, BeamPlanes[1].YAxis),
            };

            OutsidePlanes = new[]
            {
                new Plane(BeamPlanes[0].Origin - BeamPlanes[0].XAxis * ArmWidths[0] * 0.5, BeamPlanes[0].ZAxis, BeamPlanes[0].YAxis),
                new Plane(BeamPlanes[1].Origin + BeamPlanes[1].XAxis * ArmWidths[1] * 0.5, BeamPlanes[1].ZAxis, BeamPlanes[1].YAxis),
            };

            CheckSides();

            // End planes of the plate along the arms
            EndPlanes = new Plane[2];
            double[] endOffset;
            switch (Mode)
            {
                case -1:
                    endOffset = SingleInsertionDirection ? new[] { DowelDiameter, DowelDiameter } : new[] { DowelDiameter * 2, DowelDiameter * 3 };
                    break;
                case 1:
                    endOffset = SingleInsertionDirection ? new[] { DowelDiameter * 2, DowelDiameter } : new[] { DowelDiameter * 3, DowelDiameter * 2 };
                    break;
                default:
                    endOffset = new[] { DowelDiameter * 2, DowelDiameter * 2 };
                    break;
            }

            int sign = -1;
            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;

                var endPt = BeamPlanes[i].Origin;
                endPt.Transform(DowelOffsetPlanes[i].ProjectAlongVector(BeamPlanes[i].ZAxis));
                endPt = endPt + BeamPlanes[i].ZAxis * (endOffset[i] + PlateEndOffset);
                endPt.Transform(OutsidePlanes[i].ProjectAlongVector(OutsidePlanes[i].ZAxis));

                EndPlanes[i] = SingleInsertionDirection
                    ? new Plane(endPt, PlatePlane.XAxis * -sign, PlatePlane.ZAxis)
                    : new Plane(endPt, PlatePlane.Project(-BeamPlanes[i].XAxis), PlatePlane.ZAxis);

                // The end plane is placed from the outside edge of the arm. With a single insertion
                // direction it isn't square to the arm, so on the seam side it can end up close to,
                // or even below, the sill face. And when the arms are close together, their seam
                // faces only meet some way up the arms. In both cases the plate outline would cross
                // itself and the plate come out inside-out, so move the end plane out until each
                // edge is long enough and the plate reaches past the point where the seams meet.
                double seamApex = 0;
                if (RX.PlanePlanePlane(PlatePlane, SeamPlanes[0], SeamPlanes[1], out Point3d apex) &&
                    RX.PlanePlanePlane(PlatePlane, SeamPlanes[i], SillPlane, out Point3d seamAtSill))
                    seamApex = Math.Max(0, (apex - seamAtSill) * BeamDirections[i]);

                var neededSeam = Math.Max(MinimumSlotLength, seamApex > 0 ? seamApex + ToolDiameter : 0);

                for (int iteration = 0; iteration < 8; ++iteration)
                {
                    var deficit = Math.Max(
                        neededSeam - SlotEdgeLength(i, SeamPlanes[i]),
                        MinimumSlotLength - SlotEdgeLength(i, OutsidePlanes[i]));
                    if (double.IsNaN(deficit) || deficit <= 0) break;

                    // Moving the plane by d along its normal moves its edge points by d / cos along the arm
                    var n = EndPlanes[i].ZAxis * BeamDirections[i] < 0 ? -EndPlanes[i].ZAxis : EndPlanes[i].ZAxis;
                    EndPlanes[i].Origin = EndPlanes[i].Origin + n * ((deficit + 0.5) * Math.Max(n * BeamDirections[i], 0.05));
                }
            }

            // How the arms meet each other
            switch (Mode)
            {
                case -1: // arm 0 into the side of arm 1
                    AddEndCut(result, 0, SeamPlanes[1], $"EndCut_{Short(Beams[1])}");
                    break;
                case 1: // arm 1 into the side of arm 0
                    AddEndCut(result, 1, SeamPlanes[0], $"EndCut_{Short(Beams[0])}");
                    break;
                default: // split down the seam
                    if (RX.PlanePlane(SeamPlanes[0], SeamPlanes[1], out Line seam))
                    {
                        var seamPlane = new Plane(seam.From, seam.Direction, VSum);
                        AddEndCut(result, 0, seamPlane, $"EndCut_{Short(Beams[1])}");
                        AddEndCut(result, 1, seamPlane, $"EndCut_{Short(Beams[0])}");
                    }
                    break;
            }

            // Plate slots in the arms, tenon slot in the sill
            for (int i = 0; i < 2; ++i)
                CreatePlateSlot(result, i);

            CreateSillCutter(result);

            // Arm dowels
            for (int i = 0; i < 2; ++i)
            {
                var dx = RX.CurvePlane(Beams[i].Centreline, DowelOffsetPlanes[i], 0.01);
                if (dx == null || dx.Count < 1)
                {
                    result.Messages.Add($"{GetType().Name}: arm {i} does not reach the dowel position.");
                    continue;
                }

                // Frame the arm relative to the joint, so the dowel runs across the plate whichever
                // way the arm's section is oriented
                var dowelPlane = AlignSection(Beams[i], Beams[i].GetPlane(dx[0].PointA), BeamDirections[i], KPlane.YAxis, out _, out _);
                var rotPlane = new Plane(dowelPlane.Origin, dowelPlane.ZAxis, dowelPlane.YAxis);

                if (!RX.PlanePlane(rotPlane, DowelOffsetPlanes[i], out Line xpp)) continue;

                var dowelVector = xpp.Direction;
                dowelVector.Unitize();
                if (dowelVector * dowelPlane.YAxis < 0) dowelVector.Reverse();

                // Blend between the dowel vector and the beam's Y axis
                dowelVector = dowelVector + ratios[i] * (dowelPlane.YAxis - dowelVector);
                dowelVector.Unitize();

                var start = dowelPlane.Origin - dowelVector * DowelLength * 0.5;
                result.Add(new Drilling(Beams[i].Id, new Line(start - dowelVector * 10, dowelVector, DowelDrillDepth + 20), DowelDiameter));

                var dowelAxis = new Line(start, dowelVector, DowelLength);
                result.Hardware.Add(new DowelItem(dowelAxis, DowelDiameter, Beams[i].Id));

                var plateDowelAxis = dowelAxis;
                plateDowelAxis.Transform(Transform.Translation(-BeamDirections[i] * 0.5));
                PlateDowelAxes.Add(plateDowelAxis);
            }

            // Dowel through the sill and the plate tenon
            var portalPoint = (TenonSidePlanes[0].Origin + TenonSidePlanes[1].Origin) * 0.5;
            portalPoint = Beams[2].GetPlane(portalPoint).Origin;
            var portalStart = portalPoint - KPlane.YAxis * DowelLength * 0.5;

            // The sill dowel sits on the sill's centreline, so the plate tenon has to reach past it
            var toCentreline = (portalPoint - SillPlatePlane.Origin) * InsertionVector;
            var neededDepth = Math.Ceiling(toCentreline + DowelDiameter + ToleranceTenonEnd);
            if (PlateDepth < neededDepth || PlateSlotDepth < neededDepth)
                result.Messages.Add($"{GetType().Name}: the plate tenon doesn't reach the sill dowel; " +
                    $"set PlateDepth and PlateSlotDepth to at least {neededDepth} (now {PlateDepth} and {PlateSlotDepth}).");

            result.Add(new Drilling(Beams[2].Id, new Line(portalStart - KPlane.YAxis * 10, KPlane.YAxis, DowelLength + 20), DowelDiameter));
            var portalAxis = new Line(portalStart, KPlane.YAxis, DowelLength);
            result.Hardware.Add(new DowelItem(portalAxis, DowelDiameter, Beams[2].Id));
            PlateDowelAxes.Add(portalAxis);

            // The plate
            var plate = CreatePlate(context.Tolerance);
            if (plate != null)
                result.Hardware.Add(plate);
            else
                result.Messages.Add($"{GetType().Name}: failed to create the plate.");

            result.Status = result.Features.Count == 3 ? JointStatus.Ok : JointStatus.Partial;
        }

        /// <summary>
        /// Length of an arm's plate slot along one of its edges (the edge where sidePlane meets
        /// the plate plane), from the sill face to the end plane, measured along the arm.
        /// </summary>
        protected double SlotEdgeLength(int arm, Plane sidePlane)
        {
            if (!RX.PlanePlanePlane(PlatePlane, sidePlane, EndPlanes[arm], out Point3d end) ||
                !RX.PlanePlanePlane(PlatePlane, sidePlane, SillPlane, out Point3d sill))
                return double.NaN;
            return (end - sill) * BeamDirections[arm];
        }

        protected static string Short(Beam beam) => beam.Id.Length > 8 ? beam.Id.Substring(0, 8) : beam.Id;

        /// <summary>
        /// End cut of an arm, oriented so that it removes the part beyond the arm's end.
        /// </summary>
        protected void AddEndCut(JointResult result, int arm, Plane plane, string name)
        {
            var normal = plane.ZAxis;
            if (normal * BeamDirections[arm] > 0) normal.Reverse(); // BeamDirections point into the arm

            var cut = new JackRafterCut(Beams[arm].Id, new Plane(plane.Origin, normal));
            cut.Data.Set("Name", name);
            cut.Data.Set("Plane", plane);
            result.Add(cut);
        }

        protected void CreatePlatePlanes(Point3d origin, Vector3d xaxis, Vector3d yaxis)
        {
            PlatePlane = new Plane(origin, xaxis, yaxis);
            PlatePlane.Origin = PlatePlane.Origin + PlatePlane.ZAxis * PlateOffset;

            PlateFacePlanes = new[]
            {
                new Plane(PlatePlane.Origin + PlatePlane.ZAxis * PlateThickness * 0.5, PlatePlane.XAxis, PlatePlane.YAxis),
                new Plane(PlatePlane.Origin - PlatePlane.ZAxis * PlateThickness * 0.5, PlatePlane.XAxis, PlatePlane.YAxis),
            };
        }

        /// <summary>
        /// Order the arms so that seam and outside planes come out on the right sides.
        /// </summary>
        protected void OrderArms()
        {
            var xside = VSum * KPlane.XAxis < 0 ? -1 : 1;

            if ((BeamDirections[0] * KPlane.ZAxis < BeamDirections[1] * KPlane.ZAxis && xside > 0) ||
                (BeamDirections[0] * KPlane.ZAxis > BeamDirections[1] * KPlane.ZAxis && xside < 0))
            {
                (BeamDirections[0], BeamDirections[1]) = (BeamDirections[1], BeamDirections[0]);
                (BeamPlanes[0], BeamPlanes[1]) = (BeamPlanes[1], BeamPlanes[0]);
                (Beams[0], Beams[1]) = (Beams[1], Beams[0]);
                (KParts[0], KParts[1]) = (KParts[1], KParts[0]);
            }
        }

        protected Plane[] ArmPlanesForMode() => Mode switch
        {
            1 => new[] { OutsidePlanes[0], SeamPlanes[0], OutsidePlanes[1] },
            -1 => new[] { OutsidePlanes[1], SeamPlanes[1], OutsidePlanes[0] },
            _ => new[] { OutsidePlanes[0], OutsidePlanes[1] },
        };

        /// <summary>
        /// Find the sides of the plate tenon, wide enough for the tool radius at the corners.
        /// </summary>
        protected void CheckSides()
        {
            double min = double.MaxValue, max = double.MinValue;
            Vector3d minNormal = Vector3d.Unset, maxNormal = Vector3d.Unset;
            var sillNormal = PlatePlane.Project(SillPlane.ZAxis); sillNormal.Unitize();

            var xPlanes = ArmPlanesForMode();
            double toolRadius = ToolDiameter * 0.5;

            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    RX.PlanePlanePlane(PlateFacePlanes[i], SillPlane, xPlanes[j], out Point3d pt);
                    Debug.Add(pt);
                    SillPlatePlane.RemapToPlaneSpace(pt, out Point3d local);

                    if (local.Y < min) { min = local.Y; minNormal = xPlanes[j].ZAxis; }
                    if (local.Y > max) { max = local.Y; maxNormal = xPlanes[j].ZAxis; }
                }
            }

            minNormal = PlatePlane.Project(minNormal); minNormal.Unitize();
            maxNormal = PlatePlane.Project(maxNormal); maxNormal.Unitize();

            if (minNormal * sillNormal < 0) minNormal.Reverse();
            if (maxNormal * sillNormal < 0) maxNormal.Reverse();

            double minAngle = Math.Acos(Math.Max(-1, Math.Min(1, sillNormal * minNormal)));
            double maxAngle = Math.Acos(Math.Max(-1, Math.Min(1, sillNormal * maxNormal)));

            min -= toolRadius / Math.Tan(minAngle * 0.5) + toolRadius;
            max += toolRadius / Math.Tan(maxAngle * 0.5) + toolRadius;

            // A fixed tenon width (along the sill), centred on the automatic one
            if (TenonWidth > 0)
            {
                var middle = (min + max) * 0.5;
                min = middle - TenonWidth * 0.5;
                max = middle + TenonWidth * 0.5;
            }

            TenonSidePlanes = new[]
            {
                new Plane(SillPlatePlane.PointAt(0, min), InsertionVector, -PlatePlane.ZAxis),
                new Plane(SillPlatePlane.PointAt(0, max), InsertionVector, PlatePlane.ZAxis),
            };
        }

        protected PlateItem CreatePlate(double tolerance)
        {
            var tenonEndPlane = new Plane(SillPlatePlane.Origin + InsertionVector * (PlateDepth - ToleranceTenonEnd), InsertionVector);
            Debug.Add(tenonEndPlane);

            var xPlanes = ArmPlanesForMode();
            var segments = new Curve[2, 6];
            bool[] roundFlags = null;
            var faceLoops = new Curve[2];
            double radius = ToolDiameter * 0.5;
            bool corner2 = false; // The inner corner is chamfered (Mode 1 or -1)

            for (int i = 0; i < 2; ++i)
            {
                if (Mode != 0)
                {
                    RX.PlanePlanePlane(PlateFacePlanes[i], xPlanes[1], xPlanes[2], out Point3d pp);
                    if ((pp - SillPlane.Origin) * SillPlane.ZAxis > 0)
                        corner2 = true;
                }

                Plane[] xxPlanes;
                if (corner2)
                {
                    xxPlanes = Mode == 1
                        ? new[] { SillPlane, OutsidePlanes[0], EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                            OutsidePlanes[1], SeamPlanes[0], SillPlane, TenonSidePlanes[1], tenonEndPlane, TenonSidePlanes[0] }
                        : new[] { SillPlane, SeamPlanes[1], OutsidePlanes[0], EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                            OutsidePlanes[1], SillPlane, TenonSidePlanes[1], tenonEndPlane, TenonSidePlanes[0] };
                }
                else
                {
                    xxPlanes = new[] { SillPlane, OutsidePlanes[0], EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                        OutsidePlanes[1], SillPlane, TenonSidePlanes[1], tenonEndPlane, TenonSidePlanes[0] };
                }

                var pts = new Point3d[xxPlanes.Length];
                for (int j = 0; j < pts.Length; ++j)
                {
                    int jj = (j - 1).Modulus(pts.Length);
                    RX.PlanePlanePlane(PlateFacePlanes[i], xxPlanes[j], xxPlanes[jj], out pts[j]);
                }

                if (corner2 && pts[1].DistanceTo(pts[2]) < radius)
                {
                    corner2 = false;
                    var list = new List<Point3d>(pts);
                    list.RemoveAt(1);
                    pts = list.ToArray();
                    RX.PlanePlanePlane(PlateFacePlanes[i], xxPlanes[0], xxPlanes[2], out pts[1]);
                }

                // Which corners to round: inside corners, and outside ones inside a beam (in a slot
                // end). Decided on the first face and used for both, so the two faces match.
                if (i == 0)
                    roundFlags = PlateOutline.CornersToRound(pts, PlateFacePlanes[0].ZAxis, Beams.Select(b => PlateOutline.BeamBox(b, null)), 0.1);

                var segIndices = new[] { corner2 ? 4 : 3, 2, 3, 2, 3, 4 };

                int counter = 0;
                for (int j = 0; j < 6; ++j)
                {
                    var segPoints = new List<Point3d>();
                    var segFlags = new List<bool>();
                    for (int k = 0; k < segIndices[j]; ++k)
                    {
                        counter = counter.Modulus(pts.Length);
                        segPoints.Add(pts[counter]);
                        segFlags.Add(roundFlags != null && counter < roundFlags.Length && roundFlags[counter]);
                        counter++;
                    }
                    counter--;
                    // Corners are rounded within segments 0, 2 and 4, as before
                    segments[i, j] = j % 2 == 0
                        ? PlateOutline.Round(segPoints, segFlags, radius, 0.01)
                        : new Polyline(segPoints).ToNurbsCurve();
                }
            }

            var faceSegs = new List<Curve>[2];
            for (int i = 0; i < 2; ++i)
            {
                faceSegs[i] = Enumerable.Range(0, 6).Select(j => segments[i, j]).ToList();
                faceLoops[i] = Curve.JoinCurves(faceSegs[i], 0.01).FirstOrDefault();
            }

            if (faceLoops.Any(x => x == null)) return null;

            var breps = new List<Brep>();
            for (int j = 0; j < 6; ++j)
            {
                var loft = Brep.CreateFromLoft(new[] { segments[0, j], segments[1, j] }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (loft != null && loft.Length > 0)
                    breps.Add(loft[0]);
            }

            breps.AddRange(Brep.CreatePlanarBreps(faceLoops[0], 0.1) ?? new Brep[0]);
            breps.AddRange(Brep.CreatePlanarBreps(faceLoops[1], 0.1) ?? new Brep[0]);

            var joined = Brep.JoinBreps(breps, 0.01);
            if (joined == null || joined.Length < 1) return null;

            // Dowel holes in the plate
            var dowelBreps = PlateDowelAxes.Select(axis =>
                new Cylinder(new Circle(new Plane(axis.From, axis.Direction), DowelDiameter * 0.5), axis.Length).ToBrep(true, true)).ToList();

            var geometry = joined[0].Cut(dowelBreps, 0.01);
            geometry.Faces.SplitKinkyFaces(0.1);

            return new PlateItem
            {
                Name = "Plate",
                Thickness = PlateThickness,
                Plane = PlatePlane,
                Outline = faceLoops[0],
                Geometry = geometry,
                BeamIds = Beams.Select(x => x.Id).ToList(),
            };
        }

        /// <summary>
        /// Slot for the plate tenon in the sill.
        /// </summary>
        protected void CreateSillCutter(JointResult result)
        {
            var xaxis = Vector3d.CrossProduct(PlatePlane.ZAxis, InsertionVector);
            var tenonEndPlane = new Plane(SillPlatePlane.Origin + InsertionVector * PlateSlotDepth, InsertionVector);

            RX.PlanePlanePlane(SillPlane, new Plane(SillPlane.Origin, SillPlane.ZAxis, SillPlane.YAxis), PlatePlane, out Point3d origin);

            var plane = new Plane(origin, xaxis, PlatePlane.ZAxis);
            plane.Origin = plane.Origin - InsertionVector * Added;

            // The slot ends are rounded by the tool, so make the slot longer by the corner radius at
            // each end for the plate's square corners to fit
            var slotRadius = Math.Min(ToolDiameter * 0.5, PlateThickness * 0.5);
            var tsp = new[] { TenonSidePlanes[0], TenonSidePlanes[1] };
            int sign = tsp[0].ZAxis * (tsp[1].Origin - tsp[0].Origin) > 0 ? 1 : -1;
            tsp[0].Origin = tsp[0].Origin - tsp[0].ZAxis * (ToleranceTenonSide + slotRadius) * sign;
            tsp[1].Origin = tsp[1].Origin - tsp[1].ZAxis * (ToleranceTenonSide + slotRadius) * sign;
            if (sign < 0)
                tsp = new[] { tsp[1], tsp[0] };

            var pts = new Point3d[4];
            RX.PlanePlanePlane(plane, PlateFacePlanes[0], tsp[1], out pts[0]);
            RX.PlanePlanePlane(plane, PlateFacePlanes[1], tsp[1], out pts[1]);
            RX.PlanePlanePlane(plane, PlateFacePlanes[1], tsp[0], out pts[2]);
            RX.PlanePlanePlane(plane, PlateFacePlanes[0], tsp[0], out pts[3]);

            var poly = new Polyline(pts) { pts[0] };
            if (Vector3d.CrossProduct(pts[1] - pts[0], pts[2] - pts[1]) * InsertionVector < 0)
                poly.Reverse();

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), slotRadius, 0.01, 0.01) ?? poly.ToNurbsCurve();
            var extrusion = Extrusion.Create(profile, PlateSlotDepth + Added, true);
            if (extrusion == null)
            {
                result.Messages.Add($"{GetType().Name}: failed to create the sill slot.");
                return;
            }

            var slot = new Slot { BeamId = Beams[2].Id, Plane = plane, Thickness = PlateThickness, Depth = PlateSlotDepth };
            slot.Cutters.Add(Slot.PrepareCutter(extrusion.ToBrep(true)));
            slot.Data.Set("TenonSide0", tsp[0]);
            slot.Data.Set("TenonSide1", tsp[1]);
            slot.Data.Set("PlateFace0", PlateFacePlanes[0]);
            slot.Data.Set("PlateFace1", PlateFacePlanes[1]);
            slot.Data.Set("EndPlane", tenonEndPlane);
            slot.Data.Set("SlotPlane", plane);
            slot.Data.Set("Depth", PlateSlotDepth);
            slot.Data.Set("PlateThickness", PlateThickness);
            result.Add(slot);
        }

        /// <summary>
        /// Slot for the plate in an arm.
        /// </summary>
        protected void CreatePlateSlot(JointResult result, int index)
        {
            // The slot end is milled across the plate's thickness, so its edges are rounded; run it on
            // past the plate's square end by that radius so the plate never reaches the rounding
            double r = Math.Min(ToolDiameter * 0.5, PlateThickness * 0.5 - 0.1);
            var endPlane = EndPlanes[index];
            endPlane.Origin = endPlane.Origin - endPlane.ZAxis * (ToleranceSlotEnd + Math.Max(r, 0));
            var sidePlane = SeamPlanes[index];
            var outsidePlane = OutsidePlanes[index];

            var sillPlane = SillPlane;
            sillPlane.Origin = sillPlane.Origin - sillPlane.ZAxis * Added;

            int sign = sidePlane.ZAxis * (sidePlane.Origin - outsidePlane.Origin) > 0 ? 1 : -1;
            sidePlane.Origin = sidePlane.Origin + sidePlane.ZAxis * Added * sign;
            outsidePlane.Origin = outsidePlane.Origin - outsidePlane.ZAxis * Added * sign;

            var pts = new Point3d[4];
            RX.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[0], out pts[0]);
            RX.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[1], out pts[1]);
            RX.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[1], out pts[2]);
            RX.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[0], out pts[3]);

            var topLoop = new Polyline(pts) { pts[0] };
            var topFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            RX.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[0], out pts[0]);
            RX.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[1], out pts[1]);
            RX.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[1], out pts[2]);
            RX.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[0], out pts[3]);

            var btmLoop = new Polyline(pts) { pts[0] };
            var btmFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            var sideFaces = Brep.CreateFromLoft(new Curve[] { topLoop.ToNurbsCurve(), btmLoop.ToNurbsCurve() },
                Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            var breps = new List<Brep> { topFace, btmFace };
            if (sideFaces != null) breps.AddRange(sideFaces);

            var joined = breps.Any(x => x == null) ? null : Brep.JoinBreps(breps, 0.01)?.FirstOrDefault();
            if (joined == null)
            {
                result.Messages.Add($"{GetType().Name}: failed to create the plate slot in arm {index} " +
                    $"(top {topFace != null}, bottom {btmFace != null}, sides {sideFaces?.Length ?? 0}).");
                result.Debug.Add(topLoop);
                result.Debug.Add(btmLoop);
                return;
            }
            joined.Faces.SplitKinkyFaces(0.1);


            var filleted = Brep.CreateFilletEdges(joined, new[] { 8, 9 }, new[] { r, r }, new[] { r, r },
                BlendType.Fillet, RailType.RollingBall, 0.01);

            var slot = new Slot { BeamId = Beams[index].Id, Plane = PlatePlane, Thickness = PlateThickness };
            slot.Cutters.Add(Slot.PrepareCutter(filleted != null && filleted.Length > 0 ? filleted[0] : joined));
            slot.Data.Set("SidePlane", sidePlane);
            slot.Data.Set("PlatePlane", PlatePlane);
            slot.Data.Set("EndPlane", endPlane);
            slot.Data.Set("OutsidePlane", outsidePlane);
            slot.Data.Set("PlateThickness", PlateThickness);
            slot.Data.Set("TopLoop", topLoop.ToNurbsCurve());
            slot.Data.Set("BottomLoop", btmLoop.ToNurbsCurve());
            result.Add(slot);
        }
    }
}
