using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class FourWayJoint_Split : FourWayJoint
    {

        public double DefaultPlateThickness = 20.0;
        public double DefaultPlateLength = 100.0;
        public double DefaultDowelPosition = 70.0;
        public double DefaultDowelDiameter = 12.0;
        public double DefaultDowelLength = 140.0;

        public double DefaultCutterExtension = 500.0;
        public double DefaultCutterToleranceExtension = 1.0;
        public double DefaultCutterLipWidth = 5.0;

        public Brep InnerSurface;
        public Brep OuterSurface;
        public Vector3d Normal;

        /// <summary>
        /// Thickness of connector plate.
        /// </summary>
        public double PlateThickness = 21.0;

        /// <summary>
        /// Length of connector plate along each beam arm.
        /// </summary>
        public double PlateLength = 100.0;

        /// <summary>
        /// Dowel position radially from the joint origin.
        /// </summary>
        public double DowelPosition = 70.0;

        /// <summary>
        /// Dowel diameter.
        /// </summary>
        public double DowelDiameter = 12.0;

        /// <summary>
        /// Dowel length.
        /// </summary>
        public double DowelLength = 140.0;

        /// <summary>
        /// Amount to extend the intersection cutter beyond the joint.
        /// </summary>
        public double CutterExtension = 500.0;

        /// <summary>
        /// Amount to extend the intersection cutter to ensure good boolean operations.
        /// </summary>
        public double CutterToleranceExtension = 1.0;

        /// <summary>
        /// Lip of middle of intersection cutter, to make an orderly transition to each half.
        /// </summary>
        public double CutterLipWidth = 5.0;

        /// <summary>
        /// Dowels are aligned with the cross-section plane if true. If false,
        /// they follow the seam line for a more even distribution.
        /// </summary>
        public bool DowelsPerpendicular = false;

        public List<object> debug;

        public FourWayJoint_Split(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateThickness = DefaultPlateThickness;
            PlateLength = DefaultPlateLength;
            DowelPosition = DefaultDowelPosition;
            DowelDiameter = DefaultDowelDiameter;
            DowelLength = DefaultDowelLength;

            CutterExtension = DefaultCutterExtension;
            CutterToleranceExtension = DefaultCutterToleranceExtension;
            CutterLipWidth = DefaultCutterLipWidth;
    }

        public override string ToString()
        {
            return "FourWayJoint_Split";
        }

        private List<Vector3d> SortPartsClockwise()
        {
            var outerPt = OuterSurface.ClosestPoint(this.Plane.Origin);
            var innerPt = InnerSurface.ClosestPoint(this.Plane.Origin);

            ComponentIndex ci;
            double s, t;
            //Vector3d normal;

            OuterSurface.ClosestPoint(outerPt, out outerPt, out ci, out s, out t, 0, out Normal);
            if (Normal * (outerPt - innerPt) < 0) Normal.Reverse();

            Normal = outerPt - innerPt;
            Normal.Unitize();

            var beams = new Beam[4];
            for (int i = 0; i < 4; ++i)
            {
                beams[i] = (Parts[i].Element as BeamElement).Beam;
            }

            var dirs = new Vector3d[4];
            for (int i = 0; i < 4; ++i)
            {
                dirs[i] = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - this.Plane.Origin;
            }

            List<int> indices;
            GluLamb.Utility.SortVectorsAroundPoint(dirs.ToList(), this.Plane.Origin, Normal, out indices);

            var parts = new JointPart[4];
            var vectors = new Vector3d[4];
            for (int i = 0; i < indices.Count; ++i)
            {
                parts[i] = Parts[indices[i]];
                vectors[i] = dirs[indices[i]];
            }

            Parts = parts;

            return vectors.ToList();
        }

        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach (var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }

            debug = new List<object>();

            if (InnerSurface == null || OuterSurface == null) throw new Exception("Surfaces not defined!");

            // Sort elements around the joint normal
            var dirs = SortPartsClockwise();

            var beams = new Beam[4];
            for (int i = 0; i < 4; ++i)
            {
                beams[i] = (Parts[i].Element as BeamElement).Beam;
            }

            // Get beam planes for each beam
            var planes = new Plane[4];
            for (int i = 0; i < 4; ++i)
            {
                planes[i] = beams[i].GetPlane(this.Plane.Origin);
            }


            // Construct proper planes for each element
            for (int i = 0; i < 4; ++i)
            {
                int signX = 1, signY = 1, signZ = 1;
                if (planes[i].ZAxis * dirs[i] < 0) signZ = -1;
                if (planes[i].YAxis * Normal < 0) signY = -1;

                planes[i] = new Plane(planes[i].Origin, planes[i].XAxis * signX * signZ, planes[i].YAxis * signY);

            }

            debug.Add(Plane);
            //debug.Add(planes[1]);
            //debug.Add(planes[2]);

            //debug.Add(beams[0].Centreline);
            //debug.Add(beams[1].Centreline);
            //debug.Add(beams[2].Centreline);
            //debug.Add(beams[3].Centreline);

            // Array of lines that describe the seams between
            // neighbouring beams.
            // From = point on inner surface
            // To = point on outer surface
            // The first line is to the right of the beam
            // I.e. the 2 seams for beam[0] are seams[0] and seams[3]
            var seams = new Line[4];

            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 1).Modulus(4);

                // Create side planes for each seam
                var p0 = new Plane(planes[i].Origin - planes[i].XAxis * beams[i].Width * 0.5,
                  planes[i].ZAxis, planes[i].YAxis);
                var p1 = new Plane(planes[ii].Origin + planes[ii].XAxis * beams[ii].Width * 0.5,
                  planes[ii].ZAxis, planes[ii].YAxis);

                // Find intersection of planes = find seam
                Line xline;
                Rhino.Geometry.Intersect.Intersection.PlanePlane(p0, p1, out xline);
                xline.Transform(Transform.Scale((xline.From + xline.To) * 0.5, 500));
                var xlineCrv = xline.ToNurbsCurve();

                // Find intersection between surfaces and seam
                Point3d[] xpts;
                Curve[] overlapCurves;
                Rhino.Geometry.Intersect.Intersection.CurveBrep(xlineCrv, InnerSurface, 0.01,
                  out overlapCurves, out xpts);

                var innerPt = xpts[0];

                Rhino.Geometry.Intersect.Intersection.CurveBrep(xlineCrv, OuterSurface, 0.01,
                  out overlapCurves, out xpts);

                var outerPt = xpts[0];

                seams[i] = new Line(innerPt, outerPt);
                debug.Add(seams[i]);
            }

            // Vectors that describe the direction between opposing seams
            var innerCrosses = new Vector3d[4];
            var outerCrosses = new Vector3d[4];

            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 2).Modulus(4);
                outerCrosses[i] = seams[ii].To - seams[i].To;
                innerCrosses[i] = seams[ii].From - seams[i].From;
            }

            debug.Add(new Line(seams[0].To, outerCrosses[0]));
            debug.Add(new Line(seams[1].To, outerCrosses[1]));
            debug.Add(new Line(seams[0].From, innerCrosses[0]));
            debug.Add(new Line(seams[1].From, innerCrosses[1]));

            var cutterInterval = new Interval(-1000, 1000);

            /* INTERIOR JOINT SURFACES */

            // Construct cutters
            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 3).Modulus(4);

                Point3d origin;
                Vector3d xaxis, yaxis;
                double d = CutterExtension;
                double lip = CutterLipWidth;

                // Handle first seam

                var pts = new Point3d[8];
                var cutter0 = new Brep[4];

                // Construct outer surface for first seam
                origin = seams[i].To;
                xaxis = seams[i].From - seams[i].To;
                yaxis = outerCrosses[i];

                Plane pOuter0 = new Plane(origin, xaxis, yaxis);
                var projOuter0 = this.Plane.ProjectAlongVector(xaxis);

                pts[0] = origin + pOuter0.XAxis * d + pOuter0.YAxis * d;
                pts[1] = origin + pOuter0.XAxis * d - pOuter0.YAxis * CutterToleranceExtension;
                pts[2] = origin - pOuter0.XAxis * d - pOuter0.YAxis * CutterToleranceExtension;
                pts[3] = origin - pOuter0.XAxis * d + pOuter0.YAxis * d;

                pts[0].Transform(projOuter0); pts[0] = pts[0] - pOuter0.XAxis * lip;
                pts[1].Transform(projOuter0); pts[1] = pts[1] - pOuter0.XAxis * lip;

                cutter0[0] = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

                // Construct inner surface for first seam
                origin = seams[i].From;
                xaxis = seams[i].To - seams[i].From;
                yaxis = innerCrosses[i];

                Plane pInner0 = new Plane(origin, xaxis, yaxis);
                var projInner0 = this.Plane.ProjectAlongVector(xaxis);

                pts[4] = origin + pInner0.XAxis * d + pInner0.YAxis * d;
                pts[5] = origin + pInner0.XAxis * d - pInner0.YAxis * CutterToleranceExtension;
                pts[6] = origin - pInner0.XAxis * d - pInner0.YAxis * CutterToleranceExtension;
                pts[7] = origin - pInner0.XAxis * d + pInner0.YAxis * d;

                pts[4].Transform(projInner0); pts[4] = pts[4] - pInner0.XAxis * lip;
                pts[5].Transform(projInner0); pts[5] = pts[5] - pInner0.XAxis * lip;

                cutter0[1] = Brep.CreateFromCornerPoints(pts[4], pts[5], pts[6], pts[7], 0.01);
                cutter0[2] = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[5], 0.01);
                cutter0[3] = Brep.CreateFromCornerPoints(pts[5], pts[4], pts[0], 0.01);

                debug.Add(pts[0]);
                //debug.Add(pts[1]);
                //debug.Add(pts[4]);
                //debug.Add(pts[5]);

                var cutter0Joined = Brep.JoinBreps(cutter0, 0.01);

                Parts[i].Geometry.AddRange(cutter0Joined);

                // Handle second seam

                var cutter1 = new Brep[4];

                // Construct outer surface for second seam
                origin = seams[ii].To;
                xaxis = seams[ii].From - seams[ii].To;
                yaxis = outerCrosses[ii];

                Plane pOuter1 = new Plane(origin, xaxis, yaxis);
                var projOuter1 = this.Plane.ProjectAlongVector(xaxis);

                pts[0] = origin + pOuter1.XAxis * d + pOuter1.YAxis * d;
                pts[1] = origin + pOuter1.XAxis * d - pOuter1.YAxis * CutterToleranceExtension;
                pts[2] = origin - pOuter1.XAxis * d - pOuter1.YAxis * CutterToleranceExtension;
                pts[3] = origin - pOuter1.XAxis * d + pOuter1.YAxis * d;

                pts[0].Transform(projOuter1); pts[0] = pts[0] - pOuter1.XAxis * lip;
                pts[1].Transform(projOuter1); pts[1] = pts[1] - pOuter1.XAxis * lip;

                cutter1[0] = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

                // Construct inner surface for second seam
                origin = seams[ii].From;
                xaxis = seams[ii].To - seams[ii].From;
                yaxis = innerCrosses[ii];

                Plane pInner1 = new Plane(origin, xaxis, yaxis);
                var projInner1 = this.Plane.ProjectAlongVector(xaxis);

                pts[4] = origin + pInner1.XAxis * d + pInner1.YAxis * d;
                pts[5] = origin + pInner1.XAxis * d - pInner1.YAxis * CutterToleranceExtension;
                pts[6] = origin - pInner1.XAxis * d - pInner1.YAxis * CutterToleranceExtension;
                pts[7] = origin - pInner1.XAxis * d + pInner1.YAxis * d;

                pts[4].Transform(projInner1); pts[4] = pts[4] - pInner1.XAxis * lip;
                pts[5].Transform(projInner1); pts[5] = pts[5] - pInner1.XAxis * lip;

                cutter1[1] = Brep.CreateFromCornerPoints(pts[4], pts[5], pts[6], pts[7], 0.01);
                //cutter1[2] = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[5], pts[4], 0.01);
                cutter1[2] = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[5], 0.01);
                cutter1[3] = Brep.CreateFromCornerPoints(pts[5], pts[4], pts[0], 0.01);

                var cutter1Joined = Brep.JoinBreps(cutter1, 0.01);

                Parts[i].Geometry.AddRange(cutter1Joined);

            }

            /* PLATE AND DOWELS */
            // Create interior plate cutter and dowel positions

            var endPlanes = new Plane[4];
            var dowelPlanes = new Plane[4];
            var dowelCutters = new Brep[4];

            for (int i = 0; i < 4; ++i)
            {
                var dir = dirs[i];
                dir.Unitize();

                endPlanes[i] = new Plane(this.Plane.Origin + dir * PlateLength, dir);
                var dowelPt = this.Plane.Origin + dir * DowelPosition;

                var dp = beams[i].GetPlane(dowelPt);
                if (!DowelsPerpendicular)
                    dp = new Plane(dp.Origin, dp.XAxis, seams[i].Direction);

                debug.Add(endPlanes[i]);

                dowelCutters[i] = new Cylinder(
                  new Circle(new Plane(dp.Origin - dp.YAxis * DowelLength * 0.5, dp.YAxis),
                  DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

                dowelPlanes[i] = dp;
            }

            var platePlane0 = new Plane(this.Plane.Origin - this.Plane.ZAxis * PlateThickness * 0.5,
              this.Plane.XAxis, this.Plane.YAxis);

            var platePlane1 = new Plane(this.Plane.Origin + this.Plane.ZAxis * PlateThickness * 0.5,
              this.Plane.XAxis, this.Plane.YAxis);

            var platePts = new Point3d[4];

            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 1).Modulus(4);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(
                  endPlanes[i], endPlanes[ii], platePlane0, out platePts[i]);
            }

            // Create plate outline and cutter
            var plateOutline = new Polyline() { platePts[0], platePts[1], platePts[2], platePts[3], platePts[0] }.ToNurbsCurve();
            var plateSrf = Brep.CreateTrimmedPlane(platePlane0, plateOutline);
            Brep[] outBlends, outWalls;
            var plateBrep = Brep.CreateOffsetBrep(plateSrf, PlateThickness, true, true, 0.01, out outBlends, out outWalls)[0];
            plateBrep.Flip();

            for (int i = 0; i < 4; ++i)
            {
                Parts[i].Geometry.Add(plateBrep);
                Parts[i].Geometry.Add(dowelCutters[i]);
                //debug.Add(dowelCutters[i]);
            }

            return true;
        }
    }
}
