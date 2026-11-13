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

using GluLamb.Numerics;

namespace GluLamb.Reconstruction
{
    public enum InitialCentreline
    {
        /// <summary>Section centres across the main axis. Then section centres normal to the
        /// spline through them, repeated. Follows S-curves and double curves.</summary>
        Sections,

        /// <summary>Legacy. An arc through three section centres. Assumes constant curvature.</summary>
        LegacyArc,

        /// <summary>A straight line through the middle of the principal-axis box.</summary>
        Line
    }

    /// <summary>
    /// How the centreline points are fitted to the section centres. Both methods minimise the
    /// same error.
    /// </summary>
    public enum CentrelineFit
    {
        /// <summary>L-BFGS on the points. Gradients by finite differences.</summary>
        Lbfgs,

        /// <summary>Find the section centres along the spline. Move its control points to them by
        /// weighted least squares. Repeat.</summary>
        LeastSquares
    }

    public class MeshBeamOptions
    {
        /// <summary>Spacing of the fitted points.</summary>
        public double SampleSpacing = Globals.FromMillimeters(300);

        /// <summary>The start of the fit.</summary>
        public InitialCentreline Start = InitialCentreline.Sections;

        /// <summary>InitialCentreline.Sections: number of passes.</summary>
        public int StartPasses = 3;

        /// <summary>The fit method.</summary>
        public CentrelineFit Fit = CentrelineFit.Lbfgs;

        /// <summary>Thresholds.</summary>
        public ReconstructionTolerances Tolerances = new ReconstructionTolerances();

        /// <summary>LegacyArc: distance of the two outer arc sections from the drawn-back ends.</summary>
        public double ArcOffset = Globals.FromMillimeters(150);

        /// <summary>Distance that the initial centreline stays away from the ends. End cuts are noisy.</summary>
        public double EndWithdrawal = Globals.FromMillimeters(150);

        /// <summary>Iterations of each round. Tolerance: relative, Lbfgs only.</summary>
        public int Iterations = 50;
        public double Tolerance = 1e-4;

        /// <summary>Rounds of the fit. Each round sets the sample weights again.</summary>
        public int Rounds = 3;

        /// <summary>Allowed difference (fraction) between a section and the typical section.
        /// Sections that differ more (tapers, joints, end cuts) count less. They do not set the
        /// frames, width or height.</summary>
        public double TypicalTolerance = 0.05;

        /// <summary>Weight of the penalty on changes of curvature. 0: off.</summary>
        public double Smoothness = 0;

        /// <summary>How the centreline continues past its last typical sections to the mesh ends.</summary>
        public EndExtension End = EndExtension.Trend;

        /// <summary>Trend and LegacyArc: length at each end that gives the curvature to continue.
        /// 0: 15% of the length.</summary>
        public double EndFitLength = 0;

        /// <summary>Number and range (0 to 1) of the fit sections. At least three per point.</summary>
        public int FitSamples = 40;
        public Interval FitRange = new Interval(0.02, 0.98);

        /// <summary>Number and range of the sections that give the frames, width and height.</summary>
        public int SectionSamples = 20;
        public Interval SectionRange = new Interval(0.02, 0.98);

        /// <summary>Correct the centreline from the longest run of parallel sharp edges.
        /// Refinements: number of passes.</summary>
        public bool EdgeCorrection = true;
        public int Refinements = 2;
        public EdgeClusterOptions Clustering = new EdgeClusterOptions();

        /// <summary>Sharp-edge angles (radians). See Edges.SharpMeshEdges.</summary>
        public double SharpAngle = RhinoMath.ToRadians(20);
        public double JoinAngle = RhinoMath.ToRadians(15);
        public double WeldAngle = RhinoMath.ToRadians(120);

        /// <summary>Use a PlanarOrientation if all section frames lie in one plane.</summary>
        public bool SimplifyOrientation = true;
        public double PlanarAngleTolerance = RhinoMath.ToRadians(1);
    }

    public class MeshBeamResult
    {
        public Beam Beam;
        public bool Success => Beam != null;

        /// <summary>Sharp edges that the fit uses.</summary>
        public List<Polyline> Edges = new List<Polyline>();

        /// <summary>Principal-axis box of the mesh.</summary>
        public OrientedBounds Bounds;

        /// <summary>Points of the initial and the fitted centreline.</summary>
        public Point3d[] InitialPoints, FittedPoints;

        /// <summary>Typical sections along the final centreline, and the reference section.</summary>
        public List<Section> Sections = new List<Section>();
        public Section Reference;

        /// <summary>Weight of each fit sample in the last round. 1: typical. 0: taper, joint or end cut.</summary>
        public double[] Weights;

        /// <summary>The longest run of parallel sharp edges, if it corrected the centreline.</summary>
        public List<Polyline> ParallelEdges = new List<Polyline>();

        /// <summary>Fit error before and after. See MeshBeam.Error.</summary>
        public double InitialError, Error;

        public int Iterations;
        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// A free-form beam from a mesh (IFC, OBJ, STL): a centreline through the section centres,
    /// with frames, width and height.
    /// </summary>
    public static class MeshBeam
    {
        public static MeshBeamResult Reconstruct(Mesh mesh, MeshBeamOptions options = null)
        {
            options ??= new MeshBeamOptions();
            var tol = options.Tolerances ?? new ReconstructionTolerances();
            var result = new MeshBeamResult();

            if (mesh == null || mesh.Vertices.Count < 4) { result.Messages.Add("No mesh."); return result; }

            result.Edges = Reconstruction.Edges.SharpMeshEdges(mesh, options.SharpAngle, options.JoinAngle, options.WeldAngle);
            var edges = new EdgeSet(result.Edges, tol.RectangleAngle);
            if (edges.Count < 3) { result.Messages.Add($"Only {edges.Count} sharp edges found; at least 3 are needed."); return result; }

            var bounds = Pca.WeightedMesh(mesh);
            result.Bounds = bounds;
            var xaxis = bounds.Plane.XAxis;

            // Initial centreline: the middle of the box, away from the ends
            var start = bounds.SectionCentre(options.EndWithdrawal);
            var end = bounds.SectionCentre(bounds.SizeX - options.EndWithdrawal);
            int count = Math.Max(4, (int)Math.Ceiling(bounds.SizeX / options.SampleSpacing));

            // A first estimate of the section depth. It stops the walk from using points too close together.
            var axis = new InterpolatingSpline(new[] { start, Lerp(start, end, 1 / 3.0), Lerp(start, end, 2 / 3.0), end });
            if (TryReference(axis, edges, 9, tol, out Section rough) && options.SampleSpacing < tol.MinSpacingFactor * rough.Major)
                count = Math.Max(4, (int)Math.Ceiling(bounds.SizeX / (tol.MinSpacingFactor * rough.Major)));

            Point3d[] points = null;
            if (options.Start == InitialCentreline.Sections)
            {
                points = WalkSections(edges, bounds, start, end, count, options.StartPasses, options.TypicalTolerance, tol);
                if (points == null) result.Messages.Add("Couldn't start from sections; starting from a straight line.");
            }
            if (points == null && options.Start == InitialCentreline.LegacyArc)
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

            // Extend the start curve to the mesh ends. The fit then sees the full beam.
            var vertices = ExtentPoints(mesh);
            {
                var seed = FitToExtents(new InterpolatingSpline(points).ToNurbsCurve(), vertices, options.End, options.EndFitLength, tol.RunOutFraction);
                double spacing = Math.Max(options.SampleSpacing, seed.GetLength() / Math.Max(3, points.Length - 1));
                int n = Math.Max(4, (int)Math.Ceiling(seed.GetLength() / spacing) + 1);
                points = seed.DivideByCount(n - 1, true).Select(t => seed.PointAt(t)).ToArray();
            }
            result.InitialPoints = points;

            // Typical section: median size of the sections along the start curve
            if (!TryReference(new InterpolatingSpline(points), edges, options.SectionSamples, tol, out Section reference))
            { result.Messages.Add("No sections found along the initial centreline."); return result; }
            result.Reference = reference;

            // Points closer than the section depth add freedom but no information
            double minSpacing = tol.MinSpacingFactor * reference.Major;
            if (options.SampleSpacing < minSpacing)
            {
                var seed = new InterpolatingSpline(points).ToNurbsCurve();
                int fewer = Math.Max(4, (int)Math.Ceiling(seed.GetLength() / minSpacing) + 1);
                if (fewer < points.Length)
                {
                    points = seed.DivideByCount(fewer - 1, true).Select(t => seed.PointAt(t)).ToArray();
                    result.Messages.Add($"Spacing raised to {minSpacing:0.#} ({tol.MinSpacingFactor:0.##} x the section's depth): closer points don't add information.");
                }
            }

            // Fit. A sample's weight is how typical its section is, so tapers, joints and end cuts
            // do not move the centreline. Each round sets the weights again.
            if (options.Fit == CentrelineFit.LeastSquares && options.Smoothness > 0)
                result.Messages.Add("The LeastSquares fit doesn't use Smoothness; use Lbfgs for the curvature penalty.");
            var fitT = Sampling.Linspace(options.FitRange.Min, options.FitRange.Max, Math.Max(options.FitSamples, 3 * points.Length));
            var weights = Weights(points, edges, fitT, reference, options.TypicalTolerance, tol);
            result.InitialError = Error(points, edges, fitT, weights, options.Smoothness, tol);

            for (int round = 0; round < Math.Max(1, options.Rounds); ++round)
            {
                int iterations;
                points = options.Fit == CentrelineFit.LeastSquares
                    ? FitLeastSquares(points, edges, fitT, weights, options.Iterations, tol, out iterations)
                    : FitLbfgs(points, edges, fitT, weights, options, tol, out iterations);
                result.Iterations += iterations;
                result.Error = Error(points, edges, fitT, weights, options.Smoothness, tol);
                weights = Weights(points, edges, fitT, reference, options.TypicalTolerance, tol);
            }

            var fitted = points;
            result.FittedPoints = fitted;
            result.Weights = weights;
            int atypical = weights.Count(v => v < tol.TypicalWeight);
            result.Messages.Add($"Fit ({options.Fit}): error {result.InitialError:0.###} to {result.Error:0.###} in {result.Iterations} iterations; " +
                $"{atypical} of {weights.Length} samples count less (taper, joint or end).");

            // Centreline: (1) the spline between the first and last typical samples, (2) tracked
            // along the parallel edges, (3) extended to the mesh ends, (4) corrected from the edges
            int first = Array.FindIndex(weights, v => v >= tol.TypicalWeight), lastTypical = Array.FindLastIndex(weights, v => v >= tol.TypicalWeight);
            Curve curve = new InterpolatingSpline(fitted).ToNurbsCurve();
            if (first >= 0 && lastTypical > first)
                curve = curve.Trim(fitT[first], fitT[lastTypical]) ?? curve;

            // Main edges: the longest run of parallel edges. The other long edges help where a
            // joint cuts the main edges.
            EdgeSet parallel = null, tracking = null;
            int main = 0;
            if (options.EdgeCorrection)
            {
                var found = EdgeClusters.FindLargest(result.Edges.Select(e => (Curve)new PolylineCurve(e)).ToList(), options.Clustering);
                if (found != null)
                {
                    result.ParallelEdges = found.Cluster;
                    parallel = new EdgeSet(found.Cluster);
                    var others = found.Edges.Where(e => !found.Cluster.Contains(e));
                    tracking = new EdgeSet(found.Cluster.Concat(others));
                    main = found.Cluster.Count;
                    result.Messages.Add(found.Summary);
                }
            }

            if (parallel != null)
            {
                var known = TypicalSections(curve, edges, parallel, reference, options, tol);
                if (known.Count > 0)
                {
                    var along = new Beam
                    {
                        Centreline = curve,
                        Width = known.Max(s => s.Width),
                        Height = known.Max(s => s.Height),
                        Orientation = Orientation(curve, known, false, 0, new List<string>())
                    };
                    double before = curve.GetLength();
                    curve = EdgeClusters.ExtendAlongEdges(along, tracking, tol.TrackingStep(reference.Minor), tol.MaxTrackFraction * before, main, tol);
                    result.Messages.Add($"Followed the parallel edges {curve.GetLength() - before:0} further.");
                }
            }

            curve = FitToExtents(curve, vertices, options.End, options.EndFitLength, tol.RunOutFraction);

            int passes = parallel == null ? 0 : Math.Max(0, options.Refinements);
            Beam beam = null;
            for (int pass = 0; pass <= passes; ++pass)
            {
                bool last = pass == passes;
                var sections = TypicalSections(curve, edges, parallel, reference, options, tol);
                if (sections.Count == 0) { result.Messages.Add("No typical sections along the fitted centreline."); return result; }

                beam = new Beam
                {
                    Centreline = curve,
                    Width = sections.Max(s => s.Width),
                    Height = sections.Max(s => s.Height),
                };
                beam.Orientation = Orientation(curve, sections, last && options.SimplifyOrientation, options.PlanarAngleTolerance, last ? result.Messages : new List<string>());
                result.Sections = sections;

                if (last) break;

                // A large correction is an error (e.g. a joint cut an edge away). Skip it.
                var corrected = FitToExtents(EdgeClusters.Recentre(beam, tracking, main, tol), vertices, options.End, options.EndFitLength, tol.RunOutFraction);
                double moved = MaxDistance(corrected, curve);
                double limit = tol.JumpFraction * Math.Min(beam.Width, beam.Height);
                if (moved > limit)
                {
                    result.Messages.Add($"Edge correction skipped: it would move the centreline by {moved:0.#} (more than {limit:0.#}).");
                    passes = pass + 1;
                    continue;
                }
                curve = corrected;
            }

            result.Beam = beam;
            return result;
        }

        /// <summary>
        /// Initial centreline points. Section centres across the box's main axis, then section
        /// centres normal to the spline through them, repeated. Null if fewer than four sections.
        /// </summary>
        static Point3d[] WalkSections(EdgeSet edges, OrientedBounds bounds, Point3d start, Point3d end, int count, int passes, double tolerance, ReconstructionTolerances tol)
        {
            var hits = new List<Point3d>();
            var xaxis = bounds.Plane.XAxis;

            // Sections near the ends can catch the end cuts. Stay away from the ends.
            double margin = tol.WalkMargin;
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

            // Remove centres of sections that are not typical (a scarf or lap moves them)
            var walked = new InterpolatingSpline(points);
            if (TryReference(walked, edges, count, tol, out Section reference))
            {
                var typical = new List<Point3d>(count);
                for (int i = 0; i < count; ++i)
                {
                    walked.Evaluate(i / (double)(count - 1), out Point3d p, out Vector3d d);
                    if (edges.TrySection(p, d, out Section s) && Typicality(s, reference, tolerance) >= tol.TypicalWeight)
                        typical.Add(s.Centre);
                }
                if (typical.Count >= 4) points = typical;
            }

            var curve = new InterpolatingSpline(points).ToNurbsCurve();
            double run = start.DistanceTo(end) * margin;
            var extended = CurveEnds.Extend(curve, run, EndExtension.Trend);
            return extended.DivideByCount(count - 1, true).Select(t => extended.PointAt(t)).ToArray();
        }

        /// <summary>
        /// The orientation from section frames. See the overload.
        /// </summary>
        internal static CrossSectionOrientation Orientation(Curve centreline, IList<Section> sections, bool simplify, double angleTolerance, List<string> messages)
            => Orientation(centreline, sections.Select(s => s.Plane).ToList(), simplify, angleTolerance, messages);

        /// <summary>
        /// A VectorListOrientation from the Y-axes of frames along a centreline, or the equal
        /// PlanarOrientation.
        /// </summary>
        internal static CrossSectionOrientation Orientation(Curve centreline, IList<Plane> frames, bool simplify, double angleTolerance, List<string> messages)
        {
            var parameters = new List<double>();
            var vectors = new List<Vector3d>();
            foreach (var f in frames)
            {
                centreline.ClosestPoint(f.Origin, out double t);
                parameters.Add(t);
                vectors.Add(f.YAxis);
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
        /// Extends a curve past both ends, then trims it to the extent of the points along it.
        /// Closest-point queries run one at a time: parallel queries on one curve gave results
        /// that changed between runs.
        /// </summary>
        internal static Curve FitToExtents(Curve curve, IList<Point3d> points, EndExtension style = EndExtension.Trend, double fitLength = 0, double runOut = 0.25)
        {
            var extended = CurveEnds.Extend(curve, runOut * curve.GetLength(), style, fitLength);

            double tMin = double.MaxValue, tMax = double.MinValue;
            foreach (var p in points)
                if (extended.ClosestPoint(p, out double t)) { tMin = Math.Min(tMin, t); tMax = Math.Max(tMax, t); }

            if (tMax <= tMin) return curve;
            var trimmed = extended.Trim(tMin, tMax) ?? extended;
            trimmed.Domain = new Interval(0, 1);
            return trimmed;
        }

        /// <summary>
        /// The distinct vertices of the mesh. An unwelded mesh (STL) repeats each vertex.
        /// </summary>
        static Point3d[] ExtentPoints(Mesh mesh) => new HashSet<Point3d>(mesh.Vertices.ToPoint3dArray()).ToArray();

        /// <summary>
        /// The typical section: median extents of the sections along the middle of a curve.
        /// </summary>
        static bool TryReference(InterpolatingSpline spline, EdgeSet edges, int samples, ReconstructionTolerances tol, out Section reference)
        {
            reference = default;
            var majors = new List<double>();
            var minors = new List<double>();
            foreach (var t in Sampling.Linspace(tol.ReferenceStart, tol.ReferenceEnd, Math.Max(3, samples)))
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
        /// Weight of a section from its difference in size from the reference. Same size: 1.
        /// At the tolerance: 1/2.
        /// </summary>
        static double Typicality(Section section, Section reference, double tolerance)
        {
            double r = section.DifferenceFrom(reference) / Math.Max(tolerance, 1e-6);
            return 1.0 / (1.0 + r * r * r * r);
        }

        /// <summary>
        /// Weight of each fit sample along the spline. No section: 0.
        /// </summary>
        static double[] Weights(Point3d[] points, EdgeSet edges, double[] tt, Section reference, double tolerance, ReconstructionTolerances tol)
        {
            var spline = new InterpolatingSpline(points);
            var weights = new double[tt.Length];
            Parallel.For(0, tt.Length, i =>
            {
                spline.Evaluate(tt[i], out Point3d p, out Vector3d d);
                // Remove sections far from the reference: samples at the edge of a joint
                double w = edges.TrySection(p, d, out Section s) ? Typicality(s, reference, tolerance) : 0;
                weights[i] = w >= tol.MinFitWeight ? w : 0;
            });
            return weights;
        }

        /// <summary>
        /// Typical sections along a curve, aligned to the parallel edges, with consistent frames.
        /// If no section is typical: all sections.
        /// </summary>
        static List<Section> TypicalSections(Curve curve, EdgeSet edges, EdgeSet parallel, Section reference, MeshBeamOptions options, ReconstructionTolerances tol)
        {
            var all = new List<Section>();
            var typical = new List<Section>();
            foreach (var t in Sampling.Linspace(options.SectionRange.Min, options.SectionRange.Max, options.SectionSamples))
            {
                double u = curve.Domain.ParameterAt(t);
                if (!edges.TrySection(curve.PointAt(u), curve.TangentAt(u), out Section s)) continue;
                if (parallel != null) EdgeClusters.AlignToEdges(ref s, parallel, tol);

                all.Add(s);
                if (Typicality(s, reference, options.TypicalTolerance) >= tol.TypicalWeight) typical.Add(s);
            }

            var sections = typical.Count > 0 ? typical : all;
            Sections.MakeConsistent(sections, Sections.PreferredUp(curve));
            return sections;
        }

        /// <summary>
        /// Fit error of the spline through the points. Sum over the samples of
        /// weight x distance to the section centre x ErrorPerMillimetre. A sample without a
        /// section adds weight x MissingSectionError. Plus smoothness x the curvature penalty.
        /// </summary>
        static double Error(Point3d[] points, EdgeSet edges, double[] tt, double[] weights, double smoothness, ReconstructionTolerances tol)
        {
            InterpolatingSpline spline;
            try { spline = new InterpolatingSpline(points); }
            catch (InvalidOperationException) { return tol.MissingSectionError * tt.Length; }

            double perUnit = tol.ErrorPerMillimetre / Globals.FromMillimeters(1);
            var hits = new List<Point3d>();
            double error = 0;
            for (int i = 0; i < tt.Length; ++i)
            {
                if (weights[i] <= 0) continue;
                spline.Evaluate(tt[i], out Point3d p, out Vector3d d);
                if (edges.TryCentre(p, d, hits, out Point3d centre))
                    error += weights[i] * centre.DistanceTo(p) * perUnit;
                else
                    error += weights[i] * tol.MissingSectionError;
            }

            if (smoothness > 0) error += smoothness * CurvaturePenalty(spline, Math.Max(40, 2 * points.Length));
            return error;
        }

        /// <summary>
        /// One round of the Lbfgs fit.
        /// </summary>
        static Point3d[] FitLbfgs(Point3d[] points, EdgeSet edges, double[] fitT, double[] weights, MeshBeamOptions options, ReconstructionTolerances tol, out int iterations)
        {
            var fit = new Lbfgs { MaxIterations = options.Iterations, FunctionTolerance = options.Tolerance }
                .Minimize(x => Error(ToPoints(x), edges, fitT, weights, options.Smoothness, tol), ToArray(points));
            iterations = fit.Iterations;
            return ToPoints(fit.X);
        }

        /// <summary>
        /// One round of the LeastSquares fit. Each iteration finds the section centres at the
        /// samples and moves the control points to them by weighted least squares. Points move
        /// across the curve only. A step must lower the fit error: it is halved up to four times.
        /// Stops when no point moves more than LeastSquaresConvergence. Does not use Smoothness.
        /// </summary>
        static Point3d[] FitLeastSquares(Point3d[] points, EdgeSet edges, double[] fitT, double[] weights, int maxIterations, ReconstructionTolerances tol, out int iterations)
        {
            var centres = new Point3d[fitT.Length];
            var w = new double[fitT.Length];
            double error = Error(points, edges, fitT, weights, 0, tol);
            iterations = 0;
            while (iterations < Math.Max(1, maxIterations))
            {
                InterpolatingSpline spline;
                try { spline = new InterpolatingSpline(points); }
                catch (InvalidOperationException) { break; }

                Parallel.For(0, fitT.Length, () => new List<Point3d>(), (j, _, hits) =>
                {
                    w[j] = 0;
                    if (weights[j] <= 0) return hits;
                    spline.Evaluate(fitT[j], out Point3d p, out Vector3d d);
                    if (edges.TryCentre(p, d, hits, out Point3d c)) { centres[j] = c; w[j] = weights[j]; }
                    return hits;
                }, _ => { });
                if (w.Count(v => v > 0) < 4) break;

                Point3d[] target;
                try { target = spline.FitPoints(fitT, centres, w, tol.LeastSquaresDamping); }
                catch (InvalidOperationException) { break; }
                ++iterations;

                // Move across the curve only
                var delta = new Vector3d[points.Length];
                for (int i = 0; i < points.Length; ++i)
                {
                    var tangent = points[Math.Min(i + 1, points.Length - 1)] - points[Math.Max(i - 1, 0)];
                    delta[i] = target[i] - points[i];
                    if (tangent.Unitize()) delta[i] -= tangent * (delta[i] * tangent);
                }

                bool accepted = false;
                double largest = 0;
                for (double step = 1.0; step >= 1.0 / 16 && !accepted; step *= 0.5)
                {
                    var moved = new Point3d[points.Length];
                    largest = 0;
                    for (int i = 0; i < points.Length; ++i)
                    {
                        moved[i] = points[i] + delta[i] * step;
                        largest = Math.Max(largest, delta[i].Length * step);
                    }
                    double e = Error(moved, edges, fitT, weights, 0, tol);
                    if (e < error) { points = moved; error = e; accepted = true; }
                }
                if (!accepted || largest <= tol.LeastSquaresConvergence) break;
            }
            return points;
        }

        /// <summary>
        /// Change of curvature along a spline: sum of the squared changes of the curvature vector
        /// between steps, each times the length (unitless), times 100. Zero for a line or an arc.
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

            // Scale by the step count: the result does not depend on it
            return 100.0 * sum * steps / 40.0;
        }

        /// <summary>
        /// Largest distance from points on a to b.
        /// </summary>
        static double MaxDistance(Curve a, Curve b)
        {
            double max = 0;
            foreach (var t in a.DivideByCount(50, true))
            {
                var p = a.PointAt(t);
                if (b.ClosestPoint(p, out double u)) max = Math.Max(max, p.DistanceTo(b.PointAt(u)));
            }
            return max;
        }

        /// <summary>
        /// N points on the arc through three points. If the arc is shorter than length, it is
        /// extended equally at both ends. Points in a line give a straight line.
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

            // Go from p1 to p3 through p2
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
