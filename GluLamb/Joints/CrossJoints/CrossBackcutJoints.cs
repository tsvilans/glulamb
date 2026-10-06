using System;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Cross lap with tapered (backcut) sides on one side, so the beams wedge together.
    /// Port of the legacy CrossJoint_SingleBackcut to the IJoint system: same geometry, output
    /// as Lap features. Parts[0] is the over beam, Parts[1] the under beam.
    /// </summary>
    [JointType("glulamb.cross-backcut-single", Name = "Cross lap, single backcut", Arity = 2, Topology = JointTopology.Cross,
        Description = "Cross lap with tapered sides that wedge the beams together.")]
    public class CrossSingleBackcutJoint : JointBase
    {
        [JointParameter(Description = "Taper angle of the backcut sides. At least 1 degree.", Unit = "degrees")]
        public double TaperAngle { get; set; } = 3.0;

        [JointParameter(Description = "Depth used to size the taper. 0 uses the over beam's height.", Unit = "length")]
        public double DepthOverride { get; set; } = 0.0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double ExtraLength { get; set; } = 50.0;

        [JointParameter(Description = "Swap which beam is on top.")]
        public bool Flip { get; set; } = false;

        public CrossSingleBackcutJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        private static Plane UnifyPlanes(Plane p0, Plane p1)
        {
            int x = 1, y = 1;
            if (p0.YAxis * p1.YAxis < 0)
                y = -1;
            if (p0.ZAxis * p1.XAxis < 0)
                x = -1;

            return new Plane(p1.Origin, p1.XAxis * x, p1.YAxis * y);
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int oi = Flip ? 1 : 0, ui = Flip ? 0 : 1;
            var obeam = beams[oi];
            var ubeam = beams[ui];
            var tolerance = Math.Max(context.Tolerance, 0.01);

            double added = ExtraLength;

            var oPlane = obeam.GetPlane(m_parts[oi].Parameter);
            var uPlane = ubeam.GetPlane(m_parts[ui].Parameter);

            // Offset for the backcut angle
            double tan = Math.Tan(RhinoMath.ToRadians(Math.Max(1.0, TaperAngle)));
            double addedTan = added * tan;
            double depth = DepthOverride > 0.0 ? DepthOverride : obeam.Height;
            double taperOffset = depth * 0.5 * tan;

            uPlane = UnifyPlanes(oPlane, uPlane);

            var xaxis = oPlane.ZAxis;
            var yaxis = uPlane.ZAxis;
            var zaxis = Vector3d.CrossProduct(xaxis, yaxis);
            if (!zaxis.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are parallel.");
                return;
            }

            // The over beam is notched on the -zaxis side, so it has to face the under beam.
            // If the centrelines are offset the other way, mirror the joint through the lap plane.
            if ((uPlane.Origin - oPlane.Origin) * zaxis > tolerance)
                zaxis.Reverse();

            var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, zaxis);
            Position = plane;

            var oPlanes = new[]
            {
                new Plane(oPlane.Origin - oPlane.XAxis * obeam.Width * 0.5, oPlane.ZAxis, oPlane.YAxis),
                new Plane(oPlane.Origin + oPlane.XAxis * obeam.Width * 0.5, -oPlane.ZAxis, oPlane.YAxis),
            };

            var uPlanes = new[]
            {
                new Plane(uPlane.Origin - uPlane.XAxis * ubeam.Width * 0.5, uPlane.ZAxis, uPlane.YAxis),
                new Plane(uPlane.Origin + uPlane.XAxis * ubeam.Width * 0.5, -uPlane.ZAxis, uPlane.YAxis),
            };

            var corners = new Point3d[4];
            bool ok = true;
            ok &= RX.PlanePlanePlane(plane, oPlanes[0], uPlanes[0], out corners[0]);
            ok &= RX.PlanePlanePlane(plane, oPlanes[0], uPlanes[1], out corners[1]);
            ok &= RX.PlanePlanePlane(plane, oPlanes[1], uPlanes[1], out corners[2]);
            ok &= RX.PlanePlanePlane(plane, oPlanes[1], uPlanes[0], out corners[3]);

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: side planes did not intersect.");
                return;
            }

            var offsetCorners = new[]
            {
                corners[0] - yaxis * taperOffset,
                corners[1] - yaxis * taperOffset - xaxis * taperOffset,
                corners[2] - xaxis * taperOffset,
            };

            var h = obeam.Height * 0.5 + added;
            var topCorners = new[]
            {
                corners[0] - zaxis * h + yaxis * addedTan,
                corners[1] - zaxis * h + yaxis * addedTan + xaxis * addedTan,
                corners[2] - zaxis * h + xaxis * addedTan,
                corners[3] - zaxis * h,
            };

            var btmCorners = new[]
            {
                corners[0] + zaxis * h + yaxis * addedTan,
                corners[1] + zaxis * h + yaxis * addedTan + xaxis * addedTan,
                corners[2] + zaxis * h + xaxis * addedTan,
                corners[3] + zaxis * h,
            };

            var overSrf = new[]
            {
                Brep.CreateFromCornerPoints(offsetCorners[0], offsetCorners[1], offsetCorners[2] - yaxis * added, corners[3] - yaxis * added, tolerance),
                Brep.CreateFromCornerPoints(offsetCorners[0], btmCorners[0], btmCorners[1], offsetCorners[1], tolerance),
                Brep.CreateFromCornerPoints(offsetCorners[1], topCorners[1], topCorners[2] - yaxis * added, offsetCorners[2] - yaxis * added, tolerance),
                Brep.CreateFromCornerPoints(corners[3] - yaxis * added, topCorners[3] - yaxis * added, topCorners[0], offsetCorners[0], tolerance),
                Brep.CreateFromCornerPoints(topCorners[0], offsetCorners[0], btmCorners[0], tolerance),
                Brep.CreateFromCornerPoints(topCorners[1], offsetCorners[1], btmCorners[1], tolerance),
            };

            var underSrf = new[]
            {
                Brep.CreateFromCornerPoints(offsetCorners[0] - xaxis * added, offsetCorners[1], offsetCorners[2], corners[3] - xaxis * added, tolerance),
                Brep.CreateFromCornerPoints(offsetCorners[0] - xaxis * added, btmCorners[0] - xaxis * added, btmCorners[1], offsetCorners[1], tolerance),
                Brep.CreateFromCornerPoints(offsetCorners[1], topCorners[1], topCorners[2], offsetCorners[2], tolerance),
                Brep.CreateFromCornerPoints(offsetCorners[2], btmCorners[2], btmCorners[3] - xaxis * added, corners[3] - xaxis * added, tolerance),
                Brep.CreateFromCornerPoints(topCorners[2], offsetCorners[2], btmCorners[2], tolerance),
                Brep.CreateFromCornerPoints(topCorners[1], offsetCorners[1], btmCorners[1], tolerance),
            };

            CrossJointUtil.AddLap(this, result, obeam, overSrf, plane, tolerance, mergeCoplanar: true);
            CrossJointUtil.AddLap(this, result, ubeam, underSrf, plane, tolerance, mergeCoplanar: true);
            CrossJointUtil.SetStatus(result, 2);
        }
    }

    /// <summary>
    /// Cross lap with backcuts on both beams, following the beam sides (so it works on curved
    /// glulam). Port of the legacy CrossJoint_DoubleBackcut to the IJoint system: same geometry,
    /// one shared cutting surface output as a Lap feature on each beam. Parts[0] is beam A,
    /// Parts[1] beam B.
    /// </summary>
    [JointType("glulamb.cross-backcut-double", Name = "Cross lap, double backcut", Arity = 2, Topology = JointTopology.Cross,
        Description = "Cross lap with backcuts on both beams, following curved beam sides.")]
    public class CrossDoubleBackcutJoint : JointBase
    {
        [JointParameter(Description = "Offset of the backcut faces from the beam faces.", Unit = "length")]
        public double Offset1 { get; set; } = 3.0;

        [JointParameter(Description = "Extra width of the backcut faces.", Unit = "length")]
        public double Offset2 { get; set; } = 3.0;

        [JointParameter(Description = "Extension of the side surfaces past the beam ends.", Unit = "length")]
        public double Extension { get; set; } = 2.0;

        [JointParameter(Description = "Width of material left in the centre of the lap.", Unit = "length")]
        public double OffsetCentre { get; set; } = 10.0;

        [JointParameter(Description = "Swap which beam is on top when the centrelines intersect.")]
        public bool Flip { get; set; } = false;

        public CrossDoubleBackcutJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        private static Brep SideSurface(Beam beam, int side, double offset, double width, double extension, bool flip) =>
            beam is Glulam glulam
                ? glulam.GetSideSurface(side, offset, width, extension, flip)
                : BeamOps.GetSideSurface(beam, side, offset, width, extension, flip);

        private static Curve FirstIntersection(Brep a, Brep b, double tolerance)
        {
            RX.BrepBrep(a, b, tolerance, out Curve[] curves, out Point3d[] _);
            return curves != null && curves.Length > 0 ? curves[0] : null;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beamA = beams[0];
            var beamB = beams[1];
            var tolerance = context.Tolerance;
            bool flip = false;

            double widthA = beamA.Width, heightA = beamA.Height;
            double widthB = beamB.Width, heightB = beamB.Height;

            beamA.Centreline.ClosestPoints(beamB.Centreline, out Point3d ptA, out Point3d ptB);

            var cp = (ptA + ptB) / 2;
            double cDist = ptA.DistanceTo(ptB) / 2;

            var plA = beamA.GetPlane(ptA);
            var plB = beamB.GetPlane(ptB);

            double ofc = OffsetCentre;
            double ofc2 = OffsetCentre / 2;

            // Which way each beam is notched depends on which side of A beam B is on, measured
            // along each beam's Y axis. The old code unitized ptB - ptA, so when the centrelines
            // (nearly) intersect, floating-point noise gave it an arbitrary direction and both
            // beams could get notched on the same side. If the offset is within tolerance, B is
            // taken to be on +Y of A (or -Y with Flip).
            double dA = (ptB - ptA) * plA.YAxis;
            double dB = (ptA - ptB) * plB.YAxis;

            if (Math.Abs(dA) <= tolerance)
            {
                double side = Flip ? -1.0 : 1.0;
                dA = side;
                dB = -side * (plA.YAxis * plB.YAxis);
            }

            int yAFlip = dA < 0 ? 1 : -1,
                yBFlip = dB < 0 ? 1 : -1,
                xAFlip = plA.XAxis * plB.ZAxis < 0 ? 1 : -1;

            // Centre surface
            var centreSides = new[]
            {
                SideSurface(beamA, 0, (widthA / 2 - ofc2) * xAFlip, heightB * 3, Extension, flip),
                SideSurface(beamA, 0, -(widthA / 2 - ofc2) * xAFlip, heightB * 3, Extension, flip),
            };

            var centreFlat = SideSurface(beamB, 1, cDist * yAFlip, widthB - ofc, Extension, flip);

            var centreCurves = new Curve[4];
            centreCurves[0] = FirstIntersection(centreSides[0], centreFlat, tolerance);
            centreCurves[1] = FirstIntersection(centreSides[1], centreFlat, tolerance);

            if (centreCurves[0] == null || centreCurves[1] == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to intersect the centre surfaces.");
                result.Debug.Add(centreFlat);
                result.Debug.AddRange(centreSides);
                return;
            }

            var cPoints = new[]
            {
                centreCurves[0].PointAtStart,
                centreCurves[0].PointAtEnd,
                centreCurves[1].PointAtStart,
                centreCurves[1].PointAtEnd,
            };

            Plane.FitPlaneToPoints(cPoints, out Plane cPlane);
            cPlane.Origin = cp;
            Position = cPlane;

            for (int i = 0; i < 4; ++i)
                cPoints[i] = cPlane.ClosestPoint(cPoints[i]);

            centreCurves[0] = new Line(cPoints[0], cPoints[1]).ToNurbsCurve();
            centreCurves[1] = new Line(cPoints[2], cPoints[3]).ToNurbsCurve();
            centreCurves[2] = new Line(cPoints[0], cPoints[2]).ToNurbsCurve();
            centreCurves[3] = new Line(cPoints[1], cPoints[3]).ToNurbsCurve();

            var centreBrep = Brep.CreateEdgeSurface(centreCurves);

            // Beam A top and sides, beam B bottom and sides
            var aTop = SideSurface(beamA, 1, (heightA / 2 + Offset1) * -yAFlip, widthA + Offset2, Extension, false);
            var aSides = new[]
            {
                SideSurface(beamA, 0, (widthA / 2 + Offset1) * xAFlip, heightA * 2 + Offset2, Extension, flip),
                SideSurface(beamA, 0, -(widthA / 2 + Offset1) * xAFlip, heightA * 2 + Offset2, Extension, flip),
            };

            var bBottom = SideSurface(beamB, 1, (heightB / 2 + Offset1) * -yBFlip, widthB + Offset2, Extension, false);
            var bSides = new[]
            {
                SideSurface(beamB, 0, (widthB / 2 + Offset1) * xAFlip, heightB * 2 + Offset2, Extension, flip),
                SideSurface(beamB, 0, -(widthB / 2 + Offset1) * xAFlip, heightB * 2 + Offset2, Extension, flip),
            };

            var aTopBSides = new[]
            {
                FirstIntersection(aTop, bSides[0], tolerance),
                FirstIntersection(aTop, bSides[1], tolerance),
            };

            var bBottomASides = new[]
            {
                FirstIntersection(bBottom, aSides[0], tolerance),
                FirstIntersection(bBottom, aSides[1], tolerance),
            };

            if (aTopBSides.Any(x => x == null) || bBottomASides.Any(x => x == null))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to intersect the backcut surfaces.");
                return;
            }

            Brep[] Loft(Curve a, Curve b)
            {
                if (a.TangentAtStart * b.TangentAtStart < 0.0)
                    b.Reverse();
                return Brep.CreateFromLoft(new[] { a, b }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false) ?? new Brep[0];
            }

            var faces = new List<Brep> { centreBrep };
            faces.AddRange(Loft(centreCurves[3], aTopBSides[0]));
            faces.AddRange(Loft(centreCurves[2], aTopBSides[1]));
            faces.AddRange(Loft(centreCurves[0], bBottomASides[0]));
            faces.AddRange(Loft(centreCurves[1], bBottomASides[1]));

            // Webs
            faces.Add(Brep.CreateFromCornerPoints(centreCurves[0].PointAtStart, aTopBSides[1].PointAtStart, bBottomASides[0].PointAtStart, tolerance));
            faces.Add(Brep.CreateFromCornerPoints(centreCurves[0].PointAtEnd, aTopBSides[0].PointAtStart, bBottomASides[0].PointAtEnd, tolerance));
            faces.Add(Brep.CreateFromCornerPoints(centreCurves[1].PointAtEnd, aTopBSides[0].PointAtEnd, bBottomASides[1].PointAtEnd, tolerance));
            faces.Add(Brep.CreateFromCornerPoints(centreCurves[1].PointAtStart, aTopBSides[1].PointAtEnd, bBottomASides[1].PointAtStart, tolerance));

            if (faces.Any(x => x == null))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create some of the cutter faces.");
                return;
            }

            var joined = Brep.JoinBreps(faces, 0.001);
            var cutters = joined != null && joined.Length > 0 ? joined.ToList() : faces;

            foreach (var beam in beams)
                result.Add(new Lap { BeamId = beam.Id, Plane = cPlane, Cutters = cutters.Select(x => x.DuplicateBrep()).ToList() });

            result.Status = JointStatus.Ok;
        }
    }

    internal static class CrossJointUtil
    {
        internal static void AddLap(IJoint joint, JointResult result, Beam beam, Brep[] faces, Plane plane, double tolerance, bool mergeCoplanar = false)
        {
            var joined = faces.Any(x => x == null) ? null : Brep.JoinBreps(faces, tolerance);
            if (joined == null || joined.Length < 1)
            {
                result.Messages.Add($"{joint.GetType().Name}: failed to create cutter for beam {beam.Id}.");
                return;
            }

            if (mergeCoplanar)
                foreach (var brep in joined)
                    brep.MergeCoplanarFaces(tolerance);

            result.Add(new Lap { BeamId = beam.Id, Plane = plane, Cutters = joined.ToList() });
        }

        internal static void SetStatus(JointResult result, int expected)
        {
            int created = result.Features.Count;
            result.Status = created == expected ? JointStatus.Ok : created > 0 ? JointStatus.Partial : JointStatus.Failed;
        }
    }
}
