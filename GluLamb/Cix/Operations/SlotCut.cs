using GH_IO.Serialization;
using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// Operation for machining a slot from the side, with
    /// a slot-cutting tool (thick saw blade).
    /// </summary>
    public class SlotCut : Operation
    {
        public Line Path;
        public double Depth;
        public string OperationName = "SLOT_CUT";

        public SlotCut(string name = "SlotCut")
        {
            Name = name;
            Path = Line.Unset;
        }

        public override object Clone()
        {
            return new SlotCut(Name)
            {
                Path = Path,
                Depth = Depth,
                OperationName = OperationName
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Path };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_PKT_1_X={3:0.###}", prefix, OperationName, Id, Path.From.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Path.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Z={3:0.###}", prefix, OperationName, Id, -Path.From.Z));

            cix.Add(string.Format("{0}{1}_{2}_PKT_2_X={3:0.###}", prefix, OperationName, Id, Path.To.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Y={3:0.###}", prefix, OperationName, Id, Path.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Z={3:0.###}", prefix, OperationName, Id, -Path.To.Z));

            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.###}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_ALPHA={3:0.###}", prefix, OperationName, Id, 0));
        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }
    }
}
