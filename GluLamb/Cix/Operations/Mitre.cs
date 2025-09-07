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
    public class Mitre : Operation
    {
        public bool Front = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double FrontDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double FrontThickness = 0;
        public double FrontAngle = Math.PI * 0.5;

        public bool Back = false;
        /// <summary>
        /// Depth of rebate (distance from end).
        /// </summary>
        public double BackDepth = 0;
        /// <summary>
        /// Height of rebate from top surface
        /// </summary>
        public double BackThickness = 0;

        public double BackAngle = Math.PI * 0.5;

        public BeamSideType SideType = BeamSideType.End1;



        public Mitre(string name = "Rebate/Gering")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new Mitre(Name)
            {
                Front = Front,
                FrontDepth = FrontDepth,
                FrontThickness = FrontThickness,
                FrontAngle = FrontAngle,
                Back = Back,
                BackDepth = BackDepth,
                BackThickness = BackThickness,
                BackAngle = BackAngle,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}GERING_FOR={1}", prefix, Enabled && Front ? 1 : 0));

            if (Front)
            {
                cix.Add(string.Format("{0}GERING_FOR_B={1:0.###}", prefix, FrontThickness));
                cix.Add(string.Format("{0}GERING_FOR_DYBDE={1:0.###}", prefix, FrontDepth));
                cix.Add(string.Format("{0}GERING_FOR_ALFA={1:0.###}", prefix, RhinoMath.ToDegrees(FrontAngle)));
            }
            cix.Add(string.Format("{0}GERING_BAG={1}", prefix, Enabled && Back ? 1 : 0));

            if (Back)
            {
                cix.Add(string.Format("{0}GERING_BAG_B={1:0.###}", prefix, BackThickness));
                cix.Add(string.Format("{0}GERING_BAG_DYBDE={1:0.###}", prefix, BackDepth));
                cix.Add(string.Format("{0}GERING_BAG_ALFA={1:0.###}", prefix, RhinoMath.ToDegrees(BackAngle)));
            }
        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is Mitre other)
            {
                return
                    (Front == other.Front) &&
                    Math.Abs(FrontThickness - other.FrontThickness) < epsilon &&
                    Math.Abs(FrontDepth - other.FrontDepth) < epsilon &&
                    Math.Abs(FrontAngle - other.FrontAngle) < epsilon &&
                    (Back == other.Back) &&
                    Math.Abs(BackThickness - other.BackThickness) < epsilon &&
                    Math.Abs(BackDepth - other.BackDepth) < epsilon &&
                    Math.Abs(BackAngle - other.BackAngle) < epsilon;
            }
            return false;
        }

        public static Mitre FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}GERING_FOR";

            var rebate = new Mitre(name);

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.Front = true;
                rebate.FrontDepth = cix[$"{name}_DYBDE"];
                rebate.FrontThickness = cix[$"{name}_B"];
                rebate.FrontAngle = cix[$"{name}_ALFA"];

                //return new Rebate(name)
                //{
                //    Top = true,
                //    TopDepth = cix[$"{name}_DYBDE"],
                //    TopThickness = cix[$"{name}_T"],
                //};
            }

            name = $"{prefix}GERING_BAG";

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                rebate.Back = true;
                rebate.BackDepth = cix[$"{name}_DYBDE"];
                rebate.BackThickness = cix[$"{name}_B"];
                rebate.BackAngle = cix[$"{name}_ALFA"];
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

        public Mitre Combine(Mitre other)
        {
            var mitre = new Mitre(Name)
            {
                Front = Front,
                FrontDepth = FrontDepth,
                FrontThickness = FrontThickness,
                FrontAngle = FrontAngle,
                Back = Back,
                BackDepth = BackDepth,
                BackThickness = BackThickness,
                BackAngle = BackAngle
            };

            if (!Front && other.Front)
            {
                mitre.Front = other.Front;
                mitre.FrontDepth = other.FrontDepth;
                mitre.FrontThickness = other.FrontThickness;
                mitre.FrontAngle = other.FrontAngle;
            }
            if (!Back && other.Back)
            {
                mitre.Back = other.Back;
                mitre.BackDepth = other.BackDepth;
                mitre.BackThickness = other.BackThickness;
                mitre.BackAngle = other.BackAngle;
            }

            return mitre;

        }
    }
}
