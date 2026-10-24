/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using Rhino;
using Rhino.Geometry;

using GluLamb.Reconstruction.Numerics;

namespace GluLamb.Reconstruction
{
    public enum InitialCentreline
    {
        /// <summary>Centres of sections across the main axis, then of sections normal to the
        /// spline through them, a few times over. Follows S-curves and double curves.</summary>
        Sections,

        /// <summary>An arc through three section centres (the Python version).</summary>
        Arc,

        /// <summary>A straight line through the middle of the principal-axis box.</summary>
        Line
    }

    public class MeshBeamOptions
    {
        /// <summary>Spacing of the fitted centreline's points (model units).</summary>
        public double SampleSpacing = Globals.FromMillimeters(300);

        /// <summary>What the fit starts from.</summary>
        public InitialCentreline Start = InitialCentreline.Sections;

        /// <summary>For InitialCentreline.Sections: how many times the points are moved to the
        /// centres of the sections normal to the spline through them.</summary>
        public int StartPasses = 3;

        /// <summary>How far in from the drawn-back ends the outer two arc sections are taken.</summary>
        public double ArcOffset = Globals.FromMillimeters(150);

        /// <summary>How far the initial centreline is drawn back from the ends, away from noisy end cuts.</summary>
        public double EndWithdrawal = Globals.FromMillimeters(150);

        /// <summary>Iterations and relative tolerance of each round of the fit.</summary>
        public int Iterations = 20;
        public double Tolerance = 1e-2;

        /// <summary>Rounds of the fit. After each, the samples are weighted again by how typical
        /// their sections are along the new centreline.</summary>
        public int Rounds = 3;

        /// <summary>How much a section's size may differ from the beam's typical section (as a
        /// fraction) and still count fully. Sections that differ more (tapers, joints, end cuts)
        /// count less in the fit and don't set the frames, width and height.</summary>
        public double TypicalTolerance = 0.05;

        /// <summary>Weight of the penalty on changes of curvature along the centreline, which keeps
        /// the ends, where there is little to fit to, following the rest of the beam.</summary>
        public double Smoothness = 1.0;

        /// <summary>Number and range (0 to 1 along the centreline) of the sections the fit compares.</summary>
        public int FitSamples = 40;
        public Interval FitRange = new Interval(0.02, 0.98);

        /// <summary>Number and range of the sections that give the frames, width and height.</summary>
        public int SectionSamples = 20;
        public Interval SectionRange = new Interval(0.02, 0.98);

        /// <summary>Correct the centreline by where the longest run of parallel sharp edges sits
        /// in its sections, as many times over.</summary>
        public bool EdgeCorrection = true;
        public int Refinements = 2;
        public EdgeClusterOptions Clustering = new EdgeClusterOptions();

        /// <summary>Sharp-edge detection (radians): see Edges.SharpMeshEdges.</summary>
        public double SharpAngle = RhinoMath.ToRadians(60);
        public double JoinAngle = RhinoMath.ToRadians(15);
        public double WeldAngle = RhinoMath.ToRadians(120);

        /// <summary>Use a PlanarOrientation when the section frames all lie in one plane.</summary>
        public bool SimplifyOrientation = true;
        public double PlanarAngleTolerance = RhinoMath.ToRadians(1);
    }

    public class MeshBeamResult
    {
        public Beam Beam;
        public bool Success => Beam != null;

        /// <summary>Sharp edges the fit uses.</summary>
        public List<Polyline> Edges = new List<Polyline>();

        /// <summary>Principal-axis box of the mesh.</summary>
        public OrientedBounds Bounds;

        /// <summary>Points of the initial and the fitted centreline (the spline passes through them).</summary>
        public Point3d[] InitialPoints, FittedPoints;

        /// <summary>Typical sections along the final centreline, which give its frames, width and
        /// height, and the reference they were compared with.</summary>
        public List<Section> Sections = new List<Section>();
        public Section Reference;

        /// <summary>Weight of each fit sample in the last round (1: typical section, near 0: a
        /// taper, joint or end cut).</summary>
        public double[] Weights;

        /// <summary>The longest run of parallel sharp edges, if the centreline was corrected by it.</summary>
        public List<Polyline> ParallelEdges = new List<Polyline>();

        /// <summary>Fit error (the sum over the samples of weight x 100 x the distance to the
        /// section's centre, plus the curvature penalty), before and after.</summary>
        public double InitialError, Error;

        public int Iterations;
        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// A free-form beam from a mesh of it (e.g. from IFC, OBJ or STL): a centreline fitted through
    /// the centres of its sections, with section frames, width and height. Port of
    /// MeshBeamConverter in extract_beam_from_mesh_rhino.py.
    /// </summary>
    public static class MeshBeam
    {
        public static MeshBeamResult Reconstruct(Mesh mesh, MeshBeamOptions options = null)
        {
            options ??= new MeshBeamOptions();
            var result = new MeshBeamResult();

            if (mesh == null || mesh.Vertices.Count < 4) { result.Messages.Add("No mesh."); return result; }

            result.Edges = Reconstruction.Edges.SharpMeshEdges(mesh, options.SharpAngle, options.JoinAngle, options.WeldAngle);
            var edges = new EdgeSet(result.Edges);
            if (edges.Count < 3) { result.Messages.Add($"Only {edges.Count} sharp edges found; at least 3 are needed."); return result; }

            var bounds = Pca.WeightedMesh(mesh);
            result.Bounds = bounds;
            var xaxis = bounds.Plane.XAxis;

            // Initial centreline through the middle of the box, drawn back from the ends
            var start = bounds.SectionCentre(options.EndWithdrawal);
            var end = bounds.SectionCentre(bounds.SizeX - options.EndWithdrawal);
            int count = Math.Max(4, (int)Math.Ceiling(bounds.SizeX / options.SampleSpacing));

            Point3d[] points = null;
            if (options.Start == InitialCentreline.Sections)
            {
                points = WalkSections(edges, bounds, start, end, count, options.StartPasses, options.TypicalTolerance);
                if (points == null) result.Messages.Add("Couldn't start from sections; starting from an arc.");
            }
            if (points == null && options.Start != InitialCentreline.Line)
            {
                var hits = new List<Point3d>();
                var centres = new Point3d[3];
                bool ok = true;
                for (int i = 0; i < 3 && ok; ++i)
                {
                    var p = Lerp(start + xaxis * options.ArcOffset, end - xaxis * options.ArcOffset, i / 2.0);
                    ok = edges.TryCentre(p, xaxis, hits, out centres[i]);
                }

                if (ok) points = PointsOnArc(centres[0], centres[1], centres[2], count, bounds.SizeX * 0.95);
                if (points == null) result.Messages.Add("Couldn't start from an arc; starting from a straight line.");
            }
            points ??= Enumerable.Range(0, count).Select(i => Lerp(start, end, i / (double)(count - 1))).ToArray();
            result.InitialPoints = points;

            // The beam's typical section: the median size of the sections along the start curve
            if (!TryReference(new InterpolatingSpline(points), edges, options.SectionSamples, out Section reference))
            { result.Messages.Add("No sections found along the initial centreline."); return result; }
            result.Reference = reference;

            // Fit: each sample counts by how typical its section is, so that tapers, joints and
            // end cuts don't pull the centreline; a penalty on changes of curvature carries the
            // rest of the beam's shape into the ends. The weights are found again from each round's
            // result (iteratively reweighted).
            var fitT = Linspace(options.FitRange.Min, options.FitRange.Max, options.FitSamples);
            var x = ToArray(points);
            var weights = Weights(points, edges, fitT, reference, options.TypicalTolerance);
            result.InitialError = Error(points, edges, fitT, weights, options.Smoothness);

            for (int round = 0; round < Math.Max(1, options.Rounds); ++round)
            {
                var w = weights;
                var fit = new Lbfgs { MaxIterations = options.Iterations, FunctionTolerance = options.Tolerance }
                    .Minimize(xx => Error(ToPoints(xx), edges, fitT, w, options.Smoothness), x);
                x = fit.X;
                result.Iterations += fit.Iterations;
                result.Error = fit.Value;
                weights = Weights(ToPoints(x), edges, fitT, reference, options.TypicalTolerance);
            }

            var fitted = ToPoints(x);
            result.FittedPoints = fitted;
            result.Weights = weights;
            int atypical = weights.Count(v => v < 0.5);
            result.Messages.Add($"Fit: error {result.InitialError:0.###} to {result.Error:0.###} in {result.Iterations} iterations; " +
                $"{atypical} of {weights.Length} samples count less (taper, joint or end).");

            // Centreline: the spline, run out to the ends of the mesh, then corrected by the
            // longest run of parallel edges
            var vertices = mesh.Vertices.ToPoint3dArray();
            var curve = FitToExtents(new InterpolatingSpline(fitted).ToNurbsCurve(), vertices);

            EdgeSet parallel = null;
            if (options.EdgeCorrection)
            {
                var found = EdgeClusters.FindLargest(result.Edges.Select(e => (Curve)new PolylineCurve(e)).ToList(), options.Clustering);
                if (found != null)
                {
                    result.ParallelEdges = found.Cluster;
                    parallel = new EdgeSet(found.Cluster);
                    result.Messages.Add(found.Summary);
                }
            }

            int passes = parallel == null ? 0 : Math.Max(0, options.Refinements);
            Beam beam = null;
            for (int pass = 0; pass <= passes; ++pass)
            {
                bool last = pass == passes;
                var sections = TypicalSections(curve, edges, parallel, reference, options);
                if (sections.Count == 0) { result.Messages.Add("No typical sections along the fitted centreline."); return result; }

                beam = new Beam
                {
                    Centreline = curve,
                    Width = sections.Max(s => s.Width),
                    Height = sections.Max(s => s.Height),
                };
                beam.Orientation = Orientation(curve, sections, last && options.SimplifyOrientation, options.PlanarAngleTolerance, last ? result.Messages : new List<string>());
                result.Sections = sections;

                if (!last) curve = FitToExtents(EdgeClusters.Recentre(beam, parallel), vertices);
            }

            result.Beam = beam;
            return result;
        }

        /// <summary>
        /// Initial centreline points: centres of sections across the box's main axis, then, a few
        /// times over, centres of the sections normal to the spline through the points. Null if
        /// fewer than four sections are found.
        /// </summary>
        static Point3d[] WalkSections(EdgeSet edges, OrientedBounds bounds, Point3d start, Point3d end, int count, int passes, double tolerance)
        {
            var hits = new List<Point3d>();
            var xaxis = bounds.Plane.XAxis;

            // Sections near the ends can catch the end cuts and land off the beam, so the walk
            // keeps to the middle 80% and the ends are run out smoothly afterwards
            const double margin = 0.1;
            var points = new List<Point3d>();
            for (int i = 0; i < count; ++i)
                if (edges.TryCentre(Lerp(start, end, margin + (1 - 2 * margin) * i / (count - 1.0)), xaxis, hits, out Point3d c))
                    points.Add(c);
            if (points.Count < 4) return null;

            for (int pass = 0; pass < passes; ++pass)
            {
                var spline = new InterpolatingSpline(points);
                var moved = new List<Point3d>(count);
                for (int i = 0; i < count; ++i)
                {
                    spline.Evaluate(i / (double)(count - 1), out Point3d p, out Vector3d d);
                    if (edges.TryCentre(p, d, hits, out Point3d c)) moved.Add(c);
                }
                if (moved.Count < 4) break;
                points = moved;
            }

            // Leave out the centres of sections that aren't typical of the beam (a scarf or lap pulls
            // them off the centreline), so the ends are run out from the beam's own shape
            var walked = new InterpolatingSpline(points);
            if (TryReference(walked, edges, count, out Section reference))
            {
                var typical = new List<Point3d>(count);
                for (int i = 0; i < count; ++i)
                {
                    walked.Evaluate(i / (double)(count - 1), out Point3d p, out Vector3d d);
                    if (edges.TrySection(p, d, out Section s) && Typicality(s, reference, tolerance) >= 0.5)
                        typical.Add(s.Centre);
                }
                if (typical.Count >= 4) points = typical;
            }

            var curve = new InterpolatingSpline(points).ToNurbsCurve();
            double run = start.DistanceTo(end) * margin;
            var extended = curve.Extend(CurveEnd.Both, run, CurveExtensionStyle.Smooth) ?? curve;
            return extended.DivideByCount(count - 1, true).Select(t => extended.PointAt(t)).ToArray();
        }

        /// <summary>
        /// A VectorListOrientation from section frames, or the PlanarOrientation it amounts to.
        /// </summary>
        internal static CrossSectionOrientation Orientation(Curve centreline, IList<Section> sections, bool simplify, double angleTolerance, List<string> messages)
        {
            var parameters = new List<double>();
            var vectors = new List<Vector3d>();
            foreach (var s in sections)
            {
                centreline.ClosestPoint(s.Plane.Origin, out double t);
                parameters.Add(t);
                vectors.Add(s.Plane.YAxis);
            }

            var orientation = new VectorListOrientation(centreline, parameters, vectors);
            if (simplify && orientation.TryGetPlanarOrientation(centreline, angleTolerance, out PlanarOrientation planar))
            {
                messages.Add("Section frames lie in one plane: using a PlanarOrientation.");
                return planar;
            }
            return orientation;
        }

        /// <summary>
        /// Extend a curve smoothly past both ends, then trim it to the extent of the points along it.
        /// </summary>
        internal static Curve FitToExtents(Curve curve, IList<Point3d> points)
        {

            var extended = curve.Extend(CurveEnd.Both, 0.1 * curve.GetLength(), CurveExtensionStyle.Smooth) ?? curve;

            double tMin = double.MaxValue, tMax = double.MinValue;
            object sync = new object();
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, points.Count), range =>
            {
                double lo = double.MaxValue, hi = double.MinValue;
                for (int i = range.Item1; i < range.Item2; ++i)
                    if (extended.ClosestPoint(points[i], out double t)) { lo = Math.Min(lo, t); hi = Math.Max(hi, t); }
                lock (sync) { tMin = Math.Min(tMin, lo); tMax = Math.Max(tMax, hi); }
            });

            if (tMax <= tMin) return curve;
            var trimmed = extended.Trim(tMin, tMax) ?? extended;
            trimmed.Domain = new Interval(0, 1);
            return trimmed;
        }

        /// <summary>
        /// The beam's typical section: the median extents of the sections along the middle 80% of
        /// a curve.
        /// </summary>
        static bool TryReference(InterpolatingSpline spline, EdgeSet edges, int samples, out Section reference)
        {
            reference = default;
            var majors = new List<double>();
            var minors = new List<double>();
            foreach (var t in Linspace(0.1, 0.9, Math.Max(3, samples)))
            {
                spline.Evaluate(t, out Point3d p, out Vector3d d);
                if (edges.TrySection(p, d, out Section s)) { majors.Add(s.Major); minors.Add(s.Minor); }
            }
            if (majors.Count == 0) return false;

            majors.Sort(); minors.Sort();
            reference = new Section { Major = majors[majors.Count / 2], Minor = minors[minors.Count / 2] };
            return reference.Major > 0 && reference.Minor > 0;
        }

        /// <summary>
        /// Weight of a section by how far its size is from the reference: 1 when it is the same,
        /// 1/2 at the tolerance, falling off quickly beyond.
        /// </summary>
        static double Typicality(Section section, Section reference, double tolerance)
        {
            double r = section.DifferenceFrom(reference) / Math.Max(tolerance, 1e-6);
            return 1.0 / (1.0 + r * r * r * r);
        }

        /// <summary>
        /// Weight of each fit sample along the spline through the points. Samples without a
        /// section get none.
        /// </summary>
        static double[] Weights(Point3d[] points, EdgeSet edges, double[] tt, Section reference, double tolerance)
        {
            var spline = new InterpolatingSpline(points);
            var weights = new double[tt.Length];
            Parallel.For(0, tt.Length, i =>
            {
                spline.Evaluate(tt[i], out Point3d p, out Vector3d d);
                // Sections well off the reference are left out altogether, so that the fit isn't
                // pulled by samples on the edge of a joint
                double w = edges.TrySection(p, d, out Section s) ? Typicality(s, reference, tolerance) : 0;
                weights[i] = w >= 0.25 ? w : 0;
            });
            return weights;
        }

        /// <summary>
        /// Typical sections along a curve, turned to line up with the parallel edges if there are
        /// any, with their frames made consistent. Falls back to all sections if none is typical.
        /// </summary>
        static List<Section> TypicalSections(Curve curve, EdgeSet edges, EdgeSet parallel, Section reference, MeshBeamOptions options)
        {
            var all = new List<Section>();
            var typical = new List<Section>();
            foreach (var t in Linspace(options.SectionRange.Min, options.SectionRange.Max, options.SectionSamples))
            {
                double u = curve.Domain.ParameterAt(t);
                if (!edges.TrySection(curve.PointAt(u), curve.TangentAt(u), out Section s)) continue;
                if (parallel != null) EdgeClusters.AlignToEdges(ref s, parallel);

                all.Add(s);
                if (Typicality(s, reference, options.TypicalTolerance) >= 0.5) typical.Add(s);
            }

            var sections = typical.Count > 0 ? typical : all;
            Sections.MakeConsistent(sections);
            return sections;
        }

        /// <summary>
        /// Fit error of the spline through the points: over the samples, weight x 100 x the distance
        /// to the centre of the section there (or weight x 1000 where there is no section), plus
        /// smoothness x the curvature penalty.
        /// </summary>
        static double Error(Point3d[] points, EdgeSet edges, double[] tt, double[] weights, double smoothness)
        {
            InterpolatingSpline spline;
            try { spline = new InterpolatingSpline(points); }
            catch (InvalidOperationException) { return 1000.0 * tt.Length; }

            var hits = new List<Point3d>();
            double error = 0;
            for (int i = 0; i < tt.Length; ++i)
            {
                if (weights[i] <= 0) continue;
                spline.Evaluate(tt[i], out Point3d p, out Vector3d d);
                if (edges.TryCentre(p, d, hits, out Point3d centre))
                    error += weights[i] * centre.DistanceTo(p) * 1e2;
                else
                    error += weights[i] * 1000.0;
            }

            if (smoothness > 0) error += smoothness * CurvaturePenalty(spline);
            return error;
        }

        /// <summary>
        /// How much the curvature changes along a spline: over 40 equal steps, the sum of the
        /// squared changes of the curvature vector between steps, each times the length (so it is
        /// unitless), times 100 to match the fit error. Zero for a straight line or a circular
        /// arc, so it doesn't pull curved beams straight.
        /// </summary>
        static double CurvaturePenalty(InterpolatingSpline spline, int steps = 40)
        {
            var q = new Point3d[steps + 1];
            for (int j = 0; j <= steps; ++j) q[j] = spline.PointAt(j / (double)steps);

            double length = 0;
            for (int j = 0; j < steps; ++j) length += q[j].DistanceTo(q[j + 1]);

            double sum = 0;
            var previous = Vector3d.Unset;
            for (int j = 1; j < steps; ++j)
            {
                var c0 = q[j] - q[j - 1];
                var c1 = q[j + 1] - q[j];
                double l0 = c0.Length, l1 = c1.Length;
                if (l0 <= 0 || l1 <= 0) continue;

                var k = (c1 / l1 - c0 / l0) * (2.0 / (l0 + l1));
                if (previous.IsValid)
                {
                    double change = (k - previous).Length * length;
                    sum += change * change;
                }
                previous = k;
            }
            return 100.0 * sum;
        }

        /// <summary>
        /// N points along the circular arc through three points, extended equally at both ends to
        /// a length if the arc is shorter. Points (nearly) in a line give a straight line from the
        /// first to the third.
        /// </summary>
        public static Point3d[] PointsOnArc(Point3d p1, Point3d p2, Point3d p3, int count, double length)
        {
            var circle = new Circle(p1, p2, p3);
            if (!circle.IsValid || circle.Radius > 100 * Math.Max(length, p1.DistanceTo(p3)))
            {
                var dir = p3 - p1;
                double chord = dir.Length;
                if (chord <= 0) return null;
                dir /= chord;
                double half = Math.Max(length, chord) / 2;
                var mid = p1 + dir * (chord / 2);
                return Enumerable.Range(0, count).Select(i => mid + dir * (-half + 2 * half * i / (count - 1))).ToArray();
            }

            circle.ClosestParameter(p1, out double a1);
            circle.ClosestParameter(p2, out double a2);
            circle.ClosestParameter(p3, out double a3);

            // Go from p1 to p3 the way round that passes p2
            double tau = 2 * Math.PI;
            double d2 = ((a2 - a1) % tau + tau) % tau, d3 = ((a3 - a1) % tau + tau) % tau;
            double sweep = d2 < d3 ? d3 : d3 - tau;

            double start = a1, end = a1 + sweep;
            double arcLength = Math.Abs(sweep) * circle.Radius;
            if (length > arcLength)
            {
                double extra = (length - arcLength) / 2 / circle.Radius * Math.Sign(sweep);
                start -= extra; end += extra;
            }

            return Enumerable.Range(0, count).Select(i => circle.PointAt(start + (end - start) * i / (count - 1))).ToArray();
        }

        internal static double[] Linspace(double a, double b, int n)
        {
            if (n == 1) return new[] { (a + b) * 0.5 };
            return Enumerable.Range(0, n).Select(i => a + (b - a) * i / (n - 1)).ToArray();
        }

        static Point3d Lerp(Point3d a, Point3d b, double t) => a + (b - a) * t;

        static double[] ToArray(IList<Point3d> points)
        {
            var x = new double[points.Count * 3];
            for (int i = 0; i < points.Count; ++i) { x[3 * i] = points[i].X; x[3 * i + 1] = points[i].Y; x[3 * i + 2] = points[i].Z; }
            return x;
        }

        static Point3d[] ToPoints(double[] x)
        {
            var p = new Point3d[x.Length / 3];
            for (int i = 0; i < p.Length; ++i) p[i] = new Point3d(x[3 * i], x[3 * i + 1], x[3 * i + 2]);
            return p;
        }
    }
}
