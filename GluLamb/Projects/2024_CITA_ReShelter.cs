using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using Rhino;
using Rhino.Geometry;

namespace GluLamb.Projects
{
    public class CixWorkpiece
    {
        internal string Indent = "    ";

        /// <summary>
        /// The world-space coordinate system of the workpiece.
        /// </summary>
        public Plane Plane;

        /// <summary>
        /// Name of workpiece
        /// </summary>
        public string Name;

        public int NumBlankPoints = 45;
        public Curve BlankCurveInner;
        public Curve BlankCurveOuter;

        public Line BlankEnd1;
        public Line BlankEnd2;

        public int NumSplinePoints = 25;
        public Curve InnerTopSpline;
        public Curve InnerBottomSpline;
        public Curve OuterTopSpline;
        public Curve OuterBottomSpline;

        public Line CleanCut1;
        public Line CleanCut2;

        public CixWorkpiece()
        { 
        }

        public CixWorkpiece Duplicate()
        {
            return new CixWorkpiece()
            {
                Plane = Plane,
                NumBlankPoints = NumBlankPoints,
                BlankCurveInner = BlankCurveInner.DuplicateCurve(),
                BlankCurveOuter = BlankCurveOuter.DuplicateCurve(),

                BlankEnd1 = BlankEnd1,
                BlankEnd2 = BlankEnd2,

                NumSplinePoints = NumSplinePoints,
                InnerTopSpline = InnerTopSpline.DuplicateCurve(),
                InnerBottomSpline = InnerBottomSpline.DuplicateCurve(),
                OuterTopSpline = OuterTopSpline.DuplicateCurve(),
                OuterBottomSpline = OuterBottomSpline.DuplicateCurve(),

                CleanCut1 = CleanCut1,
                CleanCut2 = CleanCut2,
            };
        }

        public void Transform(Transform xform)
        {
            Plane.Transform(xform);
            BlankCurveInner.Transform(xform);
            BlankCurveOuter.Transform(xform);

            BlankEnd1.Transform(xform);
            BlankEnd2.Transform(xform);

            InnerTopSpline.Transform(xform);
            InnerBottomSpline.Transform(xform);
            OuterTopSpline.Transform(xform);
            OuterBottomSpline.Transform(xform);

            CleanCut1.Transform(xform);
            CleanCut2.Transform(xform);
        }

        private void WriteBlank(StreamWriter writer)
        {
            double[] tt;

            writer.WriteLine($"(BL_IN_CURVE)");
            tt = BlankCurveInner.DivideByCount(NumBlankPoints, true);

            for (int i = 0; i < NumBlankPoints; ++i)
            {
                var point = BlankCurveInner.PointAt(tt[i]);
                writer.WriteLine($"{Indent}BL_IN_CURVE_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}BL_IN_CURVE_P_{i + 1}_Y={point.Y}");
            }

            writer.WriteLine($"(BL_OUT_CURVE)");
            tt = BlankCurveOuter.DivideByCount(NumBlankPoints, true);

            for (int i = 0; i < NumBlankPoints; ++i)
            {
                var point = BlankCurveOuter.PointAt(tt[i]);
                writer.WriteLine($"{Indent}BL_OUT_CURVE_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}BL_OUT_CURVE_P_{i + 1}_Y={point.Y}");
            }

            writer.WriteLine($"(BL_E_1)");
            writer.WriteLine($"{Indent}BL_E_1_IN_X={BlankEnd1.FromX}");
            writer.WriteLine($"{Indent}BL_E_1_IN_Y={BlankEnd1.FromY}");
            writer.WriteLine($"{Indent}BL_E_1_OUT_X={BlankEnd1.ToX}");
            writer.WriteLine($"{Indent}BL_E_1_OUT_Y={BlankEnd1.ToY}");

            writer.WriteLine($"(BL_E_2)");
            writer.WriteLine($"{Indent}BL_E_2_IN_X={BlankEnd2.FromX}");
            writer.WriteLine($"{Indent}BL_E_2_IN_Y={BlankEnd2.FromY}");
            writer.WriteLine($"{Indent}BL_E_2_OUT_X={BlankEnd2.ToX}");
            writer.WriteLine($"{Indent}BL_E_2_OUT_Y={BlankEnd2.ToY}");
        }

        private void WriteSplines(StreamWriter writer)
        {
            double[] tt;
            string name;

            name = "TOP_IN";
            writer.WriteLine($"({name})");
            tt = InnerTopSpline.DivideByCount(NumSplinePoints, true);

            for (int i = 0; i < NumSplinePoints; ++i)
            {
                var point = InnerTopSpline.PointAt(tt[i]);
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_Y={point.Y}");
            }

            name = "TOP_OUT";
            writer.WriteLine($"({name})");
            tt = OuterTopSpline.DivideByCount(NumSplinePoints, true);

            for (int i = 0; i < NumSplinePoints; ++i)
            {
                var point = OuterTopSpline.PointAt(tt[i]);
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_Y={point.Y}");
            }

            name = "BOTTOM_IN";
            writer.WriteLine($"({name})");
            tt = InnerBottomSpline.DivideByCount(NumSplinePoints, true);

            for (int i = 0; i < NumSplinePoints; ++i)
            {
                var point = InnerBottomSpline.PointAt(tt[i]);
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_Y={point.Y}");
            }

            name = "BOTTOM_OUT";
            writer.WriteLine($"({name})");
            tt = OuterBottomSpline.DivideByCount(NumSplinePoints, true);

            for (int i = 0; i < NumSplinePoints; ++i)
            {
                var point = OuterBottomSpline.PointAt(tt[i]);
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_X={point.X}");
                writer.WriteLine($"{Indent}{name}_SPL_P_{i + 1}_Y={point.Y}");
            }
        }

        public void WriteCleanCuts(StreamWriter writer)
        {
            writer.WriteLine($"(Clean_Cut_E1)");
            writer.WriteLine($"{Indent}E_1_RENSKAER_PKT_1_X={CleanCut1.FromX}");
            writer.WriteLine($"{Indent}E_1_RENSKAER_PKT_1_Y={CleanCut1.FromY}");
            writer.WriteLine($"{Indent}E_1_RENSKAER_PKT_2_X={CleanCut1.ToX}");
            writer.WriteLine($"{Indent}E_1_RENSKAER_PKT_2_Y={CleanCut1.ToY}");

            writer.WriteLine($"(Clean_Cut_E2)");
            writer.WriteLine($"{Indent}E_2_RENSKAER_PKT_1_X={CleanCut2.FromX}");
            writer.WriteLine($"{Indent}E_2_RENSKAER_PKT_1_Y={CleanCut2.FromY}");
            writer.WriteLine($"{Indent}E_2_RENSKAER_PKT_2_X={CleanCut2.ToX}");
            writer.WriteLine($"{Indent}E_2_RENSKAER_PKT_2_Y={CleanCut2.ToY}");

        }

        private void WriteHeader(StreamWriter writer)
        {
            var dt = System.DateTime.Now;
            var datestring = $"{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}";
            var timestring = $"{dt.Hour:00}:{dt.Minute:00}:{dt.Second:00}";

            writer.WriteLine($"({Name}");
            writer.WriteLine($"({dt.Year:0000}-{dt.Month:00}-{dt.Day:00} {dt.Hour:00}:{dt.Minute:00}:{dt.Second:00})");
            writer.WriteLine($"BEGIN PUBLICVARS");
        }

        private void WriteFooter(StreamWriter writer)
        {
            writer.WriteLine($"END PUBLICVARS");
        }

        public void Write(StreamWriter writer)
        {

            var cix = Duplicate();
            cix.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane, Plane.WorldXY));

            WriteHeader(writer);

            WriteBlank(writer);
            WriteSplines(writer);
            WriteCleanCuts(writer);
            //WriteEndCuts(writer);

            WriteFooter(writer);
        }

    }

    /*
    public abstract class CixOperation
    {
        public string Name;
        public abstract void ToCix(StreamWriter writer, Transform transform, string prefix = "");
    }

    public class CleanCut : CixOperation
    {
        public int EndId;
        public Line Cut;

        public CleanCut(int endId, Line cut)
        {
            EndId = endId;
            Cut = cut;
        }

        public override void ToCix(StreamWriter writer, Transform transform, string prefix = "")
        {
            writer.WriteLine($"({Name})");
            var start = Cut.From;
            start.Transform(transform);

            var end = Cut.To;
            end.Transform(transform);

            writer.WriteLine($"{prefix}E_{EndId}_RENSKAER_PKT_1_X={start.X}");
            writer.WriteLine($"{prefix}E_{EndId}_RENSKAER_PKT_1_Y={start.Y}");
            writer.WriteLine($"{prefix}E_{EndId}_RENSKAER_PKT_2_X={end.X}");
            writer.WriteLine($"{prefix}E_{EndId}_RENSKAER_PKT_2_Y={end.Y}");
        }
    }

    public class CrossCut : CixOperation
    {
        public int EndId;
        public Plane CutPlane;
        public Line Cut;

        public CrossCut(int endid, Plane cutPlane, Line cut)
        {
            EndId = endid;
            CutPlane = cutPlane;
            Cut = cut;
        }


        public override void ToCix(StreamWriter writer, Transform transform, string prefix = "")
        {
            writer.WriteLine($"({Name})");


        }
    }
    */

}
