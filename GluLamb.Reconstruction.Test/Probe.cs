using System.Runtime.CompilerServices;

using Rhino;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace GluLamb.Reconstruction.Test
{
    internal static partial class Program
    {
        /// <summary>
        /// GluLamb.Reconstruction.Test --probe model.3dm
        /// Prints the sections of the first solid Brep in a file along its longest edge: corners,
        /// side lengths and the angles between sides.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunProbe(string path)
        {
            using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
            {
                var model = File3dm.Read(path);
                var brep = model.Objects.Select(o => o.Geometry).OfType<Brep>().First(b => b.IsSolid);
                Console.WriteLine($"Brep: {brep.Faces.Count} faces, {brep.Edges.Count} edges");
                foreach (var e in brep.Edges.OrderByDescending(e => e.GetLength()))
                    Console.WriteLine($"  edge {e.EdgeIndex}: length {e.GetLength():0}, linear {e.IsLinear(0.1)}, from {Fmt(e.PointAtStart)} to {Fmt(e.PointAtEnd)}");
                foreach (var f in brep.Faces)
                    Console.WriteLine($"  face {f.FaceIndex}: planar {f.IsPlanar(0.1)}, area {AreaMassProperties.Compute(f.DuplicateFace(false))?.Area:0}");

                var longest = brep.Edges.OrderByDescending(e => e.GetLength()).First();
                foreach (var s in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
                {
                    double t = longest.Domain.ParameterAt(s);
                    var plane = new Plane(longest.PointAt(t), longest.TangentAt(t));
                    Intersection.BrepPlane(brep, plane, 0.01, out Curve[] curves, out _);
                    foreach (var c in curves)
                    {
                        if (!c.TryGetPolyline(out Polyline pl))
                        {
                            Console.WriteLine($"  at {s}: section is not a polyline ({c.GetType().Name}, {c.SpanCount} spans)");
                            var simplified = c.Simplify(CurveSimplifyOptions.All, 0.1, 0.01) ?? c;
                            if (!simplified.TryGetPolyline(out pl)) continue;
                        }
                        var corners = pl.ToArray();
                        int n = corners.Length - (pl.IsClosed ? 1 : 0);
                        var sides = Enumerable.Range(0, n).Select(i => corners[(i + 1) % n].DistanceTo(corners[i]).ToString("0.#"));
                        var angles = Enumerable.Range(0, n).Select(i =>
                        {
                            var a = corners[(i + n - 1) % n] - corners[i];
                            var b = corners[(i + 1) % n] - corners[i];
                            return RhinoMath.ToDegrees(Vector3d.VectorAngle(a, b)).ToString("0.#");
                        });
                        Console.WriteLine($"  at {s}: {n} corners, sides {string.Join(" ", sides)}, angles {string.Join(" ", angles)}");
                    }
                }
            }
            return 0;
        }

        static string Fmt(Point3d p) => $"({p.X:0},{p.Y:0},{p.Z:0})";
    }
}
