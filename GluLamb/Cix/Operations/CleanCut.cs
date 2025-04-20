using GluLamb.Projects.HHDAC22;
using Rhino;
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
            // Turn on joint
            cix.Add(string.Format("{0}RENSKAER={1}", prefix, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}RENSKAER_PKT_1_X={1:0.###}", prefix, CutLine.From.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_1_Y={1:0.###}", prefix, CutLine.From.Y));

            cix.Add(string.Format("{0}RENSKAER_PKT_2_X={1:0.###}", prefix, CutLine.To.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_2_Y={1:0.###}", prefix, CutLine.To.Y));
        }

        public override void Transform(Transform xform)
        {
            CutLine.Transform(xform);
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is CleanCut other)
            {
                return CutLine.From.DistanceTo(other.CutLine.From) < epsilon &&
                    CutLine.To.DistanceTo(other.CutLine.To) < epsilon;
            }
            return false;
        }


        public static CleanCut FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}RENSKAER";

            if (!cix.ContainsKey($"{name}_PKT_1_X"))
                return null;

            var cleanCut = new CleanCut(name);

            cleanCut.CutLine = new Line(
                cix[$"{name}_PKT_1_X"],
                cix[$"{name}_PKT_1_Y"],
                0,
                cix[$"{name}_PKT_2_X"],
                cix[$"{name}_PKT_2_Y"],
                0
                );

            return cleanCut;
        }

        public override BoundingBox Extents(Plane plane)
        {
            var copy = CutLine;
            copy.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane));

            return copy.BoundingBox;
        }
    }
}
