using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Four beam ends meeting at a node, e.g. in a gridshell: the ends are split along the seams
    /// between neighbouring beams and joined with a plate slotted into all four, with a dowel
    /// through each arm. Port of FourWayJoint_Split to the IJoint system: same geometry; the
    /// split cutters as FreeContour features, plate slots as Slot features, dowel holes as
    /// Drilling features and the dowels as hardware.
    ///
    /// The original needed an inner and outer surface of the structure to find where the seams
    /// end. Those can still be set (InnerSurface, OuterSurface); otherwise the seams end at the
    /// beams' top and bottom faces, measured along the joint normal (the average of the beams'
    /// Y axes).
    /// </summary>
    [JointType("glulamb.four-way-split", Name = "Four-way split", Arity = 4, Topology = JointTopology.Node,
        Description = "Four beam ends split along their seams and joined with a slotted-in plate.")]
    public class FourWaySplitJoint : JointBase
    {
        [JointParameter(Description = "Plate thickness.", Unit = "length")]
        public double PlateThickness { get; set; } = 20.0;

        [JointParameter(Description = "Depth of the plate slots into the arms.", Unit = "length")]
        public double PlateDepth { get; set; } = 80.0;

        [JointParameter(Description = "Largest slot depth when the slot is tilted.", Unit = "length")]
        public double MaxPlateDepth { get; set; } = 120;

        [JointParameter(Description = "Length of the plate along each arm.", Unit = "length")]
        public double PlateLength { get; set; } = 100.0;

        [JointParameter(Description = "Clearance at the end of the plate slots.", Unit = "length")]
        public double ToleranceSlotEnd { get; set; } = 1.5;

        [JointParameter(Description = "Distance of the dowels from the node.", Unit = "length")]
        public double DowelPosition { get; set; } = 70.0;

        [JointParameter(Description = "Dowel diameter.", Unit = "length")]
        public double DowelDiameter { get; set; } = 12.0;

        [JointParameter(Description = "Dowel length.", Unit = "length")]
        public double DowelLength { get; set; } = 140.0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 50.0;

        [JointParameter(Description = "Extra length of the plate slots towards the node.", Unit = "length")]
        public double AddedSlot { get; set; } = 50.0;

        [JointParameter(Description = "How far the split cutters reach above and below the plate.", Unit = "length")]
        public double AddedPlaneOffset { get; set; } = 150;

        [JointParameter(Description = "Tool diameter for slot corner radii.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        /// <summary>
        /// Optional inner and outer surfaces of the structure, where the seams end. If either is
        /// null, the beams' top and bottom faces are used.
        /// </summary>
        public Brep InnerSurface { get; set; }
        public Brep OuterSurface { get; set; }

        protected Plane[] EndPlanes;
        protected Plane[] LeftPlanes;
        protected Plane[] RightPlanes;
        protected Line[] Seams;
        protected Plane PlatePlane;
        protected Plane NodePlane;
        protected double[] ArmWidths;

        public FourWaySplitJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Four parts, all at beam ends.
        /// </summary>
        public static double Score(JointX condition, IJointContext context) =>
            condition.Parts.Count == 4 && condition.Parts.All(x => JointPartX.IsAtEnd(x.Case)) ? 1.0 : 0.0;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var origin = Position.Origin;

            // Joint normal
            Vector3d normal;
            if (InnerSurface != null && OuterSurface != null)
            {
                var outerPt = OuterSurface.ClosestPoint(origin);
                var innerPt = InnerSurface.ClosestPoint(origin);
                normal = outerPt - innerPt;
            }
            else
            {
                // The plane that best fits the four arm directions, independent of how the beam
                // sections are oriented. Its sign follows the nearest section axis of beam 0;
                // Flip inverts it.
                var pts = beams.Select(b => { var d = b.Centreline.PointAt(b.Centreline.Domain.Mid) - origin; d.Unitize(); return origin + d; })
                    .Append(origin).ToList();
                Plane.FitPlaneToPoints(pts, out Plane fit);
                normal = fit.ZAxis;

                var p0 = beams[0].GetPlane(origin);
                var axis0 = Math.Abs(p0.YAxis * normal) >= Math.Abs(p0.XAxis * normal) ? p0.YAxis : p0.XAxis;
                if (normal * axis0 < 0) normal.Reverse();
                if (Flip) normal.Reverse();
            }

            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: could not find the joint normal.");
                return;
            }

            // Sort the arms around the normal
            var dirs = beams.Select(b => b.Centreline.PointAt(b.Centreline.Domain.Mid) - origin).ToArray();
            var xRef = dirs[0] - normal * (dirs[0] * normal);
            xRef.Unitize();
            var yRef = Vector3d.CrossProduct(normal, xRef);

            var order = Enumerable.Range(0, 4)
                .OrderBy(i => { var a = Math.Atan2(dirs[i] * yRef, dirs[i] * xRef); return a < 0 ? a + Math.PI * 2 : a; })
                .ToArray();

            var arms = order.Select(i => beams[i]).ToArray();
            var armParts = order.Select(i => m_parts[i]).ToArray();
            dirs = order.Select(i => dirs[i]).ToArray();

            NodePlane = new Plane(origin, xRef, yRef);
            Position = NodePlane;

            // Beam frames at the node: Z into the arm, Y the section axis closest to the normal
            var planes = new Plane[4];
            var widths = ArmWidths = new double[4];
            var heights = new double[4];
            for (int i = 0; i < 4; ++i)
                planes[i] = AlignSection(arms[i], arms[i].GetPlane(origin), dirs[i], normal, out widths[i], out heights[i]);

            Seams = new Line[4];
            LeftPlanes = new Plane[4];
            RightPlanes = new Plane[4];

            for (int i = 0; i < 4; ++i)
            {
                // Left faces the previous arm around the normal, right the next one, whichever
                // way the section's X axis points
                var s = planes[i].XAxis * dirs[(i + 3).Modulus(4)] >= 0 ? 1.0 : -1.0;
                // Both planes face -s * X, as the original's did for s = 1
                var (u, v) = s > 0 ? (planes[i].ZAxis, planes[i].YAxis) : (planes[i].YAxis, planes[i].ZAxis);
                LeftPlanes[i] = new Plane(planes[i].Origin + planes[i].XAxis * s * widths[i] * 0.5, u, v);
                RightPlanes[i] = new Plane(planes[i].Origin - planes[i].XAxis * s * widths[i] * 0.5, u, v);
            }

            // As in the original: 1 unit towards the outer side, with Z pointing to the inner side
            PlatePlane = new Plane(origin + normal, NodePlane.YAxis, NodePlane.XAxis);

            // Seam lines between neighbouring arms, from the inner to the outer side
            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 1).Modulus(4);
                if (!RX.PlanePlane(LeftPlanes[ii], RightPlanes[i], out Line xline))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: arms {i} and {ii} are parallel.");
                    return;
                }

                if (!SeamEnds(xline, normal, Math.Max(heights[i], heights[ii]) * 0.5, out Point3d innerPt, out Point3d outerPt))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: the seam between arms {i} and {ii} does not reach the inner and outer sides.");
                    return;
                }

                Seams[i] = new Line(innerPt, outerPt);
            }

            // Split cutters
            for (int i = 0; i < 4; ++i)
            {
                var cutter = CreateCrossCutter(i, out var data);
                if (cutter == null)
                {
                    result.Messages.Add($"{GetType().Name}: failed to create the split cutter for arm {i}.");
                    continue;
                }

                var contour = new FreeContour { BeamId = arms[i].Id, Plane = PlatePlane, Cutters = new List<Brep> { cutter } };
                contour.Data = data;
                result.Add(contour);
            }

            // Plate slots and dowels
            EndPlanes = new Plane[4];
            var projected = dirs.Select(d => { var v = PlatePlane.Project(d); v.Unitize(); return v; }).ToArray();
            var cpt = PlatePlane.ClosestPoint(origin);

            for (int i = 0; i < 4; ++i)
            {
                // Lengthen the plate where neighbouring arms are close, so the tool radius fits.
                // (The original read the neighbours' end planes before they were set, so this
                // was always PlateLength.)
                double dot = Math.Max(projected[i] * projected[(i + 1).Modulus(4)], projected[i] * projected[(i - 1).Modulus(4)]);
                double endOffset = Interpolation.Lerp(PlateLength, PlateLength * 2, Math.Max(0, dot));

                EndPlanes[i] = new Plane(cpt + projected[i] * endOffset, projected[i]);
            }

            for (int i = 0; i < 4; ++i)
            {
                CreatePlateSlot(result, arms[i], i);

                var dowelPt = origin + projected[i] * DowelPosition;
                var dp = arms[i].GetPlane(dowelPt);
                dp = new Plane(dp.Origin, dp.XAxis, Seams[i].Direction);

                var axis = new Line(dp.Origin - dp.YAxis * DowelLength * 0.5, dp.YAxis, DowelLength);
                result.Add(new Drilling(arms[i].Id, axis, DowelDiameter));
                result.Hardware.Add(new DowelItem(axis, DowelDiameter, arms[i].Id));

                // Each arm has to reach the node
                ExtendToReach(result, arms[i], armParts[i], new[] { Seams[i].From, Seams[i].To, Seams[(i + 3).Modulus(4)].From, Seams[(i + 3).Modulus(4)].To });
            }

            result.Messages.Add($"{GetType().Name}: the plate itself isn't generated yet (the original only cut the slots).");
            result.Status = result.Features.Count == 4 ? JointStatus.Ok : JointStatus.Partial;
        }

        /// <summary>
        /// Where a seam line meets the inner and outer sides: the surfaces if given, otherwise
        /// planes at halfHeight below and above the node along the normal.
        /// </summary>
        protected bool SeamEnds(Line seam, Vector3d normal, double halfHeight, out Point3d inner, out Point3d outer)
        {
            inner = outer = Point3d.Unset;
            seam.Transform(Transform.Scale((seam.From + seam.To) * 0.5, 500));

            if (InnerSurface != null && OuterSurface != null)
            {
                var crv = seam.ToNurbsCurve();
                if (!RX.CurveBrep(crv, InnerSurface, 0.01, out _, out Point3d[] ip) || ip.Length < 1) return false;
                if (!RX.CurveBrep(crv, OuterSurface, 0.01, out _, out Point3d[] op) || op.Length < 1) return false;
                inner = ip[0];
                outer = op[0];
                return true;
            }

            var origin = Position.Origin;
            if (!RX.LinePlane(seam, new Plane(origin - normal * halfHeight, normal), out double ti)) return false;
            if (!RX.LinePlane(seam, new Plane(origin + normal * halfHeight, normal), out double to)) return false;
            inner = seam.PointAt(ti);
            outer = seam.PointAt(to);
            return true;
        }

        protected void CreatePlateSlot(JointResult result, Beam arm, int index)
        {
            var endPlane = EndPlanes[index];
            endPlane.Origin = endPlane.Origin + endPlane.ZAxis * ToleranceSlotEnd;
            var sidePlane = LeftPlanes[index];

            if (!RX.PlanePlanePlane(endPlane, sidePlane, PlatePlane, out Point3d xpt))
            {
                result.Messages.Add($"{GetType().Name}: failed to create the plate slot in arm {index}.");
                return;
            }

            var xAxis = PlatePlane.Project(endPlane.ZAxis);
            var yAxis = PlatePlane.Project(sidePlane.ZAxis);

            var plane = new Plane(xpt, Vector3d.CrossProduct(yAxis, xAxis), xAxis);
            var vec = -plane.ZAxis;

            double depth = PlateDepth + Added;
            double offset = 0;
            double tilt = Math.Abs(vec * plane.ZAxis);
            if (tilt < 1)
            {
                double tiltedDepth = depth / tilt;
                double offsetSqrt = Math.Pow(tiltedDepth, 2) - Math.Pow(depth, 2);
                offset = double.IsNaN(offsetSqrt) || offsetSqrt <= 0 ? 0 : Math.Sqrt(offsetSqrt);
                depth = Math.Min(MaxPlateDepth, tiltedDepth);
            }

            plane.Origin = xpt + plane.ZAxis * Added + endPlane.ZAxis * offset;

            double slotLength = xpt.DistanceTo(NodePlane.Origin) + ToolDiameter + AddedSlot;
            var hw = PlateThickness * 0.5;

            var topLoop = new Polyline { plane.PointAt(hw, 0), plane.PointAt(-hw, 0), plane.PointAt(-hw, -slotLength), plane.PointAt(hw, -slotLength) };
            topLoop.Add(topLoop[0]);

            var w = ArmWidths[index];
            var btmLoop = new Polyline { plane.PointAt(hw, 0, -w), plane.PointAt(-hw, 0, -w), plane.PointAt(-hw, -slotLength, -w), plane.PointAt(hw, -slotLength, -w) };
            btmLoop.Add(btmLoop[0]);

            var profile = Curve.CreateFilletCornersCurve(topLoop.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01) ?? topLoop.ToNurbsCurve();
            var extrusion = Extrusion.CreateExtrusion(profile, vec * (depth + Added));
            var brep = extrusion?.ToBrep()?.CapPlanarHoles(0.01);
            if (brep == null)
            {
                result.Messages.Add($"{GetType().Name}: failed to create the plate slot in arm {index}.");
                return;
            }

            var slot = new Slot { BeamId = arm.Id, Plane = PlatePlane, Thickness = PlateThickness, Depth = depth };
            slot.Cutters.Add(Slot.PrepareCutter(brep));
            slot.Data.Set("SidePlane", sidePlane);
            slot.Data.Set("OutsidePlane", RightPlanes[index]);
            slot.Data.Set("EndPlane", endPlane);
            slot.Data.Set("PlatePlane", PlatePlane);
            slot.Data.Set("PlateThickness", PlateThickness);
            slot.Data.Set("TopLoop", topLoop.ToNurbsCurve());
            slot.Data.Set("BottomLoop", btmLoop.ToNurbsCurve());
            result.Add(slot);
        }

        protected Brep CreateCrossCutter(int index, out Rhino.Collections.ArchivableDictionary data)
        {
            int i = index;
            int ii = (index + 3).Modulus(4);
            int j = (index + 2).Modulus(4);
            int jj = (index + 1).Modulus(4);

            var plateInnerPlane = new Plane(PlatePlane.Origin + PlatePlane.ZAxis * AddedPlaneOffset, PlatePlane.XAxis, PlatePlane.YAxis);
            var plateOuterPlane = new Plane(PlatePlane.Origin - PlatePlane.ZAxis * AddedPlaneOffset, PlatePlane.XAxis, PlatePlane.YAxis);

            var ppInner = new Plane(PlatePlane.Origin + PlatePlane.ZAxis * 2, PlatePlane.ZAxis);
            var ppOuter = new Plane(PlatePlane.Origin - PlatePlane.ZAxis * 2, PlatePlane.ZAxis);

            var inner0 = new Plane(Seams[i].From, Seams[i].Direction, Seams[j].From - Seams[i].From);
            var outer0 = new Plane(Seams[i].To, -Seams[i].Direction, Seams[j].To - Seams[i].To);
            var normal0 = (inner0.YAxis + outer0.YAxis) * 0.5;

            var inner1 = new Plane(Seams[ii].From, Seams[ii].Direction, Seams[jj].From - Seams[ii].From);
            var outer1 = new Plane(Seams[ii].To, -Seams[ii].Direction, Seams[jj].To - Seams[ii].To);
            var normal1 = (inner1.YAxis + outer1.YAxis) * 0.5;

            data = new Rhino.Collections.ArchivableDictionary();
            data.Set("Inner0", inner0);
            data.Set("Outer0", outer0);
            data.Set("Inner1", inner1);
            data.Set("Outer1", outer1);
            data.Set("PlatePlane", PlatePlane);

            if (!RX.PlanePlane(inner0, inner1, out Line seamInner)) return null;
            if (!RX.PlanePlane(outer0, outer1, out Line seamOuter)) return null;

            var sInnerProj = ppInner.ProjectAlongVector(seamInner.Direction);
            var sOuterProj = ppOuter.ProjectAlongVector(seamOuter.Direction);

            Point3d[] SurfacePoints(Plane p, Transform proj, Plane offsetPlane, Transform seamProj, Line seam)
            {
                var pts = new Point3d[4];
                pts[0] = p.Origin; pts[0].Transform(offsetPlane.ProjectAlongVector(p.XAxis));
                pts[1] = p.Origin; pts[1].Transform(proj);
                pts[2] = seam.From; pts[2].Transform(seamProj);
                pts[3] = pts[2]; pts[3].Transform(offsetPlane.ProjectAlongVector(seam.Direction));
                return pts;
            }

            var inner0Pts = SurfacePoints(inner0, ppInner.ProjectAlongVector(inner0.XAxis), plateInnerPlane, sInnerProj, seamInner);
            var inner1Pts = SurfacePoints(inner1, ppInner.ProjectAlongVector(inner1.XAxis), plateInnerPlane, sInnerProj, seamInner);
            var outer0Pts = SurfacePoints(outer0, ppOuter.ProjectAlongVector(outer0.XAxis), plateOuterPlane, sOuterProj, seamOuter);
            var outer1Pts = SurfacePoints(outer1, ppOuter.ProjectAlongVector(outer1.XAxis), plateOuterPlane, sOuterProj, seamOuter);

            var breps = new List<Brep>
            {
                Brep.CreateFromCornerPoints(inner0Pts[0], inner0Pts[1], inner0Pts[2], inner0Pts[3], 0.01),
                Brep.CreateFromCornerPoints(inner1Pts[0], inner1Pts[1], inner1Pts[2], inner1Pts[3], 0.01),
                Brep.CreateFromCornerPoints(outer0Pts[0], outer0Pts[1], outer0Pts[2], outer0Pts[3], 0.01),
                Brep.CreateFromCornerPoints(outer1Pts[0], outer1Pts[1], outer1Pts[2], outer1Pts[3], 0.01),
            };

            void AddFlap(Point3d[] inner, Point3d[] outer, Vector3d n)
            {
                var f = new[] { inner[0], inner[1], outer[1], outer[0] };
                var g = f.Select(p => p - n * Added).ToArray();
                breps.Add(Brep.CreateFromCornerPoints(f[0], f[1], g[1], g[0], 0.01));
                breps.Add(Brep.CreateFromCornerPoints(f[1], f[2], g[2], g[1], 0.01));
                breps.Add(Brep.CreateFromCornerPoints(f[2], f[3], g[3], g[2], 0.01));
            }

            AddFlap(inner0Pts, outer0Pts, normal0);
            AddFlap(inner1Pts, outer1Pts, normal1);

            breps.Add(Brep.CreateFromCornerPoints(outer0Pts[2], outer1Pts[1], inner0Pts[2], 0.01));
            breps.Add(Brep.CreateFromCornerPoints(inner0Pts[2], outer1Pts[1], inner1Pts[1], 0.01));
            breps.Add(Brep.CreateFromCornerPoints(outer1Pts[2], outer0Pts[1], inner1Pts[2], 0.01));
            breps.Add(Brep.CreateFromCornerPoints(inner1Pts[2], outer0Pts[1], inner0Pts[1], 0.01));

            var cutter = Brep.JoinBreps(breps.Where(x => x != null), 0.01)?.FirstOrDefault();
            cutter?.Standardize();
            return cutter;
        }
    }
}
