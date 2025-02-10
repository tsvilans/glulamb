using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class DrillGroupTop : Operation
    {
        //public Plane Plane;
        public Point3d Point;
        public double Diameter;
        public double Depth;
        //public List<Drill2d> Drillings;

        public DrillGroupTop(string name = "DrillGroupTop")
        {
            Name = name;
            Point = Point3d.Unset;
        }

        public override object Clone()
        {
            return new DrillGroupTop(Name) { Point = Point, Diameter = Diameter, Depth = Depth };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Point };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}HUL_{1}={2}", prefix, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}HUL_{1}_X_GLOBAL={2:0.###}", prefix, Id, Point.X));
            cix.Add(string.Format("{0}HUL_{1}_Y_GLOBAL={2:0.###}", prefix, Id, Point.Y));

            cix.Add(string.Format("{0}HUL_{1}_DYBDE={2:0.###}", prefix, Id, Depth));
            cix.Add(string.Format("{0}HUL_{1}_DIA={2:0.###}", prefix, Id, Diameter));

        }

        public override void Transform(Transform xform)
        {
            Point.Transform(xform);
        }
    }
}
