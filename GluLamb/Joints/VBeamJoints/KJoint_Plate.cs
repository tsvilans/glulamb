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
            //plateOutline0.
            debug.Add(plateOutline0);

            var plateSrf0 = Brep.CreateTrimmedPlane(platePlane, plateOutline0.ToNurbsCurve());

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
}
