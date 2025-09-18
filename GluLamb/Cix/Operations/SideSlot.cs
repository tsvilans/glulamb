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
    public class SideSlot : Operation
    {
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double Width = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double Depth = 0;
        public double ZDistance = 0;


        public SideSlot(string name = "SideSlot/Not")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new SideSlot(Name)
            {
                Width = Width,
                Depth = Depth,
                ZDistance = ZDistance,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            var pre = prefix.Trim() == "IN_" ? "FOR" : "BAG";

            if (prefix.Trim() != "IN_" && prefix.Trim() != "OUT_")
            {
                throw new ArgumentException(@"SideSlot can only be used on IN or OUT sides!");
                return;
            }


            if (Enabled)
            {
                cix.Add($"    NOT_{pre}={(Enabled ? 1 : 0)}");
                cix.Add($"    NOT_{pre}_DYBDE={Depth:0.###}");
                cix.Add($"    NOT_{pre}_DIM={Width:0.###}");
                cix.Add($"    NOT_{pre}_NEDERST_PLC_REF_BUND={ZDistance:0.###}");
            }

            //if (SlotFront)
            //{
            //    cix.Add($"NOT_FOR={(Enabled && SlotFront ? 1 : 0)}");
            //    cix.Add($"NOT_FOR_DYBDE={SlotFrontDepth:0.###}");
            //    cix.Add($"NOT_FOR_DIM={SlotFrontWidth:0.###}");
            //    cix.Add($"NOT_FOR_NEDERST_PLC_REF_BUND={SlotFrontZDistance:0.###}");
            //}
        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SideSlot other)
            {
                return
                    Math.Abs(Depth - other.Depth) < epsilon &&
                    Math.Abs(Width - other.Width) < epsilon &&
                    Math.Abs(ZDistance - other.ZDistance) < epsilon;
            }
            return false;
        }

        public static SideSlot FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var pre = prefix.Trim() == "IN_" ? "FOR" : "BAG";
            var name = $"{prefix}NOT_{pre}";

            var rebate = new SideSlot(name);

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.Enabled = true;
                rebate.Width = cix[$"{name}_DIM"];
                rebate.Depth = cix[$"{name}_DYBDE"];
                rebate.ZDistance = cix[$"{name}_NEDERST_PLC_REF_BUND"];

                //return new Rebate(name)
                //{
                //    Top = true,
                //    TopDepth = cix[$"{name}_DYBDE"],
                //    TopThickness = cix[$"{name}_T"],
                //};
            }

            return rebate;
        }

        public override BoundingBox Extents(Plane plane)
        {
            //var copy = CutLine;
            //copy.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane));

            //return copy.BoundingBox;
            return BoundingBox.Empty;
        }

        public SideSlot Combine(SideSlot other)
        {
            var sideSlot = new SideSlot(Name)
            {
                Width = Width,
                Depth = Depth,
                ZDistance = ZDistance,
            };

            return sideSlot;

        }
    }
}
