using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class TenonOutline : Operation
    {
        public Line PlaneLine;
        public double Depth;
        public double OutlineRadius;
        public Polyline Outline;

        public string OperationName = "TAP_OUTLINE";


        public TenonOutline(string name = "TenonOutline")
        {
            Name = name;
            Outline = new Polyline();
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        public override List<object> GetObjects()
        {
            return new List<object> { PlaneLine, Outline };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            //cix.Add(string.Format("{0}{1}_PL_PKT_1_X={3:0.000}", prefix, OperationName, Id, PlaneLine.From.X));
            //cix.Add(string.Format("{0}{1}_PL_PKT_1_Y={3:0.000}", prefix, OperationName, Id, PlaneLine.From.Y));
            //cix.Add(string.Format("{0}{1}_PL_PKT_2_X={3:0.000}", prefix, OperationName, Id, PlaneLine.To.X));
            //cix.Add(string.Format("{0}{1}_PL_PKT_2_Y={3:0.000}", prefix, OperationName, Id, PlaneLine.To.Y));


            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.000}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_R={3:0.000}", prefix, OperationName, Id, OutlineRadius));

            for (int i = 0; i < 9; ++i)
            {
                cix.Add(string.Format("{0}{1}_{2}_PKT_{3}_X={4:0.000}", prefix, OperationName, Id, i + 1, Outline[i].X));
                cix.Add(string.Format("{0}{1}_{2}_PKT_{3}_Y={4:0.000}", prefix, OperationName, Id, i + 1, Outline[i].Y));
            }
        }

        public override void Transform(Transform xform)
        {
            throw new NotImplementedException();
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is TenonOutline other)
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
