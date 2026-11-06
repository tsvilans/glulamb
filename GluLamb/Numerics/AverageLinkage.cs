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
    public static class AverageLinkage
    {
        /// <summary>
        /// Agglomerative clustering with average linkage (UPGMA), cut at a distance: the two closest
        /// clusters are merged until no two are closer than the threshold. Same as scipy's
        /// fcluster(linkage(squareform(d), "average"), threshold, "distance").
        /// </summary>
        /// <param name="distances">Symmetric matrix of distances between the items.</param>
        /// <param name="threshold">Largest distance at which clusters are merged.</param>
        /// <returns>A cluster label (0, 1, ...) for each item.</returns>
        public static int[] Cluster(double[,] distances, double threshold)
        {
            int n = distances.GetLength(0);
            var d = (double[,])distances.Clone();
            var size = new int[n];
            var active = new bool[n];
            var parent = new int[n];
            for (int i = 0; i < n; ++i) { size[i] = 1; active[i] = true; parent[i] = i; }

            for (int merges = 0; merges < n - 1; ++merges)
            {
                int bi = -1, bj = -1;
                double best = double.MaxValue;
                for (int i = 0; i < n; ++i)
                {
                    if (!active[i]) continue;
                    for (int j = i + 1; j < n; ++j)
                        if (active[j] && d[i, j] < best) { best = d[i, j]; bi = i; bj = j; }
                }

                if (bi < 0 || best > threshold) break;

                // Merge bj into bi (Lance-Williams update for average linkage)
                for (int k = 0; k < n; ++k)
                {
                    if (!active[k] || k == bi || k == bj) continue;
                    double v = (size[bi] * d[bi, k] + size[bj] * d[bj, k]) / (size[bi] + size[bj]);
                    d[bi, k] = d[k, bi] = v;
                }
                size[bi] += size[bj];
                active[bj] = false;
                parent[bj] = bi;
            }

            int Find(int i) { while (parent[i] != i) i = parent[i]; return i; }

            var labels = new int[n];
            var map = new Dictionary<int, int>();
            for (int i = 0; i < n; ++i)
            {
                int root = Find(i);
                if (!map.TryGetValue(root, out int label)) map[root] = label = map.Count;
                labels[i] = label;
            }
            return labels;
        }
    }
}
