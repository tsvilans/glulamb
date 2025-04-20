using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class Tenon : Operation
    {
        public Line PlaneLine;
        public Line LocalSawLine; // Sawline from the end, on the plane created by PlaneLine (SAV_1)
        public Line SawLine; // Sawline from the top, on the XY axis (SAV_2)
        public double T;
        public double TU;
        public double TO;
        public double Depth;
        public double OutlineRadius;
        public bool DoOutline;
        public Polyline Outline;

        public bool DoSideCuts;
        public Polyline[] SideCuts;
        public string OperationName = "TAP";


        public Tenon(string name = "Tenon")
        {
            Name = name;
            SideCuts = new Polyline[2];
            SideCuts[0] = new Polyline();
            SideCuts[1] = new Polyline();
            Outline = new Polyline();
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        public override List<object> GetObjects()
        {
            return new List<object> { PlaneLine, SawLine, Outline, SideCuts[0], SideCuts[1] };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            //cix.Add(string.Format("{0}{1}_{2}=1", prefix, OperationName, Id));
            cix.Add(string.Format("{0}{1}={2}", prefix, OperationName, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_PL_PKT_1_X={3:0.000}", prefix, OperationName, Id, PlaneLine.From.X));
            cix.Add(string.Format("{0}{1}_PL_PKT_1_Y={3:0.000}", prefix, OperationName, Id, PlaneLine.From.Y));
            cix.Add(string.Format("{0}{1}_PL_PKT_2_X={3:0.000}", prefix, OperationName, Id, PlaneLine.To.X));
            cix.Add(string.Format("{0}{1}_PL_PKT_2_Y={3:0.000}", prefix, OperationName, Id, PlaneLine.To.Y));

            cix.Add(string.Format("{0}{1}_SAV_1_PKT_1_X={3:0.000}", prefix, OperationName, Id, LocalSawLine.From.X));
            cix.Add(string.Format("{0}{1}_SAV_1_PKT_1_Y={3:0.000}", prefix, OperationName, Id, LocalSawLine.From.Y));
            cix.Add(string.Format("{0}{1}_SAV_1_PKT_2_X={3:0.000}", prefix, OperationName, Id, LocalSawLine.To.X));
            cix.Add(string.Format("{0}{1}_SAV_1_PKT_2_Y={3:0.000}", prefix, OperationName, Id, LocalSawLine.To.Y));

            cix.Add(string.Format("{0}{1}_SAV_2_PKT_1_X={3:0.000}", prefix, OperationName, Id, SawLine.From.X));
            cix.Add(string.Format("{0}{1}_SAV_2_PKT_1_Y={3:0.000}", prefix, OperationName, Id, SawLine.From.Y));
            cix.Add(string.Format("{0}{1}_SAV_2_PKT_2_X={3:0.000}", prefix, OperationName, Id, SawLine.To.X));
            cix.Add(string.Format("{0}{1}_SAV_2_PKT_2_Y={3:0.000}", prefix, OperationName, Id, SawLine.To.Y));

            cix.Add(string.Format("{0}{1}_DYBDE={3:0.000}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_R={3:0.000}", prefix, OperationName, Id, OutlineRadius));
            cix.Add(string.Format("{0}{1}_T={3:0.000}", prefix, OperationName, Id, T));
            cix.Add(string.Format("{0}{1}_T_O={3:0.000}", prefix, OperationName, Id, TO));
            cix.Add(string.Format("{0}{1}_T_U={3:0.000}", prefix, OperationName, Id, TU));

            cix.Add(string.Format("{0}{1}_OMKRINGFRAES={3}", prefix, OperationName, Id, DoOutline ? 1 : 0));
            cix.Add(string.Format("{0}{1}_HAK_1={3}", prefix, OperationName, Id, DoSideCuts ? 1 : 0));
            cix.Add(string.Format("{0}{1}_HAK_2={3}", prefix, OperationName, Id, DoSideCuts ? 1 : 0));

            if (DoOutline)
            {
                for (int i = 0; i < 9; ++i)
                {
                    cix.Add(string.Format("{0}{1}_PKT_{3}_X={4:0.000}", prefix, OperationName, Id, i + 1, Outline[i].X));
                    cix.Add(string.Format("{0}{1}_PKT_{3}_Y={4:0.000}", prefix, OperationName, Id, i + 1, Outline[i].Y));
                }
            }
            if (DoSideCuts)
            {
                for (int i = 0; i < 2; ++i)
                {
                    for (int j = 0; j < 3; ++j)
                    {
                        cix.Add(string.Format("{0}{1}_HAK_{3}_PKT_{4}_X={5:0.000}", prefix, OperationName, Id, i + 1, j + 1, SideCuts[i][j].X));
                        cix.Add(string.Format("{0}{1}_HAK_{3}_PKT_{4}_Y={5:0.000}", prefix, OperationName, Id, i + 1, j + 1, SideCuts[i][j].Y));
                    }
                }
            }
        }

        public override void Transform(Transform xform)
        {
            throw new NotImplementedException();
        }
        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is Tenon other)
            {
                return true;
            }
            return false;
        }

        public override BoundingBox Extents(Plane plane)
        {
            throw new NotImplementedException();
        }
    }
}
