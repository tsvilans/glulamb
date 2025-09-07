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
using System.Diagnostics.Metrics;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// EndBottomChamfer can only be applied to End1 or End2.
    /// </summary>
    public class EndBottomChamfer : Operation
    {
        /// <summary>
        /// Total thickness including the rounded section that transitions from vertical and
        // meets the 45-degree chamfer.
        /// </summary>
        public double Thickness = 0;
        /// <summary>
        /// Radius of chamfer
        /// </summary>
        public double Radius1 = 0;

        public EndBottomChamfer(string name = "EndBottomChamfer/Fas")
        {
            Name = name;
        }
        public override object Clone()
        {
            return new EndBottomChamfer(Name)
            {
                Thickness = Thickness,
                Radius1 = Radius1,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add($"{prefix}FAS_BUND={(Enabled ? 1 : 0)}");

            if (Enabled)
            {
                cix.Add($"{prefix}FAS_BUND_T={Thickness:0.###}");
                cix.Add($"{prefix}FAS_BUND_R_1={Radius1:0.###}");
            }

        }

        public override void Transform(Transform xform)
        {
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is EndBottomChamfer other)
            {
                return
                    (Enabled == other.Enabled) &&
                    Math.Abs(Thickness - other.Thickness) < epsilon &&
                    Math.Abs(Radius1 - other.Radius1) < epsilon;
            }
            return false;
        }

        public static EndBottomChamfer FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}FAS_BUND";

            var endBottomChamfer = new EndBottomChamfer(name);

            if (cix.ContainsKey(name) && cix[name] > 0)
            {
                endBottomChamfer.Enabled = true;
                endBottomChamfer.Thickness = cix[$"{name}_T"];
                endBottomChamfer.Radius1 = cix[$"{name}_R_1"];
            }

            return endBottomChamfer;
        }

        public override BoundingBox Extents(Plane plane)
        {
            //return copy.BoundingBox;
            return BoundingBox.Empty;
        }

        public EndBottomChamfer Combine(EndBottomChamfer other)
        {
            var endBottomChamfer = new EndBottomChamfer(Name)
            {
                Enabled = Enabled,
                Thickness = Thickness,
                Radius1 = Radius1,
            };

            if (!Enabled && other.Enabled)
            {
                endBottomChamfer.Enabled = other.Enabled;
                endBottomChamfer.Thickness = other.Thickness;
                endBottomChamfer.Radius1 = other.Radius1;
            }

            return endBottomChamfer;
        }
    }
}
