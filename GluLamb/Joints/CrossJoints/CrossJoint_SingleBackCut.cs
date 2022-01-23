using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GluLamb.Factory;
using Rhino.Geometry;
using Rhino;

namespace GluLamb.Joints
{
    public class CrossJoint_SingleBackcut : CrossJoint
    {
        public Plane UnifyPlanes(Plane p0, Plane p1)
        {
            int x = 1, y = 1;
            if (p0.YAxis * p1.YAxis < 0)
                y = -1;
            if (p0.ZAxis * p1.XAxis < 0)
                x = -1;

            return new Plane(p1.Origin, p1.XAxis * x, p1.YAxis * y);
        }
#if DEBUG
        public List<object> debug;
#endif
        public CrossJoint_SingleBackcut(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
#if DEBUG
            debug = new List<object>();
#endif
        }

        public override string ToString()
        {
            return "CrossJoint_SingleBackcut";
        }

        public double TaperAngle = 3.0;
        public double DepthOverride = 0.0;
        public double ExtraLength = 50.0;

        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach (var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }

            double added = ExtraLength;

            var obeam = (Over.Element as BeamElement).Beam;
            var ubeam = (Under.Element as BeamElement).Beam;

            var oPlane = obeam.GetPlane(Over.Parameter);
            var uPlane = ubeam.GetPlane(Under.Parameter);

            // Calculate offset for backcut angle
            double tan = Math.Tan(RhinoMath.ToRadians(Math.Max(1.0, TaperAngle)));
            double addedTan = added * tan;
            if (DepthOverride == 0.0) DepthOverride = obeam.Height;
            double TaperOffset = DepthOverride * 0.5 * tan;

            uPlane = UnifyPlanes(oPlane, uPlane);

            var xaxis = oPlane.ZAxis;
            var yaxis = uPlane.ZAxis;
            var zaxis = Vector3d.CrossProduct(xaxis, yaxis);
            zaxis.Unitize();

            // Create centre plane
            var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, zaxis);
            var planeProj = plane.ProjectAlongVector(zaxis);

            // Create side planes for Over
            var oPlanes = new Plane[2];
            oPlanes[0] = new Plane(oPlane.Origin - oPlane.XAxis * obeam.Width * 0.5,
              oPlane.ZAxis, oPlane.YAxis);
            oPlanes[1] = new Plane(oPlane.Origin + oPlane.XAxis * obeam.Width * 0.5,
              -oPlane.ZAxis, oPlane.YAxis);

            // Create side planes for Under
            var uPlanes = new Plane[2];
            uPlanes[0] = new Plane(uPlane.Origin - uPlane.XAxis * ubeam.Width * 0.5,
              uPlane.ZAxis, uPlane.YAxis);
            uPlanes[1] = new Plane(uPlane.Origin + uPlane.XAxis * ubeam.Width * 0.5,
              -uPlane.ZAxis, uPlane.YAxis);

            var corners = new Point3d[4];

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, oPlanes[0], uPlanes[0], out corners[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, oPlanes[0], uPlanes[1], out corners[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, oPlanes[1], uPlanes[1], out corners[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, oPlanes[1], uPlanes[0], out corners[3]);

            var offsetCorners = new Point3d[3];
            offsetCorners[0] = corners[0] - yaxis * TaperOffset;
            offsetCorners[1] = corners[1] - yaxis * TaperOffset - xaxis * TaperOffset;
            offsetCorners[2] = corners[2] - xaxis * TaperOffset;

            var topCorners = new Point3d[4];
            topCorners[0] = corners[0] - zaxis * (obeam.Height * 0.5 + added) + yaxis * addedTan;
            topCorners[1] = corners[1] - zaxis * (obeam.Height * 0.5 + added) + yaxis * addedTan + xaxis * addedTan;
            topCorners[2] = corners[2] - zaxis * (obeam.Height * 0.5 + added) + xaxis * addedTan;
            topCorners[3] = corners[3] - zaxis * (obeam.Height * 0.5 + added);



            var btmCorners = new Point3d[4];
            btmCorners[0] = corners[0] + zaxis * (obeam.Height * 0.5 + added) + yaxis * addedTan;
            btmCorners[1] = corners[1] + zaxis * (obeam.Height * 0.5 + added) + yaxis * addedTan + xaxis * addedTan;
            btmCorners[2] = corners[2] + zaxis * (obeam.Height * 0.5 + added) + xaxis * addedTan;
            btmCorners[3] = corners[3] + zaxis * (obeam.Height * 0.5 + added);

#if DEBUG
            debug.Add(corners[0]);
            debug.Add(corners[1]);
            debug.Add(corners[2]);
            debug.Add(corners[3]);

            debug.Add(offsetCorners[0]);
            debug.Add(offsetCorners[1]);
            debug.Add(offsetCorners[2]);

            debug.Add(topCorners[0]);
            debug.Add(topCorners[1]);
            debug.Add(topCorners[2]);
            debug.Add(topCorners[3]);

            debug.Add(btmCorners[0]);
            debug.Add(btmCorners[1]);
            debug.Add(btmCorners[2]);
            debug.Add(btmCorners[3]);
#endif

            var overSrf = new Brep[6];
            overSrf[0] = Brep.CreateFromCornerPoints(offsetCorners[0], offsetCorners[1],
              offsetCorners[2] - yaxis * added, corners[3] - yaxis * added,
              0.01);
            overSrf[1] = Brep.CreateFromCornerPoints(offsetCorners[0], btmCorners[0], btmCorners[1], offsetCorners[1],
              0.01);
            overSrf[2] = Brep.CreateFromCornerPoints(offsetCorners[1], topCorners[1],
              topCorners[2] - yaxis * added, offsetCorners[2] - yaxis * added,
              0.01);
            overSrf[3] = Brep.CreateFromCornerPoints(corners[3] - yaxis * added, topCorners[3] - yaxis * added,
              topCorners[0], offsetCorners[0],
              0.01);
            overSrf[4] = Brep.CreateFromCornerPoints(topCorners[0], offsetCorners[0], btmCorners[0],
              0.01);
            overSrf[5] = Brep.CreateFromCornerPoints(topCorners[1], offsetCorners[1], btmCorners[1],
              0.01);

            Over.Geometry.AddRange(Brep.JoinBreps(overSrf, 0.01));
            Brep brepOver = Over.Geometry[0];
            brepOver.MergeCoplanarFaces(0.01);

            /* UNDER */
            var underSrf = new Brep[6];
            underSrf[0] = Brep.CreateFromCornerPoints(offsetCorners[0] - xaxis * added, offsetCorners[1],
              offsetCorners[2], corners[3] - xaxis * added,
              0.01);
            underSrf[1] = Brep.CreateFromCornerPoints(offsetCorners[0] - xaxis * added, btmCorners[0] - xaxis * added,
              btmCorners[1], offsetCorners[1],
              0.01);
            underSrf[2] = Brep.CreateFromCornerPoints(offsetCorners[1], topCorners[1],
              topCorners[2], offsetCorners[2],
              0.01);
            underSrf[3] = Brep.CreateFromCornerPoints(offsetCorners[2], btmCorners[2],
              btmCorners[3] - xaxis * added, corners[3] - xaxis * added,
              0.01);
            underSrf[4] = Brep.CreateFromCornerPoints(topCorners[2], offsetCorners[2], btmCorners[2],
              0.01);
            underSrf[5] = Brep.CreateFromCornerPoints(topCorners[1], offsetCorners[1], btmCorners[1],
              0.01);

            Under.Geometry.AddRange(Brep.JoinBreps(underSrf, 0.01));

            Brep brepUnder = Under.Geometry[0];
            brepUnder.MergeCoplanarFaces(0.01);

            return true;
        }
    }
}
