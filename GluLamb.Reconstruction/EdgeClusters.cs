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
    /// <summary>
    /// Options for EdgeClusters.FindLargest.
    /// </summary>
    public class EdgeClusterOptions
    {
        /// <summary>Ignore edges shorter than this fraction of the longest edge.</summary>
        public double MinEdgeRatio = 0.05;

        /// <summary>Number of points that each edge is resampled to.</summary>
        public int EdgeSamples = 100;

        /// <summary>Distance between two edges: AngleWeight x mean angle (radians) plus
        /// DistanceWeight x standard deviation of the distances between their points.</summary>
        public double AngleWeight = 1.0;
        public double DistanceWeight = 1.0 / Globals.FromMillimeters(100);

        /// <summary>Edges nearer than this distance go into one cluster.</summary>
        public double ClusterThreshold = 0.25;
    }

    /// <summary>
    /// The longest run of parallel edges of a beam, e.g. the long edges that no joint cuts.
    /// </summary>
    public class ParallelEdges
    {
        /// <summary>The compared edges (resampled), their cluster labels, and the edges of the
        /// longest cluster.</summary>
        public List<Polyline> Edges = new List<Polyline>();
        public int[] Labels;
        public List<Polyline> Cluster = new List<Polyline>();

        /// <summary>The longest edge of the cluster.</summary>
        public Curve LongestEdge;

        public string Summary;
    }

    public static class EdgeClusters
    {
        /// <summary>
        /// Groups the long edges by how parallel they are (average linkage). Gives the group with
        /// the most length.
        /// </summary>
        public static ParallelEdges FindLargest(IList<Curve> curves, EdgeClusterOptions options)
        {
            if (curves.Count == 0) return null;
            var result = new ParallelEdges();

            var lengths = curves.Select(c => c.GetLength()).ToArray();
            double minLength = options.MinEdgeRatio * lengths.Max();
            var kept = Enumerable.Range(0, curves.Count).Where(i => lengths[i] >= minLength).ToArray();

            var samples = kept.Select(i => curves[i].DivideByCount(options.EdgeSamples - 1, true).Select(t => curves[i].PointAt(t)).ToArray()).ToArray();
            result.Edges = samples.Select(s => new Polyline(s)).ToList();

            int n = samples.Length;
            var distances = new double[n, n];
            Parallel.For(0, n, i =>
            {
                for (int j = i + 1; j < n; ++j)
                {
                    double d = Math.Min(
                        Parallelness(samples[i], samples[j], false, options),
                        Parallelness(samples[i], samples[j], true, options));
                    distances[i, j] = distances[j, i] = d;
                }
            });

            var labels = AverageLinkage.Cluster(distances, options.ClusterThreshold);
            result.Labels = labels;

            var clusterLength = new Dictionary<int, double>();
            for (int i = 0; i < n; ++i)
                clusterLength[labels[i]] = clusterLength.GetValueOrDefault(labels[i]) + lengths[kept[i]];
            int best = clusterLength.OrderByDescending(kv => kv.Value).First().Key;

            var members = Enumerable.Range(0, n).Where(i => labels[i] == best).ToList();
            result.Cluster = members.Select(i => result.Edges[i]).ToList();
            result.Summary = $"{n} edges in {clusterLength.Count} clusters; the centreline follows {members.Count} edges.";

            // The longest edge has a constant place in the section, so it has the beam's shape.
            // A point-by-point mean of the edges mixes places along the beam where edges have
            // different ends (skew or shaped ends).
            int longest = members.OrderByDescending(i => lengths[kept[i]]).First();
            result.LongestEdge = curves[kept[longest]].DuplicateCurve();
            return result;
        }

        /// <summary>
        /// Corrects a centreline from the cluster's edges. On a prismatic beam each edge has a
        /// constant place in the section. That place is measured at the stations where all edges
        /// are cut. At each station the cut edges then place the centreline. At a skew end, where
        /// one edge has ended, the others still place it.
        /// </summary>
        public static Curve Recentre(Beam beam, EdgeSet clusterEdges, int main = 0, ReconstructionTolerances tol = null)
        {
            tol ??= new ReconstructionTolerances();
            var curve = beam.Centreline;
            int count = tol.StationCount(curve.GetLength());
            Stations(beam, clusterEdges, count, out Plane[] planes, out var local, out var offsets, main, tol);
            if (offsets.Count < 2) return curve;

            // The place of the centreline at each station, from at least two cut edges
            var correction = new Vector3d?[count];
            for (int i = 0; i < count; ++i)
                if (local[i] != null && TryPlace(local[i], offsets, out double dx, out double dy, out _, out _, tol.ResidualTolerance(Math.Min(beam.Width, beam.Height)), main))
                    correction[i] = new Vector3d(dx, dy, 0);
            if (correction.Count(c => c.HasValue) < 4) return curve;

            // A station without edges takes the correction of the nearest station with edges
            var points = new Point3d[count];
            for (int i = 0; i < count; ++i)
            {
                int j = i;
                for (int k = 1; !correction[j].HasValue; ++k)
                {
                    if (i - k >= 0 && correction[i - k].HasValue) j = i - k;
                    else if (i + k < count && correction[i + k].HasValue) j = i + k;
                }
                points[i] = planes[i].PointAt(correction[j].Value.X, correction[j].Value.Y);
            }

            return new InterpolatingSpline(points).ToNurbsCurve();
        }

        /// <summary>
        /// Extends a centreline past its ends along the cluster's edges, step by step. Each step
        /// goes along the last direction, cuts the edges, and puts the next point where the cut
        /// edges place the centreline. Stops when fewer than two edges remain (beam end or joint).
        /// Assumes nothing about the curvature.
        /// </summary>
        /// <param name="beam">Beam along the known part of the centreline.</param>
        /// <param name="clusterEdges">The longest run of parallel edges.</param>
        /// <param name="step">Length of each step.</param>
        /// <param name="maxLength">Maximum distance at each end.</param>
        public static Curve ExtendAlongEdges(Beam beam, EdgeSet clusterEdges, double step, double maxLength, int main = 0, ReconstructionTolerances tol = null)
        {
            tol ??= new ReconstructionTolerances();
            var curve = beam.Centreline;
            double length = curve.GetLength();
            int count = tol.StationCount(length);
            Stations(beam, clusterEdges, count, out _, out _, out var offsets, main, tol);
            if (offsets.Count < 2) return curve;

            // Start inside the known curve. Its ends are the least reliable part of the fit.
            double reach = Math.Sqrt(beam.Width * beam.Width + beam.Height * beam.Height);
            double section = Math.Min(beam.Width, beam.Height);
            double backoff = Math.Min(0.1 * length, 10 * step);
            curve.LengthParameter(backoff, out double tStart);
            curve.LengthParameter(length - backoff, out double tEnd);

            var start = Track(beam, clusterEdges, offsets, tStart, true, step, maxLength + backoff, reach, section, main, tol);
            var end = Track(beam, clusterEdges, offsets, tEnd, false, step, maxLength + backoff, reach, section, main, tol);
            if (start.Count == 0 && end.Count == 0) return curve;

            // The known curve between the start points, sampled at the step, plus the tracked points
            double a = start.Count > 0 ? tStart : curve.Domain.Min, b = end.Count > 0 ? tEnd : curve.Domain.Max;
            var middle = curve.Trim(a, b) ?? curve;
            int n = Math.Max(4, (int)Math.Ceiling(middle.GetLength() / step) + 1);
            var points = new List<Point3d>();
            // Keep each third tracked point (and the last). The spline then ignores the scatter of single steps.
            List<Point3d> Thin(List<Point3d> p) => p.Where((_, i) => (i + 1) % 3 == 0 || i == p.Count - 1).ToList();
            var startPoints = Thin(start);
            for (int i = startPoints.Count - 1; i >= 0; --i) points.Add(startPoints[i]);
            points.AddRange(middle.DivideByCount(n - 1, true).Select(t => middle.PointAt(t)));
            points.AddRange(Thin(end));
            return new InterpolatingSpline(points).ToNurbsCurve();
        }

        static List<Point3d> Track(Beam beam, EdgeSet clusterEdges, Dictionary<int, Point3d> offsets, double t0, bool atStart, double step, double maxLength, double reach, double section, int main, ReconstructionTolerances tol)
        {
            var curve = beam.Centreline;
            var frame = beam.GetPlane(t0);
            var centre = frame.Origin;
            var direction = curve.TangentAt(t0);
            if (atStart) direction.Reverse();

            var hits = new List<Point3d>();
            var indices = new List<int>();
            var tracked = new List<Point3d>();
            double residualTolerance = tol.ResidualTolerance(section);

            for (double gone = 0; gone < maxLength; gone += step)
            {
                // Estimate along the last direction. Then cut square to the step from the last point. Two passes.
                var next = centre + direction * step;
                var normal = direction;
                bool found = false;
                for (int pass = 0; pass < 2; ++pass)
                {
                    var plane = Transported(frame, next, atStart ? -normal : normal);
                    clusterEdges.Cut(plane.Origin, plane.ZAxis, hits, indices);
                    var local = new Dictionary<int, Point3d>();
                    for (int k = 0; k < hits.Count; ++k)
                        if (hits[k].DistanceTo(next) <= reach && offsets.ContainsKey(indices[k]))
                        {
                            plane.RemapToPlaneSpace(hits[k], out Point3d p);
                            local[indices[k]] = p;
                        }

                    // Fit the cut edges to their usual places. This gives the centreline's place and the frame's turn.
                    if (!TryPlace(local, offsets, out double dx, out double dy, out double angle, out double residual, residualTolerance, main)) { found = false; break; }

                    // On a real edge the point is near the estimate and the edges keep their places.
                    // If not, the plane cut something else.
                    if (Math.Sqrt(dx * dx + dy * dy) > tol.JumpFraction * section || residual > residualTolerance) { found = false; break; }

                    plane.Rotate(angle, plane.ZAxis);
                    next = plane.PointAt(dx * Math.Cos(-angle) - dy * Math.Sin(-angle), dx * Math.Sin(-angle) + dy * Math.Cos(-angle));
                    normal = next - centre;
                    if (!normal.Unitize()) { found = false; break; }
                    frame = plane;
                    found = true;
                }
                if (!found) break;

                direction = next - centre;
                direction.Unitize();
                frame.Origin = next;
                centre = next;
                tracked.Add(next);
            }
            return tracked;
        }

        /// <summary>
        /// Moves a frame to a new origin and turns it (smallest rotation) so that its Z-axis is
        /// the given direction: parallel transport.
        /// </summary>
        static Plane Transported(Plane frame, Point3d origin, Vector3d z)
        {
            z.Unitize();
            var plane = frame;
            var rotation = Transform.Rotation(frame.ZAxis, z, frame.Origin);
            plane.Transform(rotation);
            plane.Origin = origin;
            return plane;
        }

        /// <summary>
        /// The place of the centreline in a station's frame, from the cut edges. Fits their places
        /// to their usual places with a turn and a move (2D rigid fit), so a small turn of the
        /// frame does not move the centreline. Removes an edge that is outside the tolerance
        /// (e.g. a joint cut it away) and fits again. Needs two edges.
        /// </summary>
        /// <param name="dx">The centreline's place in the station's frame.</param>
        /// <param name="angle">Turn of the edges from their usual places (radians).</param>
        /// <param name="residual">Largest distance of an edge from its fitted place.</param>
        static bool TryPlace(Dictionary<int, Point3d> local, Dictionary<int, Point3d> offsets, out double dx, out double dy, out double angle, out double residual, double tolerance = double.MaxValue, int main = 0)
        {
            dx = dy = angle = residual = 0;

            // Use the main edges alone if two or more are cut. If not, add the helping edges.
            var usable = local.Where(kv => offsets.ContainsKey(kv.Key)).ToList();
            if (main > 0 && usable.Count(kv => kv.Key < main) >= 2) usable = usable.Where(kv => kv.Key < main).ToList();
            var pairs = usable.Select(kv => (Q: kv.Value, O: offsets[kv.Key])).ToList();

            while (pairs.Count >= 2)
            {
                double qx = pairs.Average(p => p.Q.X), qy = pairs.Average(p => p.Q.Y);
                double ox = pairs.Average(p => p.O.X), oy = pairs.Average(p => p.O.Y);
                double sin = 0, cos = 0;
                foreach (var (q, o) in pairs)
                {
                    double ax = o.X - ox, ay = o.Y - oy, bx = q.X - qx, by = q.Y - qy;
                    cos += ax * bx + ay * by;
                    sin += ax * by - ay * bx;
                }
                angle = Math.Atan2(sin, cos);
                double c = Math.Cos(angle), s = Math.Sin(angle);

                // q = R o + t. The centreline is at o = 0, so it is at t.
                dx = qx - (c * ox - s * oy);
                dy = qy - (s * ox + c * oy);

                residual = 0;
                int worst = -1;
                for (int i = 0; i < pairs.Count; ++i)
                {
                    var (q, o) = pairs[i];
                    double ex = c * o.X - s * o.Y + dx - q.X, ey = s * o.X + c * o.Y + dy - q.Y;
                    double e = Math.Sqrt(ex * ex + ey * ey);
                    if (e > residual) { residual = e; worst = i; }
                }

                if (residual <= tolerance || pairs.Count == 2) return true;
                pairs.RemoveAt(worst);
            }
            return false;
        }

        /// <summary>
        /// Cuts the edges at stations along a beam. Gives each station's frame (aligned to the
        /// main edges where all are cut), the place of each cut edge in it, and the usual place of
        /// each edge: its median over the stations where all main edges are cut. Only edges with a
        /// constant place get a usual place.
        /// </summary>
        /// <param name="main">The first edges are the main edges (the longest run of parallel
        /// edges). The others help. Below 1: all edges are main.</param>
        static void Stations(Beam beam, EdgeSet clusterEdges, int count, out Plane[] planes, out Dictionary<int, Point3d>[] local, out Dictionary<int, Point3d> offsets, int main, ReconstructionTolerances tol)
        {
            var curve = beam.Centreline;
            int edges = clusterEdges.Count;
            if (main < 1 || main > edges) main = edges;
            var hits = new List<Point3d>();
            var indices = new List<int>();

            planes = new Plane[count];
            local = new Dictionary<int, Point3d>[count];
            var full = new bool[count];

            var tt = Sampling.Linspace(0, 1, count);
            double reach = Math.Sqrt(beam.Width * beam.Width + beam.Height * beam.Height);
            double section = Math.Min(beam.Width, beam.Height);
            for (int i = 0; i < count; ++i)
            {
                planes[i] = beam.GetPlane(curve.Domain.ParameterAt(tt[i]));
                clusterEdges.Cut(planes[i].Origin, planes[i].ZAxis, hits, indices);

                // Where a joint or an end cut an edge away, its nearest crossing is outside the section
                var origin = planes[i].Origin;
                var near = Enumerable.Range(0, hits.Count).Where(k => hits[k].DistanceTo(origin) <= reach).ToList();
                if (near.Count == 0) continue;

                // Measure in a frame aligned to the main edges. A small orientation error would
                // otherwise move the centreline by the edge distance times the angle.
                var mainHits = near.Where(k => indices[k] < main).Select(k => hits[k]).ToList();
                full[i] = mainHits.Count == main;
                if (full[i]) planes[i] = AlignPlane(planes[i], mainHits, tol.AlignAngle);

                local[i] = new Dictionary<int, Point3d>();
                foreach (int k in near)
                {
                    planes[i].RemapToPlaneSpace(hits[k], out Point3d p);
                    local[i][indices[k]] = p;
                }
            }

            offsets = new Dictionary<int, Point3d>();
            for (int e = 0; e < edges; ++e)
            {
                var xs = new List<double>(); var ys = new List<double>();
                for (int i = 0; i < count; ++i)
                    if (full[i] && local[i].TryGetValue(e, out Point3d p)) { xs.Add(p.X); ys.Add(p.Y); }
                if (xs.Count < 3) continue;
                xs.Sort(); ys.Sort();

                // A helping edge must keep its place: the middle half of its places within the tolerance
                int lo = xs.Count / 4, hi = (3 * xs.Count) / 4;
                double spread = Math.Max(xs[hi] - xs[lo], ys[hi] - ys[lo]);
                if (e >= main && spread > tol.ResidualTolerance(section)) continue;

                offsets[e] = new Point3d(xs[xs.Count / 2], ys[ys.Count / 2], 0);
            }
        }

        /// <summary>
        /// Turns a section's frame about Z to align it with the cluster's edges. The edges are
        /// exact. The section's own axes lean when the section is oblique or cut by a joint.
        /// </summary>
        public static void AlignToEdges(ref Section section, EdgeSet clusterEdges, ReconstructionTolerances tol = null)
        {
            var hits = new List<Point3d>();
            if (clusterEdges.Cut(section.Plane.Origin, section.Plane.ZAxis, hits) < 2) return;
            section.Plane = AlignPlane(section.Plane, hits, (tol ?? new ReconstructionTolerances()).AlignAngle);
        }

        /// <summary>
        /// Turns a plane about Z so that the lines between the points lie along its X- and Y-axes.
        /// The turn is the length-weighted mean angle between each line and its nearer axis, over
        /// the lines within maxAngle of an axis. Diagonals and bevel lines are outside that angle.
        /// </summary>
        static Plane AlignPlane(Plane plane, List<Point3d> hits, double maxAngle)
        {
            if (hits.Count < 2) return plane;

            double sum = 0, weight = 0;
            for (int i = 0; i < hits.Count; ++i)
                for (int j = i + 1; j < hits.Count; ++j)
                {
                    var d = hits[j] - hits[i];
                    double x = d * plane.XAxis, y = d * plane.YAxis;
                    double length = Math.Sqrt(x * x + y * y);
                    if (length <= 0) continue;

                    // Angle from the X-axis, folded into (-45, 45] degrees about the nearer axis
                    double angle = Math.Atan2(y, x);
                    angle -= Math.Round(angle / (Math.PI / 2)) * (Math.PI / 2);
                    if (Math.Abs(angle) > maxAngle) continue;

                    sum += angle * length;
                    weight += length;
                }
            if (weight <= 0) return plane;

            var turned = plane;
            turned.Rotate(sum / weight, plane.ZAxis);
            return turned;
        }

        /// <summary>
        /// How far two resampled edges are from parallel: AngleWeight x mean angle between their
        /// segments plus DistanceWeight x spread of the distances between their points.
        /// reverse: compare b in the other direction.
        /// </summary>
        static double Parallelness(Point3d[] a, Point3d[] b, bool reverse, EdgeClusterOptions options)
        {
            int n = Math.Min(a.Length, b.Length);
            Point3d B(int i) => b[reverse ? b.Length - 1 - i : i];

            double angle = 0;
            for (int i = 0; i < n - 1; ++i)
            {
                var ta = a[i + 1] - a[i];
                var tb = B(i + 1) - B(i);
                double la = ta.Length, lb = tb.Length;
                if (la <= 0 || lb <= 0) continue;
                angle += Math.Acos(Math.Clamp(ta * tb / (la * lb), -1.0, 1.0));
            }
            angle /= Math.Max(1, n - 1);

            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; ++i)
            {
                double d = a[i].DistanceTo(B(i));
                sum += d; sumSq += d * d;
            }
            double meanD = sum / n;
            double std = Math.Sqrt(Math.Max(0, sumSq / n - meanD * meanD));

            return options.AngleWeight * angle + options.DistanceWeight * std;
        }
    }
}
