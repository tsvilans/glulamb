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
    public class CleanCut : Operation
    {
        public Line CutLine;

        public CleanCut(string name = "CleanCut")
        {
            Name = name;
            CutLine = Line.Unset;
        }

        public override object Clone()
        {
            return new CleanCut(Name)
            {
                CutLine = CutLine,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { CutLine };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}RENSKAER_PKT_1_X={1:0.###}", prefix, CutLine.From.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_1_Y={1:0.###}", prefix, CutLine.From.Y));

            cix.Add(string.Format("{0}RENSKAER_PKT_2_X={1:0.###}", prefix, CutLine.To.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_2_Y={1:0.###}", prefix, CutLine.To.Y));
        }

        public override void Transform(Transform xform)
        {
            CutLine.Transform(xform);
        }
    }
}
