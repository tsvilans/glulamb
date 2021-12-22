using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public partial class JointConstructor
    {
        public static bool ButtJoint1(TenonJoint tj)
        {
            double dowelLength = 100.0;
            double dowelExtra = 50.0;
            var tbeam = (tj.Tenon.Element as BeamElement).Beam;
            var mbeam = (tj.Mortise.Element as BeamElement).Beam;

            var mplane = mbeam.GetPlane(tj.Mortise.Parameter);
            var tplane = tbeam.GetPlane(tj.Tenon.Parameter);

            var vec = tbeam.Centreline.PointAt(tbeam.Centreline.Domain.Mid) - tbeam.Centreline.PointAt(tj.Tenon.Parameter);
            int sign = 1;

            if (vec * mplane.XAxis < 0)
                sign = -sign;

            var tz = tplane.ZAxis;
            if (tz * vec > 0)
                tz = -tz;

            var trimPlane = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign, mplane.ZAxis, mplane.YAxis);
            var trimmer = Brep.CreatePlanarBreps(new Curve[]{new Rectangle3d(trimPlane,
                new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

            var xform = trimPlane.ProjectAlongVector(tplane.ZAxis);

            for (int i = -1; i < 2; i += 2)
            {
                Point3d dp = new Point3d(tplane.Origin
                  + tplane.YAxis * 0.16 * tbeam.Height * i);

                dp.Transform(xform);
                dp.Transform(Transform.Translation(-tz * dowelLength * 0.5));

                var dowelPlane = new Plane(dp, tz);
                var cyl = new Cylinder(
                  new Circle(dowelPlane, 6.0), dowelLength + dowelExtra).ToBrep(true, true);

                tj.Tenon.Geometry.Add(cyl);
                tj.Mortise.Geometry.Add(cyl);
            }
            tj.Tenon.Geometry.AddRange(trimmer);

            return true;
        }

    }
}
