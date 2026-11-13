using System.Runtime.CompilerServices;

using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace GluLamb.Reconstruction.Test
{
    internal static partial class Program
    {
        /// <summary>
        /// GluLamb.Reconstruction.Test --file model.3dm [--out folder]
        /// Lists what is in a .3dm, reconstructs a beam from each mesh (and a blank from each Brep),
        /// and writes the results next to the inputs into folder/name-result.3dm.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunFile(string path, string outDir)
        {
            using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
            {
                var model = File3dm.Read(path);
                if (model == null) { Console.WriteLine($"Can't read {path}"); return 1; }
                Console.WriteLine($"{path}: units {model.Settings.ModelUnitSystem}, {model.Objects.Count} objects");

                var output = new File3dm();
                output.Settings.ModelUnitSystem = model.Settings.ModelUnitSystem;
                int Layer(string layer, System.Drawing.Color color) => output.AllLayers.AddLayer(layer, color);
                int lInput = Layer("Input", System.Drawing.Color.Gray);
                int lBeam = Layer("Beam", System.Drawing.Color.Red);
                int lEdges = Layer("Edges", System.Drawing.Color.Blue);
                int lParallel = Layer("Parallel edges", System.Drawing.Color.Cyan);
                int lSections = Layer("Sections", System.Drawing.Color.Orange);
                int lPoints = Layer("Points", System.Drawing.Color.Black);
                ObjectAttributes On(int layer, string name = null) => new ObjectAttributes { LayerIndex = layer, Name = name };

                foreach (var obj in model.Objects)
                {
                    var layer = obj.Attributes.LayerIndex >= 0 && obj.Attributes.LayerIndex < model.AllLayers.Count ? model.AllLayers.FindIndex(obj.Attributes.LayerIndex)?.FullPath : "?";
                    var geometry = obj.Geometry;
                    var bbox = geometry.GetBoundingBox(true);
                    Console.WriteLine($"- {geometry.ObjectType} '{obj.Attributes.Name}' on '{layer}', box {bbox.Diagonal.X:0} x {bbox.Diagonal.Y:0} x {bbox.Diagonal.Z:0}");

                    Mesh mesh = geometry as Mesh;
                    if (geometry is Extrusion ext) geometry = ext.ToBrep();
                    if (geometry is Brep brep)
                    {
                        Console.WriteLine($"  Brep: {brep.Faces.Count} faces, {brep.Edges.Count} edges, solid {brep.IsSolid}");
                        var blank = BrepBlank.Reconstruct(brep);
                        foreach (var m in blank.Messages) Console.WriteLine($"  [brep] {m}");
                        if (blank.Success)
                        {
                            Console.WriteLine($"  [brep] beam {blank.Beam.Width:0.#} x {blank.Beam.Height:0.#}, length {blank.Beam.Centreline.GetLength():0.#}");
                            output.Objects.AddBrep(blank.Beam.ToBrep(), On(lBeam, "brep blank"));
                            output.Objects.AddCurve(blank.Beam.Centreline, On(lBeam, "brep blank centreline"));
                            ReportLongEdges(brep, blank.Beam);
                            CompareWithCurves(model, blank.Beam.Centreline, "centred");

                            // The same with the top or the bottom parallel edges as guides
                            var found = EdgeClusters.FindLargest(brep.Edges.Select(e => (Curve)e).ToList(), new EdgeClusterOptions());
                            foreach (var (side, sign) in new[] { ("top edges", 1.0), ("bottom edges", -1.0) })
                            {
                                var guides = found.Cluster.Where(e => blank.Beam.ToBeamSpace(e.ToArray()).Average(p => p.Y) * sign > 0.8 * blank.Beam.Height / 2)
                                    .Select(e => (Curve)new PolylineCurve(e)).ToList();
                                if (guides.Count < 2) { Console.WriteLine($"  [brep] {side}: only {guides.Count} found"); continue; }
                                var guided = BrepBlank.Reconstruct(brep, new BrepBlankOptions { Guides = guides });
                                if (!guided.Success) { Console.WriteLine($"  [brep] {side}: failed"); continue; }
                                Console.WriteLine($"  [brep] guided by {guides.Count} {side}: beam {guided.Beam.Width:0.#} x {guided.Beam.Height:0.#}, length {guided.Beam.Centreline.GetLength():0.#}. {guided.Messages.LastOrDefault(m => m.Contains("guide"))}");
                                ReportLongEdges(brep, guided.Beam);
                                CompareWithCurves(model, guided.Beam.Centreline, side);
                                output.Objects.AddBrep(guided.Beam.ToBrep(), On(lBeam, "brep blank, " + side));
                            }
                        }
                        output.Objects.AddBrep(brep, On(lInput));
                        // Also try it as a mesh
                        mesh = new Mesh();
                        foreach (var m in Mesh.CreateFromBrep(brep, new MeshingParameters { MaximumEdgeLength = 200, SimplePlanes = false }) ?? Array.Empty<Mesh>()) mesh.Append(m);
                    }
                    else if (mesh != null)
                    {
                        Console.WriteLine($"  Mesh: {mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces, closed {mesh.IsClosed}");
                        output.Objects.AddMesh(mesh, On(lInput));
                    }

                    if (mesh == null || mesh.Vertices.Count < 4) continue;

                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = MeshBeam.Reconstruct(mesh);
                    watch.Stop();
                    foreach (var m in result.Messages) Console.WriteLine($"  [mesh] {m}");
                    Console.WriteLine($"  [mesh] {(result.Success ? "ok" : "failed")} in {watch.Elapsed.TotalSeconds:0.0} s, {result.Edges.Count} sharp edges");
                    if (result.Weights != null) Console.WriteLine("  [mesh] weights: " + string.Join(" ", result.Weights.Select(w => w.ToString("0.0"))));
                    Console.WriteLine($"  [mesh] reference section {result.Reference.Major:0.#} x {result.Reference.Minor:0.#}");

                    foreach (var e in result.Edges) output.Objects.AddPolyline(e, On(lEdges));
                    foreach (var e in result.ParallelEdges) output.Objects.AddPolyline(e, On(lParallel));
                    foreach (var s in result.Sections) output.Objects.AddPolyline(s.Outline, On(lSections));
                    if (result.FittedPoints != null) foreach (var p in result.FittedPoints) output.Objects.AddPoint(p, On(lPoints));

                    if (result.Success)
                    {
                        Console.WriteLine($"  [mesh] beam {result.Beam.Width:0.#} x {result.Beam.Height:0.#}, length {result.Beam.Centreline.GetLength():0.#}, {result.Beam.Orientation}");
                        output.Objects.AddCurve(result.Beam.Centreline, On(lBeam, "mesh centreline"));
                        output.Objects.AddBrep(result.Beam.ToBrep(), On(lBeam, "mesh beam"));

                        // How far the mesh is outside the beam, along the beam
                        var local = result.Beam.ToBeamSpace(mesh.Vertices.ToPoint3dArray());
                        double length = result.Beam.Centreline.GetLength();
                        int bins = 20;
                        var worst = new double[bins];
                        foreach (var p in local)
                        {
                            int b = Math.Clamp((int)(p.Z / length * bins), 0, bins - 1);
                            double outside = Math.Max(Math.Abs(p.X) - result.Beam.Width / 2, Math.Abs(p.Y) - result.Beam.Height / 2);
                            worst[b] = Math.Max(worst[b], outside);
                        }
                        Console.WriteLine("  [mesh] mesh outside the beam, by 5% of length: " + string.Join(" ", worst.Select(w => w.ToString("0"))));

                        // Compared with any curve in the file (e.g. the intended centreline)
                        foreach (var cobj in model.Objects.Where(o => o.Geometry is Curve))
                        {
                            var reference = (Curve)cobj.Geometry;
                            var c = result.Beam.Centreline;
                            var offs = c.DivideByCount(20, true).Select(t =>
                            {
                                var p = c.PointAt(t);
                                reference.ClosestPoint(p, out double u);
                                return p.DistanceTo(reference.PointAt(u));
                            });
                            reference.TryGetPlane(out Plane rp, 1.0);
                            var k = reference.CurvatureAt(reference.Domain.Mid).Length;
                            Console.WriteLine($"  [mesh] vs curve (length {reference.GetLength():0.#}, mid radius {(k > 0 ? 1 / k : 0):0}, planar {reference.IsPlanar(1.0)}, ends {reference.PointAtStart.DistanceTo(c.PointAtStart):0.#}/{reference.PointAtEnd.DistanceTo(c.PointAtEnd):0.#} or {reference.PointAtStart.DistanceTo(c.PointAtEnd):0.#}/{reference.PointAtEnd.DistanceTo(c.PointAtStart):0.#}): "
                                + string.Join(" ", offs.Select(d => d.ToString("0.#"))));
                            output.Objects.AddCurve(reference, On(lInput, "reference curve"));

                            if (System.Environment.GetEnvironmentVariable("RECON_DEBUG") == "1")
                            {
                                var variants = new (string, MeshBeamOptions)[]
                                {
                                    ("no smoothing", new MeshBeamOptions { Smoothness = 0 }),
                                    ("smoothness 0.01", new MeshBeamOptions { Smoothness = 0.01 }),
                                    ("smoothness 0.05", new MeshBeamOptions { Smoothness = 0.05 }),
                                    ("smoothness 0.05, 150 it", new MeshBeamOptions { Smoothness = 0.05, Iterations = 150 }),
                                    ("no smoothing, 150 it", new MeshBeamOptions { Smoothness = 0, Iterations = 150 }),
                                    ("no edge correction", new MeshBeamOptions { EdgeCorrection = false }),
                                    ("neither", new MeshBeamOptions { Smoothness = 0, EdgeCorrection = false }),
                                    ("200 iterations", new MeshBeamOptions { Iterations = 200, Tolerance = 1e-7 }),
                                    ("straight end", new MeshBeamOptions { End = EndExtension.Straight }),
                                };
                                Console.WriteLine("  curve radius along it: " + string.Join(" ", reference.DivideByCount(20, true).Select(t => { var k = reference.CurvatureAt(t).Length; return k > 0 ? (1 / k).ToString("0") : "inf"; })));
                                Console.WriteLine($"  curve: {reference.GetType().Name}, degree {reference.Degree}, span count {reference.SpanCount}");
                                var extendedRef = reference.Extend(CurveEnd.Both, 200, CurveExtensionStyle.Smooth) ?? reference;
                                foreach (var (label, opts) in variants)
                                {
                                    var r = MeshBeam.Reconstruct(mesh, opts);
                                    if (!r.Success) { Console.WriteLine($"  [{label}] failed"); continue; }
                                    var cc = r.Beam.Centreline;
                                    Console.WriteLine($"  [{label}] error {r.Error:0.#}, {r.Iterations} it, length {cc.GetLength():0.#}, off the extended curve: " + string.Join(" ", cc.DivideByCount(20, true).Select(t =>
                                    {
                                        var p = cc.PointAt(t);
                                        extendedRef.ClosestPoint(p, out double u);
                                        return p.DistanceTo(extendedRef.PointAt(u)).ToString("0.#");
                                    })));
                                    var sp = new GluLamb.Numerics.InterpolatingSpline(r.FittedPoints);
                                    Console.WriteLine($"     fitted points: " + string.Join(" ", r.FittedPoints.Select(p => { reference.ClosestPoint(p, out double u); return p.DistanceTo(reference.PointAt(u)).ToString("0.#"); })));
                                }
                            }

                            // Section centres (from the sharp edges) along the reference curve
                            var edgeSet = new EdgeSet(result.Edges);
                            var hitList = new List<Point3d>();
                            Console.WriteLine("  [mesh] section centres off the curve along it: " + string.Join(" ", reference.DivideByCount(40, true).Select(t =>
                            {
                                var p = reference.PointAt(t);
                                if (!edgeSet.TrySection(p, reference.TangentAt(t), out Section s)) return "-";
                                return $"{p.DistanceTo(s.Centre):0.#}({s.Major:0}x{s.Minor:0}/{s.Hull.Length})";
                            })));
                        }
                    }
                }

                var file = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "-result.3dm");
                output.Write(file, 8);
                Console.WriteLine($"Wrote {file}");
            }
            return 0;
        }

        /// <summary>
        /// Where the Brep's longest run of parallel edges sits in the beam's section, along each
        /// edge: an edge that guides the beam keeps one place, at a corner or on a face.
        /// </summary>
        static void ReportLongEdges(Brep brep, Beam beam)
        {
            var found = EdgeClusters.FindLargest(brep.Edges.Select(e => (Curve)e).ToList(), new EdgeClusterOptions());
            if (found == null) return;
            double hw = beam.Width / 2, hh = beam.Height / 2;
            Console.WriteLine($"  [brep] {found.Summary} Half section {hw:0.##} x {hh:0.##}. Each parallel edge in the beam's section (x range, y range; drift = how far it wanders):");
            foreach (var edge in found.Cluster.OrderByDescending(e => e.Length).Take(12))
            {
                var local = beam.ToBeamSpace(edge.ToArray());
                double x0 = local.Min(p => p.X), x1 = local.Max(p => p.X), y0 = local.Min(p => p.Y), y1 = local.Max(p => p.Y);
                if (System.Environment.GetEnvironmentVariable("RECON_DEBUG") == "1")
                    Console.WriteLine("      along: " + string.Join(" ", Enumerable.Range(0, 13).Select(i => local[Math.Min(local.Length - 1, i * (local.Length - 1) / 12)]).Select(p => $"({p.X:0.0},{p.Y:0.0},{p.Z:0})")));
                Console.WriteLine($"    length {edge.Length,7:0}: x {x0,8:0.00}..{x1,8:0.00}, y {y0,8:0.00}..{y1,8:0.00}; drift {Math.Max(x1 - x0, y1 - y0):0.00}; " +
                    $"to faces: x {hw - Math.Max(Math.Abs(x0), Math.Abs(x1)):0.00}, y {hh - Math.Max(Math.Abs(y0), Math.Abs(y1)):0.00}");
            }
        }

        /// <summary>
        /// How far a centreline is from each long curve of the file, along it.
        /// </summary>
        static void CompareWithCurves(File3dm model, Curve centreline, string label)
        {
            double length = centreline.GetLength();
            foreach (var reference in model.Objects.Select(o => o.Geometry).OfType<Curve>().Where(c => c.GetLength() > 0.8 * length && c.GetLength() < 1.2 * length))
            {
                var offs = centreline.DivideByCount(10, true).Select(t =>
                {
                    var p = centreline.PointAt(t);
                    reference.ClosestPoint(p, out double u);
                    return p.DistanceTo(reference.PointAt(u));
                }).ToList();
                if (offs.Max() > 100) continue;
                Console.WriteLine($"  [brep] {label} vs curve of length {reference.GetLength():0}: " + string.Join(" ", offs.Select(d => d.ToString("0.0"))));
            }
        }
    }
}
