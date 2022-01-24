using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class SpliceJoint_Tenon3 : SpliceJoint
    {
        public List<object> debug;

        public double TenonLength = 100;
        public double DowelLength = 150;
        public double DowelDiameter = 12.0;
        public double Added = 10.0;

        public SpliceJoint_Tenon3(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
        }

        public SpliceJoint_Tenon3(SpliceJoint sj) : base(sj)
        {
        }

        public override string ToString()
        {
            return "SpliceJoint_ThruTenon3";
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

            // TODO: Align curve tangents and ends

            var endPlanes = new Plane[2];
            endPlanes[0] = beams[0].GetPlane(planes[0].Origin - planes[0].ZAxis * TenonLength * 0.5);
            endPlanes[1] = beams[1].GetPlane(planes[1].Origin + planes[1].ZAxis * TenonLength * 0.5);

            debug.Add(planes[0]);
            debug.Add(planes[1]);
            debug.Add(endPlanes[0]);
            debug.Add(endPlanes[1]);

            double third = beams[0].Width / 3;
            double height = Math.Max(beams[0].Height, beams[1].Height);

            var topPts = new Point3d[6];
            var btmPts = new Point3d[6];

            topPts[0] = endPlanes[0].PointAt(-beams[0].Width * 0.5 - Added, height * 0.5 + Added);
            topPts[1] = endPlanes[0].PointAt(-third * 0.5, height * 0.5 + Added);
            topPts[2] = endPlanes[1].PointAt(-third * 0.5, height * 0.5 + Added);
            topPts[3] = endPlanes[1].PointAt(third * 0.5, height * 0.5 + Added);
            topPts[4] = endPlanes[0].PointAt(third * 0.5, height * 0.5 + Added);
            topPts[5] = endPlanes[0].PointAt(beams[0].Width * 0.5 + Added, height * 0.5 + Added);

            var topOutline = new Polyline(topPts).ToNurbsCurve();

            btmPts[0] = endPlanes[0].PointAt(-beams[0].Width * 0.5 - Added, -height * 0.5 - Added);
            btmPts[1] = endPlanes[0].PointAt(-third * 0.5, -height * 0.5 - Added);
            btmPts[2] = endPlanes[1].PointAt(-third * 0.5, -height * 0.5 - Added);
            btmPts[3] = endPlanes[1].PointAt(third * 0.5, -height * 0.5 - Added);
            btmPts[4] = endPlanes[0].PointAt(third * 0.5, -height * 0.5 - Added);
            btmPts[5] = endPlanes[0].PointAt(beams[0].Width * 0.5 + Added, -height * 0.5 - Added);

            var btmOutline = new Polyline(btmPts).ToNurbsCurve();

            for (int i = 0; i < 6; ++i)
            {
                debug.Add(topPts[i]);
                debug.Add(btmPts[i]);
            }

            debug.Add(topOutline);
            debug.Add(btmOutline);

            var tenonCutter = Brep.CreateFromLoft(new Curve[] { topOutline, btmOutline }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

            FirstHalf.Geometry.Add(tenonCutter);
            SecondHalf.Geometry.Add(tenonCutter);

            // Create dowels
            var dowelPlanes = new Plane[2];
            double dowelDistance = beams[0].Height / 3;

            dowelPlanes[0] = new Plane(planes[0].Origin + planes[0].YAxis * dowelDistance * 0.5, planes[0].XAxis);
            dowelPlanes[1] = new Plane(planes[0].Origin - planes[0].YAxis * dowelDistance * 0.5, planes[0].XAxis);

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
