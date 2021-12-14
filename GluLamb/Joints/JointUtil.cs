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
    }
}
