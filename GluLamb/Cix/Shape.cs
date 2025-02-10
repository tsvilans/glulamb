using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public class CixShape : ITransformable, ICix
    {
        public static int NumSplinePoints = 25;

        public Curve InnerTopSpline;
        public Curve InnerBottomSpline;
        public Curve OuterTopSpline;
        public Curve OuterBottomSpline;

        public void ToCix(List<string> cix, string prefix = "")
        {
            double[] tt;

            var names = new string[] { "TOP_IN", "TOP_OUT", "BOTTOM_IN", "BOTTOM_OUT" };
            var splines = new Curve[] { InnerTopSpline, OuterTopSpline, InnerBottomSpline, OuterBottomSpline };

            for (int i = 0; i < 4; ++i)
            {
                string name = names[i];
                var spline = splines[i];

                cix.Add($"({name})");
                tt = spline.DivideByCount(NumSplinePoints, true);

                for (int j = 0; j < NumSplinePoints; ++j)
                {
                    var point = spline.PointAt(tt[j]);
                    point.Transform(Cix.ToZDown);

                    cix.Add($"{prefix}{name}_SPL_P_{j + 1}_X={point.X:0.###}");
                    cix.Add($"{prefix}{name}_SPL_P_{j + 1}_Y={point.Y:0.###}");
                    cix.Add($"{prefix}{name}_SPL_P_{j + 1}_Z={point.Z:0.###}");
                }
            }
        }

        public void Transform(Transform xform)
        {
            InnerTopSpline.Transform(xform);
            InnerBottomSpline.Transform(xform);
            OuterTopSpline.Transform(xform);
            OuterBottomSpline.Transform(xform);
        }
    }
}
