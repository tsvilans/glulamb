using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class Fals : Operation
    {
        public Line Path;
        public double Depth;
        public double Width;
        public string OperationName = "FALS";

        public Fals(string name = "Fals")
        {
            Name = name;
        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Path };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_X={3:0.000}", prefix, OperationName, Id, Path.From.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Y={3:0.000}", prefix, OperationName, Id, Path.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_X={3:0.000}", prefix, OperationName, Id, Path.To.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Y={3:0.000}", prefix, OperationName, Id, Path.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.000}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_B={3:0.000}", prefix, OperationName, Id, Width));
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
