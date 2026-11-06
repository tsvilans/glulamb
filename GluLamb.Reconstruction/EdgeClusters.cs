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
        /// <summary>Edges shorter than this fraction of the longest edge are left out.</summary>
        public double MinEdgeRatio = 0.05;

        /// <summary>Points each edge is resampled to for comparing edges.</summary>
        public int EdgeSamples = 100;

        /// <summary>Distance between two edges: AngleWeight x their mean angle (radians) plus
        /// DistanceWeight x the standard deviation of the distances between their points.</summary>
        public double AngleWeight = 1.0;
        public double DistanceWeight = 1.0 / Globals.FromMillimeters(100);

        /// <summary>Edges closer than this (in the distance above) are clustered together.</summary>
        public double ClusterThreshold = 0.25;
    }

    /// <summary>
    /// The longest run of parallel edges of a beam, e.g. the long edges that its joints don't cut.
    /// </summary>
    public class ParallelEdges
    {
        /// <summary>The edges compared (resampled), their cluster labels, and the edges of the
        /// longest cluster.</summary>
        public List<Polyline> Edges = new List<Polyline>();
        public int[] Labels;
        public List<Polyline> Cluster = new List<Polyline>();

        /// <summary>Mean of the cluster's edges, all running the same way.</summary>
        public Curve MeanCurve;

        public string Summary;
    }

    public static class EdgeClusters
    {
        /// <summary>
        /// Group the long edges by how parallel they are (average linkage) and return the group
        /// with the most length, with the mean of its edges. Port of parallel.py, which compared
        /// edges only one way round and passed the square distance matrix to linkage().
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

            // The longest edge of the cluster: it runs at a steady place in the section, so it has
            // the beam's shape, and the place is corrected later. (Averaging the edges point by
            // point mixes different places along the beam where the edges start and end at
            // different places, e.g. at skew or shaped ends.)
            int longest = members.OrderByDescending(i => lengths[kept[i]]).First();
            result.MeanCurve = curves[kept[longest]].DuplicateCurve();
            return result;
        }

        /// <summary>
        /// A better centreline from the cluster's edges. Each edge keeps the same place in the
        /// section all along a prismatic beam: that place is measured at the stations where all of
        /// the edges are crossed, and at every station the centreline goes where the edges that are
        /// crossed there put it. Near a skew end cut, where one edge has ended, the others still
        /// place the centreline. (The mean of the edges by point count drifts where the edges have
        /// different lengths, e.g. on a twisted beam; crossings compare them at the same section.)
        /// </summary>
        public static Curve Recentre(Beam beam, EdgeSet clusterEdges, int main = 0)
        {
            var curve = beam.Centreline;
            int count = Math.Clamp((int)(curve.GetLength() / Globals.FromMillimeters(100)) + 1, 10, 200);
            Stations(beam, clusterEdges, count, out Plane[] planes, out var local, out var offsets, main);
            if (offsets.Count < 2) return curve;

            // Where the edges crossed at each station put the centreline (relative to the station),
            // from at least two of them
            var correction = new Vector3d?[count];
            for (int i = 0; i < count; ++i)
                if (local[i] != null && TryPlace(local[i], offsets, out double dx, out double dy, out _, out _, 0.02 * Math.Min(beam.Width, beam.Height) + Globals.FromMillimeters(1), main))
                    correction[i] = new Vector3d(dx, dy, 0);
            if (correction.Count(c => c.HasValue) < 4) return curve;

            // Stations no edges reach take the correction of the nearest one they do
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
        /// Extend a centreline past its ends by following the cluster's edges, one step at a time:
        /// each step goes on along the last direction, cuts the edges there, and puts the next
        /// point where the edges that are still there place the centreline. It stops where fewer
        /// than two edges are left (the end of the beam, or a joint). Nothing is assumed about the
        /// curvature: the edges carry it.
        /// </summary>
        /// <param name="beam">Beam along the part of the centreline that is known.</param>
        /// <param name="clusterEdges">The longest run of parallel edges.</param>
        /// <param name="step">Length of each step.</param>
        /// <param name="maxLength">How far to go at each end at most.</param>
        public static Curve ExtendAlongEdges(Beam beam, EdgeSet clusterEdges, double step, double maxLength, int main = 0)
        {
            var curve = beam.Centreline;
            double length = curve.GetLength();
            int count = Math.Clamp((int)(length / Globals.FromMillimeters(100)) + 1, 10, 200);
            Stations(beam, clusterEdges, count, out _, out _, out var offsets, main);
            if (offsets.Count < 2) return curve;

            // Start a little inside the known curve, where it is surest (its very ends are where the
            // fit had the least to go by), and let the edges take over from there
            double reach = Math.Sqrt(beam.Width * beam.Width + beam.Height * beam.Height);
            double section = Math.Min(beam.Width, beam.Height);
            double backoff = Math.Min(0.1 * length, 10 * step);
            curve.LengthParameter(backoff, out double tStart);
            curve.LengthParameter(length - backoff, out double tEnd);

            var start = Track(beam, clusterEdges, offsets, tStart, true, step, maxLength + backoff, reach, section, main);
            var end = Track(beam, clusterEdges, offsets, tEnd, false, step, maxLength + backoff, reach, section, main);
            if (start.Count == 0 && end.Count == 0) return curve;

            // The known curve between the starting points (or all of it at an end with nothing
            // tracked), sampled at the step, with the tracked points at either end
            double a = start.Count > 0 ? tStart : curve.Domain.Min, b = end.Count > 0 ? tEnd : curve.Domain.Max;
            var middle = curve.Trim(a, b) ?? curve;
            int n = Math.Max(4, (int)Math.Ceiling(middle.GetLength() / step) + 1);
            var points = new List<Point3d>();
            // Tracked points about every three steps (and the last), so the spline follows the edges
            // without picking up each step's small scatter as curvature
            List<Point3d> Thin(List<Point3d> p) => p.Where((_, i) => (i + 1) % 3 == 0 || i == p.Count - 1).ToList();
            var startPoints = Thin(start);
            for (int i = startPoints.Count - 1; i >= 0; --i) points.Add(startPoints[i]);
            points.AddRange(middle.DivideByCount(n - 1, true).Select(t => middle.PointAt(t)));
            points.AddRange(Thin(end));
            return new InterpolatingSpline(points).ToNurbsCurve();
        }

        static List<Point3d> Track(Beam beam, EdgeSet clusterEdges, Dictionary<int, Point3d> offsets, double t0, bool atStart, double step, double maxLength, double reach, double section, int main)
        {
            var curve = beam.Centreline;
            var frame = beam.GetPlane(t0);
            var centre = frame.Origin;
            var direction = curve.TangentAt(t0);
            if (atStart) direction.Reverse();

            var hits = new List<Point3d>();
            var indices = new List<int>();
            var tracked = new List<Point3d>();
            int edges = clusterEdges.Count;

            for (double gone = 0; gone < maxLength; gone += step)
            {
                // Guess along the last direction, then take the section there square to the step
                // from the last point, twice over
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

                    // The crossed edges' places fitted to their usual places: where the centreline
                    // is in this section, and how far the frame has turned about the centreline
                    if (!TryPlace(local, offsets, out double dx, out double dy, out double angle, out double residual, 0.02 * section + Globals.FromMillimeters(1), main)) { found = false; break; }

                    // Along a real edge the point lands close to the guess (the bend over one step)
                    // and the edges keep their places; otherwise the crossings are on something else
                    if (Math.Sqrt(dx * dx + dy * dy) > 0.25 * section || residual > 0.02 * section + Globals.FromMillimeters(1)) { found = false; break; }

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
        /// A frame moved to a new origin and turned (by the smallest rotation) so its Z-axis is the
        /// given direction: parallel transport along the centreline.
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
        /// Where the edges crossed at a station put the centreline, in the station's frame: their
        /// places fitted to their usual places by a turn about the centreline and a move (a 2D
        /// rigid fit, so a frame that has turned a little doesn't move the centreline). An edge
        /// that doesn't fit within the tolerance (e.g. one a joint has just cut away) is left out
        /// and the rest fitted again. Needs two.
        /// </summary>
        /// <param name="dx">Where the centreline is, in the station's frame.</param>
        /// <param name="angle">How far the edges are turned from their usual places (radians).</param>
        /// <param name="residual">Largest distance of an edge from its fitted place.</param>
        static bool TryPlace(Dictionary<int, Point3d> local, Dictionary<int, Point3d> offsets, out double dx, out double dy, out double angle, out double residual, double tolerance = double.MaxValue, int main = 0)
        {
            dx = dy = angle = residual = 0;

            // The main edges alone where two or more of them are there; the helping edges only
            // where they are needed
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

                // q = R o + t, so the centreline (where o = 0) is at t
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
        /// Cut the edges at stations along a beam: each station's frame (lined up with the main
        /// edges where all of them are crossed), each crossed edge's place in it, and each edge's
        /// usual place: the median over the stations where all the main edges are crossed, for the
        /// edges that keep a steady place there (edges along the beam; not, say, across a joint).
        /// </summary>
        /// <param name="main">The first this many edges are the main ones (the longest run of
        /// parallel edges); the rest only help. Below 1: all of them.</param>
        static void Stations(Beam beam, EdgeSet clusterEdges, int count, out Plane[] planes, out Dictionary<int, Point3d>[] local, out Dictionary<int, Point3d> offsets, int main = 0)
        {
            var curve = beam.Centreline;
            int edges = clusterEdges.Count;
            if (main < 1 || main > edges) main = edges;
            var hits = new List<Point3d>();
            var indices = new List<int>();

            planes = new Plane[count];
            local = new Dictionary<int, Point3d>[count];
            var full = new bool[count];

            var tt = MeshBeam.Linspace(0, 1, count);
            double reach = Math.Sqrt(beam.Width * beam.Width + beam.Height * beam.Height);
            double section = Math.Min(beam.Width, beam.Height);
            for (int i = 0; i < count; ++i)
            {
                planes[i] = beam.GetPlane(curve.Domain.ParameterAt(tt[i]));
                clusterEdges.Cut(planes[i].Origin, planes[i].ZAxis, hits, indices);

                // Where a joint or an end has cut an edge away, its nearest crossing is somewhere
                // else along it, outside the section
                var origin = planes[i].Origin;
                var near = Enumerable.Range(0, hits.Count).Where(k => hits[k].DistanceTo(origin) <= reach).ToList();
                if (near.Count == 0) continue;

                // Measure in a frame lined up with the main edges where they are all there: a small
                // error in the beam's orientation would otherwise move the centreline by the edges'
                // distance from it times the angle
                var mainHits = near.Where(k => indices[k] < main).Select(k => hits[k]).ToList();
                full[i] = mainHits.Count == main;
                if (full[i]) planes[i] = AlignPlane(planes[i], mainHits);

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

                // A helping edge has to keep its place (the middle half of its places within a
                // small fraction of the section)
                int lo = xs.Count / 4, hi = (3 * xs.Count) / 4;
                double spread = Math.Max(xs[hi] - xs[lo], ys[hi] - ys[lo]);
                if (e >= main && spread > 0.02 * section + Globals.FromMillimeters(1)) continue;

                offsets[e] = new Point3d(xs[xs.Count / 2], ys[ys.Count / 2], 0);
            }
        }

        /// <summary>
        /// Turn a section's frame about its Z-axis so that the line through the cluster's edges
        /// (e.g. two corners of one face) lies exactly along its X- or Y-axis, whichever is closer.
        /// The edges are exact, where the section's principal axes lean when the section is a
        /// little oblique or cut into by a joint.
        /// </summary>
        public static void AlignToEdges(ref Section section, EdgeSet clusterEdges)
        {
            var hits = new List<Point3d>();
            if (clusterEdges.Cut(section.Plane.Origin, section.Plane.ZAxis, hits) < 2) return;
            section.Plane = AlignPlane(section.Plane, hits);
        }

        /// <summary>
        /// Turn a section plane about its Z-axis so that the line through the points where it
        /// crosses the cluster's edges lies along its X- or Y-axis, whichever is closer.
        /// </summary>
        static Plane AlignPlane(Plane plane, List<Point3d> hits)
        {
            if (hits.Count < 2) return plane;

            Vector3d line;
            if (hits.Count == 2)
                line = hits[1] - hits[0];
            else
            {
                Pca.PrincipalAxes(hits, out _, out Vector3d[] axes);
                line = axes[0];
            }

            line -= plane.ZAxis * (line * plane.ZAxis);
            if (!line.Unitize()) return plane;

            // Only a small correction: on a section that isn't a rectangle (bevels, rebates) the
            // edges can lie along a face at an angle to the section's sides
            double closest = Math.Max(Math.Abs(line * plane.XAxis), Math.Abs(line * plane.YAxis));
            if (closest < Math.Cos(RhinoMath.ToRadians(10))) return plane;

            // Closest axis, keeping the frame's direction
            Vector3d y;
            if (Math.Abs(line * plane.XAxis) >= Math.Abs(line * plane.YAxis))
            {
                var x = line * plane.XAxis >= 0 ? line : -line;
                y = Vector3d.CrossProduct(plane.ZAxis, x);
            }
            else
                y = line * plane.YAxis >= 0 ? line : -line;

            return Utility.PlaneFromNormalAndYAxis(plane.Origin, plane.ZAxis, y);
        }

        /// <summary>
        /// How far two resampled edges are from running parallel: AngleWeight x the mean angle
        /// between their segments plus DistanceWeight x the spread of the distances between their
        /// points. Reversing b compares it running the other way.
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
