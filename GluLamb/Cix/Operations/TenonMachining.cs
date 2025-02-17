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
    /// Seems to be a replication of SlotMachining, can probably retire this one.
    /// </summary>
    public class TenonMachining : Operation
    {
        public Line XLine;
        public double Angle;
        public bool OverridePlane = false;
        public Plane Plane;
        public Polyline Outline;
        public double Radius;
        public double Depth;
        public double Depth0;
        public string OperationName = "TAP";


        public TenonMachining(string name = "TenonMachining")
        {
            Name = name;
            OperationName = "TAP";
        }
        public override object Clone()
        {
            return new TenonMachining(Name)
            {
                XLine = XLine,
                Angle = Angle,
                OverridePlane = OverridePlane,
                Plane = Plane,
                Outline = Outline.Duplicate(),
                Radius = Radius,
                Depth = Depth,
                Depth0 = Depth0,
                OperationName = OperationName,
            };
        }
        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));

            if (!Enabled) return;
            // Sort out plane transformation here

            Point3d Origin = XLine.From;
            Point3d XPoint = XLine.To;
            double angle = Angle;

            if (!OverridePlane)
            {
                var xaxis = Plane.XAxis;
                Origin = Plane.Origin;
                XPoint = Origin + xaxis * 100;

                Plane plane;
                GluLamb.Utility.AlignedPlane(Origin, Plane.ZAxis, out plane, out angle);
            }


            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_X={3:0.###}", prefix, OperationName, Id, Origin.X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Origin.Y));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_1_Z={3:0.###}", prefix, OperationName, Id, -Origin.Z));

            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_X={3:0.###}", prefix, OperationName, Id, XPoint.X));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_Y={3:0.###}", prefix, OperationName, Id, XPoint.Y));
            cix.Add(string.Format("{0}{1}_{2}_PL_PKT_2_Z={3:0.###}", prefix, OperationName, Id, -XPoint.Z));
            cix.Add(string.Format("{0}{1}_{2}_PL_ALFA={3:0.###}", prefix, OperationName, Id, RhinoMath.ToDegrees(angle)));

            int N = 9;
            if (Outline.Count != N)
            {
                throw new Exception(string.Format("Incorrect number of points for slot machining. Requires {1} points.", N));
            }

            if (Outline != null)
            {
                Point3d temp;
                for (int i = 0; i < Outline.Count; ++i)
                {
                    Plane.RemapToPlaneSpace(Outline[i], out temp);
                    cix.Add(string.Format("{0}{1}_{2}_PKT_{3}_X={4:0.###}", prefix, OperationName, Id, i + 1, temp.X));
                    cix.Add(string.Format("{0}{1}_{2}_PKT_{3}_Y={4:0.###}", prefix, OperationName, Id, i + 1, temp.Y));
                }
                var BorL = new string[] { "B", "L" };

                cix.Add(string.Format("{0}{1}_{2}_{4}={3:0.###}", prefix, OperationName, Id, Outline[5].DistanceTo(Outline[8]), BorL[0]));
                cix.Add(string.Format("{0}{1}_{2}_{4}={3:0.###}", prefix, OperationName, Id, Outline[3].DistanceTo(Outline[6]), BorL[1]));
            }

            cix.Add(string.Format("{0}{1}_R={2:0.###}", prefix, Id, Radius, OperationName));

            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.###}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_DYBDE_0={3:0.###}", prefix, OperationName, Id, Depth0));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Outline.Transform(xform);
        }
    }
}
