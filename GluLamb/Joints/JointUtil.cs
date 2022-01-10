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
