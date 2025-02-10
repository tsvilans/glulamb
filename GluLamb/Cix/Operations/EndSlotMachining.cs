using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class EndSlotMachining : Operation
    {
        public Line XLine;
        public double Angle;
        public bool OverridePlane = false;
        public Plane Plane;
        public Polyline Outline;
        public double Radius;
        public double Depth;
        public double Depth0;
        public string OperationName = "TAPHUL";
        public bool LongSlot = false;

        public bool Rough = false;

        public EndSlotMachining(string name = "EndTapHole", bool rough = false)
        {
            Name = name;
            Rough = rough;
            OperationName = "TAPHUL";
            LongSlot = false;
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            string postfix = Rough ? "_GROV" : "";
            cix.Add(string.Format("{0}{3}{2}={4}", prefix, Id, postfix, OperationName, Enabled ? 1 : 0));
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


            cix.Add(string.Format("{0}{4}{2}_PL_PKT_1_X={3:0.###}", prefix, Id, postfix, Origin.X, OperationName));
            cix.Add(string.Format("{0}{4}{2}_PL_PKT_1_Y={3:0.###}", prefix, Id, postfix, Origin.Y, OperationName));
            cix.Add(string.Format("{0}{4}{2}_PL_PKT_1_Z={3:0.###}", prefix, Id, postfix, -Origin.Z, OperationName));

            cix.Add(string.Format("{0}{4}{2}_PL_PKT_2_X={3:0.###}", prefix, Id, postfix, XPoint.X, OperationName));
            cix.Add(string.Format("{0}{4}{2}_PL_PKT_2_Y={3:0.###}", prefix, Id, postfix, XPoint.Y, OperationName));
            cix.Add(string.Format("{0}{4}{2}_PL_PKT_2_Z={3:0.###}", prefix, Id, postfix, -XPoint.Z, OperationName));
            cix.Add(string.Format("{0}{4}{2}_PL_ALFA={3:0.###}", prefix, Id, postfix, RhinoMath.ToDegrees(angle), OperationName));

            int N = Rough ? 5 : 9;

            if (Outline.Count != N)
            {
                throw new Exception(string.Format("Incorrect number of points for slot machining. Rough={0}, requires {1} points.", Rough, N));
            }

            if (Outline != null)
            {
                Point3d temp;
                for (int i = 0; i < Outline.Count; ++i)
                {
                    Plane.RemapToPlaneSpace(Outline[i], out temp);
                    cix.Add(string.Format("{0}{5}{2}_PKT_{3}_X={4:0.###}", prefix, Id, postfix, i + 1, temp.X, OperationName));
                    cix.Add(string.Format("{0}{5}{2}_PKT_{3}_Y={4:0.###}", prefix, Id, postfix, i + 1, temp.Y, OperationName));
                }

                var BorL = new string[] { "B", "T" };
                if (LongSlot && false)
                    BorL = new string[] { "T", "B" };

                if (Rough)
                {
                    cix.Add(string.Format("{0}{4}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[1].DistanceTo(Outline[2]), OperationName, BorL[0]));
                    cix.Add(string.Format("{0}{4}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[2].DistanceTo(Outline[3]), OperationName, BorL[1]));
                }
                else
                {
                    cix.Add(string.Format("{0}{4}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[2].DistanceTo(Outline[7]), OperationName, BorL[0]));
                    cix.Add(string.Format("{0}{4}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[1].DistanceTo(Outline[4]), OperationName, BorL[1]));
                }
            }

            if (!Rough)
                cix.Add(string.Format("{0}{3}_R={2:0.###}", prefix, Id, Radius, OperationName));

            cix.Add(string.Format("{0}{4}{2}_DYBDE={3:0.###}", prefix, Id, postfix, Depth, OperationName));
            //cix.Add(string.Format("{0}{4}{2}_DYBDE_0={3:0.###}", prefix, Id, postfix, Depth0, OperationName));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Outline.Transform(xform);
        }
    }
}
