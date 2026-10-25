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
    /// A box along a set of principal axes: Plane is at the box's minimum corner, X along the
    /// largest extent.
    /// </summary>
    public struct OrientedBounds
    {
        public Plane Plane;
        public double SizeX, SizeY, SizeZ;

        public Box ToBox() => new Box(Plane, new Interval(0, SizeX), new Interval(0, SizeY), new Interval(0, SizeZ));

        /// <summary>
        /// Centre of the box's section at x (a length along the X-axis).
        /// </summary>
        public Point3d SectionCentre(double x) => Plane.PointAt(x, SizeY * 0.5) + Plane.ZAxis * (SizeZ * 0.5);
    }

    public static class Pca
    {
        /// <summary>
        /// Principal axes of a mesh, with each vertex weighted by its share of the surface area, so
        /// that dense patches of mesh don't pull the axes. X is the axis of largest spread. Of the
        /// other two, Y is the one the beam bends along most (the larger variation of the mean
        /// position across 10 bins along X), and Z completes a right-handed frame.
        /// </summary>
        public static OrientedBounds WeightedMesh(Mesh mesh)
        {
            int n = mesh.Vertices.Count;
            var points = new Point3d[n];
            for (int i = 0; i < n; ++i) points[i] = mesh.Vertices.Point3dAt(i);

            var weights = new double[n];
            foreach (var face in mesh.Faces)
            {
                AddTriangle(points, weights, face.A, face.B, face.C);
                if (face.IsQuad) AddTriangle(points, weights, face.A, face.C, face.D);
            }

            double total = weights.Sum();
            if (total <= 0) { for (int i = 0; i < n; ++i) weights[i] = 1; total = n; }

            var mean = Point3d.Origin;
            for (int i = 0; i < n; ++i) mean += points[i] * (weights[i] / total);

            var cov = new double[3, 3];
            for (int i = 0; i < n; ++i)
            {
                var d = points[i] - mean;
                double w = weights[i] / total;
                cov[0, 0] += w * d.X * d.X; cov[0, 1] += w * d.X * d.Y; cov[0, 2] += w * d.X * d.Z;
                cov[1, 1] += w * d.Y * d.Y; cov[1, 2] += w * d.Y * d.Z; cov[2, 2] += w * d.Z * d.Z;
            }
            cov[1, 0] = cov[0, 1]; cov[2, 0] = cov[0, 2]; cov[2, 1] = cov[1, 2];

            SymmetricEigen.Decompose3(cov, out _, out Vector3d[] axes);
            return Bounds(points, axes[0], axes[1], axes[2]);
        }

        /// <summary>
        /// Principal axes of points (equal weights), as in WeightedMesh.
        /// </summary>
        public static OrientedBounds Points(IList<Point3d> points)
        {
            PrincipalAxes(points, out _, out Vector3d[] axes);
            return Bounds(points, axes[0], axes[1], axes[2]);
        }

        /// <summary>
        /// Mean and principal axes of points, sorted by descending spread.
        /// </summary>
        public static void PrincipalAxes(IList<Point3d> points, out Point3d centre, out Vector3d[] axes)
        {
            int n = points.Count;
            var mean = Point3d.Origin;
            for (int i = 0; i < n; ++i) mean += points[i];
            mean /= n;

            var cov = new double[3, 3];
            for (int i = 0; i < n; ++i)
            {
                var d = points[i] - mean;
                cov[0, 0] += d.X * d.X; cov[0, 1] += d.X * d.Y; cov[0, 2] += d.X * d.Z;
                cov[1, 1] += d.Y * d.Y; cov[1, 2] += d.Y * d.Z; cov[2, 2] += d.Z * d.Z;
            }
            cov[1, 0] = cov[0, 1]; cov[2, 0] = cov[0, 2]; cov[2, 1] = cov[1, 2];

            SymmetricEigen.Decompose3(cov, out _, out axes);
            centre = mean;
        }

        static void AddTriangle(Point3d[] p, double[] w, int a, int b, int c)
        {
            double area = Vector3d.CrossProduct(p[b] - p[a], p[c] - p[a]).Length * 0.5 / 3.0;
            w[a] += area; w[b] += area; w[c] += area;
        }

        static OrientedBounds Bounds(IList<Point3d> points, Vector3d x, Vector3d y, Vector3d z)
        {
            const int bins = 10;
            int n = points.Count;

            double minX = double.MaxValue, maxX = double.MinValue;
            var px = new double[n];
            for (int i = 0; i < n; ++i)
            {
                px[i] = (Vector3d)points[i] * x;
                minX = Math.Min(minX, px[i]); maxX = Math.Max(maxX, px[i]);
            }

            // Which of y and z does the mean position wander along more?
            var sumY = new double[bins]; var sumZ = new double[bins]; var count = new int[bins];
            double binSize = (maxX - minX) / bins;
            for (int i = 0; i < n; ++i)
            {
                int b = binSize > 0 ? Math.Min(bins - 1, (int)((px[i] - minX) / binSize)) : 0;
                sumY[b] += (Vector3d)points[i] * y; sumZ[b] += (Vector3d)points[i] * z; count[b]++;
            }
            var meansY = new List<double>(); var meansZ = new List<double>();
            for (int b = 0; b < bins; ++b)
                if (count[b] >= 2) { meansY.Add(sumY[b] / count[b]); meansZ.Add(sumZ[b] / count[b]); }

            if (StandardDeviation(meansZ) > StandardDeviation(meansY)) (y, z) = (z, y);
            z = Vector3d.CrossProduct(x, y);
            z.Unitize();

            double minY = double.MaxValue, maxY = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int i = 0; i < n; ++i)
            {
                double py = (Vector3d)points[i] * y, pz = (Vector3d)points[i] * z;
                minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
                minZ = Math.Min(minZ, pz); maxZ = Math.Max(maxZ, pz);
            }

            var origin = Point3d.Origin + x * minX + y * minY + z * minZ;
            return new OrientedBounds
            {
                Plane = new Plane(origin, x, y),
                SizeX = maxX - minX,
                SizeY = maxY - minY,
                SizeZ = maxZ - minZ
            };
        }

        static double StandardDeviation(List<double> values)
        {
            if (values.Count == 0) return 0;
            double mean = values.Average();
            return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
        }
    }
}
