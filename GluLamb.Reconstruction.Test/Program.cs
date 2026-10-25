using System.Runtime.CompilerServices;

using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

using GluLamb.Reconstruction.Numerics;

namespace GluLamb.Reconstruction.Test
{
    /// <summary>
    /// GluLamb.Reconstruction.Test [--out folder]
    /// Reconstructs synthetic beams from their meshes and Breps, prints how far the results are
    /// from the beams they were made from, and writes a .3dm per case to the out folder (default
    /// ./reconstruction-out). Exits with 1 if a check fails.
    /// </summary>
    internal static partial class Program
    {
        static int s_failures;

        [STAThread]
        private static int Main(string[] args)
        {
            RhinoInside.Resolver.Initialize();
            string outDir = "reconstruction-out", file = null;
            for (int i = 0; i + 1 < args.Length; ++i)
            {
                if (args[i] == "--out") outDir = args[i + 1];
                if (args[i] == "--file") file = args[i + 1];
                if (args[i] == "--probe") return RunProbe(args[i + 1]);
            }
            Directory.CreateDirectory(outDir);
            return file != null ? RunFile(file, outDir) : Run(outDir);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string outDir)
        {
            using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
            {
                Spline();
                Planar();

                var rotation = Transform.Rotation(0.4, new Vector3d(1, 2, 3), new Point3d(1000, -500, 200));

                // Straight beam, tilted in space
                var straight = new Beam
                {
                    Centreline = new LineCurve(new Point3d(0, 0, 0), new Point3d(6000, 0, 0)),
                    Orientation = new VectorOrientation(Vector3d.ZAxis),
                    Width = 140, Height = 400
                };
                straight.Transform(rotation);

                // Single-curved beam: an arc, bent in the height direction
                var arc = new Arc(new Point3d(0, 0, 0), new Point3d(4000, 0, 900), new Point3d(8000, 0, 0)).ToNurbsCurve();
                var curved = new Beam
                {
                    Centreline = arc,
                    Orientation = new PlanarOrientation(Plane.WorldZX),
                    Width = 200, Height = 600
                };
                curved.Transform(rotation);

                // Double-curved, twisted beam
                var spatial = Curve.CreateInterpolatedCurve(new[]
                {
                    new Point3d(0, 0, 0), new Point3d(2500, 600, 400), new Point3d(5000, 0, 900), new Point3d(7500, -700, 500), new Point3d(10000, 0, 0)
                }, 3);
                spatial.Domain = new Interval(0, 1);
                var twisted = new Beam
                {
                    Centreline = spatial,
                    Orientation = new VectorListOrientation(spatial, new[] { 0.0, 0.5, 1.0 }, new[] { Vector3d.ZAxis, new Vector3d(0, -1, 2), new Vector3d(0, -1, 1) }),
                    Width = 160, Height = 480
                };

                MeshCase("straight", straight, outDir);
                MeshCase("curved", curved, outDir);
                MeshCase("twisted", twisted, outDir);
                MeshCase("straight-joints", straight, outDir, true);
                MeshCase("curved-joints", curved, outDir, true);
                MeshCase("twisted-joints", twisted, outDir, true);

                // Tightly curved beam (radius about 4 m) with joints at the ends, at several spacings
                var tight = new Arc(new Point3d(0, 0, 0), new Point3d(3500, 0, 1650), new Point3d(7000, 0, 0)).ToNurbsCurve();
                var tightBeam = new Beam
                {
                    Centreline = tight,
                    Orientation = new PlanarOrientation(Plane.WorldZX),
                    Width = 180, Height = 520
                };
                tightBeam.Transform(rotation);
                foreach (var spacing in new[] { 300.0, 150.0, 75.0 })
                    MeshCase($"tight-joints-{spacing}", tightBeam, outDir, true, new MeshBeamOptions { SampleSpacing = spacing });
                MeshCase("curved-joints-75", curved, outDir, true, new MeshBeamOptions { SampleSpacing = 75 });

                // Steeply varying curvature, with skew end cuts: an Euler spiral (curvature rising
                // linearly from straight to a 900 mm radius over 5 m) ...
                var spiralPoints = new List<Point3d>();
                {
                    double length = 5000, kEnd = 1.0 / 900, step = 10, angle = 0;
                    var p = Point3d.Origin;
                    spiralPoints.Add(p);
                    for (double s = 0; s < length; s += step)
                    {
                        double k = kEnd * (s + step / 2) / length;
                        angle += k * step;
                        p += new Vector3d(Math.Cos(angle), 0, Math.Sin(angle)) * step;
                        if (((int)(s / step) + 1) % 10 == 0) spiralPoints.Add(p);
                    }
                }
                var spiral = Curve.CreateInterpolatedCurve(spiralPoints, 3);
                var spiralBeam = new Beam { Centreline = spiral, Orientation = new PlanarOrientation(Plane.WorldZX), Width = 140, Height = 360 };
                spiralBeam.Transform(rotation);
                MeshCase("spiral-skew", spiralBeam, outDir, false, null, skew: true);
                MeshCase("spiral-joints-skew", spiralBeam, outDir, true, null, skew: true);

                // ... and the curve from Tom's example (radius 460 to 1950 mm, not planar), if it's there
                var example = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outDir)) ?? ".", "reconstruction-example-01.3dm");
                var exampleCurve = File.Exists(example) ? File3dm.Read(example)?.Objects.Select(o => o.Geometry).OfType<Curve>().FirstOrDefault() : null;
                if (exampleCurve != null)
                {
                    var exampleBeam = new Beam { Centreline = exampleCurve.DuplicateCurve(), Orientation = new RmfOrientation(), Width = 100, Height = 200 };
                    MeshCase("example-curve-skew", exampleBeam, outDir, false, null, skew: true);
                    MeshCase("example-curve-skew-150", exampleBeam, outDir, false, new MeshBeamOptions { SampleSpacing = 150 }, skew: true);
                }
                else
                    Console.WriteLine($"(No {example}: skipping the example-curve cases.)");

                BrepCase("straight-joints", straight, outDir);
                BrepCase("curved-joints", curved, outDir);
                BrepCase("twisted-joints", twisted, outDir);
            }

            Console.WriteLine(s_failures == 0 ? "All checks passed." : $"{s_failures} check(s) failed.");
            return s_failures == 0 ? 0 : 1;
        }

        static void Check(bool ok, string what)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
            if (!ok) s_failures++;
        }

        static void Spline()
        {
            Console.WriteLine("Spline");
            var random = new Random(1);
            var points = Enumerable.Range(0, 9).Select(i => new Point3d(i * 500, random.NextDouble() * 300, random.NextDouble() * 200)).ToArray();
            var spline = new InterpolatingSpline(points);
            var curve = spline.ToNurbsCurve();

            double through = points.Max(p => { curve.ClosestPoint(p, out double t); return curve.PointAt(t).DistanceTo(p); });
            Check(through < 1e-6, $"passes through its points (max {through:E1})");

            double same = 0, tangent = 0;
            for (int i = 0; i <= 50; ++i)
            {
                double t = i / 50.0;
                spline.Evaluate(t, out Point3d p, out Vector3d d);
                same = Math.Max(same, p.DistanceTo(curve.PointAt(t)));
                var derivatives = curve.DerivativeAt(t, 1);
                tangent = Math.Max(tangent, (d - derivatives[1]).Length / derivatives[1].Length);
            }
            Check(same < 1e-6, $"NurbsCurve matches the spline (max {same:E1})");
            Check(tangent < 1e-6, $"derivative matches the NurbsCurve's (max relative {tangent:E1})");
        }

        static void Planar()
        {
            Console.WriteLine("VectorListOrientation to PlanarOrientation");
            var arc = new Arc(new Point3d(0, 0, 0), new Point3d(3000, 0, 600), new Point3d(6000, 0, 0)).ToNurbsCurve();
            var tt = arc.DivideByCount(6, true);
            var normals = tt.Select(t => Vector3d.CrossProduct(arc.TangentAt(t), Vector3d.YAxis)).ToArray();

            var vlo = new VectorListOrientation(arc, tt, normals);
            bool planar = vlo.TryGetPlanarOrientation(arc, RhinoMath.ToRadians(1), out PlanarOrientation po);
            double worst = 0;
            if (planar)
                foreach (var t in arc.DivideByCount(40, true))
                {
                    var a = vlo.GetOrientation(arc, t); a.Unitize();
                    var b = po.GetOrientation(arc, t); b.Unitize();
                    worst = Math.Max(worst, Vector3d.VectorAngle(a, b));
                }
            Check(planar && worst < RhinoMath.ToRadians(0.1), $"in-plane vectors convert ({RhinoMath.ToDegrees(worst):0.###} deg)");

            var twisted = new VectorListOrientation(arc, new[] { tt.First(), tt.Last() }, new[] { normals.First(), Vector3d.YAxis });
            Check(!twisted.TryGetPlanarOrientation(arc, RhinoMath.ToRadians(1), out _), "twisting vectors don't");
        }

        static void MeshCase(string name, Beam beam, string outDir, bool joints = false, MeshBeamOptions options = null, bool skew = false)
        {
            Console.WriteLine($"Mesh: {name}");
            bool cut = false;
            var brep = joints ? WithJoints(beam, out cut, true) : beam.ToBrep();
            if (joints) Console.WriteLine($"  {(cut ? "scarf, laps and notch cut" : "joints NOT cut (Boolean failed)")}");            if (skew) { brep = SkewEnds(beam, brep, out bool skewed); Console.WriteLine($"  {(skewed ? "ends cut skew" : "ends NOT cut skew (Boolean failed)")}"); }
            var mesh = new Mesh();
            foreach (var m in Mesh.CreateFromBrep(brep, new MeshingParameters { MaximumEdgeLength = 200, SimplePlanes = false }))
                mesh.Append(m);
            mesh.Faces.ConvertQuadsToTriangles();
            mesh.Unweld(0, true); // like an STL

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = MeshBeam.Reconstruct(mesh, options);
            watch.Stop();

            foreach (var m in result.Messages) Console.WriteLine($"  {m}");
            Check(result.Success, $"reconstructed ({watch.Elapsed.TotalSeconds:0.0} s, {result.Edges.Count} edges)");
            if (!result.Success) return;

            if (System.Environment.GetEnvironmentVariable("RECON_DEBUG") == "1")
            {
                foreach (var set in new[] { result.InitialPoints, result.FittedPoints })
                    Console.WriteLine("  points off: " + string.Join(" ", set.Select(p => { beam.Centreline.ClosestPoint(p, out double u); return p.DistanceTo(beam.Centreline.PointAt(u)).ToString("0"); })));
                Console.WriteLine("  weights: " + string.Join(" ", result.Weights.Select(w => w.ToString("0.0"))));
                var c = result.Beam.Centreline;
                Console.WriteLine("  final off: " + string.Join(" ", c.DivideByCount(20, true).Select(t => { var p = c.PointAt(t); beam.Centreline.ClosestPoint(p, out double u); return p.DistanceTo(beam.Centreline.PointAt(u)).ToString("0"); })));
                var clusterSet = new EdgeSet(result.ParallelEdges);
                var hitList = new List<Point3d>();
                Console.WriteLine("  parallel edges in the original's frames: " + string.Join(" ", beam.Centreline.DivideByCount(20, true).Select(t =>
                {
                    var pl = beam.GetPlane(t);
                    clusterSet.Cut(pl.Origin, pl.ZAxis, hitList);
                    return string.Join("/", hitList.Select(h => { pl.RemapToPlaneSpace(h, out Point3d lp); return $"{lp.X:0},{lp.Y:0}"; }));
                })));
                string Offs(Curve cv) => string.Join(" ", cv.DivideByCount(20, true).Select(t => { var p = cv.PointAt(t); beam.Centreline.ClosestPoint(p, out double u); return p.DistanceTo(beam.Centreline.PointAt(u)).ToString("0"); }));
                var noCorr = MeshBeam.Reconstruct(mesh, new MeshBeamOptions { EdgeCorrection = false, SampleSpacing = options?.SampleSpacing ?? 300 }).Beam;
                Console.WriteLine("  uncorrected: " + Offs(noCorr.Centreline) + $" ({noCorr.Orientation})");
                var rc = EdgeClusters.Recentre(noCorr, clusterSet);
                Console.WriteLine("  recentred once: " + Offs(rc));
                var sp = new InterpolatingSpline(result.FittedPoints);
                Console.WriteLine("  fitted spline off at t: " + string.Join(" ", new[] { 0, 0.01, 0.02, 0.05, 0.1, 0.9, 0.95, 0.98, 0.99, 1 }.Select(t => { var p = sp.PointAt(t); beam.Centreline.ClosestPoint(p, out double u); return $"{t}:{p.DistanceTo(beam.Centreline.PointAt(u)):0.#}"; })));
                var plain = MeshBeam.Reconstruct(mesh, new MeshBeamOptions { EdgeCorrection = false }).Beam.Centreline;
                Console.WriteLine("  without edge correction: " + string.Join(" ", plain.DivideByCount(20, true).Select(t => { var p = plain.PointAt(t); beam.Centreline.ClosestPoint(p, out double u); return p.DistanceTo(beam.Centreline.PointAt(u)).ToString("0"); })));
            }
            Compare(beam, result.Beam, 10, 10, new Interval(0.1, 0.9), !skew);
            Write(outDir, name + "-mesh", mesh, beam, result.Beam, result.Edges, result.Sections.Select(s => s.Outline));
        }

        static void BrepCase(string name, Beam beam, string outDir)
        {
            Console.WriteLine($"Brep: {name}");
            var brep = WithJoints(beam, out bool cut);
            Console.WriteLine($"  {(cut ? "joints cut" : "joints NOT cut (Boolean failed)")}, {brep.Edges.Count} edges");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = BrepBlank.Reconstruct(brep);
            watch.Stop();

            foreach (var m in result.Messages) Console.WriteLine($"  {m}");
            Check(result.Success, $"reconstructed ({watch.Elapsed.TotalSeconds:0.0} s)");
            if (!result.Success) return;

            Compare(beam, result.Beam, 5, 10, new Interval(0, 1));
            Check(result.Blank != null && result.Placement != null, $"blank {result.Blank?.GetType().Name} {result.Blank?.Width:0.#} x {result.Blank?.Height:0.#}");

            // Does the beam enclose the Brep?
            var points = new List<Point3d>();
            foreach (var m in Mesh.CreateFromBrep(brep, new MeshingParameters { MaximumEdgeLength = 100 })) points.AddRange(m.Vertices.ToPoint3dArray());
            var local = result.Beam.ToBeamSpace(points);
            double length = result.Beam.Centreline.GetLength();
            double outside = local.Max(p => Math.Max(
                Math.Max(Math.Abs(p.X) - result.Beam.Width / 2, Math.Abs(p.Y) - result.Beam.Height / 2),
                Math.Max(-p.Z, p.Z - length)));
            Check(outside < 1.0, $"encloses the Brep (furthest point {outside:0.###} outside)");

            Write(outDir, name + "-brep", brep, beam, result.Beam, result.Cluster, result.Sections.Select(s => s.Outline), result.Blank?.ToBrep());
        }

        /// <summary>
        /// Compare a reconstructed beam with the original: centreline deviation, length, section.
        /// </summary>
        static void Compare(Beam original, Beam rebuilt, double tolerance, double sectionTolerance, Interval orientationRange, bool checkLength = true)
        {
            double deviation = 0;
            double where = 0;
            foreach (var t in rebuilt.Centreline.DivideByCount(50, true))
            {
                var p = rebuilt.Centreline.PointAt(t);
                original.Centreline.ClosestPoint(p, out double u);
                double d = p.DistanceTo(original.Centreline.PointAt(u));
                if (d > deviation) { deviation = d; where = rebuilt.Centreline.Domain.NormalizedParameterAt(t); }
            }
            Check(deviation < tolerance, $"centreline within {deviation:0.###} of the original (worst at {where:0.##})");

            double l0 = original.Centreline.GetLength(), l1 = rebuilt.Centreline.GetLength();
            if (checkLength) Check(Math.Abs(l0 - l1) < 2 * tolerance, $"length {l1:0.#} (original {l0:0.#})");            else Console.WriteLine($"  (length {l1:0.#}, original {l0:0.#} before its ends were cut)");

            Check(Math.Abs(rebuilt.Width - original.Width) < sectionTolerance && Math.Abs(rebuilt.Height - original.Height) < sectionTolerance,
                $"section {rebuilt.Width:0.#} x {rebuilt.Height:0.#} (original {original.Width:0.#} x {original.Height:0.#})");

            double angle = 0, overall = 0;
            foreach (var t in rebuilt.Centreline.DivideByCount(40, true))
            {
                var p = rebuilt.GetPlane(t);
                original.Centreline.ClosestPoint(p.Origin, out double u);
                var q = original.GetPlane(u);
                double a = Math.Min(Vector3d.VectorAngle(p.YAxis, q.YAxis), Vector3d.VectorAngle(p.YAxis, -q.YAxis));
                overall = Math.Max(overall, a);
                if (orientationRange.IncludesParameter(rebuilt.Centreline.Domain.NormalizedParameterAt(t))) angle = Math.Max(angle, a);
            }
            Check(angle < RhinoMath.ToRadians(2), $"orientation {rebuilt.Orientation} within {RhinoMath.ToDegrees(angle):0.##} deg over {orientationRange.Min:0.##}-{orientationRange.Max:0.##} of its length ({RhinoMath.ToDegrees(overall):0.##} deg overall)");
        }

        /// <summary>
        /// The beam's Brep with joints cut into it: a lap at each end and a notch in the middle.
        /// </summary>
        static Brep WithJoints(Beam beam, out bool cut, bool scarf = false)
        {
            var brep = beam.ToBrep();
            var curve = beam.Centreline;
            double length = curve.GetLength();

            var cutters = new List<Brep>();
            void Cut(double at, double along, double depthFraction, bool top)
            {
                curve.LengthParameter(at, out double t);
                var plane = beam.GetPlane(t);
                double h = beam.Height, w = beam.Width;
                var y = top ? new Interval(h / 2 - h * depthFraction, h) : new Interval(-h, -h / 2 + h * depthFraction);
                var box = new Box(new Plane(plane.Origin, plane.XAxis, plane.YAxis), new Interval(-w, w), y, new Interval(-along / 2, along / 2));
                cutters.Add(box.ToBrep());
            }

            if (scarf)
            {
                // Scarf (a long splice) at the start: the top cut away on a slope, from 35% of the
                // height at the end to the full height 900 mm in
                var p0 = beam.GetPlane(curve.Domain.Min);
                double h = beam.Height, w = beam.Width, run = 900, drop = 0.65 * h;
                var origin = p0.Origin + p0.YAxis * (h / 2 - drop);
                var slope = p0.ZAxis * run + p0.YAxis * drop;
                double slopeLength = slope.Length;
                var box = new Box(new Plane(origin, p0.XAxis, slope), new Interval(-w, w), new Interval(-0.2 * slopeLength, 2 * slopeLength), new Interval(-2 * h, 0));
                cutters.Add(box.ToBrep());
            }
            else
                Cut(150, 300, 0.5, true);        // lap at the start
            Cut(length - 200, 400, 0.4, false);  // lap at the end
            Cut(length * 0.4, 160, 0.25, true);  // notch for a crossing beam

            var result = Brep.CreateBooleanDifference(new[] { brep }, cutters, 0.01);
            cut = result != null && result.Length == 1;
            return cut ? result[0] : brep;
        }

        static void Write(string outDir, string name, GeometryBase input, Beam original, Beam rebuilt, IEnumerable<Polyline> edges, IEnumerable<Polyline> sections, Brep blank = null)
        {
            var file = new File3dm();
            file.Settings.ModelUnitSystem = UnitSystem.Millimeters;
            int Layer(string layer, System.Drawing.Color color) { return file.AllLayers.AddLayer(layer, color); }

            int lInput = Layer("Input", System.Drawing.Color.Gray);
            int lOriginal = Layer("Original", System.Drawing.Color.Green);
            int lRebuilt = Layer("Reconstructed", System.Drawing.Color.Red);
            int lEdges = Layer("Edges", System.Drawing.Color.Blue);
            int lSections = Layer("Sections", System.Drawing.Color.Orange);
            int lBlank = Layer("Blank", System.Drawing.Color.Purple);

            ObjectAttributes On(int layer) => new ObjectAttributes { LayerIndex = layer };

            if (input is Mesh mesh) file.Objects.AddMesh(mesh, On(lInput));
            else if (input is Brep brep) file.Objects.AddBrep(brep, On(lInput));
            file.Objects.AddCurve(original.Centreline, On(lOriginal));
            file.Objects.AddCurve(rebuilt.Centreline, On(lRebuilt));
            file.Objects.AddBrep(rebuilt.ToBrep(), On(lRebuilt));
            foreach (var e in edges) file.Objects.AddPolyline(e, On(lEdges));
            foreach (var s in sections) file.Objects.AddPolyline(s, On(lSections));
            if (blank != null) file.Objects.AddBrep(blank, On(lBlank));

            file.Write(Path.Combine(outDir, name + ".3dm"), 8);
        }
    }
}
