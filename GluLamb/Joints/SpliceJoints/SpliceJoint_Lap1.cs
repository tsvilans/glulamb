using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    [Serializable]
    public class SpliceJoint_Lap1 : SpliceJoint
    {
        public static double DefaultDowelLength = 300;
        public static double DefaultDowelDiameter = 12.0;
        public static double DefaultLapLength = 250;
        public static double DefaultLapAngle = RhinoMath.ToRadians(25.0);
        public static double DefaultAdded = 10.0;
        public static double DefaultRotation = 0.0;

        public static bool DefaultBackCut = true;
        public static bool DefaultAngleDowels = false;

        public List<object> debug;

        public double DowelLength;
        public double DowelDiameter;
        public double LapLength;
        public double LapAngle;
        public double Rotation;
        public double Added;

        public bool BackCut;
        public bool AngleDowels;

        public SpliceJoint_Lap1(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            DowelLength = DefaultDowelLength;
            DowelDiameter = DefaultDowelDiameter;
            LapLength = DefaultLapLength;
            LapAngle = DefaultLapAngle;
            Added = DefaultAdded;

            BackCut = DefaultBackCut;
            AngleDowels = DefaultAngleDowels;
            Rotation = DefaultRotation;
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
            if (Rotation > 0)
                jplane.Transform(Transform.Rotation(Rotation, jplane.ZAxis, jplane.Origin));
            debug.Add(jplane);

            var width0 = beams[0].Width;
            var width1 = shift ? beams[1].Width : beams[1].Height;

            var height0 = beams[0].Height;
            var height1 = shift ? beams[1].Height : beams[1].Width;

            double width = Math.Max(width0, width1) + Added * 2;
            double height = Math.Max(height0, height1) + Added * 2;
            double tan = Math.Tan(LapAngle);
            double depth = tan * (LapLength / 2);

            double back_depth = tan * (height * 0.5 - depth);
            if (!BackCut)
                back_depth = 0;

            double hw = width / 2, hh = height / 2, hd = depth / 2;

            var pts = new Point3d[8];

            double hl = LapLength / 2;
            pts[0] = jplane.PointAt(hw, -hh, hl - back_depth);
            pts[1] = jplane.PointAt(-hw, -hh, hl - back_depth);

            pts[2] = jplane.PointAt(hw, -depth, hl);
            pts[3] = jplane.PointAt(-hw, -depth, hl);

            pts[4] = jplane.PointAt(hw, depth, -hl);
            pts[5] = jplane.PointAt(-hw, depth, -hl);

            pts[6] = jplane.PointAt(hw, hh, -hl + back_depth);
            pts[7] = jplane.PointAt(-hw, hh, -hl + back_depth);


            // Create bottom points
            var brep = Brep.CreateFromCornerPoints(pts[0], pts[1], pts[3], pts[2], 0.01);
            brep.Join(Brep.CreateFromCornerPoints(pts[2], pts[3], pts[5], pts[4], 0.01), 0.01, true);
            brep.Join(Brep.CreateFromCornerPoints(pts[4], pts[5], pts[7], pts[6], 0.01), 0.01, true);

            debug.Add(brep);

            FirstHalf.Geometry.Add(brep);
            SecondHalf.Geometry.Add(brep);

            var dowelPlane = new Plane(jplane.Origin, jplane.XAxis, jplane.ZAxis);
            if (AngleDowels)
                dowelPlane.Transform(Transform.Rotation(LapAngle, dowelPlane.XAxis, dowelPlane.Origin));

            // Create dowels
            var dowelPlanes = new Plane[2];
            double dowelDistance = LapLength / 2;

            dowelPlanes[0] = new Plane(dowelPlane.Origin + dowelPlane.YAxis * dowelDistance * 0.5, dowelPlane.ZAxis);
            dowelPlanes[1] = new Plane(dowelPlane.Origin - dowelPlane.YAxis * dowelDistance * 0.5, dowelPlane.ZAxis);

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
