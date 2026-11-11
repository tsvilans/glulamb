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
    /// A cross-section of a beam's edges: where a plane crosses them, and the convex hull of those
    /// points.
    /// </summary>
    public struct Section
    {
        /// <summary>The point the section was taken at (on the centreline).</summary>
        public Point3d Position;

        /// <summary>Centre of the rectangle around the hull that lines up with its sides (see
        /// Sections.AlignedRectangle).</summary>
        public Point3d Centre;

        /// <summary>Section plane at Position: Z along the centreline, Y along the section's
        /// largest spread (the beam's height direction), X across it.</summary>
        public Plane Plane;

        /// <summary>Hull corners, counter-clockwise.</summary>
        public Point3d[] Hull;

        /// <summary>Extent across (X) and along (Y) the section, measured both ways from
        /// Position, so that a section centred on the centreline covers the hull.</summary>
        public double Width, Height;

        /// <summary>Extent of the hull along its own principal axes, measured both ways from its
        /// centre: a measure of the section's size that doesn't depend on where the centreline is,
        /// for comparing sections.</summary>
        public double Major, Minor;

        /// <summary>
        /// How far this section's size is from a reference section's, as the larger relative
        /// difference of the two extents (0: the same).
        /// </summary>
        public double DifferenceFrom(Section reference) => Math.Max(
            Math.Abs(Major / reference.Major - 1),
            Math.Abs(Minor / reference.Minor - 1));

        public Polyline Outline
        {
            get
            {
                var polyline = new Polyline(Hull);
                if (Hull.Length > 0) polyline.Add(Hull[0]);
                return polyline;
            }
        }
    }

    /// <summary>
    /// Edge polylines prepared for cutting with planes many times over. Thread-safe.
    /// </summary>
    public sealed class EdgeSet
    {
        readonly Point3d[][] m_polylines;

        public EdgeSet(IEnumerable<Polyline> polylines)
        {
            m_polylines = polylines.Where(p => p != null && p.Count > 1).Select(p => p.ToArray()).ToArray();
        }

        public int Count => m_polylines.Length;

        /// <summary>
        /// Where a plane crosses each polyline, one point per polyline: the crossing nearest the
        /// origin. (The Python version took the first crossing along the polyline, which on a
        /// strongly curved beam can be a crossing far away from the section.)
        /// </summary>
        public int Cut(Point3d origin, Vector3d normal, List<Point3d> hits) => Cut(origin, normal, hits, null);

        /// <summary>
        /// As Cut, also listing which polyline each point is on.
        /// </summary>
        public int Cut(Point3d origin, Vector3d normal, List<Point3d> hits, List<int> indices)
        {
            hits.Clear();
            indices?.Clear();
            if (!normal.Unitize()) return 0;

            for (int k = 0; k < m_polylines.Length; ++k)
            {
                var poly = m_polylines[k];
                double best = double.MaxValue;
                Point3d hit = Point3d.Unset;
                double d0 = (poly[0] - origin) * normal;
                for (int i = 1; i < poly.Length; ++i)
                {
                    double d1 = (poly[i] - origin) * normal;
                    // Only segments that change side (or touch the plane) can cross it
                    if ((d0 <= 0 && d1 >= 0) || (d0 >= 0 && d1 <= 0))
                    {
                        double denom = d0 - d1;
                        double t = Math.Abs(denom) > 1e-12 ? d0 / denom : 0;
                        var p = poly[i - 1] + (poly[i] - poly[i - 1]) * t;
                        double dist = p.DistanceToSquared(origin);
                        if (dist < best) { best = dist; hit = p; }
                    }
                    d0 = d1;
                }
                if (hit.IsValid) { hits.Add(hit); indices?.Add(k); }
            }
            return hits.Count;
        }

        /// <summary>
        /// Centre of the section through the edges at a plane: the centre of the rectangle around
        /// its hull that lines up with its sides. The fast path for fitting a centreline.
        /// </summary>
        public bool TryCentre(Point3d origin, Vector3d normal, List<Point3d> hits, out Point3d centre)
        {
            centre = Point3d.Unset;
            if (Cut(origin, normal, hits) < 3) return false;

            var hull = Hull2d.Compute(hits, new Plane(origin, normal));
            if (hull.Length < 3) return false;

            var corners = new Point3d[hull.Length];
            for (int i = 0; i < hull.Length; ++i) corners[i] = hits[hull[i]];
            Sections.AlignedRectangle(corners, normal, out centre, out _, out _, out _);
            return true;
        }

        /// <summary>
        /// The section through the edges at a plane, with its frame and extents.
        /// </summary>
        public bool TrySection(Point3d origin, Vector3d normal, out Section section)
        {
            var hits = new List<Point3d>();
            Cut(origin, normal, hits);
            return Sections.TryFromPoints(hits, origin, normal, out section);
        }
    }

    public static class Sections
    {
        /// <summary>
        /// A section from points in (or near) the plane at origin with this normal: their convex
        /// hull, its principal axes and its extents about origin.
        /// </summary>
        public static bool TryFromPoints(IList<Point3d> points, Point3d origin, Vector3d normal, out Section section)
        {
            section = default;
            if (points.Count < 3 || !normal.Unitize()) return false;

            var hullIndices = Hull2d.Compute(points, new Plane(origin, normal));
            if (hullIndices.Length < 3) return false;

            var hull = hullIndices.Select(i => points[i]).ToArray();

            // The rectangle around the hull lined up with its sides: its centre and sides stay put
            // when the section loses a corner (e.g. near a skew end cut), where the mean of the
            // corners or the principal axes would move. Its long side is the section's height
            // direction (on a square section, MakeConsistent settles which side that is).
            AlignedRectangle(hull, normal, out Point3d centre, out Vector3d y, out double major, out double minor);
            var plane = Utility.PlaneFromNormalAndYAxis(origin, normal, y);

            double width = 0, height = 0;
            foreach (var p in hull)
            {
                var d = p - origin;
                width = Math.Max(width, Math.Abs(d * plane.XAxis));
                height = Math.Max(height, Math.Abs(d * plane.YAxis));
            }

            section = new Section
            {
                Position = origin,
                Centre = centre,
                Plane = plane,
                Hull = hull,
                Width = width * 2,
                Height = height * 2,
                Major = major,
                Minor = minor
            };
            return true;
        }

        /// <summary>Sides within this fraction of each other count as equal: a square section,
        /// whose height direction MakeConsistent takes from the direction the beam bends in.</summary>
        public const double SquareTolerance = 0.02;

        /// <summary>
        /// The rectangle around a convex polygon in a plane that lines up with the polygon's
        /// straight sides: of the directions of its edges, the one along or square to which the
        /// most edge length runs (within angleTolerance), and of equals the one with the smallest
        /// rectangle. For a rectangle with its corners cut (bevels, a groove) that is the
        /// rectangle's own direction, where the smallest rectangle could line up with a bevel.
        /// Returns its centre, the direction of its long side, and its sides.
        /// </summary>
        /// <param name="hull">Polygon corners, in order.</param>
        /// <param name="normal">Normal of the polygon's plane.</param>
        public static void AlignedRectangle(IList<Point3d> hull, Vector3d normal, out Point3d centre, out Vector3d longAxis, out double longSide, out double shortSide, double angleTolerance = 0)
        {
            normal.Unitize();
            if (angleTolerance <= 0) angleTolerance = RhinoMath.ToRadians(2);
            int n = hull.Count;
            var o = hull[0];
            centre = o; longAxis = Vector3d.Unset; longSide = shortSide = 0;

            var directions = new Vector3d[n];
            var lengths = new double[n];
            for (int i = 0; i < n; ++i)
            {
                var e = hull[(i + 1) % n] - hull[i];
                e -= normal * (e * normal);
                lengths[i] = e.Length;
                directions[i] = e.Unitize() ? e : Vector3d.Unset;
            }

            double cos2 = Math.Cos(angleTolerance);
            double bestScore = -1, bestArea = double.MaxValue;
            for (int i = 0; i < n; ++i)
            {
                var e = directions[i];
                if (!e.IsValid) continue;
                var f = Vector3d.CrossProduct(normal, e);

                // Edge length along or square to this direction
                double score = 0;
                for (int j = 0; j < n; ++j)
                {
                    if (!directions[j].IsValid) continue;
                    double c = Math.Abs(directions[j] * e), s = Math.Abs(directions[j] * f);
                    if (c >= cos2 || s >= cos2) score += lengths[j];
                }

                double minE = double.MaxValue, maxE = double.MinValue, minF = double.MaxValue, maxF = double.MinValue;
                foreach (var p in hull)
                {
                    var d = p - o;
                    double pe = d * e, pf = d * f;
                    if (pe < minE) minE = pe; if (pe > maxE) maxE = pe;
                    if (pf < minF) minF = pf; if (pf > maxF) maxF = pf;
                }
                double se = maxE - minE, sf = maxF - minF, area = se * sf;

                bool better = score > bestScore * 1.01 || (score >= bestScore * 0.99 && area < bestArea);
                if (!better) continue;

                bestScore = Math.Max(score, bestScore);
                bestArea = area;
                centre = o + e * (0.5 * (minE + maxE)) + f * (0.5 * (minF + maxF));
                if (se >= sf) { longAxis = e; longSide = se; shortSide = sf; }
                else { longAxis = f; longSide = sf; shortSide = se; }
            }
        }

        /// <summary>
        /// Turn section frames about their Z-axis where needed so that each one's Y-axis points the
        /// same way as its neighbour's: by 180 degrees, or by 90 degrees (swapping its width and
        /// height) where its sides have swapped, which happens on a square section (which side is
        /// the height is a matter of rounding) and where a joint has cut a section below its width.
        /// Starts from the middle section, which is the most likely to be typical.
        /// </summary>
        /// <param name="up">Where the middle section is square: the direction its Y-axis should be
        /// closest to (e.g. the direction the beam bends in). Unset: as found.</param>
        public static void MakeConsistent(IList<Section> sections, Vector3d up = default)
        {
            int n = sections.Count;
            if (n == 0) return;
            int middle = n / 2;

            var m = sections[middle];
            if (up.IsValid && !up.IsZero && Math.Abs(m.Major - m.Minor) <= SquareTolerance * Math.Max(m.Major, m.Minor))
                sections[middle] = Follow(m, up, false);

            for (int i = middle + 1; i < n; ++i) sections[i] = Follow(sections[i], sections[i - 1].Plane.YAxis, true);
            for (int i = middle - 1; i >= 0; --i) sections[i] = Follow(sections[i], sections[i + 1].Plane.YAxis, true);
        }

        /// <summary>
        /// A section turned by 90 degrees about its Z-axis if its X-axis is closer to the direction
        /// than its Y-axis, then (if flip) by 180 degrees if its Y-axis points away from it.
        /// </summary>
        static Section Follow(Section s, Vector3d direction, bool flip)
        {
            if (Math.Abs(s.Plane.XAxis * direction) > Math.Abs(s.Plane.YAxis * direction))
            {
                s.Plane = new Plane(s.Plane.Origin, s.Plane.YAxis, -s.Plane.XAxis);
                (s.Width, s.Height) = (s.Height, s.Width);
            }
            if (flip && s.Plane.YAxis * direction < 0)
                s.Plane = new Plane(s.Plane.Origin, -s.Plane.XAxis, -s.Plane.YAxis);
            return s;
        }

        /// <summary>
        /// The direction a square section's height should take along a curve: the direction it bends
        /// in at its middle, or world Z where it is straight.
        /// </summary>
        public static Vector3d PreferredUp(Curve curve)
        {
            var k = curve.CurvatureAt(curve.Domain.Mid);
            return k.IsValid && k.Length > 1e-9 ? k / k.Length : Vector3d.ZAxis;
        }
    }
}
