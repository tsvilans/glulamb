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
    public class BigHole : Operation
    {
        public Point3d Centre;
        public double Diameter;
        public BigHole(string name = "Big Hole")
        {
            Name = name;
            Centre = Point3d.Unset;
            Diameter = 0;
        }
        public override object Clone()
        {
            return new BigHole(Name)
            {
                Centre = Centre,
                Diameter = Diameter
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Centre, new Circle(new Plane(Centre, Vector3d.ZAxis), Diameter * 0.5 ) };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}HUL_KANT={1}", prefix, Enabled ? 1 : 0));

            if (!Enabled) return;

            cix.Add(string.Format("{0}HUL_KANT_1_X={1:0.###}", prefix, Centre.X));
            cix.Add(string.Format("{0}HUL_KANT_1_Y={1:0.###}", prefix, Centre.Y));
            cix.Add(string.Format("{0}HUL_KANT_1_DIA={1:0.###}", prefix, Diameter));

        }

        public override void Transform(Transform xform)
        {
            Centre.Transform(xform);
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is BigHole other)
            {
                return 
                    Centre.DistanceTo(other.Centre) < epsilon &&
                    Math.Abs(Diameter - other.Diameter) < epsilon;
            }
            return false;
        }

        public static BigHole FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}HUL_KANT_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var bigHole = new BigHole(name);
            bigHole.Centre = new Point3d(
                cix[$"{name}_HUL_KANT_1_X"],
                cix[$"{name}_HUL_KANT_1_Y"],
                0);

            bigHole.Diameter = cix[$"{name}_HUL_KANT_1_DIA"];

            return bigHole;
        }

        public override BoundingBox Extents(Plane plane)
        {
            var copy = Centre;
            copy.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane));

            return new BoundingBox(new Point3d[] { copy });
        }
    }
}
