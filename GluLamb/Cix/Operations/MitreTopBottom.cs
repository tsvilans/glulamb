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
    public class MitreTopBottom : Operation
    {
        public bool Top = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double TopDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double TopThickness = 0;
        public double TopAngle = Math.PI * 0.5;

        public bool Bottom = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double BottomDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double BottomThickness = 0;

        public double BottomAngle = Math.PI * 0.5;
        public double BottomRadius = 0;

        public BeamSideType SideType = BeamSideType.End1;



        public MitreTopBottom(string name = "Rebate/Gering")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new MitreTopBottom(Name)
            {
                Top = Top,
                TopDepth = TopDepth,
                TopThickness = TopThickness,
                TopAngle = TopAngle,
                Bottom = Bottom,
                BottomDepth = BottomDepth,
                BottomThickness = BottomThickness,
                BottomAngle = BottomAngle,
                BottomRadius = BottomRadius,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            //cix.Add(string.Format("{0}GERING_TOP={1}", prefix, Enabled && Top ? 1 : 0));

            //if (Top)
            //{
            //    cix.Add(string.Format("{0}GERING_TOP_B={1:0.###}", prefix, TopThickness));
            //    cix.Add(string.Format("{0}GERING_TOP_DYBDE={1:0.###}", prefix, TopDepth));
            //    cix.Add(string.Format("{0}GERING_TOP_ALFA={1:0.###}", prefix, RhinoMath.ToDegrees(TopAngle)));
            //}

            cix.Add(string.Format("{0}GERING_BUND={1}", prefix, Enabled && Bottom ? 1 : 0));

            if (Bottom)
            {
                cix.Add(string.Format("{0}GERING_BUND_T={1:0.###}", prefix, BottomThickness));
                cix.Add(string.Format("{0}GERING_BUND_R={1:0.###}", prefix, BottomRadius));
                cix.Add(string.Format("{0}GERING_BUND_DYBDE={1:0.###}", prefix, BottomDepth));
                //cix.Add(string.Format("{0}GERING_BUND_ALFA={1:0.###}", prefix, RhinoMath.ToDegrees(BottomAngle)));
            }
        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is MitreTopBottom other)
            {
                return
                    (Top == other.Top) &&
                    Math.Abs(TopThickness - other.TopThickness) < epsilon &&
                    Math.Abs(TopDepth - other.TopDepth) < epsilon &&
                    Math.Abs(TopAngle - other.TopAngle) < epsilon &&
                    (Bottom == other.Bottom) &&
                    Math.Abs(BottomThickness - other.BottomThickness) < epsilon &&
                    Math.Abs(BottomDepth - other.BottomDepth) < epsilon &&
                    Math.Abs(BottomRadius - other.BottomRadius) < epsilon &&
                    Math.Abs(BottomAngle - other.BottomAngle) < epsilon;
            }
            return false;
        }

        public static MitreTopBottom FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}GERING_TOP";

            var rebate = new MitreTopBottom(name);

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.Top = true;
                rebate.TopDepth = cix[$"{name}_DYBDE"];
                rebate.TopThickness = cix[$"{name}_B"];
                rebate.TopAngle = cix[$"{name}_ALFA"];

                //return new Rebate(name)
                //{
                //    Top = true,
                //    TopDepth = cix[$"{name}_DYBDE"],
                //    TopThickness = cix[$"{name}_T"],
                //};
            }

            name = $"{prefix}GERING_BUND";

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.Bottom = true;
                rebate.BottomDepth = cix[$"{name}_DYBDE"];
                rebate.BottomThickness = cix[$"{name}_B"];
                rebate.BottomRadius = cix[$"{name}_R"];
                rebate.BottomAngle = cix[$"{name}_ALFA"];
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

        public MitreTopBottom Combine(MitreTopBottom other)
        {
            var mitre = new MitreTopBottom(Name)
            {
                Top = Top,
                TopDepth = TopDepth,
                TopThickness = TopThickness,
                TopAngle = TopAngle,
                Bottom = Bottom,
                BottomDepth = BottomDepth,
                BottomThickness = BottomThickness,
                BottomRadius = BottomRadius,
                BottomAngle = BottomAngle
            };

            if (!Top && other.Top)
            {
                mitre.Top = other.Top;
                mitre.TopDepth = other.TopDepth;
                mitre.TopThickness = other.TopThickness;
                mitre.TopAngle = other.TopAngle;
            }
            if (!Bottom && other.Bottom)
            {
                mitre.Bottom = other.Bottom;
                mitre.BottomDepth = other.BottomDepth;
                mitre.BottomThickness = other.BottomThickness;
                mitre.BottomAngle = other.BottomAngle;
                mitre.BottomRadius = other.BottomRadius;
            }

            return mitre;

        }
    }
}
