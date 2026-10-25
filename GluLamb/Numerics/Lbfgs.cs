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

namespace GluLamb.Numerics
{
    public struct MinimizeResult
    {
        public double[] X;
        public double Value;
        public int Iterations;
        public int Evaluations;
        public bool Converged;
        public string Message;

        public override string ToString() => $"{Message}: f = {Value:0.######} after {Iterations} iterations ({Evaluations} evaluations)";
    }

    /// <summary>
    /// Limited-memory BFGS minimiser with finite-difference gradients, for objectives that have no
    /// analytic gradient (like scipy's minimize(method="L-BFGS-B") without jac or bounds). The
    /// gradient's evaluations run in parallel, so the objective has to be thread-safe.
    /// </summary>
    public sealed class Lbfgs
    {
        /// <summary>Largest number of iterations (scipy's maxiter).</summary>
        public int MaxIterations = 20;

        /// <summary>Stop when (f_k - f_k+1) / max(|f_k|, |f_k+1|, 1) is at most this (scipy's ftol).</summary>
        public double FunctionTolerance = 1e-2;

        /// <summary>Stop when the largest gradient component is at most this (scipy's gtol).</summary>
        public double GradientTolerance = 1e-5;

        /// <summary>Number of correction pairs kept (scipy's maxcor).</summary>
        public int Memory = 10;

        /// <summary>Step of the forward differences for the gradient, in the units of x.</summary>
        public double GradientStep = 1e-4;

        /// <summary>Called after each iteration with the iteration, x and f(x).</summary>
        public Action<int, double[], double> Progress;

        int m_evaluations;

        public MinimizeResult Minimize(Func<double[], double> f, double[] x0)
        {
            int n = x0.Length;
            m_evaluations = 0;

            var x = (double[])x0.Clone();
            double fx = Eval(f, x);
            var g = Gradient(f, x, fx);

            var s = new List<double[]>();
            var y = new List<double[]>();
            var rho = new List<double>();

            var result = new MinimizeResult { Message = "Iteration limit reached" };
            int iteration = 0;

            for (; iteration < MaxIterations; ++iteration)
            {
                if (MaxAbs(g) <= GradientTolerance) { result.Converged = true; result.Message = "Gradient below tolerance"; break; }

                var d = Direction(g, s, y, rho);
                double slope = Dot(g, d);
                if (slope >= 0)
                {
                    // Not a descent direction: start again from steepest descent
                    s.Clear(); y.Clear(); rho.Clear();
                    for (int i = 0; i < n; ++i) d[i] = -g[i];
                    slope = Dot(g, d);
                }

                // First step: move about one unit along steepest descent
                double step = s.Count == 0 ? Math.Min(1.0, 1.0 / Math.Sqrt(Dot(g, g))) : 1.0;

                // Backtracking line search (Armijo)
                // The objectives here are only piecewise smooth (a section can lose a corner), so a
                // finite-difference slope can overstate what a step gains; if no step meets the
                // Armijo condition, the best step that lowers f at all is taken
                var xNew = new double[n];
                var xBest = new double[n];
                double fNew = double.PositiveInfinity, fBest = fx;
                bool found = false;
                for (int ls = 0; ls < 30; ++ls)
                {
                    for (int i = 0; i < n; ++i) xNew[i] = x[i] + step * d[i];
                    fNew = Eval(f, xNew);
                    if (fNew <= fx + 1e-4 * step * slope) { found = true; break; }
                    if (fNew < fBest) { fBest = fNew; Array.Copy(xNew, xBest, n); }
                    step *= 0.5;
                }

                if (!found)
                {
                    if (fBest >= fx) { result.Message = "Line search failed"; break; }
                    Array.Copy(xBest, xNew, n);
                    fNew = fBest;
                }

                var gNew = Gradient(f, xNew, fNew);

                var si = new double[n];
                var yi = new double[n];
                for (int i = 0; i < n; ++i) { si[i] = xNew[i] - x[i]; yi[i] = gNew[i] - g[i]; }
                double sy = Dot(si, yi);
                if (sy > 1e-10)
                {
                    if (s.Count == Memory) { s.RemoveAt(0); y.RemoveAt(0); rho.RemoveAt(0); }
                    s.Add(si); y.Add(yi); rho.Add(1.0 / sy);
                }

                double change = (fx - fNew) / Math.Max(Math.Max(Math.Abs(fx), Math.Abs(fNew)), 1.0);

                x = xNew; fx = fNew; g = gNew;
                Progress?.Invoke(iteration, x, fx);

                if (change <= FunctionTolerance)
                {
                    ++iteration;
                    result.Converged = true;
                    result.Message = "Relative reduction below tolerance";
                    break;
                }
            }

            result.X = x;
            result.Value = fx;
            result.Iterations = iteration;
            result.Evaluations = m_evaluations;
            return result;
        }

        double Eval(Func<double[], double> f, double[] x)
        {
            Interlocked.Increment(ref m_evaluations);
            return f(x);
        }

        double[] Gradient(Func<double[], double> f, double[] x, double fx)
        {
            int n = x.Length;
            var g = new double[n];
            Parallel.For(0, n, () => (double[])x.Clone(), (i, state, local) =>
            {
                double xi = local[i];
                double h = GradientStep * Math.Max(1.0, Math.Abs(xi) * 1e-6);
                local[i] = xi + h;
                g[i] = (Eval(f, local) - fx) / h;
                local[i] = xi;
                return local;
            }, _ => { });
            return g;
        }

        /// <summary>
        /// Two-loop recursion: the quasi-Newton direction -H g.
        /// </summary>
        static double[] Direction(double[] g, List<double[]> s, List<double[]> y, List<double> rho)
        {
            int n = g.Length, m = s.Count;
            var q = new double[n];
            for (int i = 0; i < n; ++i) q[i] = -g[i];

            var alpha = new double[m];
            for (int j = m - 1; j >= 0; --j)
            {
                alpha[j] = rho[j] * Dot(s[j], q);
                for (int i = 0; i < n; ++i) q[i] -= alpha[j] * y[j][i];
            }

            if (m > 0)
            {
                double gamma = Dot(s[m - 1], y[m - 1]) / Dot(y[m - 1], y[m - 1]);
                for (int i = 0; i < n; ++i) q[i] *= gamma;
            }

            for (int j = 0; j < m; ++j)
            {
                double beta = rho[j] * Dot(y[j], q);
                for (int i = 0; i < n; ++i) q[i] += s[j][i] * (alpha[j] - beta);
            }
            return q;
        }

        static double Dot(double[] a, double[] b)
        {
            double d = 0;
            for (int i = 0; i < a.Length; ++i) d += a[i] * b[i];
            return d;
        }

        static double MaxAbs(double[] a)
        {
            double m = 0;
            for (int i = 0; i < a.Length; ++i) m = Math.Max(m, Math.Abs(a[i]));
            return m;
        }
    }
}
