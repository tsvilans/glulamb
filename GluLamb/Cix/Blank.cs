using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public class CixBlank : ITransformable, ICix
    {
        public double Length, Width, Height;

        public virtual void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add($"{prefix}BL_L={Length:0.###}");
            cix.Add($"{prefix}BL_W={Width:0.###}");
        }

        public virtual void Transform(Transform xform)
        {

        }
    }

    public class CixCurvedBlank : CixBlank
    {
        public static int NumPoints = 45;
        public Curve CurveInner;
        public Curve CurveOuter;

        public Line End1;
        public Line End2;

        public override void ToCix(List<string> cix, string prefix = "")
        {
            base.ToCix(cix, prefix);

            // Write other variables - TODO : Find out where these belong
            cix.Add($"{prefix}SEC_N={0}");
            cix.Add($"{prefix}SEC_E1_L={0:0.###}");
            cix.Add($"{prefix}SEC_E2_L={0:0.###}");
            cix.Add($"{prefix}V_START={0:0.###}");
            cix.Add($"{prefix}SEC_E_1_V={0:0.###}");
            cix.Add($"{prefix}SEC_E_2_V={0:0.###}");
            for (int i = 0; i <= 16; ++i)
                cix.Add($"{prefix}SEC_{i}_V={0:0.###}");

            // Create blank curve subdivisions and points
            double[] tt;

            cix.Add($"(BL_IN_CURVE)");
            tt = CurveInner.DivideByCount(NumPoints - 1, true);

            for (int i = 0; i < NumPoints; ++i)
            {
                var point = CurveInner.PointAt(tt[i]);
                cix.Add($"{prefix}BL_IN_CURVE_P_{i + 1}_X={point.X:0.###}");
                cix.Add($"{prefix}BL_IN_CURVE_P_{i + 1}_Y={point.Y:0.###}");
            }

            cix.Add($"(BL_OUT_CURVE)");
            tt = CurveOuter.DivideByCount(NumPoints, true);

            for (int i = 0; i < NumPoints; ++i)
            {
                var point = CurveOuter.PointAt(tt[i]);
                cix.Add($"{prefix}BL_OUT_CURVE_P_{i + 1}_X={point.X:0.###}");
                cix.Add($"{prefix}BL_OUT_CURVE_P_{i + 1}_Y={point.Y:0.###}");
            }

            cix.Add($"(BL_E_1)");
            cix.Add($"{prefix}BL_E_1_IN_X={End1.FromX:0.###}");
            cix.Add($"{prefix}BL_E_1_IN_Y={End1.FromY:0.###}");
            cix.Add($"{prefix}BL_E_1_OUT_X={End1.ToX:0.###}");
            cix.Add($"{prefix}BL_E_1_OUT_Y={End1.ToY:0.###}");

            cix.Add($"(BL_E_2)");
            cix.Add($"{prefix}BL_E_2_IN_X={End2.FromX:0.###}");
            cix.Add($"{prefix}BL_E_2_IN_Y={End2.FromY:0.###}");
            cix.Add($"{prefix}BL_E_2_OUT_X={End2.ToX:0.###}");
            cix.Add($"{prefix}BL_E_2_OUT_Y={End2.ToY:0.###}");

            // For segmented blank ONLY - TODO: Implemented segments, possibly in another blank class
            for (int i = 0; i < 16; ++i)
            {
                cix.Add($"{prefix}BL_SEC_{i}_{i + 1}_IN_X={0:0.###}");
                cix.Add($"{prefix}BL_SEC_{i}_{i + 1}_IN_Y={0:0.###}");
                cix.Add($"{prefix}BL_SEC_{i}_{i + 1}_OUT_X={0:0.###}");
                cix.Add($"{prefix}BL_SEC_{i}_{i + 1}_OUT_Y={0:0.###}");
            }
        }

        public override void Transform(Transform xform)
        {
            CurveInner.Transform(xform);
            CurveOuter.Transform(xform);

            End1.Transform(xform);
            End2.Transform(xform);
        }
    }
}
