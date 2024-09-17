using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace GluLamb.Joints
{
    public static class JointUtil
    {
        public static void ClassifyJointPosition(Curve c, double t, out int jointCase, out Vector3d direction, double end_tolerance = 10)
        {
            jointCase = 0; // initialize

            jointCase = t > c.Domain.Mid ? JointPartX.SetAtEnd1(jointCase) : JointPartX.SetAtEnd0(jointCase);

            double length = JointPartX.End1(jointCase) ? c.GetLength(new Interval(t, c.Domain.Max)) : c.GetLength(new Interval(c.Domain.Min, t));

            jointCase = length < end_tolerance ? JointPartX.SetAtEnd(jointCase) : JointPartX.SetAtMiddle(jointCase);

            direction = (JointPartX.End0(jointCase) && JointPartX.IsAtEnd(jointCase)) ? -c.TangentAt(t) : c.TangentAt(t);
        }

        public static JointX Connect(Beam b0, int id0, Beam b1, int id1, int joint_id = -1, double tolerance = 0.1, double overlapTolerance = 50, double endTolerance = 50)
        {
            return Connect(b0.Centreline, id0, b1.Centreline, id1, joint_id, tolerance, overlapTolerance, endTolerance);
        }

        public static JointX Connect(Curve c0, int id0, Curve c1, int id1, int joint_id = -1, double tolerance= 0.1, double overlapTolerance= 50, double endTolerance = 50)
        {
            var intersections = Rhino.Geometry.Intersect.Intersection.CurveCurve(c0, c1, tolerance, overlapTolerance);
            if (intersections == null || intersections.Count < 1) return null;

            var intersection = intersections[0];

            var p0 = intersection.PointA;
            var p1 = intersection.PointB;

            double t0 = intersection.ParameterA;
            double t1 = intersection.ParameterB;

            ClassifyJointPosition(c0, t0, out int s0, out Vector3d v0, endTolerance);
            ClassifyJointPosition(c1, t1, out int s1, out Vector3d v1, endTolerance);

            var jc = new JointX(
                new List<JointPartX>
                {
                    new JointPartX() {Case = s0, ElementIndex = id0, JointIndex = joint_id, Parameter = t0, Direction = v0 },
                        new JointPartX() {Case = s1, ElementIndex = id1, JointIndex = joint_id, Parameter = t1, Direction = v1 },
                },
                (p0 + p1) * 0.5
                );

            return jc;
        }

        public static Vector3d GetEndConnectionVector(Beam beam, Point3d jp)
        {
            var crv = beam.Centreline;
            var mid = crv.PointAt(crv.Domain.Mid);
            double t;
            crv.ClosestPoint(jp, out t);
            var tp = crv.PointAt(t);

            Vector3d vec = crv.TangentAt(t);

            if ((mid - tp) * vec < 0)
                return -vec;
            return vec;
        }

        public static Plane GetDividingPlane(Beam be0, Beam be1, Point3d pt)
        {
            var vv0 = GetEndConnectionVector(be0, pt);
            var vv1 = GetEndConnectionVector(be1, pt);

            var v0plane = be0.GetPlane(pt);
            var v1plane = be1.GetPlane(pt);

            var xaxis0 = v0plane.XAxis;
            var xaxis1 = v1plane.XAxis;

            if (xaxis1 * xaxis0 < 0)
                xaxis1 = -xaxis1;

            var yaxis = Vector3d.CrossProduct(xaxis0, xaxis1);

            return new Plane(pt, (vv0 + vv1) / 2, yaxis);
        }

        public static void GetAlignedPlanes(Beam b0, Beam b1, Point3d xpt, out Plane pl0, out Plane pl1, out double width0, out double height0, out double width1, out double height1)
        {
            var bpl0 = b0.GetPlane(xpt);
            var bpl1 = b1.GetPlane(xpt);

            var cross = Vector3d.CrossProduct(bpl0.ZAxis, bpl1.ZAxis);

            if (Math.Abs(cross * bpl0.XAxis) > Math.Abs(cross * bpl0.YAxis))
            {
                width0 = b0.Height;
                height0 = b0.Width;
                pl0 = new Plane(bpl0.Origin, bpl0.YAxis, bpl0.XAxis);
            }
            else
            {
                width0 = b0.Width;
                height0 = b0.Height;
                pl0 = bpl0;
            }

            if (Math.Abs(cross * bpl1.XAxis) > Math.Abs(cross * bpl1.YAxis))
            {
                pl1 = new Plane(bpl1.Origin, bpl1.YAxis, bpl1.XAxis);
                width1 = b1.Height;
                height1 = b1.Width;
            }
            else
            {
                width1 = b1.Width;
                height1 = b1.Height;
                pl1 = bpl1;
            }
        }
    }
}
