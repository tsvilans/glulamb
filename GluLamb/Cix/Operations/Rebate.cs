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
using Rhino.Input.Custom;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// Rebate can only be applied to End1 or End2.
    /// There is no geometry.
    /// </summary>
    public class Rebate : Operation
    {
        public bool Top = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double TopDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double TopHeight = 0;
        public bool Bottom = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double BottomDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double BottomHeight = 0;



        public Rebate(string name = "Rebate")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new Rebate(Name)
            {
                Top = Top,
                TopDepth = TopDepth,
                TopHeight = TopHeight,
                Bottom = Bottom,
                BottomDepth = BottomDepth,
                BottomHeight = BottomHeight
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}FALS_TOP={1}", prefix, Enabled && Top ? 1 : 0));

            if (Top)
            {
                cix.Add(string.Format("{0}FALS_TOP_T={1:0.###}", prefix, TopHeight));
                cix.Add(string.Format("{0}FALS_TOP_DYBDE={1:0.###}", prefix, TopDepth));
            }
            cix.Add(string.Format("{0}FALS_BUND={1}", prefix, Enabled && Bottom ? 1 : 0));

            if (Bottom)
            {
                cix.Add(string.Format("{0}FALS_BUND_T={1:0.###}", prefix, BottomHeight));
                cix.Add(string.Format("{0}FALS_BUND_DYBDE={1:0.###}", prefix, BottomDepth));
            }
        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is Rebate other)
            {
                return
                    (Top == other.Top) &&
                    Math.Abs(TopHeight - other.TopHeight) < epsilon &&
                    Math.Abs(TopDepth - other.TopDepth) < epsilon &&
                    (Bottom == other.Bottom) &&
                    Math.Abs(BottomHeight - other.BottomHeight) < epsilon &&
                    Math.Abs(BottomDepth - other.BottomDepth) < epsilon;
            }
            return false;
        }

        public static Rebate FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}FALS_TOP";

            if (cix.ContainsKey(name) && cix[name] > 0)
                return new Rebate(name)
                {
                    Top = true,
                    TopDepth = cix[$"{name}_DYBDE"],
                    TopHeight = cix[$"{name}_T"],
                };

            name = $"{prefix}FALS_BUND";

            if (cix.ContainsKey(name) && cix[name] > 0)
                return new Rebate(name)
                {
                    Bottom = true,
                    BottomDepth = cix[$"{name}_DYBDE"],
                    BottomHeight = cix[$"{name}_T"],
                };
            return null;
        }

        public override BoundingBox Extents(Plane plane)
        {
            //var copy = CutLine;
            //copy.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane));

            //return copy.BoundingBox;
            return BoundingBox.Empty;
        }
    }
}
