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
    /// <summary>
    /// Group of holes. If they are on the sides (IN or OUT),
    /// there is no alpha value for the plane - it is
    /// vertical.
    /// </summary>
    public class DrillGroup2 : Operation
    {
        public Plane Plane;
        public List<Drill2d> Drillings;
        public double Diameter;
        public double Depth;

        public DrillGroup2(string name = "DrillGroup2")
        {
            Name = name;
            Drillings = new List<Drill2d>();
            Plane = Plane.Unset;
            Diameter = 0;
            Depth = 0;
        }

        public override object Clone()
        {
            return new DrillGroup2(Name)
            {
                Plane = Plane,
                Drillings = Drillings.Select(x => x.Clone() as Drill2d).ToList(),
                Diameter = Diameter,
                Depth = Depth
            };
        }

        public override List<object> GetObjects()
        {
            var things = new List<object> { Plane };
            for (int i = 0; i < Drillings.Count; ++i)
            {
                things.AddRange(Drillings[i].GetObjects());
            }
            return things;
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}HUL_{1}={2}", prefix, Id, Enabled ? 1 : 0));

            if (!Enabled) return;

            var plane = Cix.ToCixPlane(Plane, out double angle, Plane.WorldXY);
            var xpoint = plane.Origin + plane.XAxis * 100;

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, plane.Origin.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, plane.Origin.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, -plane.Origin.Z));

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, xpoint.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, xpoint.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, -xpoint.Z));
            cix.Add(string.Format("{0}HUL_{1}_PL_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));

            var OverallDiameter = false;
            if (Diameter > 0)
            {
                OverallDiameter = true;
                cix.Add(string.Format("{0}HUL_{1}_DIA={2:0.###}", prefix, Id, Diameter));
            }

            var OverallDepth = false;
            if (Depth > 0)
            {
                OverallDepth = true;
                cix.Add(string.Format("{0}HUL_{1}_DYBDE={2:0.###}", prefix, Id, Depth));
            }

            cix.Add(string.Format("{0}HUL_{1}_N={2}", prefix, Id, Drillings.Count));

            for (int i = 0; i < Drillings.Count; ++i)
            {
                var d = Drillings[i];
                Point3d pp;
                plane.RemapToPlaneSpace(d.Position, out pp);
                cix.Add(string.Format("\t(Drill_{0}_{1})", Id, i + 1));
                cix.Add(string.Format("{0}HUL_{1}_{2}_X={3:0.###}", prefix, Id, i + 1, pp.X));
                cix.Add(string.Format("{0}HUL_{1}_{2}_Y={3:0.###}", prefix, Id, i + 1, pp.Y));
                if (OverallDiameter)
                    cix.Add(string.Format("{0}HUL_{1}_{2}_DIA={3:0.###}", prefix, Id, i + 1, d.Diameter));
                if (OverallDepth)
                    cix.Add(string.Format("{0}HUL_{1}_{2}_DYBDE={3:0.###}", prefix, Id, i + 1, d.Depth));
            }

        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            for (int i = 0; i < Drillings.Count; ++i)
                Drillings[i].Transform(xform);
        }

        public static DrillGroup2 FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}HUL_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var drillGroup = new DrillGroup2(name);

            var p0 = new Point3d(
                cix[$"{name}_PL_PKT_1_X"],
                cix[$"{name}_PL_PKT_1_Y"],
                cix[$"{name}_PL_PKT_1_Z"]
                //0
                );

            var p1 = new Point3d(
                cix[$"{name}_PL_PKT_2_X"],
                cix[$"{name}_PL_PKT_2_Y"],
                cix[$"{name}_PL_PKT_2_Z"]
                //0
                );

            var xaxis = p1 - p0;

            cix.TryGetValue($"{name}_PL_ALFA", out double angle);
            angle = RhinoMath.ToRadians(angle);
            var yaxis = -Vector3d.ZAxis;
            yaxis.Transform(Rhino.Geometry.Transform.Rotation(-angle, xaxis, p0));

            drillGroup.Plane = new Plane(p0, xaxis, yaxis);

            double diameter = 0, depth = 0;

            if (cix.ContainsKey($"{name}_DIA"))
                diameter = cix[$"{name}_DIA"];

            if (cix.ContainsKey($"{name}_DYBDE"))
                depth = cix[$"{name}_DYBDE"];

            drillGroup.Diameter = diameter;
            drillGroup.Depth = depth;

            var numDrillings = (int)(cix[$"{name}_N"]);

            for (int i = 1; i <= numDrillings; ++i)
            {
                var position = new Point3d(
                    cix[$"{name}_{i}_X"],
                    cix[$"{name}_{i}_Y"],
                    0
                );

                if (cix.ContainsKey($"{name}_{i}_DIA"))
                    diameter = cix[$"{name}_{i}_DIA"];

                if (cix.ContainsKey($"{name}_{i}_DYBDE"))
                    depth = cix[$"{name}_{i}_DYBDE"];

                var drill2d = new Drill2d(drillGroup.Plane.PointAt(position.X, position.Y, position.Z), diameter, depth);
                drillGroup.Drillings.Add(drill2d);
            }

            return drillGroup;

        }
    }
}
