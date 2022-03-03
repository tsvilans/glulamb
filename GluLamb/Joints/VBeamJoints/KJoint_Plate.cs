using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class KJoint_Plate : VBeamJoint
    {
        public List<object> debug;

        public static double DefaultPlateDepth = 60.0;
        public static double DefaultPlateThickness = 20.0;

        public static double DefaultDowelPosition = 60;
        public static double DefaultDowelLength = 220.0;
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
        public List<object> debug;

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

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile
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

            if (tilt < 1)
            {
                double tiltedDepth = depth / tilt;
                double offsetSqrt = Math.Pow(depth, 2) - Math.Pow(tiltedDepth, 2);
                offset = double.IsNaN(offsetSqrt) ? 0 : Math.Sqrt(offsetSqrt);
                depth = Math.Min(MaxPlateDepth, tiltedDepth);
            }

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

            var profile = Curve.CreateFilletCornersCurve(poly.ToNurbsCurve(), ToolDiameter * 0.5, 0.01, 0.01); // Pocket profile

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
                Parts[i].Geometry.Add(CreatePlateSlot(i, SlotDirections[i]));

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

}
