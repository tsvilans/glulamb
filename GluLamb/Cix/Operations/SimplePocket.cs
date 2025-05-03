using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// Simple pocket for the top surface. Radius of corners is 6mm.
    /// </summary>
    public class SimplePocket : Operation
    {
        public Point3d Origin;
        public double Length, Width, Depth;
        public string OperationName = "POC";

        public SimplePocket(string name = "Simple pocket")
        {
            Name = name;
            Origin = Point3d.Unset;
        }

        public override void Transform(Transform xform)
        {
            Origin.Transform(xform);
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Origin };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_PKT_X={3:0.0}", prefix, OperationName, Id, Origin));
            cix.Add(string.Format("{0}{1}_{2}_PKT_Y={3:0.0}", prefix, OperationName, Id, Origin));
            cix.Add(string.Format("{0}{1}_{2}_PKT_L={3:0.0}", prefix, OperationName, Id, Length));
            cix.Add(string.Format("{0}{1}_{2}_PKT_B={3:0.0}", prefix, OperationName, Id, Width));
            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.0}", prefix, OperationName, Id, Depth));

        }

        public override object Clone()
        {
            return new SimplePocket(Name)
            {
                Origin = Origin,
                Depth = Depth,
                Length = Length,
                Width = Width
            };
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SimplePocket other)
            {
                return Math.Abs(Depth - other.Depth) < epsilon &&
                    Math.Abs(Width - other.Width) < epsilon &&
                    Math.Abs(Length - other.Length) < epsilon &&
                    Origin.DistanceTo(other.Origin) < epsilon;                
            }

            return false;
        }

        public static SimplePocket FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}POC_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var pocket = new SimplePocket(name);

            pocket.Origin = new Point3d(
                    cix[$"{name}_PKT_X"],
                    cix[$"{name}_PKT_Y"],
                    0
                    );
            pocket.Length = cix[$"{name}_L"];
            pocket.Width = cix[$"{name}_B"];
            pocket.Depth = cix[$"{name}_DYBDE"];

            return pocket;
        }

        public override BoundingBox Extents(Plane plane)
        {
            throw new NotImplementedException();
        }
    }
}
