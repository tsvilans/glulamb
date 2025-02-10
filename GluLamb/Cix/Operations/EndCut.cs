using GH_IO.Serialization;
using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GluLamb.Cix.Operations
{
    public class EndCut : Operation
    {
        public Plane Plane;
        public Line CutLine;
        public double ExtraDepth;
        public EndCut(string name = "EndCut")
        {
            Name = name;
            Plane = Plane.Unset;
            CutLine = Line.Unset;
            ExtraDepth = 10;
        }
        public override object Clone()
        {
            return new EndCut(Name)
            {
                Plane = Plane,
                CutLine = CutLine,
                ExtraDepth = ExtraDepth
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, CutLine };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}CUT_{1}={2}", prefix, Id, Enabled ? 1 : 0));

            if (!Enabled) return;

            // Sort out plane transformation here
            // var normal = Plane.ZAxis;
            //var xaxis = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            //var yaxis = Vector3d.CrossProduct(normal, xaxis);
            //var origin = Plane.Origin;
            //var xpoint = origin + xaxis * 100;

            var sign = Vector3d.ZAxis * Plane.ZAxis > 0 ? 1 : -1;
            double angle = Vector3d.VectorAngle(-Vector3d.ZAxis, Plane.YAxis) * sign;

            Plane plane;
            Utility.AlignedPlane(Plane.Origin, Plane.ZAxis, out plane, out angle);

            //var plane = new Plane(origin, xaxis, yaxis);

            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_X={2:0.###}", prefix, Id, CutLine.From.X));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_Y={2:0.###}", prefix, Id, CutLine.From.Y));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_Z={2:0.###}", prefix, Id, CutLine.From.Z));

            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_X={2:0.###}", prefix, Id, CutLine.To.X));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_Y={2:0.###}", prefix, Id, CutLine.To.Y));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_Z={2:0.###}", prefix, Id, CutLine.To.Z));

            cix.Add(string.Format("{0}CUT_{1}_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));
            cix.Add(string.Format("{0}CUT_{1}_DYBDE_EKSTRA={2:0.###}", prefix, Id, ExtraDepth));

        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            CutLine.Transform(xform);
        }
    }
}
