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
    /// <summary>
    /// A slot machining operation for cutting slots for plates.
    /// 
    /// There are 2 methods here: 
    /// - If OverridePlane is true, then the slot plane and tool angle 
    /// are calculated from the provided plane.
    /// - If OverridePlane is false, then the XLine and Angle parameters
    /// determine the plane X-axis and tilt of the tool relative to the 
    /// normal of the XLine (i.e., 0 is pointing perpendicular to the XLine
    /// relative to the Z-axis, 90 is down, 180 is pointing the other way).
    /// 
    /// This operation covers 
    /// </summary>
    public class SlotMachining : Operation
    {
        public Line XLine;
        public double Angle;
        public bool OverridePlane = false;
        public Plane Plane;
        public Polyline Outline;
        public double Radius;
        public double Depth;
        public double Depth0;
        public string OperationName = "SLIDS_LODRET";
        public bool LongSlot = false;

        public bool Rough = false;

        public SlotMachining(string name = "SlotMachining", bool rough = false)
        {
            Name = name;
            Rough = rough;
            OperationName = "SLIDS_LODRET";
            LongSlot = false;
        }
        public override object Clone()
        {
            return new SlotMachining(Name, Rough)
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
                LongSlot = LongSlot
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            string postfix = Rough ? "_GROV" : "";
            cix.Add(string.Format("{0}{3}_{1}{2}={4}", prefix, Id, postfix, OperationName, Enabled ? 1 : 0));

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


            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_1_X={3:0.###}", prefix, Id, postfix, Origin.X, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_1_Y={3:0.###}", prefix, Id, postfix, Origin.Y, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_1_Z={3:0.###}", prefix, Id, postfix, -Origin.Z, OperationName));

            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_2_X={3:0.###}", prefix, Id, postfix, XPoint.X, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_2_Y={3:0.###}", prefix, Id, postfix, XPoint.Y, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_PL_PKT_2_Z={3:0.###}", prefix, Id, postfix, -XPoint.Z, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_PL_ALFA={3:0.###}", prefix, Id, postfix, RhinoMath.ToDegrees(angle), OperationName));

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
                    cix.Add(string.Format("{0}{5}_{1}{2}_PKT_{3}_X={4:0.###}", prefix, Id, postfix, i + 1, temp.X, OperationName));
                    cix.Add(string.Format("{0}{5}_{1}{2}_PKT_{3}_Y={4:0.###}", prefix, Id, postfix, i + 1, temp.Y, OperationName));
                }

                var BorL = new string[] { "B", "L" };
                if (LongSlot && false)
                    BorL = new string[] { "L", "B" };

                if (Rough)
                {
                    cix.Add(string.Format("{0}{4}_{1}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[1].DistanceTo(Outline[2]), OperationName, BorL[0]));
                    cix.Add(string.Format("{0}{4}_{1}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[2].DistanceTo(Outline[3]), OperationName, BorL[1]));
                }
                else
                {
                    cix.Add(string.Format("{0}{4}_{1}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[5].DistanceTo(Outline[8]), OperationName, BorL[0]));
                    cix.Add(string.Format("{0}{4}_{1}{2}_{5}={3:0.###}", prefix, Id, postfix, Outline[3].DistanceTo(Outline[6]), OperationName, BorL[1]));
                }
            }

            if (!Rough)
                cix.Add(string.Format("{0}{3}_{1}_R={2:0.###}", prefix, Id, Radius, OperationName));

            cix.Add(string.Format("{0}{4}_{1}{2}_DYBDE={3:0.###}", prefix, Id, postfix, Depth, OperationName));
            cix.Add(string.Format("{0}{4}_{1}{2}_DYBDE_0={3:0.###}", prefix, Id, postfix, Depth0, OperationName));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Outline.Transform(xform);
            XLine.Transform(xform);
        }

        /// <summary>
        /// Reconstruct operation from CIX variables.
        /// </summary>
        /// <param name="cix">Dictionary of CIX variables.</param>
        /// <param name="prefix">The operation prefix (IN, OUT, E_1, etc.).</param>
        /// <param name="id">The number of the operation.</param>
        /// <param name="operationName">The name of the operation in the CIX file. Is usually SLIDS_LODRET but also applies to TAPHUL and SLIDS.</param>
        /// <returns></returns>
        public static SlotMachining FromCix(Dictionary<string, double> cix, string prefix = "", string id = "", string operationName = "SLIDS_LODRET")
        {
            var name = string.IsNullOrEmpty(id) ? $"{prefix}{operationName}" : $"{prefix}{operationName}_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var slot = new SlotMachining(name);

            slot.XLine = new Line(
                cix[$"{name}_PL_PKT_1_X"],
                cix[$"{name}_PL_PKT_1_Y"],
                -cix[$"{name}_PL_PKT_1_Z"],
                cix[$"{name}_PL_PKT_2_X"],
                cix[$"{name}_PL_PKT_2_Y"],
                -cix[$"{name}_PL_PKT_2_Z"]
                );

            slot.Outline = new Polyline();
            for (int i = 1; i <= 9; ++i)
            {
                slot.Outline.Add(
                    new Point3d(
                        cix[$"{name}_PKT_{i}_X"],
                        cix[$"{name}_PKT_{i}_Y"],
                        0
                ));
            }

            slot.Depth = cix[$"{name}_DYBDE"];

            cix.TryGetValue($"{name}_DYBDE_0", out slot.Depth0);

            slot.Angle = RhinoMath.ToRadians(cix[$"{name}_PL_ALFA"]);

            var yaxis = -Vector3d.ZAxis;
            yaxis.Transform(Rhino.Geometry.Transform.Rotation(-slot.Angle, slot.XLine.Direction, slot.XLine.From));

            slot.Plane = new Plane(slot.XLine.From, slot.XLine.Direction, yaxis);

            slot.Outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, slot.Plane));


            return slot;
        }
    }
}
