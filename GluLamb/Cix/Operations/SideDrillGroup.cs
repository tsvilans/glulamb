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
    /// A simplified version of DrillGroup which is for machining the dowel holes
    /// for the plate connectors. These should be aligned on the beam's YZ plane, so 
    /// should be perpendicular to the beam sides. This means that the plane defining
    /// the drillings is perpendicular to the blank sides, so we don't need the Z-value
    /// or the alpha angle.
    /// 
    /// There should only be one drilling in the Drillings list.
    /// </summary>
    public class SideDrillGroup : Operation
    {
        public Plane Plane;
        public List<Drill2d> Drillings;

        public SideDrillGroup(string name = "SideDrillGroup")
        {
            Name = name;
            Drillings = new List<Drill2d>();
            Plane = Plane.Unset;
        }

        public override object Clone()
        {
            return new SideDrillGroup(Name)
            {
                Plane = Plane,
                Drillings = Drillings.Select(x => x.Clone() as Drill2d).ToList()
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
            // Sort out plane transformation here

            var xaxis = Plane.XAxis;
            var origin = Plane.Origin;
            var xpoint = origin + xaxis * 100;

            Plane plane;
            double angle;
            GluLamb.Utility.AlignedPlane(origin, Plane.ZAxis, out plane, out angle);


            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, origin.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, origin.Y));
            //cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, origin.Z));

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, xpoint.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, xpoint.Y));
            //cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, xpoint.Z));
            //cix.Add(string.Format("{0}HUL_{1}_PL_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));

            cix.Add(string.Format("{0}HUL_{1}_N={2}", prefix, Id, Drillings.Count));

            for (int i = 0; i < Drillings.Count; ++i)
            {
                var d = Drillings[i];
                Point3d pp;
                plane.RemapToPlaneSpace(d.Position, out pp);
                cix.Add(string.Format("\t(PlateDowel_{0}_{1})", Id, i + 1));
                cix.Add(string.Format("{0}HUL_{1}_{2}_X={3:0.###}", prefix, Id, i + 1, pp.X));
                cix.Add(string.Format("{0}HUL_{1}_{2}_Y={3:0.###}", prefix, Id, i + 1, pp.Y));
                cix.Add(string.Format("{0}HUL_{1}_{2}_DIA={3:0.###}", prefix, Id, i + 1, d.Diameter));
                cix.Add(string.Format("{0}HUL_{1}_{2}_DYBDE={3:0.###}", prefix, Id, i + 1, d.Depth));
            }

        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            for (int i = 0; i < Drillings.Count; ++i)
                Drillings[i].Transform(xform);
        }
    }
}
