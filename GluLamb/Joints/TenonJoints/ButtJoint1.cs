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
        public double DowelLength = 100.0;
        public double DowelOffset = 30.0;
        public double DowelDiameter = 12;
        public double DowelLengthExtra = 50.0;

        public ButtJoint1(List<Element> elements, Factory.JointCondition jc) : base(elements, jc)
        {

        }

        public ButtJoint1(List<Element> elements, Factory.JointConditionPart tenon, Factory.JointConditionPart mortise) : base(elements, tenon, mortise)
        {

        }

        public override string ToString()
        {
            return "ButtJoint_Flat2Dowels";
        }

        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach(var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }

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
            Tenon.Geometry.AddRange(trimmer);

            var projTrim = trimPlane.ProjectAlongVector(tplane.ZAxis);

            for (int i = -1; i < 2; i += 2)
            {
                //Point3d dp = new Point3d(tplane.Origin + tplane.YAxis * (tbeam.Height * i - DowelOffset));
                Point3d dp = new Point3d(tplane.Origin + tplane.YAxis * (DowelOffset * i));

                dp.Transform(projTrim);
                dp.Transform(Transform.Translation(-tz * DowelLength * 0.5));

                var dowelPlane = new Plane(dp, tz);
                var cyl = new Cylinder(
                  new Circle(dowelPlane, DowelDiameter * 0.5), DowelLength + DowelLengthExtra).ToBrep(true, true);

                Tenon.Geometry.Add(cyl);
                Mortise.Geometry.Add(cyl);
            }

            return true;
        }
    }

}
