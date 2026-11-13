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
    /// A cross-section of a beam's edges: the points where a plane cuts them, and their convex hull.
    /// </summary>
    public struct Section
    {
        /// <summary>The point of the section on the centreline.</summary>
        public Point3d Position;

        /// <summary>Centre of the aligned rectangle around the hull. See Sections.AlignedRectangle.</summary>
        public Point3d Centre;

        /// <summary>Plane at Position. Z: along the centreline. Y: the long side (height). X: across.</summary>
        public Plane Plane;

        /// <summary>Hull corners, counter-clockwise.</summary>
        public Point3d[] Hull;

        /// <summary>Extents along X and Y, measured both ways from Position. A section centred on
        /// the centreline with these extents covers the hull.</summary>
        public double Width, Height;

        /// <summary>Sides of the aligned rectangle. They do not depend on the centreline, so they
        /// compare sections.</summary>
        public double Major, Minor;

        /// <summary>
        /// Difference in size from a reference section: the larger relative difference of the two
        /// sides. 0: the same.
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
    /// Edge polylines, prepared to be cut by many planes. Thread-safe.
    /// </summary>
    public sealed class EdgeSet
    {
        readonly Point3d[][] m_polylines;

        readonly double m_rectangleAngle;

        /// <param name="rectangleAngle">See ReconstructionTolerances.RectangleAngle. 0: its default.</param>
        public EdgeSet(IEnumerable<Polyline> polylines, double rectangleAngle = 0)
        {
            m_rectangleAngle = rectangleAngle;
            m_polylines = polylines.Where(p => p != null && p.Count > 1).Select(p => p.ToArray()).ToArray();
        }

        public int Count => m_polylines.Length;

        /// <summary>
        /// Cuts each polyline with a plane. One point per polyline: the crossing nearest the origin.
        /// </summary>
        public int Cut(Point3d origin, Vector3d normal, List<Point3d> hits) => Cut(origin, normal, hits, null);

        /// <summary>
        /// As Cut. Also gives the polyline index of each point.
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
                    // A segment crosses the plane only if its ends are on different sides (or on the plane)
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
        /// Centre of the section at a plane: the centre of the aligned rectangle around its hull.
        /// The fast path for the fit.
        /// </summary>
        public bool TryCentre(Point3d origin, Vector3d normal, List<Point3d> hits, out Point3d centre)
        {
            centre = Point3d.Unset;
            if (Cut(origin, normal, hits) < 3) return false;

            var hull = Hull2d.Compute(hits, new Plane(origin, normal));
            if (hull.Length < 3) return false;

            var corners = new Point3d[hull.Length];
            for (int i = 0; i < hull.Length; ++i) corners[i] = hits[hull[i]];
            Sections.AlignedRectangle(corners, normal, out centre, out _, out _, out _, m_rectangleAngle);
            return true;
        }

        /// <summary>
        /// The section at a plane, with its frame and extents.
        /// </summary>
        public bool TrySection(Point3d origin, Vector3d normal, out Section section)
        {
            var hits = new List<Point3d>();
            Cut(origin, normal, hits);
            return Sections.TryFromPoints(hits, origin, normal, out section, m_rectangleAngle);
        }
    }

    public static class Sections
    {
        /// <summary>
        /// A section from points in the plane at origin: their convex hull, frame and extents.
        /// </summary>
        public static bool TryFromPoints(IList<Point3d> points, Point3d origin, Vector3d normal, out Section section, double rectangleAngle = 0)
        {
            section = default;
            if (points.Count < 3 || !normal.Unitize()) return false;

            var hullIndices = Hull2d.Compute(points, new Plane(origin, normal));
            if (hullIndices.Length < 3) return false;

            var hull = hullIndices.Select(i => points[i]).ToArray();

            // The aligned rectangle keeps its centre and sides when the section loses a corner
            // (e.g. at a skew end cut). The mean of the corners and the principal axes do not.
            // Its long side is the height direction. For a square, MakeConsistent chooses the side.
            AlignedRectangle(hull, normal, out Point3d centre, out Vector3d y, out double major, out double minor, rectangleAngle);
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

        /// <summary>Sides within this fraction of each other are equal: a square section.</summary>
        public const double SquareTolerance = 0.02;

        /// <summary>
        /// The rectangle around a convex polygon, aligned to its straight sides. Its direction is
        /// the edge direction with the most edge length along or square to it (within
        /// angleTolerance). Of equals: the smallest rectangle. A rectangle with cut corners
        /// (bevels, a groove) keeps its own direction. Gives the centre, the direction of the long
        /// side, and the sides.
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
        /// Turns section frames about Z so that each Y-axis points the same way as its
        /// neighbour's. A turn is 180 degrees, or 90 degrees (width and height swap) where the
        /// sides swapped: on a square section, or where a joint cut the section below its width.
        /// Starts from the middle section.
        /// </summary>
        /// <param name="up">If the middle section is square: its Y-axis takes the side nearest
        /// this direction. Unset: no change.</param>
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
        /// Turns a section 90 degrees about Z if its X-axis is nearer the direction than its
        /// Y-axis. Then, if flip, 180 degrees if its Y-axis points away.
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
        /// The height direction for a square section: the direction that the curve bends at its
        /// middle. A straight curve: world Z.
        /// </summary>
        public static Vector3d PreferredUp(Curve curve)
        {
            var k = curve.CurvatureAt(curve.Domain.Mid);
            return k.IsValid && k.Length > 1e-9 ? k / k.Length : Vector3d.ZAxis;
        }
    }
}
