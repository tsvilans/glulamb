using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net.NetworkInformation;

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

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is EndCut other)
            {
                return 
                    Vector3d.VectorAngle(Plane.Normal, other.Plane.Normal) < epsilon &&
                    CutLine.From.DistanceTo(other.CutLine.From) < epsilon &&
                    CutLine.To.DistanceTo(other.CutLine.To) < epsilon;
            }
            else if (op is CleanCut cc)
            {
                return
                    CutLine.From.DistanceTo(cc.CutLine.From) < epsilon &&
                    CutLine.To.DistanceTo(cc.CutLine.To) < epsilon;
            }
                return false;
        }

        public static EndCut FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}CUT_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var endCut = new EndCut(name);

            endCut.CutLine.FromX = cix[$"{name}_LINE_PKT_1_X"];
            endCut.CutLine.FromY = cix[$"{name}_LINE_PKT_1_Y"];
            endCut.CutLine.FromZ = cix[$"{name}_LINE_PKT_1_Z"];

            endCut.CutLine.ToX = cix[$"{name}_LINE_PKT_2_X"];
            endCut.CutLine.ToY = cix[$"{name}_LINE_PKT_2_Y"];
            endCut.CutLine.ToZ = cix[$"{name}_LINE_PKT_2_Z"];

            // This fails on some older files, so let's make it optional.
            cix.TryGetValue($"{name}_DYBDE_EKSTRA", out endCut.ExtraDepth);

            var alpha = RhinoMath.ToRadians(cix[$"{name}_ALFA"]);

            var yaxis = -Vector3d.ZAxis;
            yaxis.Transform(Rhino.Geometry.Transform.Rotation(-alpha, endCut.CutLine.Direction, endCut.CutLine.From));

            endCut.Plane = new Plane(endCut.CutLine.From, endCut.CutLine.Direction, yaxis);

            return endCut;
        }

        public override BoundingBox Extents(Plane plane)
        {
            var copy = CutLine;
            copy.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane));

            return copy.BoundingBox;
        }
    }
}
