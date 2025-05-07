using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// Simple tenon for E1 or E2. Radius of corners is 6mm.
    /// </summary>
    public class SimpleTenon : Operation
    {
        /// <summary>
        /// The horizontal distance from the Outside plane to the tenon.
        /// </summary>
        public double WidthFromOutside;
        /// <summary>
        /// The horizontal width of the tenon.
        /// </summary>
        public double Width;
        /// <summary>
        /// The thickness of the tenon.
        /// </summary>
        public double Thickness;
        /// <summary>
        /// The depth/length of the tenon (out of plane).
        /// </summary>
        public double Depth;
        /// <summary>
        /// The distance from the Bottom plane to the bottom of the tenon.
        /// </summary>
        public double UnderThickness;
        public string OperationName = "TAP";

        public SimpleTenon(string name = "Simple tenon")
        {
            Name = name;
        }

        public override void Transform(Transform xform)
        {
        }

        public override List<object> GetObjects()
        {
            return new List<object> { new Rectangle3d(Plane.WorldXY, 
                new Interval(WidthFromOutside, WidthFromOutside + Width), 
                new Interval (UnderThickness, UnderThickness + Thickness)) };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_B_BAG={2:0.###}", prefix, OperationName, WidthFromOutside));
            cix.Add(string.Format("{0}{1}_B={2:0.###}", prefix, OperationName, Width));
            cix.Add(string.Format("{0}{1}_DYBDE={2:0.###}", prefix, OperationName, Depth));
            cix.Add(string.Format("{0}{1}_T={2:0.###}", prefix, OperationName, Thickness));
            cix.Add(string.Format("{0}{1}_T_U={2:0.###}", prefix, OperationName, UnderThickness));

        }

        public override object Clone()
        {
            return new SimpleTenon(Name)
            {
                WidthFromOutside = WidthFromOutside,
                UnderThickness = UnderThickness,
                Thickness = Thickness,
                Depth = Depth,
                Width = Width,
                Id = Id,
            };
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SimpleTenon other)
            {
                return Math.Abs(Depth - other.Depth) < epsilon &&
                    Math.Abs(Width - other.Width) < epsilon &&
                    Math.Abs(WidthFromOutside - other.WidthFromOutside) < epsilon &&
                    Math.Abs(UnderThickness - other.UnderThickness) < epsilon &&
                    Math.Abs(Thickness - other.Thickness) < epsilon;
            }

            return false;
        }

        public static SimpleTenon FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}TAP_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;
            var alpha = cix[$"{name}_ALFA"];

            var tenon = new SimpleTenon(name);

            tenon.Id = int.Parse(id);
            tenon.Thickness = cix[$"{name}_T"];
            tenon.UnderThickness = cix[$"{name}_T_U"];
            tenon.Width = cix[$"{name}_B"];
            tenon.WidthFromOutside = cix[$"{name}_B_BAG"];
            tenon.Depth = cix[$"{name}_DYBDE"];

            return tenon;
        }

        public override BoundingBox Extents(Plane plane)
        {
            throw new NotImplementedException();
        }
    }
}
