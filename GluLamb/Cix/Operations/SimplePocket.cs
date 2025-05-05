using Rhino;
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
        public Plane Plane;
        public double Length, Width, Depth;
        public string OperationName = "POC";

        public SimplePocket(string name = "Simple pocket")
        {
            Name = name;
            Plane = Plane.Unset;
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
        }

        public override List<object> GetObjects()
        {
            return new List<object> { new Box(Plane, new Interval(0, Length), new Interval (0, Width), new Interval(0, -Depth)) };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            // def clockwise_angle(vec, ref):
            // angle = np.arctan2(vec[1], vec[0]) - np.arctan2(ref [1], ref [0])
            // return (2 * np.pi - angle) % (2 * np.pi)


            var alpha = Math.Atan2(Plane.XAxis.X, Plane.XAxis.Y) - Math.Atan2(1, 0);
            alpha = (2 * Math.PI - alpha) % (2 * Math.PI);

            cix.Add(string.Format("{0}{1}_{2}_PKT_X={3:0.0}", prefix, OperationName, Id, Plane.Origin.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_Y={3:0.0}", prefix, OperationName, Id, Plane.Origin.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_L={3:0.0}", prefix, OperationName, Id, Length));
            cix.Add(string.Format("{0}{1}_{2}_PKT_B={3:0.0}", prefix, OperationName, Id, Width));
            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.0}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_ALFA={3:0.0}", prefix, OperationName, Id, RhinoMath.ToDegrees(alpha)));

        }

        public override object Clone()
        {
            return new SimplePocket(Name)
            {
                Plane = Plane,
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
                    Plane.Origin.DistanceTo(other.Plane.Origin) < epsilon &&
                    Vector3d.VectorAngle(Plane.XAxis, other.Plane.XAxis) < epsilon;
            }

            return false;
        }

        public static SimplePocket FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}POC_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;
            var alpha = cix[$"{name}_ALFA"];

            var pocket = new SimplePocket(name);

            pocket.Plane = new Plane(
                new Point3d(
                    cix[$"{name}_PKT_X"],
                    cix[$"{name}_PKT_Y"],
                    0
                    ), Vector3d.XAxis, Vector3d.YAxis);

            pocket.Plane.Transform(Rhino.Geometry.Transform.Rotation(alpha, pocket.Plane.Origin));
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
