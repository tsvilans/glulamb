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
    /// A flank cut from the side, to shave off the end of a beam.
    /// Used in the 4-way joint, to cut the split ends. Two SKRAA
    /// cuts were used from either side.
    /// 
    /// This could also be represented by a 3d rectangle, where the 
    /// tool starts at one edge and follows the plane of the rectangle
    /// to the opposite edge (length). The perpendicular dimension (width) would 
    /// be the depth of the cut.
    /// </summary>
    public class LineMachining : Operation
    {
        /// <summary>
        /// The path of the tool down the slope while cutting 
        /// the flank.
        /// </summary>
        public Line Path;
        /// <summary>
        /// The line at the top of the cut, defining the full "flank"
        /// of the flank cut. This is flat/planar to the top of the material.
        /// </summary>
        public Line PlaneX;
        /// <summary>
        /// The inclination of the tool against the top of the material (angle 
        /// between tool and PlaneX line).
        /// </summary>
        public double Tilt;
        /// <summary>
        /// Depth of the cut, how deep the tool will go.
        /// </summary>
        public double Depth;
        public Plane CheckPlane;

        public string OperationName = "SKRAA";

        public LineMachining(string name = "LineMachining")
        {
            Name = name;
        }

        public override object Clone()
        {
            return new LineMachining(Name)
            {
                Path = Path,
                PlaneX = PlaneX,
                Tilt = Tilt,
                Depth = Depth,
                CheckPlane = CheckPlane,
                OperationName = OperationName
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Path, CheckPlane };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));

            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_P_1_X={3:0.###}", prefix, OperationName, Id, PlaneX.From.X));
            cix.Add(string.Format("{0}{1}_{2}_P_1_Y={3:0.###}", prefix, OperationName, Id, PlaneX.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_P_1_Z={3:0.###}", prefix, OperationName, Id, -PlaneX.From.Z));

            cix.Add(string.Format("{0}{1}_{2}_P_2_X={3:0.###}", prefix, OperationName, Id, PlaneX.To.X));
            cix.Add(string.Format("{0}{1}_{2}_P_2_Y={3:0.###}", prefix, OperationName, Id, PlaneX.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_P_2_Z={3:0.###}", prefix, OperationName, Id, -PlaneX.To.Z));

            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_1_X={3:0.###}", prefix, OperationName, Id, Path.From.X));
            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Path.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_1_Z={3:0.###}", prefix, OperationName, Id, -Path.From.Z));

            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_2_X={3:0.###}", prefix, OperationName, Id, Path.To.X));
            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_2_Y={3:0.###}", prefix, OperationName, Id, Path.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_LINE_1_PKT_2_Z={3:0.###}", prefix, OperationName, Id, -Path.To.Z));


            cix.Add(string.Format("{0}{1}_{2}_ALFA={3:0.###}", prefix, OperationName, Id, RhinoMath.ToDegrees(Tilt)));
            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.###}", prefix, OperationName, Id, Depth));

        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }
    }
}
