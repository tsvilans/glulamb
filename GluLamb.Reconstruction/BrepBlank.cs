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
using Rhino.Geometry.Intersect;

using GluLamb.Blanks;
using GluLamb.Reconstruction.Numerics;

namespace GluLamb.Reconstruction
{
    public class BrepBlankOptions
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

        /// <summary>Number and range (0 to 1 along the centreline) of the sections that give the
        /// orientation.</summary>
        public int SectionSamples = 30;
        public Interval SectionRange = new Interval(0.02, 0.98);

        /// <summary>Passes after the first that start again from the centred centreline.</summary>
        public int Refinements = 2;

        /// <summary>Extra material on each side of the section and at each end.</summary>
        public double Allowance = 0;
        public double EndAllowance = 0;

        /// <summary>Use a PlanarOrientation when the section frames all lie in one plane.</summary>
        public bool SimplifyOrientation = true;
        public double PlanarAngleTolerance = RhinoMath.ToRadians(1);

        /// <summary>Tolerance for Brep and plane intersections.</summary>
        public double Tolerance = Globals.FromMillimeters(0.01);
    }

    public class BrepBlankResult
    {
        /// <summary>The simplified beam: centred on its section, enclosing the Brep.</summary>
        public Beam Beam;

        /// <summary>The blank made from it (a Glulam, so its section may be rounded up to whole
        /// lamellae), and where the beam sits in it.</summary>
        public Glulam Blank;
        public Placement Placement;

        public bool Success => Beam != null;

        /// <summary>Edges compared, their cluster labels, and the edges of the cluster the
        /// centreline comes from.</summary>
        public List<Polyline> Edges = new List<Polyline>();
        public int[] Labels;
        public List<Polyline> Cluster = new List<Polyline>();

        /// <summary>Mean of the cluster's edges, before it is moved to the section centre.</summary>
        public Curve MeanCurve;

        public List<Section> Sections = new List<Section>();

        /// <summary>Extents of the Brep in the beam's space (X across, Y up, Z along).</summary>
        public BoundingBox Extents;

        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// The blank of a detailed beam Brep: a rectangular section swept along a centreline that
    /// follows the Brep's most common run of parallel edges, sized to enclose the Brep. Port of
    /// parallel.py, with the sizing done in the beam's curved space.
    /// </summary>
    public static class BrepBlank
    {
        public static BrepBlankResult Reconstruct(Brep brep, BrepBlankOptions options = null)
        {
            options ??= new BrepBlankOptions();
            var result = new BrepBlankResult();
            if (brep == null) { result.Messages.Add("No Brep."); return result; }

            // 1. Long edges, resampled to the same number of points
            var curves = brep.Edges.Select(e => (Curve)e).Where(c => c != null).ToList();
            if (curves.Count == 0) { result.Messages.Add("The Brep has no edges."); return result; }

            var lengths = curves.Select(c => c.GetLength()).ToArray();
            double minLength = options.MinEdgeRatio * lengths.Max();
            var kept = Enumerable.Range(0, curves.Count).Where(i => lengths[i] >= minLength).ToArray();

            var samples = kept.Select(i => curves[i].DivideByCount(options.EdgeSamples - 1, true).Select(t => curves[i].PointAt(t)).ToArray()).ToArray();
            result.Edges = samples.Select(s => new Polyline(s)).ToList();

            // 2. Cluster them by parallelness and take the cluster with the most length
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
            result.Messages.Add($"{n} edges in {clusterLength.Count} clusters; the centreline follows {members.Count} edges.");

            // 3. Mean of the cluster's edges, all running the same way as the first
            var reference = samples[members[0]];
            var mean = new Point3d[options.EdgeSamples];
            foreach (int i in members)
            {
                bool reverse = Parallelness(reference, samples[i], true, options) < Parallelness(reference, samples[i], false, options);
                for (int k = 0; k < mean.Length; ++k)
                    mean[k] += samples[i][reverse ? mean.Length - 1 - k : k];
            }
            for (int k = 0; k < mean.Length; ++k) mean[k] /= members.Count;

            var meanCurve = new InterpolatingSpline(mean).ToNurbsCurve();
            result.MeanCurve = meanCurve;

            // 4-6. Sections, the Brep's extents in the beam's curved space, and the centred beam.
            // The mean curve can be off the section centre (e.g. the mean of two edges on one
            // face), which tilts its sections on a twisted beam, so the pass is repeated from the
            // centred curve.
            var brepPoints = BrepPoints(brep);
            var clusterEdges = new EdgeSet(result.Cluster);
            Curve curve = meanCurve;
            Beam beam = null;
            for (int pass = 0; pass <= options.Refinements; ++pass)
            {
                bool last = pass == options.Refinements;
                if (!Pass(brep, brepPoints, clusterEdges, curve, options, last, result, out beam))
                    return result;
                if (!last) curve = Recentre(beam, clusterEdges);
            }
            result.Beam = beam;

            try
            {
                result.Blank = Glulam.CreateGlulam(beam, beam.Orientation);
                result.Placement = new Placement { BlankId = result.Blank.Id, BeamId = beam.Id };
            }
            catch (Exception ex)
            {
                result.Messages.Add($"Couldn't make a glulam blank: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// A better centreline from the cluster's edges: at stations along the beam, where its
        /// section plane crosses the edges, less their average position in the section. (The mean
        /// of the edges by point count drifts where the edges have different lengths, e.g. on a
        /// twisted beam; crossings compare the edges at the same section.)
        /// </summary>
        static Curve Recentre(Beam beam, EdgeSet clusterEdges)
        {
            var curve = beam.Centreline;
            int count = Math.Clamp((int)(curve.GetLength() / Globals.FromMillimeters(200)), 10, 100);
            var hits = new List<Point3d>();
            var planes = new Plane[count];
            var local = new Point3d?[count];

            var tt = MeshBeam.Linspace(0, 1, count);
            for (int i = 0; i < count; ++i)
            {
                planes[i] = beam.GetPlane(curve.Domain.ParameterAt(tt[i]));
                if (clusterEdges.Cut(planes[i].Origin, planes[i].ZAxis, hits) != clusterEdges.Count) continue;

                var mean = Point3d.Origin;
                foreach (var h in hits) mean += h;
                mean /= hits.Count;

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
        static void AlignToEdges(ref Section section, EdgeSet clusterEdges)
        {
            var hits = new List<Point3d>();
            if (clusterEdges.Cut(section.Plane.Origin, section.Plane.ZAxis, hits) < 2) return;

            Vector3d line;
            if (hits.Count == 2)
                line = hits[1] - hits[0];
            else
            {
                Pca.PrincipalAxes(hits, out _, out Vector3d[] axes);
                line = axes[0];
            }

            var plane = section.Plane;
            line -= plane.ZAxis * (line * plane.ZAxis);
            if (!line.Unitize()) return;

            // Closest axis, keeping the frame's direction
            Vector3d y;
            if (Math.Abs(line * plane.XAxis) >= Math.Abs(line * plane.YAxis))
            {
                var x = line * plane.XAxis >= 0 ? line : -line;
                y = Vector3d.CrossProduct(plane.ZAxis, x);
            }
            else
                y = line * plane.YAxis >= 0 ? line : -line;

            section.Plane = Utility.PlaneFromNormalAndYAxis(plane.Origin, plane.ZAxis, y);
        }

        /// <summary>
        /// One pass along a centreline: sections of the Brep normal to it give the orientation, the
        /// Brep's extents in the curved space of a beam along it give the section, and the curve is
        /// moved to the section's centre and trimmed to the Brep's length.
        /// </summary>
        static bool Pass(Brep brep, List<Point3d> brepPoints, EdgeSet clusterEdges, Curve curve, BrepBlankOptions options, bool last, BrepBlankResult result, out Beam beam)
        {
            beam = null;
            var messages = last ? result.Messages : new List<string>();

            var sections = new List<Section>();
            foreach (var t in MeshBeam.Linspace(options.SectionRange.Min, options.SectionRange.Max, options.SectionSamples))
            {
                double u = curve.Domain.ParameterAt(t);
                var plane = new Plane(curve.PointAt(u), curve.TangentAt(u));
                if (!Intersection.BrepPlane(brep, plane, options.Tolerance, out Curve[] sectionCurves, out Point3d[] sectionPoints)) continue;

                var points = new List<Point3d>(sectionPoints);
                foreach (var c in sectionCurves)
                    points.AddRange(Reconstruction.Edges.ToPolyline(c, 16));

                if (Sections.TryFromPoints(points, plane.Origin, plane.ZAxis, out Section section))

                {

                    AlignToEdges(ref section, clusterEdges);

                    sections.Add(section);

                }
            }

            if (sections.Count == 0) { result.Messages.Add("No sections of the Brep found along the centreline."); return false; }
            Sections.MakeConsistent(sections);
            result.Sections = sections;

            // The Brep in the curved space of a beam along the (extended) curve
            var extended = curve.Extend(CurveEnd.Both, 0.1 * curve.GetLength(), CurveExtensionStyle.Smooth) ?? curve.DuplicateCurve();
            extended.Domain = new Interval(0, 1);

            var probe = new Beam { Centreline = extended };
            probe.Orientation = MeshBeam.Orientation(extended, sections, false, 0, messages);

            var extents = new BoundingBox(probe.ToBeamSpace(brepPoints, true, 200));
            result.Extents = extents;

            // Trim to the Brep's length and centre the section on the curve
            double a = options.Allowance, e = options.EndAllowance;
            double extLength = extended.GetLength();
            double z0 = Math.Max(0, extents.Min.Z - e), z1 = Math.Min(extLength, extents.Max.Z + e);
            if (!extended.LengthParameter(z0, out double t0) || !extended.LengthParameter(z1, out double t1) || t1 <= t0)
            { result.Messages.Add("Couldn't trim the centreline to the Brep."); return false; }

            var trimmed = extended.Trim(t0, t1);
            var orientation = probe.Orientation.Duplicate();
            orientation.Remap(extended, trimmed);

            var offset = new Beam
            {
                Centreline = trimmed,
                Orientation = orientation,
                OffsetX = (extents.Min.X + extents.Max.X) * 0.5,
                OffsetY = (extents.Min.Y + extents.Max.Y) * 0.5,
                Width = extents.Max.X - extents.Min.X + 2 * a,
                Height = extents.Max.Y - extents.Min.Y + 2 * a
            };

            var centreline = BeamOps.SectionCentreline(offset, out _);
            centreline.Domain = new Interval(0, 1);

            // The orientation on the centred curve, from the section frames moved with it
            var moved = sections.Select(s =>
            {
                var p = s.Plane;
                p.Origin = p.PointAt(offset.OffsetX, offset.OffsetY);
                return new Section { Plane = p };
            }).ToList();

            beam = new Beam { Centreline = centreline, Width = offset.Width, Height = offset.Height };
            beam.Orientation = MeshBeam.Orientation(centreline, moved, last && options.SimplifyOrientation, options.PlanarAngleTolerance, messages);
            return true;
        }

        /// <summary>
        /// How far two resampled edges are from running parallel: AngleWeight x the mean angle
        /// between their segments plus DistanceWeight x the spread of the distances between their
        /// points. Reversing b compares it running the other way.
        /// </summary>
        static double Parallelness(Point3d[] a, Point3d[] b, bool reverse, BrepBlankOptions options)
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

        /// <summary>
        /// Points over a Brep that find its extents: mesh vertices (with a mesh fine enough to
        /// follow curved faces) and points along its edges.
        /// </summary>
        static List<Point3d> BrepPoints(Brep brep)
        {
            var bbox = brep.GetBoundingBox(false);
            var parameters = new MeshingParameters
            {
                MaximumEdgeLength = bbox.Diagonal.Length / 50,
                SimplePlanes = false,
                JaggedSeams = true
            };

            var points = new List<Point3d>();
            var meshes = Mesh.CreateFromBrep(brep, parameters);
            if (meshes != null)
                foreach (var m in meshes) points.AddRange(m.Vertices.ToPoint3dArray());

            foreach (var edge in brep.Edges)
                points.AddRange(Reconstruction.Edges.ToPolyline(edge, 20));

            return points;
        }
    }
}
