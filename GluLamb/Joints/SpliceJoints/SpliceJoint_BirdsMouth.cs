using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

using Rhino;

namespace GluLamb.Joints
{
    public class SpliceJoint_BirdsMouth : SpliceJoint
    {
        public List<object> debug;

        public double DowelLength = 150;
        public double DowelDiameter = 12.0;
        public double Angle = RhinoMath.ToRadians(30.0);
        public double Added = 10.0;

        public SpliceJoint_BirdsMouth(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
        }

        public override bool Construct(bool append = false)
        {
            debug = new List<object>();

            var beams = new Beam[2];
            beams[0] = (FirstHalf.Element as BeamElement).Beam;
            beams[1] = (SecondHalf.Element as BeamElement).Beam;

            var planes = new Plane[2];
            for (int i = 0; i < 2; ++i)
            {
                planes[i] = beams[i].GetPlane(Parts[i].Parameter);
            }

            bool shift = Math.Abs(planes[0].XAxis * planes[1].XAxis) > Math.Abs(planes[0].XAxis * planes[1].YAxis) ? true : false;

            var x0 = planes[0].XAxis;
            var x1 = shift ? planes[1].XAxis : planes[1].YAxis;
            if (x0 * x1 < 0)
                x1 = -x1;

            var commonX = (x0 + x1) / 2;

            var y0 = planes[0].YAxis;
            var y1 = shift ? planes[1].YAxis : planes[1].XAxis;
            if (y0 * y1 < 0)
                y1 = -y1;

            var commonY = (y0 + y1) / 2;

            var jplane = new Plane((planes[0].Origin + planes[1].Origin) / 2, commonX, commonY);
            debug.Add(jplane);

            var width0 = beams[0].Width;
            var width1 = shift ? beams[1].Width : beams[1].Height;

            var height0 = beams[0].Height;
            var height1 = shift ? beams[1].Height : beams[1].Width;

            double width = Math.Max(width0, width1) + Added * 2;
            double height = Math.Max(height0, height1) + Added;
            double depth = Math.Tan(Angle) * (height / 2);

            //if (width < height)
            //  jplane = new Plane(jplane.Origin, jplane.YAxis, -jplane.XAxis);


            double hw = width / 2, hh = height / 2, hd = depth / 2;

            // Create middle points
            var pts = new Point3d[6];

            pts[0] = jplane.PointAt(hw, 0, -hd);
            pts[1] = jplane.PointAt(-hw, 0, -hd);

            pts[2] = jplane.PointAt(hw, hh, hd);
            pts[3] = jplane.PointAt(-hw, hh, hd);

            // Create top points
            pts[4] = jplane.PointAt(hw, -hh, hd);
            pts[5] = jplane.PointAt(-hw, -hh, hd);

            // Create bottom points
            var brep = Brep.CreateFromCornerPoints(pts[0], pts[2], pts[3], pts[1], 0.01);
            brep.Join(Brep.CreateFromCornerPoints(pts[0], pts[1], pts[5], pts[4], 0.01), 0.01, true);

            FirstHalf.Geometry.Add(brep);
            SecondHalf.Geometry.Add(brep);

            // Create dowels
            var dowelPlanes = new Plane[2];
            double dowelDistance = beams[0].Height / 2;

            dowelPlanes[0] = new Plane(planes[0].Origin + planes[0].YAxis * dowelDistance * 0.5, planes[0].ZAxis);
            dowelPlanes[1] = new Plane(planes[0].Origin - planes[0].YAxis * dowelDistance * 0.5, planes[0].ZAxis);

            dowelPlanes[0].Transform(Transform.Translation(dowelPlanes[0].ZAxis * -DowelLength * 0.5));
            dowelPlanes[1].Transform(Transform.Translation(dowelPlanes[1].ZAxis * -DowelLength * 0.5));

            var dowels = new Brep[2];
            for (int i = 0; i < 2; ++i)
            {
                dowels[i] = new Cylinder(
                  new Circle(dowelPlanes[i], DowelDiameter * 0.5), DowelLength).ToBrep(true, true);
            }

            FirstHalf.Geometry.AddRange(dowels);
            SecondHalf.Geometry.AddRange(dowels);

            return true;
        }
    }
}
