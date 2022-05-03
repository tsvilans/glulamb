using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;
using Rhino;
using Rhino.Collections;

namespace GluLamb.Joints
{
    public class KJoint_Plate : VBeamJoint
    {
        public static double DefaultPlateDepth = 60.0;
        public static double DefaultPlateThickness = 20.0;

        public static double DefaultDowelPosition = 60;
        public static double DefaultDowelLength = 220.0;
        public static double DefaultDowelDiameter = 12.0;

        public static bool DefaultSingleInsertionDirection = true;

        public static int DefaultMode = 0;

        public static double DefaultCutterSize = 300;

        public double PlateDepth = 60.0;
        public double PlateThickness = 20.0;

        public double DowelPosition = 60;
        public double DowelLength = 220.0;
        public double DowelDiameter = 12.0;

        public bool SingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public int Mode = 0;

        public double CutterSize = 300;

        public KJoint_Plate(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateDepth = DefaultPlateDepth;
            PlateThickness = DefaultPlateThickness;

            DowelPosition = DefaultDowelPosition;
            DowelLength = DefaultDowelLength;
            DowelDiameter = DefaultDowelDiameter;

            SingleInsertionDirection = DefaultSingleInsertionDirection;
            Mode = DefaultMode;

            CutterSize = DefaultCutterSize;
    }

        public override string ToString()
        {
            return "KJoint_Plate";
        }

        public override bool Construct(bool append = false)
        {

            debug = new List<object>();
            var cutterInterval = new Interval(-CutterSize, CutterSize);

            var beams = Parts.Select(x => (x.Element as BeamElement).Beam).ToArray();

            var kplane = beams[2].GetPlane(Parts[2].Parameter);
            debug.Add(kplane);

            var dirs = new Vector3d[2];
            var planes = new Plane[2];

            for (int i = 0; i < 2; ++i)
            {
                var d0 = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - kplane.Origin;
                //debug.Add(new Line(kplane.Origin, d0));
                planes[i] = beams[i].GetPlane(Parts[i].Parameter);
                //debug.Add(planes[i]);
                int signX = 1, signY = 1, signZ = 1;

                if (planes[i].ZAxis * d0 < 0) signZ = -1;
                if (planes[i].YAxis * kplane.YAxis < 0) signY = -1;

                planes[i] = new Plane(planes[i].Origin, planes[i].XAxis * signX * signZ, planes[i].YAxis * signY);
                dirs[i] = planes[i].ZAxis;
                debug.Add(planes[i]);
            }

            // dsum is actually the plate insertion direction
            var dsum = dirs[0] + dirs[1];
            dsum.Unitize();

            var xside = dsum * kplane.XAxis < 0 ? -1 : 1;

            // Check the order of the V-beams and flip if necessary.
            // This is important for getting correct Seam and Outside planes, among other things.
            if (((dirs[0] * kplane.ZAxis) < (dirs[1] * kplane.ZAxis) && xside > 0) ||
              ((dirs[0] * kplane.ZAxis) > (dirs[1] * kplane.ZAxis) && xside < 0))
            {
                var dirTemp = dirs[0];
                dirs[0] = dirs[1];
                dirs[1] = dirTemp;

                var planeTemp = planes[0];
                planes[0] = planes[1];
                planes[1] = planeTemp;

                var beamTemp = beams[0];
                beams[0] = beams[1];
                beams[1] = beamTemp;

                var partTemp = Parts[0];
                Parts[0] = Parts[1];
                Parts[1] = partTemp;
            }

            debug.Add(new Line(kplane.Origin, dsum * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[0] * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[1] * 200.0));

            // Find correct plane axis
            Vector3d xaxis;
            double dotx = kplane.XAxis * dsum;
            double doty = kplane.YAxis * dsum;
            double width;

            if (Math.Abs(dotx) > Math.Abs(doty))
            {
                width = beams[2].Width;
                if (dotx < 0)
                    xaxis = -kplane.XAxis;
                else
                    xaxis = kplane.XAxis;
            }
            else
            {
                width = beams[2].Height;
                if (doty < 0)
                    xaxis = -kplane.YAxis;
                else
                    xaxis = kplane.YAxis;
            }

            var yaxis = kplane.YAxis;

            this.Plane = new Plane(kplane.Origin, xaxis, kplane.ZAxis);

            // plane0 is the plane on top of the sill beam, where the two V-beams meet
            var plane0 = new Plane(kplane.Origin + xaxis * width * 0.5, kplane.ZAxis, kplane.YAxis);
            // planeOffset0 is the safety plane for making cutters with a slight overlap
            var planeOffset0 = new Plane(plane0.Origin - plane0.ZAxis * 3.0, plane0.XAxis, plane0.YAxis);
            //debug.Add(plane0);
            // dowelPlane is the plane on which all the dowel points lie
            var dowelPlane = new Plane(plane0.Origin + xaxis * DowelPosition, plane0.XAxis, plane0.YAxis);

            //debug.Add(dowelPlane);

            // Create sill cutters
            for (int i = 0; i < 2; ++i)
            {
                Point3d xpt;
                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[i].Centreline, plane0, 0.01);
                if (res != null && res.Count > 0)
                    xpt = res[0].PointA;
                else
                    xpt = plane0.ClosestPoint(planes[i].Origin);

                var cutterPlane = new Plane(xpt, plane0.XAxis, plane0.YAxis);
                var cutterRec = new Rectangle3d(cutterPlane, cutterInterval, cutterInterval);
                var cutterBrep = Brep.CreatePlanarBreps(new Curve[] { cutterRec.ToNurbsCurve() }, 0.01)[0];

                Parts[i].Geometry.Add(cutterBrep);
            }

            // END sill cutters

            // Find the seam between the V-beams

            // Find all seam and outside planes, including offsets

            double PlaneOffset = 5.0;
            var seamPlanes = new Plane[2];
            var seamOffsetPlanes = new Plane[2];

            var endPlanes = new Plane[2];
            var endPts = new Point3d[2];
            endPts[0] = planes[0].Origin;
            endPts[0].Transform(dowelPlane.ProjectAlongVector(planes[0].ZAxis));
            endPts[0] = endPts[0] + planes[0].ZAxis * 30.0;

            endPts[1] = planes[1].Origin;
            endPts[1].Transform(dowelPlane.ProjectAlongVector(planes[1].ZAxis));
            endPts[1] = endPts[1] + planes[1].ZAxis * 30.0;

            endPlanes[0] = new Plane(endPts[0], planes[0].XAxis, planes[0].YAxis);
            endPlanes[1] = new Plane(endPts[1], planes[1].XAxis, planes[1].YAxis);

            //debug.Add(endPlanes[0]);
            //debug.Add(endPlanes[1]);

            seamPlanes[0] = new Plane(planes[0].Origin + planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            seamPlanes[1] = new Plane(planes[1].Origin - planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);


            var outPlanes = new Plane[2];
            var outOffsetPlanes = new Plane[2];
            outPlanes[0] = new Plane(planes[0].Origin - planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            outPlanes[1] = new Plane(planes[1].Origin + planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);

            int sign = -1;

            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;
                seamOffsetPlanes[i] = new Plane(seamPlanes[i].Origin + seamPlanes[i].ZAxis * sign * PlaneOffset, seamPlanes[i].XAxis, seamPlanes[i].YAxis);
                outOffsetPlanes[i] = new Plane(outPlanes[i].Origin - outPlanes[i].ZAxis * sign * PlaneOffset, outPlanes[i].XAxis, outPlanes[i].YAxis);

                //debug.Add(seamPlanes[i]);
                //debug.Add(outPlanes[i]);
                //debug.Add(seamOffsetPlanes[i]);
                //debug.Add(outOffsetPlanes[i]);
            }

            Plane seamPlane;
            Rectangle3d seamRec;
            Brep seamBrep;

            // Pick method of intersecting the 2 arms beams
            switch (Mode)
            {
                case (-1): // beam0 into the side of beam1
                    seamRec = new Rectangle3d(seamPlanes[1], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[0].Geometry.Add(seamBrep);
                    break;
                case (1): // beam1 into the side of beam0
                    seamRec = new Rectangle3d(seamPlanes[0], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[1].Geometry.Add(seamBrep);
                    break;
                default: // centre split
                    Line seam;
                    Rhino.Geometry.Intersect.Intersection.PlanePlane(seamPlanes[0], seamPlanes[1], out seam);

                    seamPlane = new Plane(seam.From, seam.Direction, dsum);

                    seamRec = new Rectangle3d(seamPlane, cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];

                    Parts[0].Geometry.Add(seamBrep);
                    Parts[1].Geometry.Add(seamBrep);
                    break;
            }

            // END seam

            // Find plate plane and figure out geometry
            var platePlane = new Plane(kplane.Origin, dsum, dirs[0]);
            platePlane.Origin = platePlane.Origin - platePlane.ZAxis * PlateThickness * 0.5;
            var sillproj = plane0.ProjectAlongVector(dsum);

            var pts = new Point3d[9];

            var proj0 = platePlane.ProjectAlongVector(planes[0].YAxis);
            var proj1 = platePlane.ProjectAlongVector(planes[1].YAxis);

            // Get first arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlanes[0], outOffsetPlanes[0], platePlane, out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlanes[0], seamOffsetPlanes[0], platePlane, out pts[1]);

            if (SingleInsertionDirection)
            {
                pts[1] = pts[0];
                pts[1].Transform(seamOffsetPlanes[0].ProjectAlongVector(dsum));
            }

            // Get point on seam
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[0], seamOffsetPlanes[1], platePlane, out pts[2]);

            // Get second arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlanes[1], outOffsetPlanes[1], platePlane, out pts[3]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlanes[1], seamOffsetPlanes[1], platePlane, out pts[4]);

            if (SingleInsertionDirection)
            {
                pts[4] = pts[3];
                pts[4].Transform(seamOffsetPlanes[1].ProjectAlongVector(dsum));
            }

            // Get intersection of first arm and sill
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[0], planeOffset0, platePlane, out pts[5]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[1], planeOffset0, platePlane, out pts[6]);

            for (int i = 0; i < 9; ++i)
                debug.Add(pts[i]);

            // Create outline for arms
            var plateOutline0 = new Polyline() { pts[0], pts[1], pts[2], pts[4], pts[3], pts[6], pts[5], pts[0] };
            debug.Add(plateOutline0);

            var plateSrf0 = Brep.CreateTrimmedPlane(platePlane, plateOutline0.ToNurbsCurve());
            plateSrf0.Transform(Transform.Translation(platePlane.ZAxis * -PlateThickness * 0.5));

            Brep[] outBlends, outWalls;
            var plateBreps = Brep.CreateOffsetBrep(plateSrf0, PlateThickness, true, true, 0.01, out outBlends, out outWalls);
            if (plateBreps != null && plateBreps.Length > 0)
            {
                if (plateBreps[0].SolidOrientation != BrepSolidOrientation.Outward)
                    plateBreps[0].Flip();

                for (int i = 0; i < 2; ++i)
                {
                    Parts[i].Geometry.Add(plateBreps[0]);
                }
            }

            //var plateBrep0 = Brep.CreateOffsetBrep(plateSrf0, PlateThickness, true, true, 0.01, out outBlends, out outWalls)[0];
            //plateBrep0.Flip();

            //for (int i = 0; i < 2; ++i)
            //{
            //  Parts[i].Geometry.Add(plateBrep0);
            //}

            // Create outline for sill
            planeOffset0.Origin = plane0.Origin + plane0.ZAxis * 5.0;
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outPlanes[0], plane0, platePlane, out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outPlanes[1], plane0, platePlane, out pts[1]);

            pts[2] = pts[1] - dsum * PlateDepth;
            pts[3] = pts[0] - dsum * PlateDepth;

            pts[0] = pts[0] + dsum * 10.0;
            pts[1] = pts[1] + dsum * 10.0;

            var plateOutline1 = new Polyline() { pts[0], pts[1], pts[2], pts[3], pts[0] };
            debug.Add(plateOutline1);

            var plateSrf1 = Brep.CreateTrimmedPlane(platePlane, plateOutline1.ToNurbsCurve());

            plateBreps = Brep.CreateOffsetBrep(plateSrf1, PlateThickness, true, true, 0.01, out outBlends, out outWalls);
            if (plateBreps != null && plateBreps.Length > 0)
            {
                if (plateBreps[0].SolidOrientation != BrepSolidOrientation.Outward)
                    plateBreps[0].Flip();

                this.Beam.Geometry.Add(plateBreps[0]);
            }

            //var plateBrep1 = Brep.CreateOffsetBrep(plateSrf1, PlateThickness, true, true, 0.01, out outBlends, out outWalls)[0];
            //plateBrep1.Flip();

            //this.Beam.Geometry.Add(plateBrep1);

            // Create dowels
            var dowelPoints = new Point3d[3];

            var dproj0 = dowelPlane.ProjectAlongVector(planes[0].ZAxis);
            var dproj1 = dowelPlane.ProjectAlongVector(planes[1].ZAxis);

            dowelPoints[0] = planes[0].PointAt(0, 0, DowelPosition); dowelPoints[0].Transform(dproj0);
            dowelPoints[1] = planes[1].PointAt(0, 0, DowelPosition); dowelPoints[1].Transform(dproj1);
            dowelPoints[2] = kplane.Origin;

            var dowelPlane2 = new Plane(dowelPoints[2] - kplane.YAxis * DowelLength * 0.5, kplane.YAxis);
            var dowelCyl2 = new Cylinder(
              new Circle(dowelPlane2, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            this.Beam.Geometry.Add(dowelCyl2);

            for (int i = 0; i < 2; ++i)
            {
                var dowelPlane01 = new Plane(dowelPoints[i] - planes[i].YAxis * DowelLength * 0.5, planes[i].YAxis);
                var dowelCyl = new Cylinder(
                  new Circle(dowelPlane01, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

                Parts[i].Geometry.Add(dowelCyl);

            }

            // END plate

            return true;
        }
    }

    public class KJoint_Plate4 : VBeamJoint
    {
        public static double DefaultPlateDepth = 60.0;
        public static double DefaultPlateWidth = 60.0;
        public static double DefaultPlateThickness = 20.0;

        public static double DefaultDowelPosition = 60;
        public static double DefaultDowelLength = 220.0;
        public static double DefaultDowelDiameter = 12.0;

        public double DefaultAdded = 20.0;
        public double DefaultAddedSlot = 50;
        public double DefaultMaxPlateDepth = 120;

        public static bool DefaultSingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public static int DefaultMode = 0;

        public static double DefaultCutterSize = 300;

        public double PlateDepth = 60.0;
        public double PlateWidth = 60.0;
        public double PlateThickness = 20.0;
        public double ToolDiameter = 16.0;

        public double DowelPosition = 60;
        public double DowelLength = 220.0;
        public double DowelDiameter = 12.0;
        public double Added = 20.0;
        public double AddedSlot = 50;
        public double MaxPlateDepth = 120;

        public bool SingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public int Mode = 0;

        public double CutterSize = 300;

        // *******************
        // Private variables
        // *******************


        /// <summary>
        /// Planes on the inside of the arms, in the seam.
        /// </summary>
        protected Plane[] SeamPlanes;

        /// <summary>
        /// Planes on the ouside of the arms, away from the seam.
        /// </summary>
        protected Plane[] OutsidePlanes;

        /// <summary>
        /// Planes at the end of the arm slots
        /// </summary>
        protected Plane[] EndPlanes;

        /// <summary>
        /// Plane on the beam side.
        /// </summary>
        protected Plane SillPlane;

        /// <summary>
        /// Plane that the connector plate lies on
        /// </summary>
        protected Plane PlatePlane;


        public KJoint_Plate4(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateDepth = DefaultPlateDepth;
            PlateWidth = DefaultPlateWidth;
            PlateThickness = DefaultPlateThickness;

            DowelPosition = DefaultDowelPosition;
            DowelLength = DefaultDowelLength;
            DowelDiameter = DefaultDowelDiameter;

            Added = DefaultAdded;
            AddedSlot = DefaultAddedSlot;
            MaxPlateDepth = DefaultMaxPlateDepth;

            SingleInsertionDirection = DefaultSingleInsertionDirection;
            Mode = DefaultMode;

            CutterSize = DefaultCutterSize;
        }


        public override string ToString()
        {
            return "KJoint_Plate";
        }

        private Brep CreateSillCutter()
        {
            var hlength = PlateWidth * 0.5;
            var hwidth = PlateThickness * 0.5;

            var plane = new Plane(SillPlane.Origin, SillPlane.XAxis, PlatePlane.ZAxis);
            plane.Origin = plane.Origin - plane.ZAxis * Added;

            var pts = new Point3d[4];
            pts[0] = plane.PointAt(hlength, hwidth);
            pts[1] = plane.PointAt(-hlength, hwidth);
            pts[2] = plane.PointAt(-hlength, -hwidth);
            pts[3] = plane.PointAt(hlength, -hwidth);

            var poly = new Polyline(pts);
            poly.Add(poly[0]);
            var polyCrv = poly.ToNurbsCurve();
            if (polyCrv == null) return null;

            var profile = Curve.CreateFilletCornersCurve(polyCrv, ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile
            //Extrusion extrusion = Extrusion.CreateExtrusion(profile, SillPlane.ZAxis * -PlateDepth);

            var extrusion = Extrusion.Create(profile, PlateDepth + Added, true);
            return extrusion.ToBrep(true);
        }

        private Brep CreatePlateSlot(int index, Vector3d vec)
        {
            var endPlane = EndPlanes[index];
            var sidePlane = SeamPlanes[index];
            vec.Unitize();

            Point3d xpt;
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlatePlane, out xpt);

            var xAxis = PlatePlane.Project(endPlane.ZAxis);
            var yAxis = PlatePlane.Project(sidePlane.ZAxis);

            int sign = index > 0 ? -1 : 1;

            var plane = new Plane(xpt, Vector3d.CrossProduct(yAxis, xAxis) * sign, xAxis);

            double tilt = Math.Abs(vec * plane.ZAxis);
            double depth = PlateDepth + Added;
            double offset = 0;

            if (!RhinoMath.EpsilonEquals(tilt, 1.0, RhinoMath.Epsilon))
            {
                double tiltedDepth = depth / tilt;
                double offsetSqrt = Math.Pow(tiltedDepth, 2) - Math.Pow(depth, 2);
                offset = double.IsNaN(offsetSqrt) || offsetSqrt <= 0 ? 0 : Math.Sqrt(offsetSqrt);
                depth = Math.Min(MaxPlateDepth, tiltedDepth);
            }

            if (double.IsNaN(offset)) offset = 0;

            plane.Origin = xpt + (plane.ZAxis * Added) + endPlane.ZAxis * offset;

            var pts = new Point3d[4];

            var sillProj = SillPlane.ProjectAlongVector(endPlane.ZAxis);
            var sxpt = xpt;
            sxpt.Transform(sillProj);

            double slot_length = xpt.DistanceTo(sxpt) + ToolDiameter + AddedSlot;
            var hwidth = PlateThickness * 0.5;

            pts[0] = plane.PointAt(hwidth, 0);
            pts[1] = plane.PointAt(-hwidth, 0);
            pts[2] = plane.PointAt(-hwidth, -slot_length);
            pts[3] = plane.PointAt(hwidth, -slot_length);

            var poly = new Polyline(pts);
            poly.Add(poly[0]);

            debug.Add(poly);

            var polyCrv = poly.ToNurbsCurve();
            if (polyCrv == null) return null;

            var profile = Curve.CreateFilletCornersCurve(polyCrv, ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile

            var extrusion = Extrusion.CreateExtrusion(profile, vec * (depth + Added));
            Brep extBrep = extrusion.ToBrep();
            extBrep.CapPlanarHoles(0.01);

            debug.Add(extBrep);

            return extBrep;
        }

        public override bool Construct(bool append = false)
        {

            debug = new List<object>();
            var cutterInterval = new Interval(-CutterSize, CutterSize);

            var beams = Parts.Select(x => (x.Element as BeamElement).Beam).ToArray();

            var kplane = beams[2].GetPlane(Parts[2].Parameter);
            debug.Add(kplane);

            var dirs = new Vector3d[2];
            var planes = new Plane[2];

            for (int i = 0; i < 2; ++i)
            {
                var d0 = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - kplane.Origin;
                planes[i] = beams[i].GetPlane(Parts[i].Parameter);
                int signX = 1, signY = 1, signZ = 1;

                if (planes[i].ZAxis * d0 < 0) signZ = -1;
                if (planes[i].YAxis * kplane.YAxis < 0) signY = -1;

                planes[i] = new Plane(planes[i].Origin, planes[i].XAxis * signX * signZ, planes[i].YAxis * signY);
                dirs[i] = planes[i].ZAxis;
                debug.Add(planes[i]);
            }

            // dsum is actually the plate insertion direction
            var dsum = dirs[0] + dirs[1];
            dsum.Unitize();

            var xside = dsum * kplane.XAxis < 0 ? -1 : 1;

            // Check the order of the V-beams and flip if necessary.
            // This is important for getting correct Seam and Outside planes, among other things.
            if (((dirs[0] * kplane.ZAxis) < (dirs[1] * kplane.ZAxis) && xside > 0) ||
              ((dirs[0] * kplane.ZAxis) > (dirs[1] * kplane.ZAxis) && xside < 0))
            {
                var dirTemp = dirs[0];
                dirs[0] = dirs[1];
                dirs[1] = dirTemp;

                var planeTemp = planes[0];
                planes[0] = planes[1];
                planes[1] = planeTemp;

                var beamTemp = beams[0];
                beams[0] = beams[1];
                beams[1] = beamTemp;

                var partTemp = Parts[0];
                Parts[0] = Parts[1];
                Parts[1] = partTemp;
            }

            debug.Add(new Line(kplane.Origin, dsum * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[0] * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[1] * 200.0));

            // Find correct plane axis
            Vector3d xaxis;
            double dotx = kplane.XAxis * dsum;
            double doty = kplane.YAxis * dsum;
            double width;

            if (Math.Abs(dotx) > Math.Abs(doty))
            {
                width = beams[2].Width;
                if (dotx < 0)
                    xaxis = -kplane.XAxis;
                else
                    xaxis = kplane.XAxis;
            }
            else
            {
                width = beams[2].Height;
                if (doty < 0)
                    xaxis = -kplane.YAxis;
                else
                    xaxis = kplane.YAxis;
            }

            var yaxis = kplane.YAxis;

            this.Plane = new Plane(kplane.Origin, xaxis, kplane.ZAxis);

            // plane0 is the plane on top of the sill beam, where the two V-beams meet
            SillPlane = new Plane(kplane.Origin + xaxis * width * 0.5, kplane.ZAxis, kplane.YAxis);


            // planeOffset0 is the safety plane for making cutters with a slight overlap
            var planeOffset0 = new Plane(SillPlane.Origin - SillPlane.ZAxis * 3.0, SillPlane.XAxis, SillPlane.YAxis);
            //debug.Add(plane0);

            // dowelPlane is the plane on which all the dowel points lie
            var dowelPlane = new Plane(SillPlane.Origin + xaxis * DowelPosition, SillPlane.XAxis, SillPlane.YAxis);

            //debug.Add(dowelPlane);

            // Create sill cutters
            for (int i = 0; i < 2; ++i)
            {
                Point3d xpt;
                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[i].Centreline, SillPlane, 0.01);
                if (res != null && res.Count > 0)
                    xpt = res[0].PointA;
                else
                    xpt = SillPlane.ClosestPoint(planes[i].Origin);

                var cutterPlane = new Plane(xpt, SillPlane.XAxis, SillPlane.YAxis);
                var cutterRec = new Rectangle3d(cutterPlane, cutterInterval, cutterInterval);
                var cutterBrep = Brep.CreatePlanarBreps(new Curve[] { cutterRec.ToNurbsCurve() }, 0.01)[0];

                Parts[i].Geometry.Add(cutterBrep);
            }

            // END sill cutters

            // Find the seam between the V-beams

            // Find all seam and outside planes, including offsets

            double PlaneOffset = 5.0;
            SeamPlanes = new Plane[2];
            var seamOffsetPlanes = new Plane[2];

            EndPlanes = new Plane[2];
            var endPts = new Point3d[2];
            endPts[0] = planes[0].Origin;
            endPts[0].Transform(dowelPlane.ProjectAlongVector(planes[0].ZAxis));
            endPts[0] = endPts[0] + planes[0].ZAxis * 15;

            endPts[1] = planes[1].Origin;
            endPts[1].Transform(dowelPlane.ProjectAlongVector(planes[1].ZAxis));
            endPts[1] = endPts[1] + planes[1].ZAxis * 15;

            EndPlanes[0] = new Plane(endPts[0], planes[0].XAxis, planes[0].YAxis);
            EndPlanes[1] = new Plane(endPts[1], planes[1].XAxis, planes[1].YAxis);

            SeamPlanes[0] = new Plane(planes[0].Origin + planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            SeamPlanes[1] = new Plane(planes[1].Origin - planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);


            OutsidePlanes = new Plane[2];
            var outOffsetPlanes = new Plane[2];
            OutsidePlanes[0] = new Plane(planes[0].Origin - planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            OutsidePlanes[1] = new Plane(planes[1].Origin + planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);

            int sign = -1;

            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;
                seamOffsetPlanes[i] = new Plane(SeamPlanes[i].Origin + SeamPlanes[i].ZAxis * sign * PlaneOffset, SeamPlanes[i].XAxis, SeamPlanes[i].YAxis);
                outOffsetPlanes[i] = new Plane(OutsidePlanes[i].Origin - OutsidePlanes[i].ZAxis * sign * PlaneOffset, OutsidePlanes[i].XAxis, OutsidePlanes[i].YAxis);
            }

            Plane seamPlane;
            Rectangle3d seamRec;
            Brep seamBrep;

            // Pick method of intersecting the 2 arms beams
            switch (Mode)
            {
                case (-1): // beam0 into the side of beam1
                    seamRec = new Rectangle3d(SeamPlanes[1], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[0].Geometry.Add(seamBrep);
                    break;
                case (1): // beam1 into the side of beam0
                    seamRec = new Rectangle3d(SeamPlanes[0], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[1].Geometry.Add(seamBrep);
                    break;
                default: // centre split
                    Line seam;
                    Rhino.Geometry.Intersect.Intersection.PlanePlane(SeamPlanes[0], SeamPlanes[1], out seam);

                    seamPlane = new Plane(seam.From, seam.Direction, dsum);

                    seamRec = new Rectangle3d(seamPlane, cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];

                    Parts[0].Geometry.Add(seamBrep);
                    Parts[1].Geometry.Add(seamBrep);
                    break;
            }

            // *****************
            // END seam
            // *****************

            // Find plate plane and figure out geometry
            PlatePlane = new Plane(kplane.Origin, dsum, dirs[0]);
            PlatePlane.Origin = PlatePlane.Origin - PlatePlane.ZAxis * PlateThickness * 0.5;
            var sillproj = SillPlane.ProjectAlongVector(dsum);

            var pts = new Point3d[9];


            //dsum = PlatePlane.Project(dsum);
            var proj0 = PlatePlane.ProjectAlongVector(planes[0].YAxis);
            var proj1 = PlatePlane.ProjectAlongVector(planes[1].YAxis);

            // Get first arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[0], outOffsetPlanes[0], PlatePlane, out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[0], seamOffsetPlanes[0], PlatePlane, out pts[1]);


            Vector3d[] SlotDirections = new Vector3d[2];

            if (SingleInsertionDirection)
            {
                pts[1] = pts[0];
                pts[1].Transform(seamOffsetPlanes[0].ProjectAlongVector(dsum));
            }

            SlotDirections[0] = pts[0] - pts[1];

            // Get point on seam
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[0], seamOffsetPlanes[1], PlatePlane, out pts[2]);

            // Get second arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[1], outOffsetPlanes[1], PlatePlane, out pts[3]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[1], seamOffsetPlanes[1], PlatePlane, out pts[4]);

            if (SingleInsertionDirection)
            {
                pts[4] = pts[3];
                pts[4].Transform(seamOffsetPlanes[1].ProjectAlongVector(dsum));
            }


            SlotDirections[1] = pts[3] - pts[4];

            // Get intersection of arms and sill

            switch (Mode)
            {
                case (1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[0], planeOffset0, PlatePlane, out pts[6]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[0], planeOffset0, PlatePlane, out pts[5]);
                    break;
                case (-1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[1], planeOffset0, PlatePlane, out pts[5]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[1], planeOffset0, PlatePlane, out pts[6]);
                    break;
                default:
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[0], planeOffset0, PlatePlane, out pts[5]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outOffsetPlanes[1], planeOffset0, PlatePlane, out pts[6]);
                    break;
            }

            for (int i = 0; i < 2; ++i)
            {
                var slot = CreatePlateSlot(i, SlotDirections[i]);
                if (slot != null)
                    Parts[i].Geometry.Add(slot);
            }

            // ***********************
            // Create outline for sill
            // ***********************

            planeOffset0.Origin = SillPlane.Origin + SillPlane.ZAxis * 5.0;

            switch (Mode)
            {
                case (1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[0], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], SillPlane, PlatePlane, out pts[1]);
                    break;
                case (-1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(seamOffsetPlanes[1], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], SillPlane, PlatePlane, out pts[1]);
                    break;
                default:
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], SillPlane, PlatePlane, out pts[1]);
                    break;
            }

            //dsum = plane0.ZAxis;
            pts[2] = pts[1] - dsum * PlateDepth;
            pts[3] = pts[0] - dsum * PlateDepth;

            pts[0] = pts[0] + dsum * 10.0;
            pts[1] = pts[1] + dsum * 10.0;

            SillPlane.Origin = (pts[0] + pts[1]) * 0.5;
            var tenon = CreateSillCutter();
            if (tenon != null)
                this.Beam.Geometry.Add(tenon);
            /*
            var plateOutline1 = new Polyline() { pts[0], pts[1], pts[2], pts[3], pts[0] };
            debug.Add(plateOutline1);

            var plateSrf1 = Brep.CreateTrimmedPlane(PlatePlane, plateOutline1.ToNurbsCurve());
            plateSrf1.Transform(Transform.Translation(PlatePlane.ZAxis * -PlateThickness * 0.5));

            plateBreps = Brep.CreateOffsetBrep(plateSrf1, PlateThickness, true, true, 0.01, out outBlends, out outWalls);
            if (plateBreps != null && plateBreps.Length > 0)
            {
              if (plateBreps[0].SolidOrientation != BrepSolidOrientation.Outward)
                plateBreps[0].Flip();

              this.Beam.Geometry.Add(plateBreps[0]);
            }
            */

            //var plateBrep1 = Brep.CreateOffsetBrep(plateSrf1, PlateThickness, true, true, 0.01, out outBlends, out outWalls)[0];
            //plateBrep1.Flip();

            //this.Beam.Geometry.Add(plateBrep1);

            // ***********************
            // Create dowels
            // ***********************
            var dowelPoints = new Point3d[3];

            Transform dproj0, dproj1;

            dproj0 = dowelPlane.ProjectAlongVector(planes[0].ZAxis);
            dproj1 = dowelPlane.ProjectAlongVector(planes[1].ZAxis);

            var dx0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[0].Centreline, dowelPlane, 0.01);
            var dx1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[1].Centreline, dowelPlane, 0.01);

            dowelPoints[0] = beams[0].GetPlane(dx0[0].PointA).Origin;
            dowelPoints[1] = beams[1].GetPlane(dx1[0].PointA).Origin;

            //dowelPoints[0] = planes[0].PointAt(0, 0, DowelPosition); dowelPoints[0].Transform(dproj0);
            //dowelPoints[1] = planes[1].PointAt(0, 0, DowelPosition); dowelPoints[1].Transform(dproj1);
            var portalDowelPoint = (pts[0] + pts[1]) * 0.5;
            dowelPoints[2] = beams[2].GetPlane(portalDowelPoint).Origin;
            //dowelPoints[2] = kplane.Origin;

            var dowelPlane2 = new Plane(dowelPoints[2] - kplane.YAxis * DowelLength * 0.5, kplane.YAxis);
            var dowelCyl2 = new Cylinder(
              new Circle(dowelPlane2, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            this.Beam.Geometry.Add(dowelCyl2);

            for (int i = 0; i < 2; ++i)
            {
                var dowelPlane01 = new Plane(dowelPoints[i] - planes[i].YAxis * DowelLength * 0.5, planes[i].YAxis);
                var dowelCyl = new Cylinder(
                  new Circle(dowelPlane01, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

                Parts[i].Geometry.Add(dowelCyl);

            }

            // END plate

            return true;
        }
    }

    public class KJoint_Plate5 : GluLamb.Joints.VBeamJoint, IPlateJoint
    {
        public static double DefaultPlateDepth = 50.0;
        public static double DefaultPlateSlotDepth = 50.0;
        public static double DefaultPlateThickness = 20.0;

        public static double DefaultDowelPosition = 50;
        public static double DefaultDowelLength = 250.0;
        public static double DefaultDowelDiameter = 12.0;

        public static bool DefaultSingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public static int DefaultMode = 0;

        public static double DefaultCutterSize = 300;

        ConnectorPlate Plate;

        public double PlateDepth = 50.0;
        public double PlateSlotDepth = 50.0;
        public double PlateWidth = 80.0;
        public double PlateThickness = 20.0;
        public double PlateOffset = 0.0;
        public double ToolDiameter = 16.0;

        public double MaxFilletRadius { get; set; }

        public double DowelPosition = 60;
        public double DowelLength { get; set; }
        public double DowelLengthExtra { get; set; }
        public double DowelDiameter { get; set; }
        public List<Dowel> Dowels { get; set; }
        public double Added = 5.0;
        public double AddedSlot = 100;

        public bool SingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public int Mode = 0;

        public double CutterSize = 300;

        // *******************
        // Protected variables
        // *******************

        /// <summary>
        /// Planes on the inside of the arms, in the seam.
        /// </summary>
        protected Plane[] SeamPlanes;
        protected Plane[] SeamOffsetPlanes;

        /// <summary>
        /// Planes on the ouside of the arms, away from the seam.
        /// </summary>
        protected Plane[] OutsidePlanes;
        protected Plane[] OutsideOffsetPlanes;

        /// <summary>
        /// Planes at the end of the arm slots
        /// </summary>
        protected Plane[] EndPlanes;

        /// <summary>
        /// Plane on the beam side.
        /// </summary>
        protected Plane SillPlane;
        protected Plane SillOffsetPlane;
        /// <summary>
        /// Planes for tenon sides.
        /// </summary>
        protected Plane[] TenonSidePlanes;


        /// <summary>
        /// Plane that the connector plate lies on
        /// </summary>
        protected Plane PlatePlane;
        /// <summary>
        /// Planes for top and bottom of plate.
        /// </summary>
        protected Plane[] PlateFacePlanes;


        /// <summary>
        /// Plane that all dowel holes lie on, at least their origins
        /// </summary>
        protected Plane[] DowelOffsetPlanes;

        protected Vector3d InsertionVector;



        public KJoint_Plate5(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateDepth = DefaultPlateDepth;
            PlateThickness = DefaultPlateThickness;
            PlateSlotDepth = DefaultPlateSlotDepth;
            MaxFilletRadius = ToolDiameter;

            DowelPosition = DefaultDowelPosition;
            DowelLength = DefaultDowelLength;
            DowelDiameter = DefaultDowelDiameter;

            SingleInsertionDirection = DefaultSingleInsertionDirection;
            Mode = DefaultMode;

            CutterSize = DefaultCutterSize;
        }

        public override string ToString()
        {
            return "KJoint_Plate";
        }

        public void SetTenonPlanes()
        {
            // Calculate slot corners
            var pts = new Point3d[4];

            switch (Mode)
            {
                case (1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], PlateFacePlanes[0], SillPlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], PlateFacePlanes[1], SillPlane, out pts[1]);

                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[0], PlateFacePlanes[0], SillPlane, out pts[2]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[0], PlateFacePlanes[1], SillPlane, out pts[3]);
                    break;
                case (-1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[1], PlateFacePlanes[0], SillPlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[1], PlateFacePlanes[1], SillPlane, out pts[1]);

                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], PlateFacePlanes[0], SillPlane, out pts[2]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], PlateFacePlanes[1], SillPlane, out pts[3]);
                    break;
                default:
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], PlateFacePlanes[0], SillPlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], PlateFacePlanes[1], SillPlane, out pts[1]);

                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], PlateFacePlanes[0], SillPlane, out pts[2]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], PlateFacePlanes[1], SillPlane, out pts[3]);
                    break;
            }

            var dot00 = Math.Abs((pts[0] - SillPlane.Origin) * SillPlane.XAxis);
            var dot01 = Math.Abs((pts[1] - SillPlane.Origin) * SillPlane.XAxis);
            int index0 = dot00 > dot01 ? 0 : 1;

            var pt0 = pts[index0];
            //var dot0 = Math.Min(dot00, dot01);

            var dot10 = Math.Abs((pts[2] - SillPlane.Origin) * SillPlane.XAxis);
            var dot11 = Math.Abs((pts[3] - SillPlane.Origin) * SillPlane.XAxis);
            int index1 = dot00 > dot01 ? 2 : 3;

            //var dot1 = Math.Max(dot10, dot11);
            var pt1 = pts[index1];

            pt0 = PlatePlane.ClosestPoint(pt0);
            pt1 = PlatePlane.ClosestPoint(pt1);

            double ToolRadius = ToolDiameter * 0.5;
            double angle;

            Line[] lines = new Line[3];
            Rhino.Geometry.Intersect.Intersection.PlanePlane(SillPlane, PlateFacePlanes[0], out lines[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlane(OutsidePlanes[0], PlateFacePlanes[index0], out lines[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlane(OutsidePlanes[1], PlateFacePlanes[index1 - 2], out lines[2]);

            angle = Vector3d.VectorAngle(lines[0].Direction, -lines[1].Direction);
            var length0 = ToolRadius / Math.Tan(angle / 2) + ToolRadius;

            angle = Vector3d.VectorAngle(lines[0].Direction, lines[2].Direction);
            var length1 = ToolRadius / Math.Tan(angle / 2) + ToolRadius;

            pt0 = pt0 - SillPlane.XAxis * length0;
            pt1 = pt1 + SillPlane.XAxis * length1;

            TenonSidePlanes = new Plane[2];
            //TenonSidePlanes[0] = new Plane(SillPlane.Origin + SillPlane.XAxis * dot0,
            //  SillPlane.XAxis);
            //TenonSidePlanes[1] = new Plane(SillPlane.Origin + SillPlane.XAxis * (dot1 + ToolDiameter * 2),
            //  -SillPlane.XAxis);

            TenonSidePlanes[0] = new Plane(pt0,
              SillPlane.XAxis);
            TenonSidePlanes[1] = new Plane(pt1,
              -SillPlane.XAxis);
        }

        public Brep CreatePlate()
        {
            //var insertionVector = PlatePlane.Project(-SillPlane.ZAxis);
            //insertionVector.Unitize();
            //insertionVector = -PlatePlane.XAxis;

            var TenonEndPlane = new Plane(SillPlane.Origin + InsertionVector * PlateDepth, InsertionVector);
            Plate = new ConnectorPlate();
            Plate.Thickness = PlateThickness;
            Plate.Plane = PlatePlane;

            var objs = new List<object>();

            // ******************
            // Top plate outline
            // ******************

            var pts = new Point3d[11];

            double dotYp = 0, dotYn = 0;

            Curve[] FaceLoops = new Curve[2];

            Curve[,] Segments = new Curve[2, 6];
            double[,] FilletRadii = new double[2, 6];
            double radius = ToolDiameter * 0.5;

            for (int i = 0; i < 2; ++i)
            {
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], TenonSidePlanes[0], SillPlane, out pts[0]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], OutsidePlanes[0], SillPlane, out pts[1]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], EndPlanes[0], OutsidePlanes[0], out pts[2]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], EndPlanes[0], SeamPlanes[0], out pts[3]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], SeamPlanes[0], SeamPlanes[1], out pts[4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], EndPlanes[1], SeamPlanes[1], out pts[5]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], EndPlanes[1], OutsidePlanes[1], out pts[6]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], OutsidePlanes[1], SillPlane, out pts[7]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], TenonSidePlanes[1], SillPlane, out pts[8]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], TenonSidePlanes[1], TenonEndPlane, out pts[9]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], TenonSidePlanes[0], TenonEndPlane, out pts[10]);

                Utility.MaxFilletRadius(pts[0], pts[1], pts[2], out FilletRadii[i, 0], radius);
                Utility.MaxFilletRadius(pts[3], pts[4], pts[5], out FilletRadii[i, 2], radius);
                Utility.MaxFilletRadius(pts[6], pts[7], pts[8], out FilletRadii[i, 4], radius);

                FilletRadii[i, 0] = Math.Min(FilletRadii[i, 0], MaxFilletRadius);
                FilletRadii[i, 2] = Math.Min(FilletRadii[i, 2], MaxFilletRadius);
                FilletRadii[i, 4] = Math.Min(FilletRadii[i, 4], MaxFilletRadius);


                var poly = new Polyline(pts);
                poly.Add(poly[0]);

                Plate.Outlines[i] = poly;

                Segments[i, 0] = new Polyline(new Point3d[] { pts[0], pts[1], pts[2] }).ToNurbsCurve();
                Segments[i, 1] = new Polyline(new Point3d[] { pts[2], pts[3] }).ToNurbsCurve();
                Segments[i, 2] = new Polyline(new Point3d[] { pts[3], pts[4], pts[5] }).ToNurbsCurve();
                Segments[i, 3] = new Polyline(new Point3d[] { pts[5], pts[6] }).ToNurbsCurve();
                Segments[i, 4] = new Polyline(new Point3d[] { pts[6], pts[7], pts[8] }).ToNurbsCurve();
                Segments[i, 5] = new Polyline(new Point3d[] { pts[8], pts[9], pts[10], pts[0] }).ToNurbsCurve();

                FaceLoops[i] = poly.ToNurbsCurve();
            }

            var faceSegs = new List<Curve>[2];

            for (int i = 0; i < 2; ++i)
            {
                faceSegs[i] = new List<Curve>();

                Segments[i, 0] = Curve.CreateFilletCornersCurve(Segments[i, 0], FilletRadii[i, 0], 0.01, 0.01);
                Segments[i, 2] = Curve.CreateFilletCornersCurve(Segments[i, 2], FilletRadii[i, 2], 0.01, 0.01);
                Segments[i, 4] = Curve.CreateFilletCornersCurve(Segments[i, 4], FilletRadii[i, 4], 0.01, 0.01);


                for (int j = 0; j < 6; ++j)
                    faceSegs[i].Add(Segments[i, j]);
            }

            FaceLoops[0] = Curve.JoinCurves(faceSegs[0], 0.01)[0];
            FaceLoops[1] = Curve.JoinCurves(faceSegs[1], 0.01)[0];

            Brep[] Fragments = new Brep[6];
            for (int i = 0; i < 6; ++i)
            {
                var res = Brep.CreateFromLoft(new Curve[] { Segments[0, i], Segments[1, i] }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (res != null && res.Length > 0)
                    Fragments[i] = res[0];
            }
            objs.AddRange(Fragments);

            var breps = new List<Brep>();
            var topFace = Brep.CreatePlanarBreps(FaceLoops[0], 0.1);
            //objs.Add(topFace[0]);
            var btmFace = Brep.CreatePlanarBreps(FaceLoops[1], 0.1);
            //objs.Add(btmFace[0]);

            var sides = Brep.CreateFromLoft(FaceLoops, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);


            breps.AddRange(topFace);
            breps.AddRange(btmFace);
            breps.AddRange(sides);

            var joined = Brep.JoinBreps(breps, 0.01);
            joined[0].Faces.SplitKinkyFaces(0.1);

            Plate.Geometry = joined[0];

            return joined[0];
        }

        public ConnectorPlate GetConnectorPlate()
        {
            return Plate;
        }

        protected Brep CreateSillCutter()
        {
            var xaxis = PlatePlane.Project(SillPlane.XAxis);

            var plane = new Plane(SillPlane.Origin, xaxis, PlatePlane.ZAxis);
            plane.Origin = plane.Origin - plane.ZAxis * Added;

            var pts = new Point3d[4];

            var tsp0 = TenonSidePlanes[0];
            var tsp1 = TenonSidePlanes[1];
            double TENON_TOLERANCE = 0.5;

            int sign = tsp0.ZAxis * (tsp1.Origin - tsp0.Origin) > 0 ? 1 : -1;
            tsp0.Origin = tsp0.Origin - tsp0.ZAxis * TENON_TOLERANCE * sign;
            tsp1.Origin = tsp1.Origin - tsp1.ZAxis * TENON_TOLERANCE * sign;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[0], tsp0, out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[1], tsp0, out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[1], tsp1, out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[0], tsp1, out pts[3]);

            var poly = new Polyline(pts);
            poly.Add(poly[0]);

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile
                                                                                                               //Extrusion extrusion = Extrusion.CreateExtrusion(profile, SillPlane.ZAxis * -PlateDepth);
            profile.TryGetPlane(out Plane profilePlane);
            if (profilePlane.ZAxis * InsertionVector < 0)
                profile.Reverse();

            var extrusion = Extrusion.Create(profile, PlateSlotDepth + Added, true);
            return extrusion.ToBrep(true);
        }

        protected Brep CreatePlateSlot(int index, Vector3d vec)
        {
            double PLATE_END_TOLERANCE = 1.0;
            var endPlane = EndPlanes[index];
            endPlane.Origin = endPlane.Origin + endPlane.ZAxis * PLATE_END_TOLERANCE;
            var sidePlane = SeamPlanes[index];
            var outsidePlane = OutsidePlanes[index];

            var sillPlane = SillPlane;
            sillPlane.Origin = sillPlane.Origin - sillPlane.ZAxis * Added;

            int sign = sidePlane.ZAxis * (sidePlane.Origin - outsidePlane.Origin) > 0 ? 1 : -1;

            sidePlane.Origin = sidePlane.Origin + sidePlane.ZAxis * Added * sign;
            outsidePlane.Origin = outsidePlane.Origin - outsidePlane.ZAxis * Added * sign;

            // *************************
            // Plane intersection method
            // *************************

            var pts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[0], out pts[3]);

            var topLoop = new Polyline(pts);
            topLoop.Add(topLoop[0]);

            var topFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[0], out pts[3]);

            var btmLoop = new Polyline(pts);
            btmLoop.Add(btmLoop[0]);

            var btmFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            var sideFaces = Brep.CreateFromLoft(new Curve[] { topLoop.ToNurbsCurve(), btmLoop.ToNurbsCurve() },
              Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            var breps = new List<Brep>();
            breps.Add(topFace);
            breps.Add(btmFace);
            breps.AddRange(sideFaces);

            var joined = Brep.JoinBreps(breps, 0.01)[0];
            joined.Faces.SplitKinkyFaces(0.1);

            //return joined;

            double r = 8;
            var filleted = Brep.CreateFilletEdges(joined, new int[] { 8, 9 }, new double[] { r, r }, new double[] { r, r },
              BlendType.Fillet, RailType.RollingBall, 0.01);

            return filleted[0];
        }

        protected void CreatePlatePlanes(Point3d origin, Vector3d xaxis, Vector3d yaxis)
        {
            // Find PlatePlane and figure out geometry
            PlatePlane = new Plane(origin, xaxis, yaxis);
            PlatePlane.Origin = PlatePlane.Origin - PlatePlane.ZAxis * PlateThickness * 0.5 + PlatePlane.ZAxis * PlateOffset;

            //PlatePlane = platePlane;

            PlateFacePlanes = new Plane[2];
            PlateFacePlanes[0] = new Plane(
              PlatePlane.Origin + PlatePlane.ZAxis * PlateThickness * 0.5,
              PlatePlane.XAxis,
              PlatePlane.YAxis);

            PlateFacePlanes[1] = new Plane(
              PlatePlane.Origin - PlatePlane.ZAxis * PlateThickness * 0.5,
              PlatePlane.XAxis,
              PlatePlane.YAxis);
        }

        public override bool Construct(bool append = false)
        {
            debug = new List<object>();
            var cutterInterval = new Interval(-CutterSize, CutterSize);

            // ************************************************
            // Initialize basic variables and set some initial
            // base directions.
            // ************************************************

            var beams = Parts.Select(x => (x.Element as BeamElement).Beam).ToArray();
            var kplane = beams[2].GetPlane(Parts[2].Parameter);
            var dirs = new Vector3d[2];
            var planes = new Plane[2];

            for (int i = 0; i < 2; ++i)
            {
                var d0 = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - kplane.Origin;
                planes[i] = beams[i].GetPlane(Parts[i].Parameter);
                int signX = 1, signY = 1, signZ = 1;

                if (planes[i].ZAxis * d0 < 0) signZ = -1;
                if (planes[i].YAxis * kplane.YAxis < 0) signY = -1;

                planes[i] = new Plane(planes[i].Origin, planes[i].XAxis * signX * signZ, planes[i].YAxis * signY);
                dirs[i] = planes[i].ZAxis;
            }

            // **********************************************
            // -dsum is actually the plate insertion direction
            // **********************************************

            var normal = Vector3d.CrossProduct(dirs[0], dirs[1]);
            var normalPlane = new Plane(kplane.Origin, normal);

            var dsum = dirs[0] + dirs[1];
            dsum.Unitize();

            var binormal = kplane.XAxis * dsum > 0? -kplane.XAxis : kplane.XAxis;
            InsertionVector = normalPlane.Project(binormal);

            // ***************
            // PlatePlanes
            // ***************
            CreatePlatePlanes(kplane.Origin, InsertionVector, dirs[0]);

            // *******************************************************
            // Check the order of the V-beams and flip if necessary.
            // This is important for getting correct Seam and Outside planes, among other things.
            // *******************************************************

            var xside = dsum * kplane.XAxis < 0 ? -1 : 1;

            if (((dirs[0] * kplane.ZAxis) < (dirs[1] * kplane.ZAxis) && xside > 0) ||
              ((dirs[0] * kplane.ZAxis) > (dirs[1] * kplane.ZAxis) && xside < 0))
            {
                var dirTemp = dirs[0];
                dirs[0] = dirs[1];
                dirs[1] = dirTemp;

                var planeTemp = planes[0];
                planes[0] = planes[1];
                planes[1] = planeTemp;

                var beamTemp = beams[0];
                beams[0] = beams[1];
                beams[1] = beamTemp;

                var partTemp = Parts[0];
                Parts[0] = Parts[1];
                Parts[1] = partTemp;
            }

            //debug.Add(new Line(kplane.Origin, dsum * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[0] * 200.0));
            //debug.Add(new Line(kplane.Origin, dirs[1] * 200.0));

            // *************************
            // Find correct plane axis
            // *************************

            Vector3d xaxis;
            double dotx = kplane.XAxis * dsum;
            double doty = kplane.YAxis * dsum;
            double width;

            if (Math.Abs(dotx) > Math.Abs(doty))
            {
                width = beams[2].Width;
                if (dotx < 0)
                    xaxis = -kplane.XAxis;
                else
                    xaxis = kplane.XAxis;
            }
            else
            {
                width = beams[2].Height;
                if (doty < 0)
                    xaxis = -kplane.YAxis;
                else
                    xaxis = kplane.YAxis;
            }

            var yaxis = kplane.YAxis;

            this.Plane = new Plane(kplane.Origin, xaxis, kplane.ZAxis);

            // ***************
            // SillPlane
            // ***************

            // SillPlane is the plane on top of the sill beam, where the two V-beams meet
            SillPlane = new Plane(kplane.Origin + xaxis * width * 0.5, kplane.ZAxis, kplane.YAxis);
            if (SillPlane.ZAxis * dsum < 0)
                SillPlane = new Plane(SillPlane.Origin, -SillPlane.XAxis, SillPlane.YAxis);
            debug.Add(SillPlane);

            // SillOffsetPlane is the safety plane for making cutters with a slight overlap
            var SillOffsetPlane = new Plane(SillPlane.Origin - SillPlane.ZAxis * 3.0, SillPlane.XAxis, SillPlane.YAxis);

            // ***************
            // DowelOffsetPlanes
            // ***************

            DowelOffsetPlanes = new Plane[2];

            double Ratio = 0.0;
            var Ratios = new double[2] { Ratio, Ratio };
            var DowelPlaneOffsets = new double[2];

            switch (Mode)
            {
                case (-1):
                    Ratios = new double[2] { 1.0, 0.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition + 10, DowelPosition };
                    break;
                case (1):
                    Ratios = new double[2] { 0.0, 1.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition, DowelPosition + 10 };
                    break;
                default:
                    Ratios = new double[2] { 0.0, 0.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition, DowelPosition };
                    break;
            }

            // DowelOffsetPlanes are the planes on which all the dowel points lie
            for (int i = 0; i < 2; ++i)
                DowelOffsetPlanes[i] = new Plane(SillPlane.Origin + xaxis * DowelPlaneOffsets[i], SillPlane.XAxis, SillPlane.YAxis);
            var dowelPlane = new Plane(SillPlane.Origin + xaxis * DowelPosition, SillPlane.XAxis, SillPlane.YAxis);

            debug.Add(dowelPlane);

            // **********************
            // Sill cutter (end cuts)
            // **********************
            for (int i = 0; i < 2; ++i)
            {
                Point3d xpt;
                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[i].Centreline, SillPlane, 0.01);
                if (res != null && res.Count > 0)
                    xpt = res[0].PointA;
                else
                    xpt = SillPlane.ClosestPoint(planes[i].Origin);

                var cutterPlane = new Plane(xpt, SillPlane.XAxis, SillPlane.YAxis);
                var cutterRec = new Rectangle3d(cutterPlane, cutterInterval, cutterInterval);
                var cutterBrep = Brep.CreatePlanarBreps(new Curve[] { cutterRec.ToNurbsCurve() }, 0.01)[0];

                Parts[i].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[2].Element.Name), cutterPlane);

                Parts[i].Geometry.Add(cutterBrep);
            }


            // *****************************************************
            // Find all seam and outside planes, including offsets
            // *****************************************************

            double PlaneOffset = 5.0;
            SeamPlanes = new Plane[2];
            SeamOffsetPlanes = new Plane[2];

            SeamPlanes[0] = new Plane(planes[0].Origin + planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            SeamPlanes[1] = new Plane(planes[1].Origin - planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);

            OutsidePlanes = new Plane[2];
            OutsideOffsetPlanes = new Plane[2];
            OutsidePlanes[0] = new Plane(planes[0].Origin - planes[0].XAxis * beams[0].Width * 0.5, planes[0].ZAxis, planes[0].YAxis);
            OutsidePlanes[1] = new Plane(planes[1].Origin + planes[1].XAxis * beams[1].Width * 0.5, planes[1].ZAxis, planes[1].YAxis);

            // **********
            // EndPlanes
            // **********

            EndPlanes = new Plane[2];
            var endPts = new Point3d[2];
            endPts[0] = planes[0].Origin;
            endPts[0].Transform(DowelOffsetPlanes[0].ProjectAlongVector(planes[0].ZAxis));
            endPts[0] = endPts[0] + planes[0].ZAxis * DowelPlaneOffsets[0];
            endPts[0].Transform(OutsidePlanes[0].ProjectAlongVector(OutsidePlanes[0].ZAxis));

            endPts[1] = planes[1].Origin;
            endPts[1].Transform(DowelOffsetPlanes[1].ProjectAlongVector(planes[1].ZAxis));
            endPts[1] = endPts[1] + planes[1].ZAxis * DowelPlaneOffsets[1];
            endPts[1].Transform(OutsidePlanes[1].ProjectAlongVector(OutsidePlanes[1].ZAxis));

            if (SingleInsertionDirection)
            {
                EndPlanes[0] = new Plane(endPts[0], PlatePlane.XAxis, planes[0].YAxis);
                EndPlanes[1] = new Plane(endPts[1], -PlatePlane.XAxis, planes[1].YAxis);
            }
            else
            {
                EndPlanes[0] = new Plane(endPts[0], planes[0].XAxis, planes[0].YAxis);
                EndPlanes[1] = new Plane(endPts[1], planes[1].XAxis, planes[1].YAxis);
            }


            int sign = -1;

            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;
                SeamOffsetPlanes[i] = new Plane(SeamPlanes[i].Origin + SeamPlanes[i].ZAxis * sign * PlaneOffset, SeamPlanes[i].XAxis, SeamPlanes[i].YAxis);
                OutsideOffsetPlanes[i] = new Plane(OutsidePlanes[i].Origin - OutsidePlanes[i].ZAxis * sign * PlaneOffset, OutsidePlanes[i].XAxis, OutsidePlanes[i].YAxis);
            }

            Plane seamPlane;
            Rectangle3d seamRec;
            Brep seamBrep;

            // ************************************************
            // Pick method of intersecting the 2 arms beams
            // ************************************************

            switch (Mode)
            {
                case (-1): // beam0 into the side of beam1
                    seamRec = new Rectangle3d(SeamPlanes[1], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[0].Geometry.Add(seamBrep);
                    Parts[0].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[1].Element.Name), SeamPlanes[1]);

                    break;
                case (1): // beam1 into the side of beam0
                    seamRec = new Rectangle3d(SeamPlanes[0], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[1].Geometry.Add(seamBrep);
                    Parts[1].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[0].Element.Name), SeamPlanes[0]);

                    break;
                default: // centre split
                    Line seam;
                    Rhino.Geometry.Intersect.Intersection.PlanePlane(SeamPlanes[0], SeamPlanes[1], out seam);

                    seamPlane = new Plane(seam.From, seam.Direction, dsum);

                    seamRec = new Rectangle3d(seamPlane, cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];

                    Parts[0].Geometry.Add(seamBrep);
                    Parts[1].Geometry.Add(seamBrep);
                    Parts[0].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[1].Element.Name), seamPlane);
                    Parts[1].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[0].Element.Name), seamPlane);

                    break;
            }

            // *****************
            // END seam
            // *****************


            var sillproj = SillPlane.ProjectAlongVector(dsum);

            var pts = new Point3d[9];

            //dsum = PlatePlane.Project(dsum); -> dsum == PlatePlane.XAxis already
            var proj0 = PlatePlane.ProjectAlongVector(planes[0].YAxis);
            var proj1 = PlatePlane.ProjectAlongVector(planes[1].YAxis);

            // Get first arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[0], OutsideOffsetPlanes[0], PlatePlane, out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[0], SeamOffsetPlanes[0], PlatePlane, out pts[1]);

            Vector3d[] SlotDirections = new Vector3d[2];

            if (SingleInsertionDirection)
            {
                pts[1] = pts[0];
                pts[1].Transform(SeamOffsetPlanes[0].ProjectAlongVector(InsertionVector));
            }

            SlotDirections[0] = pts[0] - pts[1];

            // Get point on seam
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[0], SeamOffsetPlanes[1], PlatePlane, out pts[2]);

            // Get second arm of plate
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[1], OutsideOffsetPlanes[1], PlatePlane, out pts[3]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(EndPlanes[1], SeamOffsetPlanes[1], PlatePlane, out pts[4]);

            if (SingleInsertionDirection)
            {
                pts[4] = pts[3];
                pts[4].Transform(SeamOffsetPlanes[1].ProjectAlongVector(InsertionVector));
            }

            SlotDirections[1] = pts[3] - pts[4];

            // Get intersection of arms and sill

            switch (Mode)
            {
                case (1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[0], SillOffsetPlane, PlatePlane, out pts[6]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsideOffsetPlanes[0], SillOffsetPlane, PlatePlane, out pts[5]);
                    break;
                case (-1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[1], SillOffsetPlane, PlatePlane, out pts[5]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsideOffsetPlanes[1], SillOffsetPlane, PlatePlane, out pts[6]);
                    break;
                default:
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsideOffsetPlanes[0], SillOffsetPlane, PlatePlane, out pts[5]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsideOffsetPlanes[1], SillOffsetPlane, PlatePlane, out pts[6]);
                    break;
            }

            for (int i = 0; i < 2; ++i)
            {
                var slot = CreatePlateSlot(i, SlotDirections[i]);
                if (slot != null)
                    Parts[i].Geometry.Add(slot);
            }


            // ***********************
            // Create outline for sill
            // ***********************

            SillOffsetPlane.Origin = SillPlane.Origin + SillPlane.ZAxis * 5.0;

            switch (Mode)
            {
                case (1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[0], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], SillPlane, PlatePlane, out pts[1]);
                    break;
                case (-1):
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SeamOffsetPlanes[1], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], SillPlane, PlatePlane, out pts[1]);
                    break;
                default:
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[0], SillPlane, PlatePlane, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OutsidePlanes[1], SillPlane, PlatePlane, out pts[1]);
                    break;
            }


            //dsum = plane0.ZAxis;
            //pts[2] = pts[1] - dsum * PlateDepth;
            //pts[3] = pts[0] - dsum * PlateDepth;

            //pts[0] = pts[0] + dsum * 10.0;
            //pts[1] = pts[1] + dsum * 10.0;

            SillPlane.Origin = (pts[0] + pts[1]) * 0.5;

            SetTenonPlanes();

            var tenon = CreateSillCutter();
            if (tenon != null)
                this.Beam.Geometry.Add(tenon);

            // ***********************
            // Create clearance cutter for sill
            // ***********************

            var tmpPlateDepth = PlateDepth;
            var tmpPlateWidth = PlateWidth;

            PlateDepth = 15;
            PlateWidth = pts[0].DistanceTo(pts[1]) + ToolDiameter;

            var tenonClearance = CreateSillCutter();
            //if (tenonClearance != null)
            //  this.Beam.Geometry.Add(tenonClearance);
            //debug.Add(tenonClearance);

            PlateDepth = tmpPlateDepth;
            PlateWidth = tmpPlateWidth;

            // ***********************
            // Create dowels
            // ***********************
            var dowelPlanes = new Plane[3];

            // 2022-03-15 align dowel vectors parallel to dowel plane

            //debug.Add(dowelPlane);

            var dowelVectors = new Vector3d[2];

            for (int i = 0; i < 2; ++i)
            {
                //var dproj = dowelPlane.ProjectAlongVector(planes[i].ZAxis);
                var dx = Rhino.Geometry.Intersect.Intersection.CurvePlane(beams[i].Centreline, DowelOffsetPlanes[i], 0.01);

                dowelPlanes[i] = beams[i].GetPlane(dx[0].PointA);
                //debug.Add(dowelPlanes[i]);
                //dowelPoints[i] = dowelPlanes[i].Origin;

                var dRotPlane = new Plane(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis, dowelPlanes[i].YAxis);

                var dot = Math.Abs(dowelPlanes[i].YAxis * kplane.YAxis);
                //Ratios[i] = 1 - dot;

                Line xPP;
                //Rhino.Geometry.Intersect.Intersection.PlanePlane(dRotPlane, dowelPlane, out xPP);
                Rhino.Geometry.Intersect.Intersection.PlanePlane(dRotPlane, DowelOffsetPlanes[i], out xPP);

                dowelVectors[i] = xPP.Direction;
                dowelVectors[i].Unitize();

                if (dowelVectors[i] * dowelPlanes[i].YAxis < 0) dowelVectors[i].Reverse();

                // Simple lerp between dowel vector and plane Y-axis
                dowelVectors[i] = dowelVectors[i] + Ratios[i] * (dowelPlanes[i].YAxis - dowelVectors[i]);

                dowelPlanes[i] = new Plane(dowelPlanes[i].Origin - dowelVectors[i] * DowelLength * 0.5, dowelVectors[i]);
                //dowelPlanes[i] = new Plane(dowelPlanes[i].Origin - planes[i].YAxis * DowelLength * 0.5, planes[i].YAxis);

                debug.Add(dowelPlanes[i]);

                //var dowelPlane01 = new Plane(dowelPoints[i] - planes[i].YAxis * DowelLength * 0.5, planes[i].YAxis);
                var dowelCyl = new Cylinder(
                  new Circle(dowelPlanes[i], DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

                Dowels.Add(new Dowel(new Line(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis * DowelLength), DowelDiameter));

                Parts[i].Geometry.Add(dowelCyl);
            }

            var portalDowelPoint = (pts[0] + pts[1]) * 0.5;
            portalDowelPoint = beams[2].GetPlane(portalDowelPoint).Origin;

            var portalDowelPlane = new Plane(portalDowelPoint - kplane.YAxis * DowelLength * 0.5, kplane.YAxis);
            var portalDowelCyl = new Cylinder(
              new Circle(portalDowelPlane, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            this.Beam.Geometry.Add(portalDowelCyl);

            return true;
        }
    }


    /// <summary>
    /// Variation of the KJoint_Plate joint, with an added joist member. Developed
    /// during the HH DAC project (2022). 
    /// </summary>
    public class KJoint_Plate6Joist : KJoint_Plate6
    {
        public JointPart Joist { 
            get { return Parts[3]; } 
            protected set { Parts[3] = value; } }

        //public new JointPart[] Parts = new JointPart[4];

        public KJoint_Plate6Joist(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            var new_parts = Parts;
            Array.Resize(ref new_parts, 4);
            Parts = new_parts;
        }

        /*
        public KJoint_Plate5Joist(VBeamJoint vbj, Element joist, double t, int i)
        {
          Parts[0] = new JointPart(vbj.Parts[0].Element, this, vbj.Parts[0].Index, vbj.Parts[0].Parameter);
          Parts[1] = new JointPart(vbj.Parts[1].Element, this, vbj.Parts[1].Index, vbj.Parts[1].Parameter);
          Parts[2] = new JointPart(vbj.Parts[2].Element, this, vbj.Parts[2].Index, vbj.Parts[2].Parameter);
          Parts[3] = new JointPart(joist, this, i, t);

          PlateDepth = DefaultPlateDepth;
          PlateThickness = DefaultPlateThickness;

          DowelPosition = DefaultDowelPosition;
          DowelLength = DefaultDowelLength;
          DowelDiameter = DefaultDowelDiameter;

          SingleInsertionDirection = DefaultSingleInsertionDirection;
          Mode = DefaultMode;

          CutterSize = DefaultCutterSize;
        }*/

        public override string ToString()
        {
            return "KJoint_PlateJoist2";
        }

        public void AddJoist(List<Element> elements, JointConditionPart jcp)
        {
            Joist = new JointPart(elements[jcp.Index], this, jcp.Index, jcp.Parameter);
        }

        public Brep CreateJoistCutter(bool is_sill)
        {
            // START joist cutter
            var joist = (Joist.Element as BeamElement).Beam;
            var sill = (Beam.Element as BeamElement).Beam;

            var jPlane = joist.GetPlane(Joist.Parameter);
            var sPlane = sill.GetPlane(Beam.Parameter);


            var jDir = jPlane.ZAxis;
            if (jDir * (joist.Centreline.PointAt(joist.Centreline.Domain.Mid) - jPlane.Origin) < 0)
            {
                jPlane = new Plane(jPlane.Origin, -jPlane.XAxis, jPlane.YAxis);
                jDir.Reverse();
            }
            jDir.Unitize();

            var offsetPlatePlane = PlatePlane;
            var offsetdir = jDir * offsetPlatePlane.ZAxis < 0 ? -1 : 1;
            offsetPlatePlane.Origin = offsetPlatePlane.Origin + offsetPlatePlane.ZAxis * PlateThickness * 0.5 * offsetdir;

            var joistExtended = joist.Centreline.Extend(CurveEnd.Both, 200, CurveExtensionStyle.Line);
            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(joistExtended, offsetPlatePlane, 0.01);
            jPlane.Origin = res[0].PointA;

            debug.Add(jPlane);


            // Hardcoded mess that tries to compensate for the non-perpendicularity of
            // the joist and sill, so that the flat end of the joist cutter extends past
            // the slanted end of the joist
            var plateZProj = Plane.WorldXY.Project(sPlane.ZAxis);
            plateZProj.Unitize();
            var jDirProj = Plane.WorldXY.Project(jDir);
            jDirProj.Unitize();

            var angleFactor = 1 - Math.Abs(plateZProj * jDirProj);

            double thickness = PlateThickness + 5.0;
            double angleOffset = angleFactor > 0 ? thickness / angleFactor : thickness;

            var pt = jPlane.Origin - jPlane.ZAxis * angleOffset;
            debug.Add(pt);

            // End hardcoded mess


            double jhw = joist.Width * 0.5;
            double jhh = joist.Height * 0.5;

            var jEndPlane = new Plane(jPlane.Origin - jDir * (angleOffset), jPlane.XAxis, jPlane.YAxis);

            double insetDistance = 40;
            jPlane.Origin = jPlane.Origin + jDir * insetDistance; // TODO


            var proj = is_sill ? jEndPlane.ProjectAlongVector(jPlane.ZAxis) : offsetPlatePlane.ProjectAlongVector(jPlane.ZAxis);

            double endOffset = 70 / angleFactor - insetDistance;

            var jpts = new Point3d[16];
            jpts[0] = jPlane.PointAt(jhw, jhh);
            jpts[1] = jPlane.PointAt(-jhw, jhh);

            jpts[2] = jPlane.PointAt(jhw, 0);
            jpts[3] = jPlane.PointAt(-jhw, 0);

            jpts[4] = jPlane.PointAt(jhw, 0);
            jpts[5] = jPlane.PointAt(-jhw, 0);

            jpts[6] = jPlane.PointAt(jhw, -jhh - Added);
            jpts[7] = jPlane.PointAt(-jhw, -jhh - Added);

            jpts[8] = jPlane.PointAt(jhw, jhh, endOffset);
            jpts[9] = jPlane.PointAt(-jhw, jhh, endOffset);

            jpts[10] = jPlane.PointAt(jhw, -jhh - Added, endOffset);
            jpts[11] = jPlane.PointAt(-jhw, -jhh - Added, endOffset);

            jpts[12] = jPlane.PointAt(jhw + Added, jhh + Added, endOffset);
            jpts[13] = jPlane.PointAt(-jhw - Added, jhh + Added, endOffset);
            jpts[14] = jPlane.PointAt(jhw + Added, -jhh - Added * 2, endOffset);
            jpts[15] = jPlane.PointAt(-jhw - Added, -jhh - Added * 2, endOffset);


            for (int i = 0; i < 4; ++i)
            {
                jpts[i].Transform(proj);
            }

            int num_faces = is_sill ? 7 : 11;
            var joistCutout = new Brep[num_faces];

            joistCutout[0] = Brep.CreateFromCornerPoints(jpts[0], jpts[1], jpts[3], jpts[2], 0.01);
            joistCutout[1] = Brep.CreateFromCornerPoints(jpts[2], jpts[3], jpts[5], jpts[4], 0.01);
            joistCutout[2] = Brep.CreateFromCornerPoints(jpts[4], jpts[5], jpts[7], jpts[6], 0.01);
            joistCutout[3] = Brep.CreateFromCornerPoints(jpts[6], jpts[7], jpts[11], jpts[10], 0.01);
            joistCutout[4] = Brep.CreateFromCornerPoints(jpts[8], jpts[9], jpts[1], jpts[0], 0.01);
            joistCutout[5] = Brep.CreatePlanarBreps(new Curve[]{
        new Polyline(){jpts[0], jpts[2], jpts[4], jpts[6], jpts[10], jpts[8], jpts[0]}.ToNurbsCurve()}, 0.01)[0];
            joistCutout[6] = Brep.CreatePlanarBreps(new Curve[]{
        new Polyline(){jpts[1], jpts[3], jpts[5], jpts[7], jpts[11], jpts[9], jpts[1]}.ToNurbsCurve()}, 0.01)[0];

            if (!is_sill)
            {
                joistCutout[7] = Brep.CreateFromCornerPoints(jpts[12], jpts[13], jpts[9], jpts[8], 0.01);
                joistCutout[8] = Brep.CreateFromCornerPoints(jpts[13], jpts[15], jpts[11], jpts[9], 0.01);
                joistCutout[9] = Brep.CreateFromCornerPoints(jpts[15], jpts[14], jpts[10], jpts[11], 0.01);
                joistCutout[10] = Brep.CreateFromCornerPoints(jpts[14], jpts[12], jpts[10], jpts[8], 0.01);
            }

            var joined = Brep.JoinBreps(joistCutout, 0.01)[0];
            joined.MergeCoplanarFaces(0.01);


            double ToolRadius = ToolDiameter * 0.5;

            int[] findex = new int[] { 4, 6, 10, 12, 14, 15 };
            double[] radii = new double[] { ToolRadius, ToolRadius, ToolRadius, ToolRadius, ToolRadius, ToolRadius };

            var filleted = Brep.CreateFilletEdges(joined, findex, radii, radii, BlendType.Fillet, RailType.RollingBall, 0.01);

            if (filleted.Length > 0)
            {
                var cutter = filleted[0];
                //cutter.Standardize();
                //cutter.Faces.Reverse();
                return cutter;
            }
            return joined; // If the filleting fails, return null
        }

        public override bool Construct(bool append = false)
        {
            debug = new List<object>();
            base.Construct();

            var cutterInterval = new Interval(-CutterSize, CutterSize);

            var beams = new Beam[4];
            for (int i = 0; i < Parts.Length; ++i)
            {
                beams[i] = (Parts[i].Element as BeamElement).Beam;
            }
            beams[3] = (Joist.Element as BeamElement).Beam;



            // ***********************
            // START joist cutter
            // ***********************

            var joist = (Joist.Element as BeamElement).Beam;
            var jPlane = joist.GetPlane(Joist.Parameter);

            Beam.Geometry.Add(CreateJoistCutter(true));
            Joist.Geometry.Add(CreateJoistCutter(false));

            return true;
        }
        /*
        public void CreateJoistDowels()
        {
          var dowelPoints = new Point3d[3];

          var dproj0 = dowelPlane.ProjectAlongVector(planes[0].ZAxis);
          var dproj1 = dowelPlane.ProjectAlongVector(planes[1].ZAxis);

          dowelPoints[0] = planes[0].PointAt(0, 0, DowelPosition); dowelPoints[0].Transform(dproj0);
          dowelPoints[1] = planes[1].PointAt(0, 0, DowelPosition); dowelPoints[1].Transform(dproj1);
          dowelPoints[2] = kplane.Origin;


          var JoistEndHolePoint = (TenonSidePlanes[0].Origin + TenonSidePlanes[1].Origin) * 0.5;
          var dowelPlane2 = new Plane(dowelPoints[2] - kplane.YAxis * DowelLength * 0.5, kplane.YAxis);

          var dowelPlane3 = new Plane(SillPlane.Origin + SillPlane.XAxis * DowelDiameter - plateSillNormal * 60.0 - SillPlane.ZAxis * DowelLength, SillPlane.ZAxis);

          if (Math.Abs(SillPlane.ZAxis * jPlane.ZAxis) > Math.Cos(RhinoMath.ToRadians(10)))
            dowelPlane3.Rotate(RhinoMath.ToRadians(10), SillPlane.YAxis);

          // Horizontal dowel
          var dowelCyl2 = new Cylinder(
            new Circle(dowelPlane2, DowelDiameter * 0.5), DowelLength * 2).ToBrep(true, true);

          // Vertical dowel
          var dowelCyl3 = new Cylinder(
            new Circle(dowelPlane3, DowelDiameter * 0.5), DowelLength * 2).ToBrep(true, true);

          // Beam and joist dowels
          this.Beam.Geometry.Add(dowelCyl2);
          this.Joist.Geometry.Add(dowelCyl2);

          this.Beam.Geometry.Add(dowelCyl3);
          this.Joist.Geometry.Add(dowelCyl3);

          for (int i = 0; i < 2; ++i)
          {
            var daxis = dowelPlane.Project(planes[i].YAxis);
            var dowelPlane01 = new Plane(dowelPoints[i] - daxis * DowelLength * 0.5, daxis);
            var dowelCyl = new Cylinder(
              new Circle(dowelPlane01, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            Parts[i].Geometry.Add(dowelCyl);
          }
        }
      */
    }

    public class KJoint_Plate6 : GluLamb.Joints.VBeamJoint, IPlateJoint, IDowelJoint
    {
        public static double DefaultPlateDepth = 50.0;
        public static double DefaultPlateThickness = 20.0;
        public static double DefaultPlateSlotDepth = 50.0;
        public static double DefaultPlateOffset = 0;
        public static double DefaultPlateEndOffset = 0;

        public static double DefaultDowelPosition = 40;
        public static double DefaultDowelLength = 130;
        public static double DefaultDowelDrillDepth = 270;

        public static double DefaultDowelDiameter = 16.0;

        public static bool DefaultSingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public static int DefaultMode = 0;

        public static double DefaultCutterSize = 300;

        public double PlateDepth = 50.0;
        public double PlateSlotDepth = 50.0;
        public double PlateWidth = 80.0;
        public double PlateThickness = 20.0;
        public double PlateOffset = 0.0;
        public double PlateEndOffset = 0;

        public double ToolDiameter = 16.0;

        public double DowelPosition = 20;
        public double DowelLengthExtra { get; set; }
        public double DowelLength { get; set; }
        public double DowelDrillDepth { get; set; }
        public double DowelDiameter { get; set; }
        public List<Dowel> Dowels { get; set; }



        public double Added = 5.0;
        public double AddedSlot = 100;

        public double ToleranceTenonSide = 0.5;
        public double ToleranceTenonEnd = 1.5;
        public double ToleranceSlotEnd = 1.5;
        public double TolerancePlateDowels = 1.5;

        public bool SingleInsertionDirection = true;

        /// <summary>
        /// Joint mode for arm beams.
        ///0 = beams are split down the seam
        ///-1 = beam0 goes into beam1
        ///1 = beam1 goes into beam0
        /// </summary>
        public int Mode = 0;

        public double CutterSize = 300;

        // *******************
        // Protected variables
        // *******************

        /// <summary>
        /// Planes on the inside of the arms, in the seam.
        /// </summary>
        protected Plane[] SeamPlanes;
        protected Plane[] SeamOffsetPlanes;

        /// <summary>
        /// Planes on the ouside of the arms, away from the seam.
        /// </summary>
        protected Plane[] OutsidePlanes;
        protected Plane[] OutsideOffsetPlanes;

        /// <summary>
        /// Planes at the end of the arm slots
        /// </summary>
        protected Plane[] EndPlanes;

        /// <summary>
        /// Plane on the beam side.
        /// </summary>
        protected Plane SillPlane;
        protected Plane SillOffsetPlane;
        /// <summary>
        /// Planes for tenon sides.
        /// </summary>
        protected Plane[] TenonSidePlanes;

        protected ConnectorPlate Plate;

        /// <summary>
        /// Plane that the connector plate lies on
        /// </summary>
        protected Plane PlatePlane;
        /// <summary>
        /// Planes for top and bottom of plate.
        /// </summary>
        protected Plane[] PlateFacePlanes;

        protected Plane SillPlatePlane;

        /// <summary>
        /// Plane that is perpendicular to the Beam element, at the
        /// point of intersection.
        /// </summary>
        protected Plane KPlane;

        /// <summary>
        /// Average vector of V0 and V1 elements.
        /// </summary>
        protected Vector3d VSum;

        /// <summary>
        /// Vector for inserting the plate.
        /// </summary>
        protected Vector3d InsertionVector;

        /// <summary>
        /// Array of all three beams, just for brevity.
        /// </summary>
        protected Beam[] Beams;

        protected Vector3d[] BeamDirections;
        protected Plane[] BeamPlanes;


        /// <summary>
        /// Plane that all dowel holes lie on, at least their origins
        /// </summary>
        protected Plane[] DowelOffsetPlanes;

        public double MaxFilletRadius { get; set; }

        public KJoint_Plate6(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            PlateDepth = DefaultPlateDepth;
            PlateThickness = DefaultPlateThickness;
            PlateOffset = DefaultPlateOffset;
            PlateEndOffset = DefaultPlateEndOffset;
            PlateSlotDepth = DefaultPlateSlotDepth;
            MaxFilletRadius = ToolDiameter;

            DowelPosition = DefaultDowelPosition;
            DowelLength = DefaultDowelLength;
            DowelDrillDepth = DefaultDowelDrillDepth;
            DowelDiameter = DefaultDowelDiameter;

            SingleInsertionDirection = DefaultSingleInsertionDirection;
            Mode = DefaultMode;

            CutterSize = DefaultCutterSize;

            Dowels = new List<Dowel>();
        }

        public override string ToString()
        {
            return "KJoint_Plate6";
        }

        public void CheckSides()
        {
            // Cases to check the maximum distance for:
            // - top of plate, left
            // - top of plate, right
            // - bottom of plate, left
            // - bottom of plate, right
            // Find min and max of intersections on SillPlatePlane
            double min = double.MaxValue, max = double.MinValue;

            Vector3d minNormal = Vector3d.Unset, maxNormal = Vector3d.Unset;
            Vector3d sillNormal = PlatePlane.Project(SillPlane.ZAxis); sillNormal.Unitize();

            Plane[] xPlanes;
            double ToolRadius = ToolDiameter * 0.5;

            switch (Mode)
            {
                case (1):
                    xPlanes = new Plane[] { OutsidePlanes[0], SeamPlanes[0], OutsidePlanes[1] };
                    break;
                case (-1):
                    xPlanes = new Plane[] { OutsidePlanes[1], SeamPlanes[1], OutsidePlanes[0] };
                    break;
                default:
                    xPlanes = new Plane[] { OutsidePlanes[0], OutsidePlanes[1] };
                    break;
            }

            Point3d pt, local_pt;
            for (int i = 0; i < 2; ++i)
            {
                //for (int j = 0; j < xPlanes.Length; ++j)
                for (int j = 0; j < 2; ++j)
                {
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], SillPlane, xPlanes[j], out pt);

                    debug.Add(pt);
                    SillPlatePlane.RemapToPlaneSpace(pt, out local_pt);

                    if (local_pt.Y < min)
                    {
                        min = local_pt.Y;
                        minNormal = xPlanes[j].ZAxis;
                    }
                    if (local_pt.Y > max)
                    {
                        max = local_pt.Y;
                        maxNormal = xPlanes[j].ZAxis;
                    }
                }
            }

            minNormal = PlatePlane.Project(minNormal); minNormal.Unitize();
            maxNormal = PlatePlane.Project(maxNormal); maxNormal.Unitize();

            if (minNormal * sillNormal < 0) minNormal.Reverse();
            if (maxNormal * sillNormal < 0) maxNormal.Reverse();

            double minAngle = Math.Acos(sillNormal * minNormal);
            double maxAngle = Math.Acos(sillNormal * maxNormal);

            //debug.Add(new Line(SillPlatePlane.PointAt(0, max), sillNormal * 50));
            //debug.Add(new Line(SillPlatePlane.PointAt(0, max), maxNormal * 50));

            min -= ToolRadius / Math.Tan(minAngle * 0.5) + ToolRadius;
            max += ToolRadius / Math.Tan(maxAngle * 0.5) + ToolRadius;

            //debug.Add(SillPlatePlane.PointAt(0, min));
            //debug.Add(SillPlatePlane.PointAt(0, max));

            TenonSidePlanes = new Plane[2];

            //TenonSidePlanes[0] = new Plane(SillPlatePlane.PointAt(0, min), InsertionVector,
            //  -SillPlatePlane.YAxis);
            //TenonSidePlanes[1] = new Plane(SillPlatePlane.PointAt(0, max), InsertionVector,
            //  SillPlatePlane.YAxis);

            TenonSidePlanes[0] = new Plane(SillPlatePlane.PointAt(0, min), InsertionVector,
              -PlatePlane.ZAxis);
            TenonSidePlanes[1] = new Plane(SillPlatePlane.PointAt(0, max), InsertionVector,
              PlatePlane.ZAxis);
        }

        public Brep CreatePlate()
        {
            //var insertionVector = PlatePlane.Project(-SillPlane.ZAxis);
            //insertionVector.Unitize();

            var TenonEndPlane = new Plane(SillPlatePlane.Origin + InsertionVector * (PlateDepth - ToleranceTenonEnd), InsertionVector);

            debug.Add(TenonEndPlane);
            debug.Add(SillPlatePlane);

            Plate = new ConnectorPlate();
            Plate.Plane = PlatePlane;
            Plate.Thickness = PlateThickness;

            Plane[] xPlanes;

            switch (Mode)
            {
                case (1):
                    xPlanes = new Plane[] { OutsidePlanes[0], SeamPlanes[0], OutsidePlanes[1] };
                    break;
                case (-1):
                    xPlanes = new Plane[] { OutsidePlanes[1], SeamPlanes[1], OutsidePlanes[0] };
                    break;
                default:
                    xPlanes = new Plane[] { OutsidePlanes[0], OutsidePlanes[1] };
                    break;
            }

            var pts = new Point3d[11];

            Curve[] FaceLoops = new Curve[2];

            Curve[,] Segments = new Curve[2, 6];
            double[,] FilletRadii = new double[2, 6];
            double radius = ToolDiameter * 0.5;
            double maxRadius = radius;

            bool corner2 = false; // Flag if the innner corner is chamfered (Mode 1 or -1)

            for (int i = 0; i < 2; ++i)
            {
                // Determine if we have a chamfered corner or a sharp one
                Point3d pp, ps; // PlanePlane, PlaneSill
                if (Mode != 0)
                {
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], SillPlane, xPlanes[2], out ps);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], xPlanes[1], xPlanes[2], out pp);

                    if ((pp - SillPlane.Origin) * SillPlane.ZAxis > 0)
                        corner2 = true;
                }

                Plane[] xxPlanes = new Plane[0];

                if (corner2)
                {
                    switch (Mode)
                    {
                        case (1):
                            xxPlanes = new Plane[]{SillPlane, OutsidePlanes[0],
                                EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                                OutsidePlanes[1], SeamPlanes[0], SillPlane, TenonSidePlanes[1], TenonEndPlane,
                                TenonSidePlanes[0]};
                            break;
                        case (-1):
                            xxPlanes = new Plane[]{SillPlane, SeamPlanes[1], OutsidePlanes[0],
                                EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                                OutsidePlanes[1], SillPlane, TenonSidePlanes[1], TenonEndPlane,
                                TenonSidePlanes[0]};
                            break;
                    }
                }
                else
                {
                    xxPlanes = new Plane[]{SillPlane, OutsidePlanes[0],
                        EndPlanes[0], SeamPlanes[0], SeamPlanes[1], EndPlanes[1],
                        OutsidePlanes[1], SillPlane, TenonSidePlanes[1], TenonEndPlane, TenonSidePlanes[0]};
                }

                pts = new Point3d[xxPlanes.Length];

                for (int j = 0; j < pts.Length; ++j)
                {
                    int jj = (j - 1).Modulus(pts.Length);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], xxPlanes[j], xxPlanes[jj], out pts[j]);
                }

                Plate.Outlines[i] = new Polyline(pts);
                Plate.Outlines[i].Add(pts[0]);

                if (corner2)
                {
                    double angle = Vector3d.VectorAngle(pts[0] - pts[1], pts[3] - pts[2]);
                    var length = radius / Math.Tan(angle / 2);

                    if (pts[1].DistanceTo(pts[2]) < radius)
                    {
                        corner2 = false;
                        var ptsList = new List<Point3d>(pts);
                        ptsList.RemoveAt(1);
                        pts = ptsList.ToArray();

                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(PlateFacePlanes[i], xxPlanes[0], xxPlanes[2], out pts[1]);

                    }
                }

                int[] segIndices = new int[] { 3, 2, 3, 2, 3, 4 };
                if (corner2)
                    segIndices[0] = 4;

                int counter = 0;
                for (int j = 0; j < 6; ++j)
                {
                    var segPoly = new Polyline();
                    for (int k = 0; k < segIndices[j]; ++k)
                    {
                        counter = counter.Modulus(pts.Length);
                        segPoly.Add(pts[counter]);
                        counter++;
                    }
                    counter--;

                    Segments[i, j] = segPoly.ToNurbsCurve();
                }

                //GluLamb.Utility.MaxFilletRadius(pts[0], pts[1], pts[2], out FilletRadii[i, 0], 0);
                //GluLamb.Utility.MaxFilletRadius(pts[3], pts[4], pts[5], out FilletRadii[i, 2], 0);
                //GluLamb.Utility.MaxFilletRadius(pts[6], pts[7], pts[8], out FilletRadii[i, 4], 0);

                //FilletRadii[i, 0] = Math.Min(FilletRadii[i, 0], maxRadius);
                //FilletRadii[i, 2] = Math.Min(FilletRadii[i, 2], maxRadius);
                //FilletRadii[i, 4] = Math.Min(FilletRadii[i, 4], maxRadius);

                FilletRadii[i, 0] = radius;
                FilletRadii[i, 2] = radius;
                FilletRadii[i, 4] = radius;
            }

            var faceSegs = new List<Curve>[2];

            for (int i = 0; i < 2; ++i)
            {
                faceSegs[i] = new List<Curve>();

                var fillets = Curve.CreateFilletCornersCurve(Segments[i, 0], FilletRadii[i, 0], 0.01, 0.01);
                if (fillets != null)
                    Segments[i, 0] = fillets;

                fillets = Curve.CreateFilletCornersCurve(Segments[i, 2], FilletRadii[i, 2], 0.01, 0.01);
                if (fillets != null)
                    Segments[i, 2] = fillets;

                fillets = Curve.CreateFilletCornersCurve(Segments[i, 4], FilletRadii[i, 4], 0.01, 0.01);
                if (fillets != null)
                    Segments[i, 4] = fillets;

                for (int j = 0; j < 6; ++j)
                    faceSegs[i].Add(Segments[i, j]);
            }

            FaceLoops[0] = Curve.JoinCurves(faceSegs[0], 0.01)[0];
            FaceLoops[1] = Curve.JoinCurves(faceSegs[1], 0.01)[0];

            Brep[] Fragments = new Brep[6];
            for (int i = 0; i < 6; ++i)
            {
                var res = Brep.CreateFromLoft(new Curve[] { Segments[0, i], Segments[1, i] }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (res != null && res.Length > 0)
                    Fragments[i] = res[0];
            }

            var breps = new List<Brep>();
            var topFace = Brep.CreatePlanarBreps(FaceLoops[0], 0.1);
            var btmFace = Brep.CreatePlanarBreps(FaceLoops[1], 0.1);

            breps.AddRange(topFace);
            breps.AddRange(btmFace);
            breps.AddRange(Fragments);

            var joined = Brep.JoinBreps(breps, 0.01);
            if (joined == null || joined.Length < 1)
                return null;

            joined[0].Faces.SplitKinkyFaces(0.1);
            Plate.Geometry = joined[0];

            return Plate.Geometry;
        }

        protected Brep CreateSillCutter()
        {
            //var xaxis = PlatePlane.Project(SillPlane.XAxis);
            //xaxis = InsertionVector;
            //xaxis = SillPlatePlane.YAxis;
            var xaxis = Vector3d.CrossProduct(PlatePlane.ZAxis, InsertionVector);
            debug.Add(SillPlane);
            
            Point3d origin;
            var TenonEndPlane = new Plane(SillPlatePlane.Origin + InsertionVector * PlateSlotDepth, InsertionVector);

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SillPlane, new Plane(SillPlane.Origin, SillPlane.ZAxis, SillPlane.YAxis), PlatePlane, out origin);

            var plane = new Plane(origin, xaxis, PlatePlane.ZAxis);
            debug.Add(plane);
            plane.Origin = plane.Origin - InsertionVector * Added;

            var pts = new Point3d[4];

            var tsp0 = TenonSidePlanes[0];
            var tsp1 = TenonSidePlanes[1];
            //double ToleranceTenonSide = 0.5;

            var tsp = new Plane[] { TenonSidePlanes[0], TenonSidePlanes[1] };

            int sign = tsp[0].ZAxis * (tsp[1].Origin - tsp[0].Origin) > 0 ? 1 : -1;
            tsp[0].Origin = tsp[0].Origin - tsp[0].ZAxis * ToleranceTenonSide * sign;
            tsp[1].Origin = tsp[1].Origin - tsp[1].ZAxis * ToleranceTenonSide * sign;

            if (sign < 0)
                tsp = new Plane[] { tsp[1], tsp[0] };

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[0], tsp[1], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[1], tsp[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[1], tsp[0], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, PlateFacePlanes[0], tsp[0], out pts[3]);

            var ad = new ArchivableDictionary();
            ad.Set("TenonSide0", tsp[0]);
            ad.Set("TenonSide1", tsp[1]);
            ad.Set("PlateFace0", PlateFacePlanes[0]);
            ad.Set("PlateFace1", PlateFacePlanes[1]);
            ad.Set("EndPlane", TenonEndPlane);
            ad.Set("SlotPlane", plane);
            ad.Set("Depth", PlateSlotDepth);
            ad.Set("PlateThickness", PlateThickness);

            Beam.Element.UserDictionary.Set(String.Format("TenonSlot_{0}_{1}",V0.Element.Name, V1.Element.Name), ad);

            var poly = new Polyline(pts);
            poly.Add(poly[0]);

            if (Vector3d.CrossProduct(pts[1] - pts[0], pts[2] - pts[1]) * InsertionVector < 0)
                poly.Reverse();

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile
                                                                                                               //Extrusion extrusion = Extrusion.CreateExtrusion(profile, SillPlane.ZAxis * -PlateDepth);
            var extrusion = Extrusion.Create(profile, PlateSlotDepth + Added, true);
            return extrusion.ToBrep(true);
        }

        protected Brep CreatePlateSlot(int index)
        {
            //double ToleranceSlotEnd = 1.5;
            var endPlane = EndPlanes[index];
            endPlane.Origin = endPlane.Origin - endPlane.ZAxis * ToleranceSlotEnd;
            var sidePlane = SeamPlanes[index];
            var outsidePlane = OutsidePlanes[index];

            var sillPlane = SillPlane;
            sillPlane.Origin = sillPlane.Origin - sillPlane.ZAxis * Added;

            int sign = sidePlane.ZAxis * (sidePlane.Origin - outsidePlane.Origin) > 0 ? 1 : -1;

            sidePlane.Origin = sidePlane.Origin + sidePlane.ZAxis * Added * sign;
            outsidePlane.Origin = outsidePlane.Origin - outsidePlane.ZAxis * Added * sign;

            // *************************
            // Plane intersection method
            // *************************

            var pts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, sidePlane, PlateFacePlanes[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, sidePlane, PlateFacePlanes[0], out pts[3]);

            var topLoop = new Polyline(pts);
            topLoop.Add(topLoop[0]);

            var topFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endPlane, outsidePlane, PlateFacePlanes[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sillPlane, outsidePlane, PlateFacePlanes[0], out pts[3]);

            var btmLoop = new Polyline(pts);
            btmLoop.Add(btmLoop[0]);

            var ad = new ArchivableDictionary();
            ad.Set("SidePlane", sidePlane);
            ad.Set("PlatePlane", PlatePlane);
            ad.Set("EndPlane", endPlane);
            ad.Set("OutsidePlane", outsidePlane);
            ad.Set("PlateThickness", PlateThickness);
            ad.Set("TopLoop", topLoop.ToNurbsCurve());
            ad.Set("BottomLoop", btmLoop.ToNurbsCurve());
            Parts[index].Element.UserDictionary.Set(String.Format("PlateSlot_{0}", Guid.NewGuid().ToString().Substring(0, 8)), ad);

            var btmFace = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            var sideFaces = Brep.CreateFromLoft(new Curve[] { topLoop.ToNurbsCurve(), btmLoop.ToNurbsCurve() },
              Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            var breps = new List<Brep>();
            breps.Add(topFace);
            breps.Add(btmFace);
            breps.AddRange(sideFaces);

            var joined = Brep.JoinBreps(breps, 0.01)[0];
            joined.Faces.SplitKinkyFaces(0.1);

            //return joined;

            double r = 8;
            var filleted = Brep.CreateFilletEdges(joined, new int[] { 8, 9 }, new double[] { r, r }, new double[] { r, r },
              BlendType.Fillet, RailType.RollingBall, 0.01);
            if (filleted.Length > 0)
                return filleted[0];
            return joined;
        }

        protected void CreatePlatePlanes(Point3d origin, Vector3d xaxis, Vector3d yaxis)
        {
            // Find PlatePlane and figure out geometry
            PlatePlane = new Plane(origin, xaxis, yaxis);
            PlatePlane.Origin = PlatePlane.Origin + PlatePlane.ZAxis * PlateOffset;

            PlateFacePlanes = new Plane[2];
            PlateFacePlanes[0] = new Plane(
              PlatePlane.Origin + PlatePlane.ZAxis * PlateThickness * 0.5,
              PlatePlane.XAxis,
              PlatePlane.YAxis);

            PlateFacePlanes[1] = new Plane(
              PlatePlane.Origin - PlatePlane.ZAxis * PlateThickness * 0.5,
              PlatePlane.XAxis,
              PlatePlane.YAxis);
        }

        /// <summary>
        /// Check the order of the V-beams and flip if necessary.
        /// This is important for getting correct Seam and Outside planes, among other things.
        /// </summary>
        /// <returns></returns>
        protected void OrderVBeams()
        {
            var xside = VSum * KPlane.XAxis < 0 ? -1 : 1;

            if (((BeamDirections[0] * KPlane.ZAxis) < (BeamDirections[1] * KPlane.ZAxis) && xside > 0) ||
              ((BeamDirections[0] * KPlane.ZAxis) > (BeamDirections[1] * KPlane.ZAxis) && xside < 0))
            {
                var dirTemp = BeamDirections[0];
                BeamDirections[0] = BeamDirections[1];
                BeamDirections[1] = dirTemp;

                var planeTemp = BeamPlanes[0];
                BeamPlanes[0] = BeamPlanes[1];
                BeamPlanes[1] = planeTemp;

                var beamTemp = Beams[0];
                Beams[0] = Beams[1];
                Beams[1] = beamTemp;

                var partTemp = Parts[0];
                Parts[0] = Parts[1];
                Parts[1] = partTemp;
            }
        }

        public override bool Construct(bool append = false)
        {
            debug = new List<object>();
            var cutterInterval = new Interval(-CutterSize, CutterSize);

            // ************************************************
            // Initialize basic variables and set some initial
            // base directions.
            // ************************************************

            Beams = Parts.Select(x => (x.Element as BeamElement).Beam).ToArray();
            KPlane = Beams[2].GetPlane(Parts[2].Parameter);
            BeamDirections = new Vector3d[2];
            BeamPlanes = new Plane[2];

            for (int i = 0; i < 2; ++i)
            {
                var d0 = Beams[i].Centreline.PointAt(Beams[i].Centreline.Domain.Mid) - KPlane.Origin;
                BeamPlanes[i] = Beams[i].GetPlane(Parts[i].Parameter);
                int signX = 1, signY = 1, signZ = 1;

                if (BeamPlanes[i].ZAxis * d0 < 0) signZ = -1;
                if (BeamPlanes[i].YAxis * KPlane.YAxis < 0) signY = -1;

                BeamPlanes[i] = new Plane(BeamPlanes[i].Origin, BeamPlanes[i].XAxis * signX * signZ, BeamPlanes[i].YAxis * signY);
                BeamDirections[i] = BeamPlanes[i].ZAxis;
            }

            // **********************************************
            // Finde VSum and InsertionVector
            // **********************************************

            VSum = BeamDirections[0] + BeamDirections[1];
            VSum.Unitize();

            InsertionVector = -KPlane.Project(VSum);
            InsertionVector.Unitize();



            // *******************************************************
            // Check the order of the V-beams and flip if necessary.
            // This is important for getting correct Seam and Outside planes, among other things.
            // *******************************************************

            OrderVBeams();

            // *************************
            // Find correct plane axis
            // *************************


            Vector3d xaxis;
            double dotx = KPlane.XAxis * VSum;
            double doty = KPlane.YAxis * VSum;
            double width;

            if (Math.Abs(dotx) > Math.Abs(doty))
            {
                width = Beams[2].Width;
                if (dotx < 0)
                    xaxis = -KPlane.XAxis;
                else
                    xaxis = KPlane.XAxis;
            }
            else
            {
                width = Beams[2].Height;
                if (doty < 0)
                    xaxis = -KPlane.YAxis;
                else
                    xaxis = KPlane.YAxis;
            }

            this.Plane = new Plane(KPlane.Origin, xaxis, KPlane.ZAxis);


            // ***************
            // SillPlane
            // ***************

            // SillPlane is the plane on top of the sill beam, where the two V-Beams meet
            SillPlane = new Plane(KPlane.Origin + xaxis * width * 0.5, KPlane.ZAxis, KPlane.YAxis);
            if (SillPlane.ZAxis * VSum < 0)
                SillPlane = new Plane(SillPlane.Origin, -SillPlane.XAxis, SillPlane.YAxis);

            // ***************
            // PlatePlanes
            // ***************

            CreatePlatePlanes(KPlane.Origin, InsertionVector, BeamDirections[0]);

            // ************************************
            // SillOffsetPlane and SillPlatePlane
            // ************************************
            // SillOffsetPlane is the safety plane for making cutters with a slight overlap
            var SillOffsetPlane = new Plane(SillPlane.Origin - SillPlane.ZAxis * 3.0, SillPlane.XAxis, SillPlane.YAxis);

            // SillPlatePlane is the plane projected onto SillPlane that is aligned with the PlatePlane normal vector
            SillPlatePlane = new Plane(SillPlane.Origin, SillPlane.Project(PlatePlane.ZAxis), SillPlane.XAxis);
            if (SillPlatePlane.ZAxis * VSum < 0)
                SillPlatePlane = new Plane(SillPlatePlane.Origin, -SillPlatePlane.XAxis, SillPlatePlane.YAxis);

            Point3d SillPlateOrigin;
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(SillPlane, PlatePlane, KPlane, out SillPlateOrigin);
            SillPlatePlane.Origin = SillPlateOrigin;

            //debug.Add(this.Plane);

            // ***************
            // DowelOffsetPlanes
            // ***************

            DowelOffsetPlanes = new Plane[2];

            double Ratio = 0.0;
            var Ratios = new double[2] { Ratio, Ratio };
            var DowelPlaneOffsets = new double[2];

            switch (Mode)
            {
                case (-1):
                    Ratios = new double[2] { 1.0, 0.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition + DowelDiameter * 1.5, DowelPosition };
                    break;
                case (1):
                    Ratios = new double[2] { 0.0, 1.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition, DowelPosition + DowelDiameter * 1.5 };
                    break;
                default:
                    Ratios = new double[2] { 0.0, 0.0 };
                    DowelPlaneOffsets = new double[2] { DowelPosition, DowelPosition };
                    break;
            }

            // DowelOffsetPlanes are the planes on which all the dowel points lie
            for (int i = 0; i < 2; ++i)
                DowelOffsetPlanes[i] = new Plane(SillPlane.Origin + xaxis * DowelPlaneOffsets[i], SillPlane.XAxis, SillPlane.YAxis);
            var dowelPlane = new Plane(SillPlane.Origin + xaxis * DowelPosition, SillPlane.XAxis, SillPlane.YAxis);


            // **********************
            // Sill cutter (end cuts)
            // **********************
            for (int i = 0; i < 2; ++i)
            {
                Point3d xpt;
                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(Beams[i].Centreline, SillPlane, 0.01);
                if (res != null && res.Count > 0)
                    xpt = res[0].PointA;
                else
                    xpt = SillPlane.ClosestPoint(BeamPlanes[i].Origin);

                var cutterPlane = new Plane(xpt, SillPlane.XAxis, SillPlane.YAxis);
                var cutterRec = new Rectangle3d(cutterPlane, cutterInterval, cutterInterval);
                var cutterBrep = Brep.CreatePlanarBreps(new Curve[] { cutterRec.ToNurbsCurve() }, 0.01)[0];

                Parts[i].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[2].Element.Name), cutterPlane);
                Parts[i].Geometry.Add(cutterBrep);
            }


            // *****************************************************
            // Find all seam and outside planes, including offsets
            // *****************************************************

            double PlaneOffset = 5.0;
            SeamPlanes = new Plane[2];
            SeamOffsetPlanes = new Plane[2];

            SeamPlanes[0] = new Plane(BeamPlanes[0].Origin + BeamPlanes[0].XAxis * Beams[0].Width * 0.5, BeamPlanes[0].ZAxis, BeamPlanes[0].YAxis);
            SeamPlanes[1] = new Plane(BeamPlanes[1].Origin - BeamPlanes[1].XAxis * Beams[1].Width * 0.5, BeamPlanes[1].ZAxis, BeamPlanes[1].YAxis);

            OutsidePlanes = new Plane[2];
            OutsideOffsetPlanes = new Plane[2];
            OutsidePlanes[0] = new Plane(BeamPlanes[0].Origin - BeamPlanes[0].XAxis * Beams[0].Width * 0.5, BeamPlanes[0].ZAxis, BeamPlanes[0].YAxis);
            OutsidePlanes[1] = new Plane(BeamPlanes[1].Origin + BeamPlanes[1].XAxis * Beams[1].Width * 0.5, BeamPlanes[1].ZAxis, BeamPlanes[1].YAxis);

            CheckSides();

            // **********
            // EndPlanes
            // **********

            EndPlanes = new Plane[2];
            var endPts = new Point3d[2];
            int sign = -1;
            double[] endOffset;
            switch (Mode)
            {
                case (-1):
                    endOffset = SingleInsertionDirection ? new double[] { DowelDiameter * 1, DowelDiameter * 1 } : new double[] { DowelDiameter * 2, DowelDiameter * 3 };
                    break;
                case (1):
                    endOffset = SingleInsertionDirection ? new double[] { DowelDiameter * 2, DowelDiameter * 1 } : new double[] { DowelDiameter * 3, DowelDiameter * 2 };
                    break;
                default:
                    endOffset = SingleInsertionDirection ? new double[] { DowelDiameter * 2, DowelDiameter * 2 } : new double[] { DowelDiameter * 2, DowelDiameter * 2 };
                    break;
            }

            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;

                endPts[i] = BeamPlanes[i].Origin;
                endPts[i].Transform(DowelOffsetPlanes[i].ProjectAlongVector(BeamPlanes[i].ZAxis));  //debug.Add(endPts[i]);
                endPts[i] = endPts[i] + BeamPlanes[i].ZAxis * (endOffset[i] + PlateEndOffset);        //debug.Add(endPts[i]);
                endPts[i].Transform(OutsidePlanes[i].ProjectAlongVector(OutsidePlanes[i].ZAxis));

                if (SingleInsertionDirection)
                    EndPlanes[i] = new Plane(endPts[i], PlatePlane.XAxis * -sign, PlatePlane.ZAxis);
                else
                {
                    EndPlanes[i] = new Plane(endPts[i], PlatePlane.Project(-BeamPlanes[i].XAxis), PlatePlane.ZAxis);
                }
            }

            sign = -1;
            for (int i = 0; i < 2; ++i)
            {
                sign += i * 2;
                SeamOffsetPlanes[i] = new Plane(SeamPlanes[i].Origin + SeamPlanes[i].ZAxis * sign * PlaneOffset, SeamPlanes[i].XAxis, SeamPlanes[i].YAxis);
                OutsideOffsetPlanes[i] = new Plane(OutsidePlanes[i].Origin - OutsidePlanes[i].ZAxis * sign * PlaneOffset, OutsidePlanes[i].XAxis, OutsidePlanes[i].YAxis);
            }

            Plane seamPlane;
            Rectangle3d seamRec;
            Brep seamBrep;

            // ************************************************
            // Pick method of intersecting the 2 arms beams
            // ************************************************

            switch (Mode)
            {
                case (-1): // beam0 into the side of beam1
                    seamRec = new Rectangle3d(SeamPlanes[1], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[0].Geometry.Add(seamBrep);
                    Parts[0].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[1].Element.Name), SeamPlanes[1]);

                    break;
                case (1): // beam1 into the side of beam0
                    seamRec = new Rectangle3d(SeamPlanes[0], cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];
                    Parts[1].Geometry.Add(seamBrep);
                    Parts[1].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[0].Element.Name), SeamPlanes[0]);

                    break;
                default: // centre split
                    Line seam;
                    Rhino.Geometry.Intersect.Intersection.PlanePlane(SeamPlanes[0], SeamPlanes[1], out seam);

                    seamPlane = new Plane(seam.From, seam.Direction, VSum);

                    seamRec = new Rectangle3d(seamPlane, cutterInterval, cutterInterval);
                    seamBrep = Brep.CreatePlanarBreps(new Curve[] { seamRec.ToNurbsCurve() }, 0.01)[0];

                    Parts[0].Geometry.Add(seamBrep);
                    Parts[1].Geometry.Add(seamBrep);
                    Parts[0].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[1].Element.Name), seamPlane);
                    Parts[1].Element.UserDictionary.Set(string.Format("EndCut_{0}", Parts[0].Element.Name), seamPlane);

                    break;
            }

            // *****************
            // END seam
            // *****************


            for (int i = 0; i < 2; ++i)
            {
                var slot = CreatePlateSlot(i);
                if (slot != null)
                    Parts[i].Geometry.Add(slot);
            }

            var tenon = CreateSillCutter();
            if (tenon != null)
                this.Beam.Geometry.Add(tenon);


            // ***********************
            // Create dowels
            // ***********************
            var dowelPlanes = new Plane[3];

            // 2022-03-15 align dowel vectors parallel to dowel plane

            var dowelVectors = new Vector3d[2];

            for (int i = 0; i < 2; ++i)
            {
                //var dproj = dowelPlane.ProjectAlongVector(BeamPlanes[i].ZAxis);
                var dx = Rhino.Geometry.Intersect.Intersection.CurvePlane(Beams[i].Centreline, DowelOffsetPlanes[i], 0.01);

                dowelPlanes[i] = Beams[i].GetPlane(dx[0].PointA);
                //debug.Add(dowelPlanes[i]);
                //dowelPoints[i] = dowelPlanes[i].Origin;

                var dRotPlane = new Plane(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis, dowelPlanes[i].YAxis);

                var dot = Math.Abs(dowelPlanes[i].YAxis * KPlane.YAxis);
                //Ratios[i] = 1 - dot;

                Line xPP;
                //Rhino.Geometry.Intersect.Intersection.PlanePlane(dRotPlane, dowelPlane, out xPP);
                Rhino.Geometry.Intersect.Intersection.PlanePlane(dRotPlane, DowelOffsetPlanes[i], out xPP);

                dowelVectors[i] = xPP.Direction;
                dowelVectors[i].Unitize();

                if (dowelVectors[i] * dowelPlanes[i].YAxis < 0) dowelVectors[i].Reverse();

                // Simple lerp between dowel vector and plane Y-axis
                dowelVectors[i] = dowelVectors[i] + Ratios[i] * (dowelPlanes[i].YAxis - dowelVectors[i]);

                dowelPlanes[i] = new Plane(dowelPlanes[i].Origin - dowelVectors[i] * DowelLength * 0.5, dowelVectors[i]);
                //dowelPlanes[i] = new Plane(dowelPlanes[i].Origin - BeamPlanes[i].YAxis * DowelLength * 0.5, BeamPlanes[i].YAxis);

                //debug.Add(dowelPlanes[i]);

                //var dowelPlane01 = new Plane(dowelPoints[i] - BeamPlanes[i].YAxis * DowelLength * 0.5, BeamPlanes[i].YAxis);
                var dowelCyl = new Cylinder(
                  new Circle(dowelPlanes[i], DowelDiameter * 0.5), DowelDrillDepth).ToBrep(true, true);

                Dowels.Add(new Dowel(new Line(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis * DowelLength), DowelDiameter, DowelDrillDepth));

                Parts[i].Geometry.Add(dowelCyl);
                Parts[i].Element.UserDictionary.Set(String.Format("PlateDowel_{0}", Guid.NewGuid().ToString().Substring(0, 8)), new Line(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis * DowelLength));

            }

            var portalDowelPoint = (TenonSidePlanes[0].Origin + TenonSidePlanes[1].Origin) * 0.5;
            portalDowelPoint = Beams[2].GetPlane(portalDowelPoint).Origin;

            var portalDowelPlane = new Plane(portalDowelPoint - KPlane.YAxis * DowelLength * 0.5, KPlane.YAxis);
            var portalDowelCyl = new Cylinder(
              new Circle(portalDowelPlane, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            Dowels.Add(new Dowel(new Line(portalDowelPlane.Origin, portalDowelPlane.ZAxis * DowelLength), DowelDiameter));

            this.Beam.Geometry.Add(portalDowelCyl);
            this.Beam.Element.UserDictionary.Set(String.Format("PlateDowel_{0}", Guid.NewGuid().ToString().Substring(0, 8)), new Line(portalDowelPlane.Origin, portalDowelPlane.ZAxis * DowelLength));

            return true;
        }

        public ConnectorPlate GetConnectorPlate()
        {
            return Plate;
        }
    }

}
