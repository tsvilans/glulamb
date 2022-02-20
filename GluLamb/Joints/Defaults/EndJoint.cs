using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class EndJoint : Joint1
    {
        public static Plane DefaultCutPlane = Plane.Unset;
        public static double DefaultAdded = 30;

        public Plane CutPlane;
        public double Added;

        public EndJoint(List<Element> elements, JointCondition jc)
        {
            if (jc.Parts.Count < Parts.Length) throw new Exception("EndJoint needs 1 elements.");

            Parts[0] = new JointPart(elements, jc.Parts[0], this);

            CutPlane = DefaultCutPlane;
            Added = DefaultAdded;
        }

        public override string ToString()
        {
            return "EndJoint";
        }

        public override bool Construct(bool append = false)
        {
            var beam = (Parts[0].Element as BeamElement).Beam;

            Plane bplane;

            if (CutPlane == Plane.Unset)
            {
                CutPlane = beam.GetPlane(Parts[0].Parameter);
                bplane = beam.GetPlane(Parts[0].Parameter);
            }
            else
            {
                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(beam.Centreline, CutPlane, 0.01);
                if (res == null || res.Count < 1) return false;

                var pt = res[0].PointA;
                bplane = beam.GetPlane(pt);
            }

            var proj = CutPlane.ProjectAlongVector(bplane.ZAxis);

            double hw = beam.Width * 0.5;
            double hh = beam.Height * 0.5;

            var pts = new Point3d[4];
            pts[0] = bplane.PointAt(-hw - Added, -hh - Added);
            pts[1] = bplane.PointAt(-hw - Added, hh + Added);
            pts[2] = bplane.PointAt(hw + Added, hh + Added);
            pts[3] = bplane.PointAt(hw + Added, -hh - Added);

            for (int i = 0; i < 4; ++i)
            {
                pts[i].Transform(proj);
            }

            var cutter = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[2], pts[3], 0.01);

            Parts[0].Geometry.Add(cutter);


            return true;
        }
    }
}
