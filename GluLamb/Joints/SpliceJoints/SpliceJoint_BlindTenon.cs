using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;
using Rhino.Collections;

namespace GluLamb.Joints
{
    public class SpliceJoint_BlindTenon : SpliceJoint, IDowelJoint
    {
        public static double DefaultTenonLength = 100;
        public static double DefaultTenonWidth = 40;
        public static double DefaultTenonHeight = 80;
        public static double DefaultAdded = 10;
        public static double DefaultToolDiameter = 16;

        public static double DefaultDowelLength = 220;
        public static double DefaultDowelDrillDepth = 220;
        public static double DefaultDowelDiameter = 12;
        public static double DefaultDowelLengthExtra = 15;
        public static double DefaultDowelSideTolerance = 0.5;


        //public List<object> debug;

        public double TenonLength;
        public double TenonWidth;
        public double TenonHeight;
        public double Added;
        public double ToolDiameter;

        public double DowelLength { get; set; }
        public double DowelDrillDepth { get; set; }
        public double DowelDiameter { get; set; }
        public double DowelLengthExtra { get; set; }
        public double DowelSideTolerance { get; set; }
        public List<Dowel> Dowels { get; set; }

        public double ToleranceSide = 0.5;
        public double ToleranceEnd = 1.5;

        private Plane EndPlane;
        private Plane EndPlaneT;

        private Plane FacePlane;
        private double Width, Height;

        public SpliceJoint_BlindTenon(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
            TenonLength = DefaultTenonLength;
            TenonWidth = DefaultTenonWidth;
            TenonHeight = DefaultTenonHeight;
            Added = DefaultAdded;
            ToolDiameter = DefaultToolDiameter;

            DowelLength = DefaultDowelLength;
            DowelDrillDepth = DefaultDowelDrillDepth;
            DowelDiameter = DefaultDowelDiameter;
            DowelLengthExtra = DefaultDowelLengthExtra;
            DowelSideTolerance = DefaultDowelSideTolerance;
            Dowels = new List<Dowel>();
        }

        public SpliceJoint_BlindTenon(SpliceJoint sj) : base(sj)
        {
            TenonLength = DefaultTenonLength;
            TenonWidth = DefaultTenonWidth;
            TenonHeight = DefaultTenonHeight;
            Added = DefaultAdded;
            ToolDiameter = DefaultToolDiameter;

            DowelLength = DefaultDowelLength;
            DowelDrillDepth = DefaultDowelDrillDepth;

            DowelDiameter = DefaultDowelDiameter;
            DowelLengthExtra = DefaultDowelLengthExtra;
            DowelSideTolerance = DefaultDowelSideTolerance;
            Dowels = new List<Dowel>();

        }

        public override string ToString()
        {
            return "SpliceJoint_BlindTenon";
        }

        private Brep CreateTenon(double sideTolerance = 0.0, double endTolerance = 0.0)
        {
            var basePts = new Point3d[5];
            var baseTenonPts = new Point3d[5];
            var tipTenonPts = new Point3d[5];

            basePts[0] = FacePlane.PointAt(Width * 0.5 + Added, Height * 0.5 + Added);
            basePts[1] = FacePlane.PointAt(Width * 0.5 + Added, -Height * 0.5 - Added);
            basePts[2] = FacePlane.PointAt(-Width * 0.5 - Added, -Height * 0.5 - Added);
            basePts[3] = FacePlane.PointAt(-Width * 0.5 - Added, Height * 0.5 + Added);
            basePts[4] = basePts[0];

            var baseOutline = new Polyline(basePts).ToNurbsCurve();

            baseTenonPts[0] = FacePlane.PointAt(TenonWidth * 0.5 + sideTolerance, TenonHeight * 0.5 + sideTolerance);
            baseTenonPts[1] = FacePlane.PointAt(TenonWidth * 0.5 + sideTolerance, -TenonHeight * 0.5 - sideTolerance);
            baseTenonPts[2] = FacePlane.PointAt(-TenonWidth * 0.5 - sideTolerance, -TenonHeight * 0.5 - sideTolerance);
            baseTenonPts[3] = FacePlane.PointAt(-TenonWidth * 0.5 - sideTolerance, TenonHeight * 0.5 + sideTolerance);
            baseTenonPts[4] = baseTenonPts[0];
            var baseTenonOutline = new Polyline(baseTenonPts).ToNurbsCurve();
            baseTenonOutline = Curve.CreateFilletCornersCurve(baseTenonOutline, ToolDiameter * 0.5 - sideTolerance, 0.01, 0.1).ToNurbsCurve();

            EndPlaneT = new Plane(EndPlane.Origin - EndPlane.ZAxis * endTolerance, EndPlane.XAxis, EndPlane.YAxis);

            tipTenonPts[0] = EndPlaneT.PointAt(TenonWidth * 0.5 + sideTolerance, TenonHeight * 0.5 + sideTolerance);
            tipTenonPts[1] = EndPlaneT.PointAt(TenonWidth * 0.5 + sideTolerance, -TenonHeight * 0.5 - sideTolerance);
            tipTenonPts[2] = EndPlaneT.PointAt(-TenonWidth * 0.5 - sideTolerance, -TenonHeight * 0.5 - sideTolerance);
            tipTenonPts[3] = EndPlaneT.PointAt(-TenonWidth * 0.5 - sideTolerance, TenonHeight * 0.5 + sideTolerance);
            tipTenonPts[4] = tipTenonPts[0];
            var tipTenonOutline = new Polyline(tipTenonPts).ToNurbsCurve();
            tipTenonOutline = Curve.CreateFilletCornersCurve(tipTenonOutline, ToolDiameter * 0.5 - sideTolerance, 0.01, 0.1).ToNurbsCurve();

            for (int i = 0; i < 4; ++i)
            {
                debug.Add(basePts[i]);
                debug.Add(baseTenonPts[i]);
                debug.Add(tipTenonPts[i]);
            }

            debug.Add(baseOutline);
            debug.Add(baseTenonOutline);
            debug.Add(tipTenonOutline);

            var baseSrf = Brep.CreatePlanarBreps(new Curve[] { baseOutline, baseTenonOutline }, 0.01)[0];
            var tenonSrf = Brep.CreateFromLoft(new Curve[] { baseTenonOutline, tipTenonOutline }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
            var tenonTipSrf = Brep.CreatePlanarBreps(new Curve[] { tipTenonOutline }, 0.01)[0];

            var cutterJoined = Brep.JoinBreps(new Brep[] { baseSrf, tenonSrf, tenonTipSrf }, 0.01);

            return cutterJoined[0];
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

            EndPlane = new Plane(planes[0].Origin + planes[0].ZAxis * TenonLength, planes[0].XAxis, planes[0].YAxis);
            FacePlane = planes[0];

            debug.Add(planes[0]);
            debug.Add(planes[1]);
            debug.Add(EndPlane);

            Height = Math.Max(beams[0].Height, beams[1].Height);
            Width = Math.Max(beams[0].Width, beams[1].Width);

            TenonWidth = Math.Min(Width, TenonWidth);
            TenonHeight = Math.Min(Height, TenonHeight);

            var cutterHole = CreateTenon(ToleranceSide, 0);
            var cutter = CreateTenon(0, ToleranceEnd);

            FirstHalf.Geometry.Add(cutter);
            SecondHalf.Geometry.Add(cutterHole);

            FirstHalf.Element.UserDictionary.Set(string.Format("EndCut_{0}", SecondHalf.Element.Name), EndPlaneT);
            SecondHalf.Element.UserDictionary.Set(string.Format("EndCut_{0}", FirstHalf.Element.Name), FacePlane);

            var tapAd = new ArchivableDictionary();
            tapAd.Set("EndPlane", EndPlane);
            tapAd.Set("SlotPlane", FacePlane);
            tapAd.Set("PlateFace0", new Plane(FacePlane.Origin + FacePlane.YAxis * TenonHeight / 2, FacePlane.ZAxis, FacePlane.XAxis));
            tapAd.Set("PlateFace1", new Plane(FacePlane.Origin - FacePlane.YAxis * TenonHeight / 2, FacePlane.ZAxis, FacePlane.XAxis));
            tapAd.Set("TenonSide0", new Plane(FacePlane.Origin + FacePlane.XAxis * TenonWidth / 2, FacePlane.ZAxis, FacePlane.YAxis));
            tapAd.Set("TenonSide1", new Plane(FacePlane.Origin - FacePlane.XAxis * TenonWidth / 2, FacePlane.ZAxis, FacePlane.YAxis));
            tapAd.Set("PlateThickness", TenonHeight);
            tapAd.Set("Depth", Math.Abs(EndPlane.DistanceTo(FacePlane.Origin)));

            SecondHalf.Element.UserDictionary.Set(String.Format("TenonSlot_{0}", FirstHalf.Element.Name), tapAd);

            // Create dowels
            var dowelPlane = new Plane(planes[0].Origin + planes[0].ZAxis * TenonLength * 0.5, planes[0].YAxis);
            dowelPlane.Transform(Transform.Translation(-dowelPlane.ZAxis * Width * 0.5));

            Dowels.Add(new Dowel(new Line(dowelPlane.Origin, dowelPlane.ZAxis * DowelLength), DowelDiameter));

            var dowel = new Cylinder(
              new Circle(dowelPlane, DowelDiameter * 0.5), DowelLength);

            dowel.Height1 = -DowelLengthExtra;
            dowel.Height2 = DowelLength;

            var dowelTenonPlane = dowelPlane;
            dowelTenonPlane.Origin = dowelTenonPlane.Origin - EndPlane.ZAxis * DowelSideTolerance;

            var dowelTenon = new Cylinder(
              new Circle(dowelTenonPlane, DowelDiameter * 0.5), DowelLength);

            dowelTenon.Height1 = -DowelLengthExtra;
            dowelTenon.Height2 = DowelLength;

            FirstHalf.Geometry.Add(dowel.ToBrep(true, true));
            SecondHalf.Geometry.Add(dowelTenon.ToBrep(true, true));

            //Parts[i].Element.UserDictionary.Set(String.Format("PlateDowel_{0}", Guid.NewGuid().ToString().Substring(0, 8)), new Line(dowelPlanes[i].Origin, dowelPlanes[i].ZAxis * DowelLength));


            FirstHalf.Element.UserDictionary.Set(string.Format("PlateDowel_{0}", SecondHalf.Element.Name), 
                    new Line(dowel.BasePlane.Origin + dowel.BasePlane.ZAxis * dowel.Height1, dowel.BasePlane.Origin +
                    dowel.BasePlane.ZAxis * dowel.Height2));

            SecondHalf.Element.UserDictionary.Set(string.Format("PlateDowel_{0}", FirstHalf.Element.Name),
                new Line(dowelTenon.BasePlane.Origin + dowelTenon.BasePlane.ZAxis * dowelTenon.Height1, dowelTenon.BasePlane.Origin +
                dowelTenon.BasePlane.ZAxis * dowelTenon.Height2));

            return true;
        }
    }
}
