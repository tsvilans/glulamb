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
        public bool SlotBack = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double SlotBackWidth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double SlotBackDepth = 0;
        public double SlotBackZDistance = 0;

        public bool SlotFront = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double SlotFrontWidth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double SlotFrontDepth = 0;

        public double SlotFrontZDistance = 0;


        public SideSlot(string name = "SideSlot/Not")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new SideSlot(Name)
            {
                SlotBack = SlotBack,
                SlotBackWidth = SlotBackWidth,
                SlotBackDepth = SlotBackDepth,
                SlotBackZDistance = SlotBackZDistance,
                SlotFront = SlotFront,
                SlotFrontWidth = SlotFrontWidth,
                SlotFrontDepth = SlotFrontDepth,
                SlotFrontZDistance = SlotFrontZDistance,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {

            if (SlotBack)
            {
                cix.Add($"NOT_BAG={(Enabled && SlotBack ? 1 : 0)}");
                cix.Add($"NOT_BAG_DYBDE={SlotBackDepth:0.###}");
                cix.Add($"NOT_BAG_DIM={SlotBackWidth:0.###}");
                cix.Add($"NOT_BAG_NEDERST_PLC_REF_BUND={SlotBackZDistance:0.###}");
            }

            if (SlotFront)
            {
                cix.Add($"NOT_FOR={(Enabled && SlotFront ? 1 : 0)}");
                cix.Add($"NOT_FOR_DYBDE={SlotFrontDepth:0.###}");
                cix.Add($"NOT_FOR_DIM={SlotFrontWidth:0.###}");
                cix.Add($"NOT_FOR_NEDERST_PLC_REF_BUND={SlotFrontZDistance:0.###}");
            }
        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SideSlot other)
            {
                return
                    (SlotBack == other.SlotBack) &&
                    Math.Abs(SlotBackDepth - other.SlotBackDepth) < epsilon &&
                    Math.Abs(SlotBackWidth - other.SlotBackWidth) < epsilon &&
                    Math.Abs(SlotBackZDistance - other.SlotBackZDistance) < epsilon &&
                    (SlotFront == other.SlotFront) &&
                    Math.Abs(SlotFrontDepth - other.SlotFrontDepth) < epsilon &&
                    Math.Abs(SlotFrontWidth - other.SlotFrontWidth) < epsilon &&
                    Math.Abs(SlotFrontZDistance - other.SlotFrontZDistance) < epsilon;
            }
            return false;
        }

        public static SideSlot FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}NOT_BAG";

            var rebate = new SideSlot(name);

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.SlotBack = true;
                rebate.SlotBackWidth = cix[$"{name}_DIM"];
                rebate.SlotBackDepth = cix[$"{name}_DYBDE"];
                rebate.SlotBackZDistance = cix[$"{name}_NEDERST_PLC_REF_BUND"];

                //return new Rebate(name)
                //{
                //    Top = true,
                //    TopDepth = cix[$"{name}_DYBDE"],
                //    TopThickness = cix[$"{name}_T"],
                //};
            }

            name = $"{prefix}NOT_FOR";

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.SlotFront = true;
                rebate.SlotFrontWidth = cix[$"{name}_DIM"];
                rebate.SlotFrontDepth = cix[$"{name}_DYBDE"];
                rebate.SlotFrontZDistance = cix[$"{name}_NEDERST_PLC_REF_BUND"];
                //return new Rebate(name)
                //{
                //    Bottom = true,
                //    BottomDepth = cix[$"{name}_DYBDE"],
                //    BottomThickness = cix[$"{name}_T"],
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
                SlotBack = SlotBack,
                SlotBackWidth = SlotBackWidth,
                SlotBackDepth = SlotBackDepth,
                SlotBackZDistance = SlotBackZDistance,
                SlotFront = SlotFront,
                SlotFrontWidth = SlotFrontWidth,
                SlotFrontDepth = SlotFrontDepth,
                SlotFrontZDistance = SlotFrontZDistance
            };

            if (!SlotBack && other.SlotBack)
            {
                sideSlot.SlotBack = other.SlotBack;
                sideSlot.SlotBackWidth = other.SlotBackWidth;
                sideSlot.SlotBackDepth = other.SlotBackDepth;
                sideSlot.SlotBackZDistance = other.SlotBackZDistance;
            }
            if (!SlotFront && other.SlotFront)
            {
                sideSlot.SlotFront = other.SlotFront;
                sideSlot.SlotFrontWidth = other.SlotFrontWidth;
                sideSlot.SlotFrontDepth = other.SlotFrontDepth;
                sideSlot.SlotFrontZDistance = other.SlotFrontZDistance;
            }

            return sideSlot;

        }
    }
}
