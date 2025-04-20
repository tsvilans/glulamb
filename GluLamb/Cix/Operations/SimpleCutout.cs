using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class SimpleCutout : Operation
    {
        public Line Span;
        public string OperationName = "OSS";

        public SimpleCutout(string name = "Simple cutout")
        {
            Name = name;
        }

        public override void Transform(Transform xform)
        {
            Span.Transform(xform);
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Span };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_PKT_1_X={3:0.000}", prefix, OperationName, Id, Span.From.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Y={3:0.000}", prefix, OperationName, Id, Span.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_X={3:0.000}", prefix, OperationName, Id, Span.To.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Y={3:0.000}", prefix, OperationName, Id, Span.To.Y));
        }

        public override object Clone()
        {
            return new SimpleCutout(Name)
            {
                Span = Span
            };
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SimpleCutout other)
            {
                return Span.From.DistanceTo(other.Span.From) < epsilon && Span.To.DistanceTo(other.Span.To) < epsilon;
            }
            return false;
        }

        public static SimpleCutout FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}OSS_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var cutout = new SimpleCutout(name);

            cutout.Span = new Line(
                new Point3d(
                    cix[$"{name}_PKT_1_X"],
                    cix[$"{name}_PKT_1_Y"],
                    0
                    ),
                new Point3d(
                    cix[$"{name}_PKT_2_X"],
                    cix[$"{name}_PKT_2_Y"],
                    0
                    )
                );

            return cutout;
        }

        public override BoundingBox Extents(Plane plane)
        {
            throw new NotImplementedException();
        }
    }
}
