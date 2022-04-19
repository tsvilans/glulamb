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

        public static double DefaultPlateThickness = 20.0;
        public static double DefaultPlateDepth = 80.0;
        public static double DefaultMaxPlateDepth = 120;

        public static double DefaultPlateLength = 100.0;
        public static double DefaultDowelPosition = 70.0;
        public static double DefaultDowelDiameter = 12.0;
        public static double DefaultDowelLength = 140.0;

        public static double DefaultCutterExtension = 150;
        public static double DefaultCutterToleranceExtension = 10;
        public static double DefaultCutterLipWidth = 5.0;

        public Brep InnerSurface;
        public Brep OuterSurface;
        public Vector3d Normal;

        /// <summary>
        /// Thickness of connector plate.
        /// </summary>
        public double PlateThickness = 21.0;

        /// <summary>
        /// Depth (or width) of plate
        /// </summary>
        public double PlateDepth = 80.0;
        public double MaxPlateDepth = 120;


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
        public double CutterExtension = 150;

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

        public double Added = 50.0;
        public double AddedSlot = 50.0;
        public double AddedPlaneOffset = 150;

        public double ToolDiameter = 16.0;

        protected Plane[] EndPlanes;
        protected Plane[] SeamPlanes;
        protected Plane[] LeftPlanes;
        protected Plane[] RightPlanes;
        protected Line[] Seams;

        protected Plane PlatePlane { get; set; }
        protected double SlotLength = 100;

        public FourWayJoint_Split(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateThickness = DefaultPlateThickness;
            PlateDepth = DefaultPlateDepth;
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


        private Brep CreatePlateSlot(int index, Vector3d vec)
        {
            var endPlane = EndPlanes[index];
            var sidePlane = LeftPlanes[index];

            //vec = PlatePlane.Project(vec);
            //vec.Unitize();

            Point3d xpt;
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlatePlane, out xpt);

            //debug.Add(xpt);

            var xAxis = PlatePlane.Project(endPlane.ZAxis);
            var yAxis = PlatePlane.Project(sidePlane.ZAxis);

            int sign = index > 0 ? -1 : 1;
            sign = 1;

            var plane = new Plane(xpt, Vector3d.CrossProduct(yAxis, xAxis) * sign, xAxis);
            vec = -plane.ZAxis;
            //if (vec * plane.ZAxis < 0) vec.Reverse();

            double tilt = Math.Abs(vec * plane.ZAxis);
            double depth = PlateDepth + Added;
            double offset = 0;
            double offsetSqrt = 0;

            if (tilt < 1 || false)
            {
                double tiltedDepth = depth / tilt;
                offsetSqrt = Math.Pow(tiltedDepth, 2) - Math.Pow(depth, 2);
                offset = double.IsNaN(offsetSqrt) || offsetSqrt <= 0 ? 0 : Math.Sqrt(offsetSqrt);
                depth = Math.Min(MaxPlateDepth, tiltedDepth);
            }
            //debug.Add(plane);

            //debug.Add(new Point3d(999, 0, offsetSqrt));

            plane.Origin = xpt + (plane.ZAxis * Added) + endPlane.ZAxis * offset;

            var pts = new Point3d[4];

            var sillProj = this.Plane.ProjectAlongVector(endPlane.ZAxis);
            var sxpt = xpt;
            sxpt.Transform(sillProj);

            double slot_length = xpt.DistanceTo(sxpt) + ToolDiameter + AddedSlot;
            slot_length = xpt.DistanceTo(this.Plane.Origin) + ToolDiameter + AddedSlot;

            var hwidth = PlateThickness * 0.5;


            pts[0] = plane.PointAt(hwidth, 0);
            pts[1] = plane.PointAt(-hwidth, 0);
            pts[2] = plane.PointAt(-hwidth, -slot_length);
            pts[3] = plane.PointAt(hwidth, -slot_length);


            var poly = new Polyline(pts);
            poly.Add(poly[0]);

            //debug.Add(poly);

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile

            var extrusion = Extrusion.CreateExtrusion(profile, vec * (depth + Added));
            Brep extBrep = extrusion.ToBrep();
            extBrep.CapPlanarHoles(0.01);

            //debug.Add(extBrep);

            return extBrep;
        }

        private Brep CreateCrossCutter(int index)
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

            if (false)
            {
                debug.Add(inner0);
                debug.Add(outer0);
                debug.Add(new Line((inner0.Origin + outer0.Origin) * 0.5, -normal0, 200));

                debug.Add(inner1);
                debug.Add(outer1);
                debug.Add(new Line((inner1.Origin + outer1.Origin) * 0.5, -normal1, 200));
            }

            Line seamInner;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(inner0, inner1, out seamInner);
            var sInnerProj = ppInner.ProjectAlongVector(seamInner.Direction);

            Line seamOuter;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(outer0, outer1, out seamOuter);
            var sOuterProj = ppOuter.ProjectAlongVector(seamOuter.Direction);

            var inner0Proj = ppInner.ProjectAlongVector(inner0.XAxis);
            var outer0Proj = ppOuter.ProjectAlongVector(outer0.XAxis);
            var inner1Proj = ppInner.ProjectAlongVector(inner1.XAxis);
            var outer1Proj = ppOuter.ProjectAlongVector(outer1.XAxis);

            var inner0Pts = new Point3d[4];
            var inner1Pts = new Point3d[4];
            var outer0Pts = new Point3d[4];
            var outer1Pts = new Point3d[4];

            var breps = new Brep[14];
            // Inner surface 0
            {
                inner0Pts[0] = inner0.Origin; inner0Pts[0].Transform(plateInnerPlane.ProjectAlongVector(inner0.XAxis));
                inner0Pts[1] = inner0.Origin; inner0Pts[1].Transform(inner0Proj);
                inner0Pts[2] = seamInner.From; inner0Pts[2].Transform(sInnerProj);
                inner0Pts[3] = inner0Pts[2]; inner0Pts[3].Transform(plateInnerPlane.ProjectAlongVector(seamInner.Direction));

                var brep0 = Brep.CreateFromCornerPoints(inner0Pts[0], inner0Pts[1], inner0Pts[2], inner0Pts[3], 0.01);
                breps[0] = brep0;
            }
            // Inner surface 1
            {
                inner1Pts[0] = inner1.Origin; inner1Pts[0].Transform(plateInnerPlane.ProjectAlongVector(inner1.XAxis));
                inner1Pts[1] = inner1.Origin; inner1Pts[1].Transform(inner1Proj);
                inner1Pts[2] = seamInner.From; inner1Pts[2].Transform(sInnerProj);
                inner1Pts[3] = inner1Pts[2]; inner1Pts[3].Transform(plateInnerPlane.ProjectAlongVector(seamInner.Direction));

                var brep1 = Brep.CreateFromCornerPoints(inner1Pts[0], inner1Pts[1], inner1Pts[2], inner1Pts[3], 0.01);
                breps[1] = brep1;
            }
            // Outer surface 0
            {
                outer0Pts[0] = outer0.Origin; outer0Pts[0].Transform(plateOuterPlane.ProjectAlongVector(outer0.XAxis));
                outer0Pts[1] = outer0.Origin; outer0Pts[1].Transform(outer0Proj);
                outer0Pts[2] = seamOuter.From; outer0Pts[2].Transform(sOuterProj);
                outer0Pts[3] = outer0Pts[2]; outer0Pts[3].Transform(plateOuterPlane.ProjectAlongVector(seamOuter.Direction));

                var brep2 = Brep.CreateFromCornerPoints(outer0Pts[0], outer0Pts[1], outer0Pts[2], outer0Pts[3], 0.01);
                breps[2] = brep2;
            }
            // Outer surface 1
            {
                outer1Pts[0] = outer1.Origin; outer1Pts[0].Transform(plateOuterPlane.ProjectAlongVector(outer1.XAxis));
                outer1Pts[1] = outer1.Origin; outer1Pts[1].Transform(outer1Proj);
                outer1Pts[2] = seamOuter.From; outer1Pts[2].Transform(sOuterProj);
                outer1Pts[3] = outer1Pts[2]; outer1Pts[3].Transform(plateOuterPlane.ProjectAlongVector(seamOuter.Direction));

                var brep3 = Brep.CreateFromCornerPoints(outer1Pts[0], outer1Pts[1], outer1Pts[2], outer1Pts[3], 0.01);
                breps[3] = brep3;
            }

            // Flap 0
            var fPts = new Point3d[8];
            {
                fPts[0] = inner0Pts[0];
                fPts[1] = inner0Pts[1];
                fPts[2] = outer0Pts[1];
                fPts[3] = outer0Pts[0];


                fPts[4] = fPts[0] - normal0 * Added;
                fPts[5] = fPts[1] - normal0 * Added;
                fPts[6] = fPts[2] - normal0 * Added;
                fPts[7] = fPts[3] - normal0 * Added;

                var flap00 = Brep.CreateFromCornerPoints(fPts[0], fPts[1], fPts[5], fPts[4], 0.01);
                var flap01 = Brep.CreateFromCornerPoints(fPts[1], fPts[2], fPts[6], fPts[5], 0.01);
                var flap02 = Brep.CreateFromCornerPoints(fPts[2], fPts[3], fPts[7], fPts[6], 0.01);

                breps[4] = flap00;
                breps[5] = flap01;
                breps[6] = flap02;
            }
            // Flap 1
            {
                fPts[0] = inner1Pts[0];
                fPts[1] = inner1Pts[1];
                fPts[2] = outer1Pts[1];
                fPts[3] = outer1Pts[0];

                fPts[4] = fPts[0] - normal1 * Added;
                fPts[5] = fPts[1] - normal1 * Added;
                fPts[6] = fPts[2] - normal1 * Added;
                fPts[7] = fPts[3] - normal1 * Added;

                var flap10 = Brep.CreateFromCornerPoints(fPts[0], fPts[1], fPts[5], fPts[4], 0.01);
                var flap11 = Brep.CreateFromCornerPoints(fPts[1], fPts[2], fPts[6], fPts[5], 0.01);
                var flap12 = Brep.CreateFromCornerPoints(fPts[2], fPts[3], fPts[7], fPts[6], 0.01);

                breps[7] = flap10;
                breps[8] = flap11;
                breps[9] = flap12;
            }

            // Create inner surfaces
            {
                breps[10] = Brep.CreateFromCornerPoints(outer0Pts[2], outer1Pts[1], inner0Pts[2], 0.01);
                breps[11] = Brep.CreateFromCornerPoints(inner0Pts[2], outer1Pts[1], inner1Pts[1], 0.01);

                breps[12] = Brep.CreateFromCornerPoints(outer1Pts[2], outer0Pts[1], inner1Pts[2], 0.01);
                breps[13] = Brep.CreateFromCornerPoints(inner1Pts[2], outer0Pts[1], inner0Pts[1], 0.01);
            }

            //debug.Add(seamInner);
            //debug.Add(seamOuter);

            Brep cutter = Brep.JoinBreps(breps, 0.01)[0];
            cutter.Standardize();
            return cutter;

        }
        public override bool Construct(bool append = false)
        {
            debug = new List<object>();

            if (InnerSurface == null || OuterSurface == null) throw new Exception("Surfaces not defined!");

            // Sort elements around the joint normal
            var dirs = SortPartsClockwise();

            var beams = new Beam[4];
            for (int i = 0; i < 4; ++i)
                beams[i] = (Parts[i].Element as BeamElement).Beam;

            // Get beam planes for each beam
            var planes = new Plane[4];
            for (int i = 0; i < 4; ++i)
                planes[i] = beams[i].GetPlane(this.Plane.Origin);

            // Construct proper planes for each element
            for (int i = 0; i < 4; ++i)
            {
                int signX = 1, signY = 1, signZ = 1;
                if (planes[i].ZAxis * dirs[i] < 0) signZ = -1;
                if (planes[i].YAxis * Normal < 0) signY = -1;

                planes[i] = new Plane(planes[i].Origin, planes[i].XAxis * signX * signZ, planes[i].YAxis * signY);
            }

            //debug.Add(Plane);
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
            Seams = new Line[4];
            SeamPlanes = new Plane[4];
            LeftPlanes = new Plane[4];
            RightPlanes = new Plane[4];

            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 1).Modulus(4);
                SeamPlanes[i] = new Plane(planes[i].Origin + planes[i].XAxis * beams[i].Width * 0.5,
                  planes[i].ZAxis, planes[i].YAxis);

                LeftPlanes[i] = new Plane(planes[i].Origin + planes[i].XAxis * beams[i].Width * 0.5,
                  planes[i].ZAxis, planes[i].YAxis);

                RightPlanes[i] = new Plane(planes[i].Origin - planes[i].XAxis * beams[i].Width * 0.5,
                  planes[i].ZAxis, planes[i].YAxis);
            }

            PlatePlane = new Plane(this.Plane.Origin - this.Plane.ZAxis,
              this.Plane.XAxis, this.Plane.YAxis);

            // Create seam lines for each pair of arms
            for (int i = 0; i < 4; ++i)
            {
                int ii = (i + 1).Modulus(4);

                // Create side planes for each seam
                var p1 = RightPlanes[i];
                var p0 = LeftPlanes[ii];

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

                Seams[i] = new Line(innerPt, outerPt);
                //debug.Add(innerPt);
                //debug.Add(outerPt);
            }

            //debug.Add(new Line(seams[0].To, outerCrosses[0]));
            //debug.Add(new Line(seams[1].To, outerCrosses[1]));
            //debug.Add(new Line(seams[0].From, innerCrosses[0]));
            //debug.Add(new Line(seams[1].From, innerCrosses[1]));

            /* ******************************* */
            /* INTERIOR JOINT SURFACES */
            /* ******************************* */
            for (int i = 0; i < 4; ++i)
            {
                var cutter = CreateCrossCutter(i);
                Parts[i].Geometry.Add(cutter);
            }

            /* ******************************* */
            /* PLATE AND DOWELS */
            /* ******************************* */

            // Create interior plate cutter and dowel positions
            EndPlanes = new Plane[4];
            var dowelPlanes = new Plane[4];
            var dowelCutters = new Brep[4];

            for (int i = 0; i < 4; ++i)
            {
                var dir = dirs[i];
                dir = PlatePlane.Project(dir);
                dir.Unitize();
                var cpt = PlatePlane.ClosestPoint(this.Plane.Origin);

                // Check for neighbouring planes and extend plate length to
                // accommodate tool radius in plate fillet

                EndPlanes[i] = new Plane(cpt + dir * PlateLength, dir);

                var dowelPt = this.Plane.Origin + dir * DowelPosition;

                var dp = beams[i].GetPlane(dowelPt);
                dp = new Plane(dp.Origin, dp.XAxis, Seams[i].Direction);

                dowelCutters[i] = new Cylinder(
                  new Circle(new Plane(dp.Origin - dp.YAxis * DowelLength * 0.5, dp.YAxis),
                  DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

                dowelPlanes[i] = dp;

                Parts[i].Geometry.Add(CreatePlateSlot(i, planes[i].XAxis));
                Parts[i].Geometry.Add(dowelCutters[i]);
            }

            return true;
        }
    }
}
