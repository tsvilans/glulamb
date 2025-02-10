using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class SideClearing : Operation
    {
        public Polyline Path;
        public string OperationName = "SIDE_CLEARING";

        public SideClearing(string name = "SideClearing")
        {
            Name = name;
            Path = null;
        }

        public override object Clone() => new SideClearing(Name) { Path = Path.Duplicate(), OperationName = OperationName, Enabled = Enabled };

        public override List<object> GetObjects()
        {
            return new List<object> { Path };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            if (Path == null) return;

            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_X={3:0.###}", prefix, OperationName, Id, Path[0].X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Path[0].Y));

            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_X={3:0.###}", prefix, OperationName, Id, Path[1].X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_Y={3:0.###}", prefix, OperationName, Id, Path[1].Y));

            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_3_X={3:0.###}", prefix, OperationName, Id, Path[2].X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_3_Y={3:0.###}", prefix, OperationName, Id, Path[2].Y));
        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }
    }
}
