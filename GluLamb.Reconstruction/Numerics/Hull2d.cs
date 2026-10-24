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

using Rhino.Geometry;

namespace GluLamb.Reconstruction.Numerics
{
    public static class Hull2d
    {
        /// <summary>
        /// Convex hull of points in a plane (monotone chain), as indices into the points in
        /// counter-clockwise order about the plane's normal. Points in a line give no hull.
        /// </summary>
        /// <param name="points">Points, in or near the plane.</param>
        /// <param name="plane">Plane to project the points onto.</param>
        /// <param name="tolerance">Hull points closer than this to the line through their
        /// neighbours are left out.</param>
        public static int[] Compute(IList<Point3d> points, Plane plane, double tolerance = 1e-2)
        {
            int n = points.Count;
            if (n < 3) return Array.Empty<int>();

            var xs = new double[n]; var ys = new double[n];
            for (int i = 0; i < n; ++i)
            {
                var d = points[i] - plane.Origin;
                xs[i] = d * plane.XAxis; ys[i] = d * plane.YAxis;
            }

            var order = Enumerable.Range(0, n).ToArray();
            Array.Sort(order, (a, b) => xs[a] != xs[b] ? xs[a].CompareTo(xs[b]) : ys[a].CompareTo(ys[b]));

            double Cross(int o, int a, int b) => (xs[a] - xs[o]) * (ys[b] - ys[o]) - (ys[a] - ys[o]) * (xs[b] - xs[o]);

            var hull = new int[2 * n];
            int k = 0;
            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], order[i]) <= 0) k--;
                hull[k++] = order[i];
            }
            for (int i = n - 2, t = k + 1; i >= 0; --i)
            {
                while (k >= t && Cross(hull[k - 2], hull[k - 1], order[i]) <= 0) k--;
                hull[k++] = order[i];
            }
            k--; // the last point is the first

            if (k < 3) return Array.Empty<int>();

            // Drop points that are (nearly) in line with their neighbours
            var kept = new List<int>(k);
            for (int i = 0; i < k; ++i)
            {
                int prev = hull[(i + k - 1) % k], cur = hull[i], next = hull[(i + 1) % k];
                double ex = xs[next] - xs[prev], ey = ys[next] - ys[prev];
                double len = Math.Sqrt(ex * ex + ey * ey);
                double dist = len > 0 ? Math.Abs(Cross(prev, next, cur)) / len : 0;
                if (dist >= tolerance) kept.Add(cur);
            }

            return kept.Count < 3 ? Array.Empty<int>() : kept.ToArray();
        }
    }
}
