using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class ButtJoint1 : TenonJoint
    {
        public double TrimPlaneSize = 300.0;
        public ButtJoint1(List<Element> elements, Factory.JointCondition jc) : base(elements, jc)
        {

        }

        public override bool Construct(bool append = false)
        {
            double dowelLength = 100.0;
            double dowelExtra = 50.0;
            var tbeam = (Tenon.Element as BeamElement).Beam;
            var mbeam = (Mortise.Element as BeamElement).Beam;

            var trimInterval = new Interval(-TrimPlaneSize, TrimPlaneSize);

            var mplane = mbeam.GetPlane(Mortise.Parameter);
            var tplane = tbeam.GetPlane(Tenon.Parameter);

            var vec = tbeam.Centreline.PointAt(tbeam.Centreline.Domain.Mid) - tbeam.Centreline.PointAt(Tenon.Parameter);
            int sign = 1;

            if (vec * mplane.XAxis < 0)
                sign = -sign;

            var tz = tplane.ZAxis;
            if (tz * vec > 0)
                tz = -tz;

            var trimPlane = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign, mplane.ZAxis, mplane.YAxis);
            var trimmer = Brep.CreatePlanarBreps(new Curve[]{new Rectangle3d(trimPlane,
                trimInterval, trimInterval).ToNurbsCurve()}, 0.01);

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

                Tenon.Geometry.Add(cyl);
                Mortise.Geometry.Add(cyl);
            }
            Tenon.Geometry.AddRange(trimmer);

            return true;
        }
    }

}
