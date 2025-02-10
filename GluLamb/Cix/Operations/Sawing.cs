using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class Sawing : Operation
    {
        public Line Path;
        public Vector3d Normal;
        public string OperationName = "SAWING";

        public Sawing(string name = "Sawing")
        {
            Name = name;
            Path = Line.Unset;
            Normal = Vector3d.ZAxis;
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        public override List<object> GetObjects()
        {
            return new List<object> { new Plane(Path.From, Normal), Path };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            double angle = Vector3d.VectorAngle(Vector3d.CrossProduct(Path.Direction, Normal), Vector3d.ZAxis);

            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_X={3:0.###}", prefix, OperationName, Id, Path.From.X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Path.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_Z={3:0.###}", prefix, OperationName, Id, -Path.From.Z));

            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_X={3:0.###}", prefix, OperationName, Id, Path.To.X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_Y={3:0.###}", prefix, OperationName, Id, Path.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_Z={3:0.###}", prefix, OperationName, Id, -Path.To.Z));

            cix.Add(string.Format("{0}{1}_{2}_PL_ALFA={3:0.###}", prefix, OperationName, Id, RhinoMath.ToDegrees(angle)));
        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }
    }
}
