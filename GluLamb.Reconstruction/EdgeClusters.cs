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

            var reference = samples[members[0]];
            var mean = new Point3d[options.EdgeSamples];
            foreach (int i in members)
            {
                bool reverse = Parallelness(reference, samples[i], true, options) < Parallelness(reference, samples[i], false, options);
                for (int k = 0; k < mean.Length; ++k)
                    mean[k] += samples[i][reverse ? mean.Length - 1 - k : k];
            }
            for (int k = 0; k < mean.Length; ++k) mean[k] /= members.Count;

            result.MeanCurve = new InterpolatingSpline(mean).ToNurbsCurve();
            return result;
        }

        /// <summary>
        /// A better centreline from the cluster's edges: at stations along the beam, where its
        /// section plane crosses the edges, less their average position in the section. (The mean
        /// of the edges by point count drifts where the edges have different lengths, e.g. on a
        /// twisted beam; crossings compare the edges at the same section.)
        /// </summary>
        public static Curve Recentre(Beam beam, EdgeSet clusterEdges)
        {
            var curve = beam.Centreline;
            int count = Math.Clamp((int)(curve.GetLength() / Globals.FromMillimeters(200)), 10, 100);
            var hits = new List<Point3d>();
            var planes = new Plane[count];
            var local = new Point3d?[count];

            var tt = MeshBeam.Linspace(0, 1, count);
            double reach = Math.Sqrt(beam.Width * beam.Width + beam.Height * beam.Height);
            for (int i = 0; i < count; ++i)
            {
                planes[i] = beam.GetPlane(curve.Domain.ParameterAt(tt[i]));
                if (clusterEdges.Cut(planes[i].Origin, planes[i].ZAxis, hits) != clusterEdges.Count) continue;

                // Where a joint has cut one of the edges away, its nearest crossing is somewhere
                // else along it, outside the section
                var origin = planes[i].Origin;
                if (hits.Any(h => h.DistanceTo(origin) > reach)) continue;

                var mean = Point3d.Origin;
                foreach (var h in hits) mean += h;
                mean /= hits.Count;

                // Measure in a frame lined up with the edges themselves: a small error in the
                // beam's orientation would otherwise move the centreline by the edges' distance
                // from it times the angle
                planes[i] = AlignPlane(planes[i], hits);
                planes[i].RemapToPlaneSpace(mean, out Point3d p);
                local[i] = p;
            }

            var found = local.Where(p => p.HasValue).Select(p => p.Value).ToList();
            if (found.Count < 4) return curve;

            // The edges' position in the section is the same all along a prismatic beam: the
            // centreline moves by how far they are off it. Stations the edges don't reach (e.g. at
            // a joint near an end) take the correction of the nearest station they do.
            var xs = found.Select(p => p.X).OrderBy(x => x).ToList();
            var ys = found.Select(p => p.Y).OrderBy(y => y).ToList();
            double cx = xs[xs.Count / 2], cy = ys[ys.Count / 2];

            var points = new Point3d[count];
            for (int i = 0; i < count; ++i)
            {
                int j = i;
                for (int k = 1; !local[j].HasValue; ++k)
                {
                    if (i - k >= 0 && local[i - k].HasValue) j = i - k;
                    else if (i + k < count && local[i + k].HasValue) j = i + k;
                }
                points[i] = planes[i].PointAt(local[j].Value.X - cx, local[j].Value.Y - cy);
            }

            var recentred = new InterpolatingSpline(points).ToNurbsCurve();
            return recentred;
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
