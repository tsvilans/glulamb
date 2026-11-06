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

namespace GluLamb.Numerics
{
    /// <summary>
    /// A cubic B-spline through a list of points, parameterised by chord length on [0, 1], with
    /// knots at the interior points (not-a-knot ends). The same spline as scipy's
    /// splprep(s=0, k=3), so a fit made with it can be turned into an identical NurbsCurve.
    /// Fewer than 4 points give a lower degree. Thread-safe once built.
    /// </summary>
    public sealed class InterpolatingSpline
    {
        readonly int m_degree;
        readonly double[] m_knots;     // full knot vector, Count + degree + 1 values
        readonly Point3d[] m_cvs;

        public int Degree => m_degree;
        public int Count => m_cvs.Length;

        public InterpolatingSpline(IList<Point3d> points)
        {
            int n = points.Count;
            if (n < 2) throw new ArgumentException("A spline needs at least two points.");
            int k = Math.Min(3, n - 1);
            m_degree = k;

            // Chord-length parameters
            var u = new double[n];
            for (int i = 1; i < n; ++i)
                u[i] = u[i - 1] + Math.Max(points[i].DistanceTo(points[i - 1]), 1e-12);
            double total = u[n - 1];
            for (int i = 1; i < n; ++i) u[i] /= total;
            u[n - 1] = 1.0;

            // Knots: clamped ends, interior knots at the data parameters (odd degree) or between them
            m_knots = new double[n + k + 1];
            for (int i = 0; i <= k; ++i) { m_knots[i] = 0.0; m_knots[n + i] = 1.0; }
            for (int j = 0; j < n - k - 1; ++j)
                m_knots[k + 1 + j] = (k % 2 == 1) ? u[j + (k + 1) / 2] : 0.5 * (u[j + k / 2] + u[j + k / 2 + 1]);

            // Collocation: the spline passes through each point at its parameter
            var a = new double[n, n];
            var N = new double[k + 1];
            for (int i = 0; i < n; ++i)
            {
                int span = FindSpan(u[i]);
                Basis(span, u[i], k, N);
                for (int r = 0; r <= k; ++r) a[i, span - k + r] = N[r];
            }

            m_cvs = Solve(a, points);
        }

        int FindSpan(double t)
        {
            int n = m_cvs == null ? m_knots.Length - m_degree - 1 : m_cvs.Length;
            if (t >= m_knots[n]) return n - 1;
            if (t <= m_knots[m_degree]) return m_degree;

            int lo = m_degree, hi = n;
            int mid = (lo + hi) / 2;
            while (t < m_knots[mid] || t >= m_knots[mid + 1])
            {
                if (t < m_knots[mid]) hi = mid; else lo = mid;
                mid = (lo + hi) / 2;
            }
            return mid;
        }

        /// <summary>
        /// The nonzero basis functions of degree p at t (Piegl and Tiller, A2.2).
        /// </summary>
        void Basis(int span, double t, int p, double[] N)
        {
            Span<double> left = stackalloc double[4], right = stackalloc double[4];
            N[0] = 1.0;
            for (int j = 1; j <= p; ++j)
            {
                left[j] = t - m_knots[span + 1 - j];
                right[j] = m_knots[span + j] - t;
                double saved = 0.0;
                for (int r = 0; r < j; ++r)
                {
                    double temp = N[r] / (right[r + 1] + left[j - r]);
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                N[j] = saved;
            }
        }

        public Point3d PointAt(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            int k = m_degree, span = FindSpan(t);
            var N = new double[k + 1];
            Basis(span, t, k, N);

            double x = 0, y = 0, z = 0;
            for (int r = 0; r <= k; ++r)
            {
                var p = m_cvs[span - k + r];
                x += N[r] * p.X; y += N[r] * p.Y; z += N[r] * p.Z;
            }
            return new Point3d(x, y, z);
        }

        /// <summary>
        /// Point and first derivative at t.
        /// </summary>
        public void Evaluate(double t, out Point3d point, out Vector3d derivative)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            int k = m_degree, span = FindSpan(t);
            var N = new double[k + 1];
            var M = new double[k];
            Basis(span, t, k, N);
            Basis(span, t, k - 1, M);

            double x = 0, y = 0, z = 0, dx = 0, dy = 0, dz = 0;
            for (int r = 0; r <= k; ++r)
            {
                int i = span - k + r;
                // N'(i, k) = k * (N(i, k-1) / (t[i+k] - t[i]) - N(i+1, k-1) / (t[i+k+1] - t[i+1]));
                // N(i, k-1) is M[r - 1] (zero for r = 0), N(i+1, k-1) is M[r] (zero for r = k)
                double d = 0;
                if (r > 0) { double den = m_knots[i + k] - m_knots[i]; if (den > 0) d += M[r - 1] / den; }
                if (r < k) { double den = m_knots[i + k + 1] - m_knots[i + 1]; if (den > 0) d -= M[r] / den; }
                d *= k;

                var p = m_cvs[i];
                x += N[r] * p.X; y += N[r] * p.Y; z += N[r] * p.Z;
                dx += d * p.X; dy += d * p.Y; dz += d * p.Z;
            }
            point = new Point3d(x, y, z);
            derivative = new Vector3d(dx, dy, dz);
        }

        /// <summary>
        /// The spline as a NurbsCurve on the domain [0, 1].
        /// </summary>
        public NurbsCurve ToNurbsCurve()
        {
            int n = m_cvs.Length, k = m_degree;
            var curve = new NurbsCurve(3, false, k + 1, n);
            for (int i = 0; i < n; ++i) curve.Points.SetPoint(i, m_cvs[i]);
            // Rhino leaves out the first and last value of the full knot vector
            for (int i = 0; i < n + k - 1; ++i) curve.Knots[i] = m_knots[i + 1];
            return curve;
        }

        /// <summary>
        /// Solve a x = b for the three coordinates of b (Gaussian elimination, partial pivoting).
        /// </summary>
        static Point3d[] Solve(double[,] a, IList<Point3d> b)
        {
            int n = b.Count;
            var x = new double[n]; var y = new double[n]; var z = new double[n];
            for (int i = 0; i < n; ++i) { x[i] = b[i].X; y[i] = b[i].Y; z[i] = b[i].Z; }

            for (int c = 0; c < n; ++c)
            {
                int pivot = c;
                for (int r = c + 1; r < n; ++r)
                    if (Math.Abs(a[r, c]) > Math.Abs(a[pivot, c])) pivot = r;
                if (Math.Abs(a[pivot, c]) < 1e-300) throw new InvalidOperationException("Singular spline system.");

                if (pivot != c)
                {
                    for (int j = c; j < n; ++j) (a[c, j], a[pivot, j]) = (a[pivot, j], a[c, j]);
                    (x[c], x[pivot]) = (x[pivot], x[c]);
                    (y[c], y[pivot]) = (y[pivot], y[c]);
                    (z[c], z[pivot]) = (z[pivot], z[c]);
                }

                // The matrix is banded, so only the next few rows have anything below the pivot
                int last = Math.Min(n, c + 5);
                for (int r = c + 1; r < last; ++r)
                {
                    double f = a[r, c] / a[c, c];
                    if (f == 0) continue;
                    for (int j = c; j < Math.Min(n, c + 9); ++j) a[r, j] -= f * a[c, j];
                    x[r] -= f * x[c]; y[r] -= f * y[c]; z[r] -= f * z[c];
                }
            }

            var result = new Point3d[n];
            for (int i = n - 1; i >= 0; --i)
            {
                double sx = x[i], sy = y[i], sz = z[i];
                for (int j = i + 1; j < Math.Min(n, i + 9); ++j)
                {
                    sx -= a[i, j] * result[j].X; sy -= a[i, j] * result[j].Y; sz -= a[i, j] * result[j].Z;
                }
                result[i] = new Point3d(sx / a[i, i], sy / a[i, i], sz / a[i, i]);
            }
            return result;
        }
    }
}
