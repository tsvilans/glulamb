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

using Rhino.Geometry;

namespace GluLamb.Numerics
{
    /// <summary>
    /// Eigen decomposition of small symmetric matrices (cyclic Jacobi), e.g. covariance matrices for
    /// principal axes.
    /// </summary>
    public static class SymmetricEigen
    {
        /// <summary>
        /// Eigenvalues and eigenvectors of a symmetric n x n matrix, sorted by descending eigenvalue.
        /// The input is not changed. Eigenvector i is column i of vectors.
        /// </summary>
        public static void Decompose(double[,] matrix, out double[] values, out double[,] vectors)
        {
            int n = matrix.GetLength(0);
            var a = (double[,])matrix.Clone();
            var v = new double[n, n];
            for (int i = 0; i < n; ++i) v[i, i] = 1;

            for (int sweep = 0; sweep < 64; ++sweep)
            {
                double off = 0, diag = 0;
                for (int i = 0; i < n; ++i)
                {
                    diag += a[i, i] * a[i, i];
                    for (int j = i + 1; j < n; ++j) off += a[i, j] * a[i, j];
                }
                if (off <= 1e-28 * Math.Max(diag, 1e-300)) break;

                for (int p = 0; p < n - 1; ++p)
                    for (int q = p + 1; q < n; ++q)
                    {
                        if (Math.Abs(a[p, q]) < 1e-300) continue;
                        double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                        double t = theta == 0 ? 1 : Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        double c = 1 / Math.Sqrt(t * t + 1), s = t * c;

                        for (int k = 0; k < n; ++k)
                        {
                            double akp = a[k, p], akq = a[k, q];
                            a[k, p] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < n; ++k)
                        {
                            double apk = a[p, k], aqk = a[q, k];
                            a[p, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < n; ++k)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
            }

            var order = new int[n];
            values = new double[n];
            for (int i = 0; i < n; ++i) { order[i] = i; values[i] = a[i, i]; }
            var keys = (double[])values.Clone();
            Array.Sort(keys, order);
            Array.Reverse(order);

            vectors = new double[n, n];
            for (int i = 0; i < n; ++i)
            {
                values[i] = a[order[i], order[i]];
                for (int k = 0; k < n; ++k) vectors[k, i] = v[k, order[i]];
            }
        }

        /// <summary>
        /// Eigen decomposition of a symmetric 3 x 3 matrix, as unit vectors sorted by descending
        /// eigenvalue.
        /// </summary>
        public static void Decompose3(double[,] matrix, out double[] values, out Vector3d[] axes)
        {
            Decompose(matrix, out values, out double[,] vectors);
            axes = new Vector3d[3];
            for (int i = 0; i < 3; ++i)
            {
                axes[i] = new Vector3d(vectors[0, i], vectors[1, i], vectors[2, i]);
                axes[i].Unitize();
            }
        }
    }
}
