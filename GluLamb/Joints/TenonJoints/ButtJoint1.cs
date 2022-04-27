using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Collections;
using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class ButtJoint1 : TenonJoint, IDowelJoint
    {
        public static double DefaultTrimPlaneSize = 300.0;
        public static double DefaultDowelLength = 100.0;
        public static double DefaultDowelDrillDepth = 110;
        public static double DefaultDowelOffset = 30.0;
        public static double DefaultDowelDiameter = 12;
        public static double DefaultDowelLengthExtra = 20.0;

        public double TrimPlaneSize = 300.0;
        public double DowelOffset = 30.0;
        public double DowelDiameter {get;set;}
        public double DowelLength { get; set; }
        public double DowelDrillDepth { get; set; }

        public double DowelLengthExtra { get; set; }
        public double DowelSideTolerance { get; set; }
        public List<double> DowelLengths { get; set; }

        public List<Dowel> Dowels { get; set; }

        public ButtJoint1(List<Element> elements, Factory.JointCondition jc) : base(elements, jc)
        {
            TrimPlaneSize = DefaultTrimPlaneSize;
            DowelLength = DefaultDowelLength;
            DowelDrillDepth = DefaultDowelDrillDepth;

            DowelOffset = DefaultDowelOffset;
            DowelDiameter = DefaultDowelDiameter;
            DowelLengthExtra = DefaultDowelLengthExtra;

            Dowels = new List<Dowel>();
        }

        public ButtJoint1(List<Element> elements, Factory.JointConditionPart tenon, Factory.JointConditionPart mortise) : base(elements, tenon, mortise)
        {
            TrimPlaneSize = DefaultTrimPlaneSize;
            DowelLength = DefaultDowelLength;
            DowelDrillDepth = DefaultDowelDrillDepth;
            DowelOffset = DefaultDowelOffset;
            DowelDiameter = DefaultDowelDiameter;
            DowelLengthExtra = DefaultDowelLengthExtra;

            Dowels = new List<Dowel>();

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

            Tenon.Element.UserDictionary.Set(String.Format("EndCut_{0}", Mortise.Element.Name), trimPlane);

            var projTrim = trimPlane.ProjectAlongVector(tplane.ZAxis);

            var adTenon = new ArchivableDictionary();
            var adMortise = new ArchivableDictionary();

            double drillDepth = DowelDrillDepth;

            int counter = 0;
            for (int i = -1; i < 2; i += 2)
            {
                //Point3d dp = new Point3d(tplane.Origin + tplane.YAxis * (tbeam.Height * i - DowelOffset));
                Point3d dp = new Point3d(tplane.Origin + tplane.YAxis * (DowelOffset * i));

                dp.Transform(projTrim);
                //dp.Transform(Transform.Translation(-tz * DowelLength * 0.5));

                var dowelPlaneTenon = new Plane(dp, -tz);
                var dowelPlaneMortise = new Plane(dp, tz);

                var cylTenon = new Cylinder(
                  new Circle(dowelPlaneTenon, DowelDiameter * 0.5), drillDepth);//.ToBrep(true, true);

                var dowelAxis = new Line(dowelPlaneTenon.Origin, dowelPlaneTenon.ZAxis * DowelLength);

                cylTenon.Height1 = -DowelLengthExtra;
                cylTenon.Height2 = drillDepth;

                var adTenonAxis = new Line(cylTenon.BasePlane.Origin + cylTenon.BasePlane.ZAxis * cylTenon.Height1, cylTenon.BasePlane.Origin +
                    cylTenon.BasePlane.ZAxis * cylTenon.Height2);

                adTenon.Set(string.Format("Plane{0}", counter), dowelPlaneTenon);
                adTenon.Set(string.Format("Dowel{0}_{1}", counter, Mortise.Element.Name), adTenonAxis
                    );

                Dowels.Add(new Dowel(dowelAxis, DowelDiameter, drillDepth));

                //Tenon.Element.UserDictionary.Set(String.Format("Dowel{0}T_{1}", counter, Mortise.Element.Name),
                //    new Line(cylTenon.BasePlane.Origin + cylTenon.BasePlane.ZAxis * cylTenon.Height1, cylTenon.BasePlane.Origin +
                //    cylTenon.BasePlane.ZAxis * cylTenon.Height2));

                var cylMortise = new Cylinder(
                  new Circle(dowelPlaneMortise, DowelDiameter * 0.5), DowelLength);//.ToBrep(true, true);

                cylMortise.Height1 = -DowelLengthExtra;
                cylMortise.Height2 = mbeam.Width + DowelLengthExtra;

                //Mortise.Element.UserDictionary.Set(String.Format("Dowel{0}M_{1}", counter, Tenon.Element.Name),
                //    new Line(cylMortise.BasePlane.Origin + cylMortise.BasePlane.ZAxis * cylMortise.Height1, cylMortise.BasePlane.Origin +
                //    cylMortise.BasePlane.ZAxis * cylMortise.Height2));

                var adMortiseAxis = new Line(cylMortise.BasePlane.Origin + cylMortise.BasePlane.ZAxis * cylMortise.Height1, cylMortise.BasePlane.Origin +
                    cylMortise.BasePlane.ZAxis * cylMortise.Height2);

                adMortise.Set(string.Format("Plane{0}", counter), dowelPlaneMortise);
                adMortise.Set(String.Format("Dowel{0}_{1}", counter, Tenon.Element.Name), adMortiseAxis
                    );

                //throw new Exception(string.Format("{0}: height1 {1}; height2 {2}; baseplane {3}; total height: {4}; radius {5}", 
                //    DowelDiameter, cylTenon.Height1, cylTenon.Height2, cylTenon.BasePlane, cylTenon.TotalHeight, cylTenon.Radius));

                Tenon.Geometry.Add(cylTenon.ToBrep(true, true));
                Mortise.Geometry.Add(cylMortise.ToBrep(true, true));

                counter++;
            }

            Tenon.Element.UserDictionary.Set(string.Format("DowelGroupT_{0}", Mortise.Element.Name), adTenon);
            Mortise.Element.UserDictionary.Set(string.Format("DowelGroupM_{0}", Tenon.Element.Name), adMortise);

            return true;
        }
    }

}
