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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Rhino.Geometry;

using GluLamb.Joints;

namespace GluLamb.Blanks
{
    /// <summary>
    /// Groups near-identical beams so that they can share one blank. Beams are compared by
    /// rigid alignment (no mirroring) of points along their centrelines and offset along
    /// their cross-section Y-axes, so the roll of the section counts too. Two beams match
    /// when the largest point deviation after alignment is within the tolerance; a cluster
    /// is made only of beams that all match each other (complete linkage).
    /// </summary>
    public static class BeamClustering
    {
        /// <summary>
        /// Cluster beams.
        /// </summary>
        /// <param name="beams">Beams to cluster.</param>
        /// <param name="tolerance">Largest deviation between any two beams of a cluster. Sections must also be within it.</param>
        /// <param name="lengthTolerance">How much shorter than the blank a member may be: within a cluster,
        /// the longest and shortest members differ by at most this, and the shorter ones are cut from the
        /// blank (still within the tolerance over their length). 0: full-length matches only.</param>
        /// <param name="samples">Number of stations along each beam.</param>
        /// <param name="extensions">Optional extensions per beam that the blank must also cover.</param>
        /// <param name="standard">Standard for the blanks' lamella thickness (from their curvature).</param>
        public static BeamClusterResult Cluster(IList<Beam> beams, double tolerance, double lengthTolerance = 0, int samples = 24, IList<BeamExtension> extensions = null,
            Standards.Standard standard = Standards.Standard.Eurocode)
        {
            if (!(tolerance > 0))
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be greater than zero.");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            int count = beams.Count;
            samples = Math.Max(samples, 4);
            bool allowPartial = lengthTolerance > 0;

            var result = new BeamClusterResult
            {
                BlankIndex = Enumerable.Repeat(-1, count).ToArray(),
                Transforms = Enumerable.Repeat(Transform.Identity, count).ToArray(),
                Deviation = new double[count],
                Partial = new bool[count],
                PlacedBlanks = new Glulam[count]
            };

            var profiles = new Profile[count];
            for (int i = 0; i < count; ++i)
                profiles[i] = Profile.Create(beams[i], i, samples, allowPartial);

            var valid = profiles.Where(x => x != null).ToArray();

            // Pairwise matches within tolerance
            var matches = new ConcurrentBag<(int A, int B, double Deviation, bool Partial)>();
            long alignments = 0;

            if (!allowPartial)
            {
                // A rigid alignment within d moves every point by at most d, so the distance
                // between any two stations differs by at most 2d: sweep by end-to-end chord.
                var sorted = valid.OrderBy(x => x.Invariants[0]).ToArray();
                Parallel.For(0, sorted.Length, i =>
                {
                    var a = sorted[i];
                    for (int j = i + 1; j < sorted.Length && sorted[j].Invariants[0] - a.Invariants[0] <= 2 * tolerance; ++j)
                    {
                        var b = sorted[j];
                        if (!SectionsMatch(a, b, tolerance) || !InvariantsMatch(a, b, 2 * tolerance)) continue;
                        System.Threading.Interlocked.Increment(ref alignments);

                        var match = AlignFull(a, b, false);
                        if (match.Deviation <= tolerance)
                            matches.Add((a.Index, b.Index, match.Deviation, false));
                    }
                });
            }
            else
            {
                var sorted = valid.OrderBy(x => x.Length).ToArray();
                Parallel.For(0, sorted.Length, i =>
                {
                    var a = sorted[i];
                    for (int j = i + 1; j < sorted.Length; ++j)
                    {
                        var b = sorted[j];
                        if (!SectionsMatch(a, b, tolerance)) continue;

                        if (InvariantsMatch(a, b, 2 * tolerance))
                        {
                            System.Threading.Interlocked.Increment(ref alignments);
                            var match = AlignFull(a, b, false);
                            if (match.Deviation <= tolerance)
                            {
                                matches.Add((a.Index, b.Index, match.Deviation, false));
                                continue;
                            }
                        }

                        if (b.Length > a.Length && b.Length - a.Length <= lengthTolerance)
                        {
                            long tried = 0;
                            var match = AlignPartial(b, a, tolerance, false, ref tried);
                            System.Threading.Interlocked.Add(ref alignments, tried);
                            if (match.Deviation <= tolerance)
                                matches.Add((a.Index, b.Index, match.Deviation, true));
                        }
                    }
                });
            }

            result.Alignments = alignments;

            var neighbours = new Dictionary<int, (double Deviation, bool Partial)>[count];
            for (int i = 0; i < count; ++i)
                neighbours[i] = new Dictionary<int, (double, bool)>();
            foreach (var (a, b, deviation, partial) in matches)
            {
                neighbours[a][b] = (deviation, partial);
                neighbours[b][a] = (deviation, partial);
            }

            // Greedy complete linkage: seed with the beam that has the most unclustered
            // neighbours (ties: longest), then add its neighbours (full matches first, then
            // closest) if they match every member so far. With a length tolerance, seed with
            // the longest unclustered beam instead, so that clusters form length bands below
            // it rather than chaining, and take the closest lengths first.
            var alive = new HashSet<int>(valid.Select(x => x.Index));
            var clusters = new List<List<int>>();

            while (alive.Count > 0)
            {
                int seed = allowPartial
                    ? alive
                        .OrderByDescending(x => profiles[x].Length)
                        .ThenByDescending(x => neighbours[x].Keys.Count(alive.Contains))
                        .ThenBy(x => x)
                        .First()
                    : alive
                        .OrderByDescending(x => neighbours[x].Keys.Count(alive.Contains))
                        .ThenByDescending(x => profiles[x].Length)
                        .ThenBy(x => x)
                        .First();

                var members = new List<int> { seed };
                var candidates = neighbours[seed]
                    .Where(x => alive.Contains(x.Key))
                    .OrderBy(x => x.Value.Partial ? 1 : 0)
                    .ThenByDescending(x => allowPartial ? profiles[x.Key].Length : 0)
                    .ThenBy(x => x.Value.Deviation)
                    .Select(x => x.Key);

                foreach (var candidate in candidates)
                    if (members.All(m => neighbours[m].ContainsKey(candidate)))
                        members.Add(candidate);

                foreach (var m in members)
                    alive.Remove(m);
                clusters.Add(members);
            }

            // Blanks: the longest member is the reference; align it to every member
            foreach (var members in clusters)
            {
                int reference = members.OrderByDescending(x => profiles[x].Length).First();
                var refProfile = profiles[reference];
                var refBeam = beams[reference];
                var refExtension = extensions != null && reference < extensions.Count ? extensions[reference] : new BeamExtension();

                double start = refExtension.Start, end = refExtension.End;
                double width = refBeam.Width, height = refBeam.Height;
                int clusterIndex = result.Clusters.Count;

                foreach (var m in members)
                {
                    result.BlankIndex[m] = clusterIndex;
                    if (m == reference) continue;

                    var p = profiles[m];
                    var match = AlignFull(refProfile, p, true);
                    if (allowPartial && match.Deviation > tolerance && p.Length < refProfile.Length)
                    {
                        long dummy = 0;
                        var partial = AlignPartial(refProfile, p, tolerance, true, ref dummy);
                        if (partial.Deviation < match.Deviation)
                            match = partial;
                    }

                    result.Transforms[m] = match.Transform;
                    result.Deviation[m] = match.Deviation;
                    result.Partial[m] = match.Partial;

                    width = Math.Max(width, beams[m].Width + 2 * match.MaxX);
                    height = Math.Max(height, beams[m].Height + 2 * match.MaxY);

                    // Where the member's ends (with extensions) land along the reference
                    var extension = extensions != null && m < extensions.Count ? extensions[m] : new BeamExtension();
                    double scale = match.Partial ? 1.0 : refProfile.Length / p.Length;
                    double u0 = -extension.Start - match.Deviation, u1 = p.Length + extension.End + match.Deviation;
                    double s0 = match.Offset + (match.Reversed ? p.Length - u0 : u0) * scale;
                    double s1 = match.Offset + (match.Reversed ? p.Length - u1 : u1) * scale;

                    start = Math.Max(start, -Math.Min(s0, s1));
                    end = Math.Max(end, Math.Max(s0, s1) - refProfile.Length);
                }

                var curve = refBeam.Centreline.DuplicateCurve();
                if (start > 0)
                    curve = curve.Extend(CurveEnd.Start, start, CurveExtensionStyle.Line) ?? curve;
                if (end > 0)
                    curve = curve.Extend(CurveEnd.End, end, CurveExtensionStyle.Line) ?? curve;

                // The lamellae come from the standard and the blank's curvature, sized to make up
                // exactly the blank's width and height
                var section = new Beam
                {
                    Centreline = curve,
                    Orientation = refBeam.Orientation.Duplicate(),
                    Width = width,
                    Height = height
                };
                var blank = Glulam.CreateGlulam(section, section.Orientation, standard);
                blank.Id = $"blank-{clusterIndex}";
                blank.OffsetX = refBeam.OffsetX;
                blank.OffsetY = refBeam.OffsetY;

                // Reference first, then the rest in input order
                var ordered = new List<int> { reference };
                ordered.AddRange(members.Where(x => x != reference).OrderBy(x => x));

                result.Clusters.Add(new BeamCluster
                {
                    Blank = blank,
                    Reference = reference,
                    Members = ordered,
                    Extension = new BeamExtension(start, end)
                });

                foreach (var m in members)
                {
                    // Duplicate keeps the blank's id
                    var placed = blank.DuplicateGlulam();
                    placed.Transform(result.Transforms[m]);
                    result.PlacedBlanks[m] = placed;
                }
            }

            result.Elapsed = watch.Elapsed;
            return result;
        }

        /// <summary>
        /// Extension of one beam from a list of {start, end} values: one pair for all the beams,
        /// or a pair per beam (as many pairs as beams).
        /// </summary>
        /// <param name="values">The values, e.g. a branch of Construct Joints' extensions.</param>
        /// <param name="item">Index of the beam.</param>
        /// <param name="beamCount">Number of beams the values are for.</param>
        public static BeamExtension ExtensionFrom(IList<double> values, int item, int beamCount)
        {
            if (values == null || values.Count < 2) return new BeamExtension();
            int k = values.Count >= 2 * beamCount ? 2 * item : 0;
            return new BeamExtension(values[k], values[k + 1]);
        }

        static bool SectionsMatch(Profile a, Profile b, double tolerance) =>
            Math.Abs(a.Width - b.Width) <= tolerance && Math.Abs(a.Height - b.Height) <= tolerance;

        static bool InvariantsMatch(Profile a, Profile b, double slack) =>
            InvariantsMatch(a.Invariants, b.Invariants, slack, 2 * Math.Abs(a.R - b.R));

        /// <summary>
        /// The invariants from the offset points get extra slack for beams whose offsets differ.
        /// </summary>
        static bool InvariantsMatch(double[] a, double[] b, double slack, double offsetSlack)
        {
            for (int i = 0; i < a.Length; ++i)
                if (Math.Abs(a[i] - b[i]) > (i < Profile.OffsetInvariants ? slack : slack + offsetSlack)) return false;
            return true;
        }

        /// <summary>
        /// Beam sampled at stations equally spaced by arc length: section centres and
        /// cross-section axes. With partial matching, also a dense sampling to take
        /// windows from.
        /// </summary>
        sealed class Profile
        {
            public int Index;
            public double Length, Width, Height;

            /// <summary>
            /// Offset along Y of the second point at each station, for the invariants.
            /// </summary>
            public double R;
            public Point3d[] C;
            public Vector3d[] X, Y;
            public Point3d[] DenseC;
            public Vector3d[] DenseX, DenseY;
            public double[] DenseS;

            /// <summary>
            /// Rigid invariants that are the same for the reversed beam, and change by at most
            /// about 2d when every station moves by d: distances between stations i and N-1-i
            /// for a few i (end-to-end chord first), and the distances of the middle and quarter
            /// stations from the end-to-end chord (quarters sorted). Then, so that the roll of
            /// the section counts, the same chords between the points offset by +R and by -R
            /// along Y (sorted, since a half-turn roll swaps them).
            /// </summary>
            public double[] Invariants;

            static readonly double[] ChordFractions = { 0, 0.125, 0.25, 0.375 };
            static readonly double[] OffsetFractions = { 0, 0.25 };

            /// <summary>
            /// Index of the first invariant from the offset points.
            /// </summary>
            public const int OffsetInvariants = 7;

            public static Profile Create(Beam beam, int index, int samples, bool dense)
            {
                if (beam?.Centreline == null || beam.Orientation == null) return null;
                double length = beam.Centreline.GetLength();
                if (!(length > Rhino.RhinoMath.ZeroTolerance)) return null;

                var p = new Profile { Index = index, Length = length, Width = beam.Width, Height = beam.Height, R = 0.5 * Math.Max(beam.Width, beam.Height) };

                if (dense)
                {
                    // Dense enough that linear interpolation between samples stays well below
                    // typical tolerances (about 25 units apart, at least 16 per station). The
                    // samples are evenly spaced in the curve parameter (dividing by arc length
                    // is slow) and looked up by their cumulative chord length.
                    int denseCount = Math.Min(4000, Math.Max(16 * samples, (int)Math.Ceiling(length / 25.0)));
                    var domain = beam.Centreline.Domain;
                    var tt = Enumerable.Range(0, denseCount).Select(i => domain.ParameterAt(i / (denseCount - 1.0))).ToArray();
                    Sample(beam, tt, out p.DenseC, out p.DenseX, out p.DenseY);

                    p.DenseS = new double[denseCount];
                    for (int i = 1; i < denseCount; ++i)
                        p.DenseS[i] = p.DenseS[i - 1] + p.DenseC[i - 1].DistanceTo(p.DenseC[i]);
                    p.Length = p.DenseS[denseCount - 1];

                    p.C = new Point3d[samples];
                    p.X = new Vector3d[samples];
                    p.Y = new Vector3d[samples];
                    p.Window(0, p.Length, p.C, p.X, p.Y);
                }
                else
                {
                    var tt = beam.Centreline.DivideByCount(samples - 1, true);
                    if (tt == null || tt.Length != samples) return null;
                    Sample(beam, tt, out p.C, out p.X, out p.Y);
                }

                p.Invariants = InvariantsOf(p.C, p.Y, p.R);
                return p;
            }

            public static double[] InvariantsOf(Point3d[] c, Vector3d[] y, double r)
            {
                int n = c.Length;
                var invariants = new double[OffsetInvariants + 2 * OffsetFractions.Length];
                for (int k = 0; k < ChordFractions.Length; ++k)
                {
                    int i = (int)Math.Round(ChordFractions[k] * (n - 1));
                    invariants[k] = c[i].DistanceTo(c[n - 1 - i]);
                }

                var chord = new Line(c[0], c[n - 1]);
                double Sagitta(int i) => chord.Length > 1e-9 ? chord.DistanceTo(c[i], false) : c[i].DistanceTo(c[0]);
                double q1 = Sagitta((n - 1) / 4), q3 = Sagitta(n - 1 - (n - 1) / 4);
                invariants[ChordFractions.Length] = 0.5 * (Sagitta((n - 1) / 2) + Sagitta(n / 2));
                invariants[ChordFractions.Length + 1] = Math.Min(q1, q3);
                invariants[ChordFractions.Length + 2] = Math.Max(q1, q3);

                for (int k = 0; k < OffsetFractions.Length; ++k)
                {
                    int i = (int)Math.Round(OffsetFractions[k] * (n - 1)), j = n - 1 - i;
                    double plus = (c[i] + y[i] * r).DistanceTo(c[j] + y[j] * r);
                    double minus = (c[i] - y[i] * r).DistanceTo(c[j] - y[j] * r);
                    invariants[OffsetInvariants + 2 * k] = Math.Min(plus, minus);
                    invariants[OffsetInvariants + 2 * k + 1] = Math.Max(plus, minus);
                }
                return invariants;
            }

            static void Sample(Beam beam, double[] tt, out Point3d[] c, out Vector3d[] x, out Vector3d[] y)
            {
                int count = tt.Length;
                var planes = beam.GetPlanes(tt);
                c = new Point3d[count];
                x = new Vector3d[count];
                y = new Vector3d[count];
                for (int i = 0; i < count; ++i)
                {
                    // Section centre, so offset sections line up
                    c[i] = planes[i].PointAt(beam.OffsetX, beam.OffsetY);
                    x[i] = planes[i].XAxis;
                    y[i] = planes[i].YAxis;
                }
            }

            /// <summary>
            /// Stations of the window [offset, offset + length] along the dense sampling.
            /// </summary>
            public void Window(double offset, double length, Point3d[] c, Vector3d[] x, Vector3d[] y)
            {
                int n = c.Length;
                for (int i = 0; i < n; ++i)
                {
                    double s = offset + length * i / (n - 1);
                    Interpolate(s, out c[i], out x[i], out y[i]);
                }
            }

            public Point3d PointAt(double s)
            {
                Interpolate(s, out Point3d c, out _, out _);
                return c;
            }

            void Interpolate(double s, out Point3d c, out Vector3d x, out Vector3d y)
            {
                int last = DenseS.Length - 1;
                s = Math.Max(0, Math.Min(DenseS[last], s));
                int i0 = Array.BinarySearch(DenseS, s);
                if (i0 < 0) i0 = ~i0 - 1;
                i0 = Math.Max(0, Math.Min(i0, last - 1));
                double span = DenseS[i0 + 1] - DenseS[i0];
                double t = span > 0 ? (s - DenseS[i0]) / span : 0;
                c = DenseC[i0] + (DenseC[i0 + 1] - DenseC[i0]) * t;
                x = DenseX[i0] * (1 - t) + DenseX[i0 + 1] * t;
                y = DenseY[i0] * (1 - t) + DenseY[i0 + 1] * t;
                x.Unitize();
                y.Unitize();
            }
        }

        struct Match
        {
            public double Deviation;
            public bool Reversed, Partial;
            public double Offset;
            public Transform Transform;

            /// <summary>
            /// Largest deviation of the member from the reference along the reference's section axes.
            /// </summary>
            public double MaxX, MaxY;

            public static Match None => new Match { Deviation = double.MaxValue, Transform = Transform.Identity };
        }

        /// <summary>
        /// Align beam a onto beam b, station to station, forwards and reversed, and with the
        /// section rolled half a turn.
        /// </summary>
        static Match AlignFull(Profile a, Profile b, bool details)
        {
            double r = 0.5 * Math.Max(Math.Max(a.Width, a.Height), Math.Max(b.Width, b.Height));
            return Best(a.C, a.X, a.Y, b, r, details);
        }

        /// <summary>
        /// Slide the shorter beam along the longer one and align it to each window.
        /// </summary>
        static Match AlignPartial(Profile longer, Profile shorter, double tolerance, bool details, ref long alignments)
        {
            double range = longer.Length - shorter.Length;
            if (range <= 0) return Match.None;

            double r = 0.5 * Math.Max(Math.Max(longer.Width, longer.Height), Math.Max(shorter.Width, shorter.Height));
            int n = shorter.C.Length;
            var c = new Point3d[n];
            var x = new Vector3d[n];
            var y = new Vector3d[n];

            int steps = Math.Max(1, Math.Min(400, (int)Math.Ceiling(range / (0.5 * tolerance))));
            double step = range / steps;

            // Coarse pass. The invariant tests are lower bounds at each offset; the slack
            // covers an optimum up to half a step away. When only asking whether the beams
            // match (no details), the first window within tolerance will do.
            double slack = 2 * tolerance + step;
            int bestK = -1;
            double bestCoarse = double.MaxValue;
            for (int k = 0; k <= steps; ++k)
            {
                // The window's end-to-end chord changes by at most 2 per unit of offset, so
                // skip the offsets where it can't come within the slack
                double o = k * step;
                double gap = Math.Abs(longer.PointAt(o).DistanceTo(longer.PointAt(o + shorter.Length)) - shorter.Invariants[0]);
                if (gap > slack)
                {
                    k += (int)((gap - slack) / (2 * step));
                    continue;
                }

                longer.Window(o, shorter.Length, c, x, y);
                if (!InvariantsMatch(Profile.InvariantsOf(c, y, shorter.R), shorter.Invariants, slack, 0)) continue;

                ++alignments;
                double deviation = Best(c, x, y, shorter, r, false).Deviation;
                if (deviation < bestCoarse) { bestCoarse = deviation; bestK = k; }
                if (!details && deviation <= tolerance) break;
            }

            if (bestK < 0 || bestCoarse > tolerance + step) return Match.None;

            double offset = bestK * step;
            if (details || bestCoarse > tolerance)
            {
                // Golden section search around the best coarse offset
                int refinements = 0;
                Func<double, double> f = o =>
                {
                    longer.Window(o, shorter.Length, c, x, y);
                    ++refinements;
                    return Best(c, x, y, shorter, r, false).Deviation;
                };

                const double g = 0.6180339887498949;
                double lo = Math.Max(0, offset - step), hi = Math.Min(range, offset + step);
                double o1 = hi - g * (hi - lo), o2 = lo + g * (hi - lo);
                double f1 = f(o1), f2 = f(o2);
                for (int it = 0; it < 12; ++it)
                {
                    if (f1 < f2) { hi = o2; o2 = o1; f2 = f1; o1 = hi - g * (hi - lo); f1 = f(o1); }
                    else { lo = o1; o1 = o2; f1 = f2; o2 = lo + g * (hi - lo); f2 = f(o2); }
                }

                if (Math.Min(f1, f2) < bestCoarse)
                    offset = f1 < f2 ? o1 : o2;
                alignments += refinements;
            }

            longer.Window(offset, shorter.Length, c, x, y);
            var best = Best(c, x, y, shorter, r, details);
            best.Offset = offset;
            best.Partial = true;
            return best;
        }

        /// <summary>
        /// Best rigid alignment of reference stations (pc, px, py) onto the stations of b,
        /// over reversal and a half-turn roll.
        /// </summary>
        static Match Best(Point3d[] pc, Vector3d[] px, Vector3d[] py, Profile b, double r, bool details)
        {
            var best = Match.None;
            for (int k = 0; k < 4; ++k)
            {
                bool reversed = (k & 1) != 0;
                double roll = (k & 2) != 0 ? -1 : 1;
                var match = Fit(pc, px, py, b.C, b.Y, r, reversed, roll, details);
                if (match.Deviation < best.Deviation)
                    best = match;
            }
            return best;
        }

        /// <summary>
        /// Least-squares rigid alignment (Horn's quaternion method, so never a mirror) of the
        /// reference points onto the member points. The points are the section centres and
        /// the centres offset by r along the section Y-axes. Returns the largest deviation.
        /// </summary>
        static Match Fit(Point3d[] pc, Vector3d[] px, Vector3d[] py, Point3d[] qc, Vector3d[] qy, double r, bool reversed, double roll, bool details)
        {
            int n = pc.Length;

            Point3d P(int i) => i < n ? pc[i] : pc[i - n] + py[i - n] * r;
            Point3d Q(int i)
            {
                int j = reversed ? n - 1 - (i % n) : i % n;
                return i < n ? qc[j] : qc[j] + qy[j] * (r * roll);
            }

            var cp = new Vector3d();
            var cq = new Vector3d();
            for (int i = 0; i < 2 * n; ++i)
            {
                cp += (Vector3d)P(i);
                cq += (Vector3d)Q(i);
            }
            cp /= 2 * n;
            cq /= 2 * n;

            double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
            for (int i = 0; i < 2 * n; ++i)
            {
                var p = (Vector3d)P(i) - cp;
                var q = (Vector3d)Q(i) - cq;
                sxx += p.X * q.X; sxy += p.X * q.Y; sxz += p.X * q.Z;
                syx += p.Y * q.X; syy += p.Y * q.Y; syz += p.Y * q.Z;
                szx += p.Z * q.X; szy += p.Z * q.Y; szz += p.Z * q.Z;
            }

            var m = new double[4, 4]
            {
                { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
                { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
                { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
                { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz }
            };
            var qr = LargestEigenvector(m);
            double w = qr[0], qx = qr[1], qy2 = qr[2], qz = qr[3];

            double r00 = 1 - 2 * (qy2 * qy2 + qz * qz), r01 = 2 * (qx * qy2 - w * qz), r02 = 2 * (qx * qz + w * qy2);
            double r10 = 2 * (qx * qy2 + w * qz), r11 = 1 - 2 * (qx * qx + qz * qz), r12 = 2 * (qy2 * qz - w * qx);
            double r20 = 2 * (qx * qz - w * qy2), r21 = 2 * (qy2 * qz + w * qx), r22 = 1 - 2 * (qx * qx + qy2 * qy2);

            double maxSq = 0, maxX = 0, maxY = 0;
            for (int i = 0; i < 2 * n; ++i)
            {
                var p = (Vector3d)P(i) - cp;
                var q = (Vector3d)Q(i) - cq;
                // Deviation of the member point from the transformed reference point
                var d = new Vector3d(
                    q.X - (r00 * p.X + r01 * p.Y + r02 * p.Z),
                    q.Y - (r10 * p.X + r11 * p.Y + r12 * p.Z),
                    q.Z - (r20 * p.X + r21 * p.Y + r22 * p.Z));
                maxSq = Math.Max(maxSq, d.SquareLength);

                if (details)
                {
                    // The same deviation in the reference's frame (inverse rotation)
                    var e = new Vector3d(
                        r00 * d.X + r10 * d.Y + r20 * d.Z,
                        r01 * d.X + r11 * d.Y + r21 * d.Z,
                        r02 * d.X + r12 * d.Y + r22 * d.Z);
                    int s = i % n;
                    maxX = Math.Max(maxX, Math.Abs(e * px[s]));
                    maxY = Math.Max(maxY, Math.Abs(e * py[s]));
                }
            }

            var match = new Match { Deviation = Math.Sqrt(maxSq), Reversed = reversed, MaxX = maxX, MaxY = maxY, Transform = Transform.Identity };

            if (details)
            {
                // x -> R(x - cp) + cq
                var t = cq - new Vector3d(
                    r00 * cp.X + r01 * cp.Y + r02 * cp.Z,
                    r10 * cp.X + r11 * cp.Y + r12 * cp.Z,
                    r20 * cp.X + r21 * cp.Y + r22 * cp.Z);
                var xf = Transform.Identity;
                xf.M00 = r00; xf.M01 = r01; xf.M02 = r02; xf.M03 = t.X;
                xf.M10 = r10; xf.M11 = r11; xf.M12 = r12; xf.M13 = t.Y;
                xf.M20 = r20; xf.M21 = r21; xf.M22 = r22; xf.M23 = t.Z;
                match.Transform = xf;
            }

            return match;
        }

        /// <summary>
        /// Unit eigenvector of the largest eigenvalue of a symmetric 4x4 matrix (cyclic Jacobi).
        /// </summary>
        static double[] LargestEigenvector(double[,] a)
        {
            var v = new double[4, 4];
            for (int i = 0; i < 4; ++i) v[i, i] = 1;

            for (int sweep = 0; sweep < 32; ++sweep)
            {
                double off = 0, diag = 0;
                for (int i = 0; i < 4; ++i)
                {
                    diag += a[i, i] * a[i, i];
                    for (int j = i + 1; j < 4; ++j) off += a[i, j] * a[i, j];
                }
                if (off <= 1e-24 * Math.Max(diag, 1e-300)) break;

                for (int p = 0; p < 3; ++p)
                    for (int q = p + 1; q < 4; ++q)
                    {
                        if (Math.Abs(a[p, q]) < 1e-300) continue;
                        double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        if (theta == 0) t = 1;
                        double c = 1 / Math.Sqrt(t * t + 1), s = t * c;

                        for (int k = 0; k < 4; ++k)
                        {
                            double akp = a[k, p], akq = a[k, q];
                            a[k, p] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < 4; ++k)
                        {
                            double apk = a[p, k], aqk = a[q, k];
                            a[p, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < 4; ++k)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
            }

            int best = 0;
            for (int i = 1; i < 4; ++i)
                if (a[i, i] > a[best, best]) best = i;

            var result = new double[4];
            double norm = 0;
            for (int k = 0; k < 4; ++k) { result[k] = v[k, best]; norm += result[k] * result[k]; }
            norm = Math.Sqrt(norm);
            for (int k = 0; k < 4; ++k) result[k] /= norm;
            return result;
        }
    }
}
