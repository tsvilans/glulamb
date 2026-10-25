using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public static class Strain
    {
        /// <summary>
        /// Eigenvalues and eigenvectors of a symmetric 3x3 matrix, in the order this class has
        /// always used: the largest, the smallest, then the middle one, whose vector is the cross
        /// product of the first two (a right-handed, orthonormal set).
        /// </summary>
        public static (double[] values, Vector3d[] vectors)
                EigenDecompositionSymmetric(Matrix U)
        {
            var a = new double[3, 3];
            for (int i = 0; i < 3; ++i)
                for (int j = 0; j < 3; ++j)
                    a[i, j] = U[i, j];

            // Sorted largest first
            Numerics.SymmetricEigen.Decompose3(a, out double[] sorted, out Vector3d[] axes);

            var values = new[] { sorted[0], sorted[2], sorted[1] };
            var vectors = new[] { axes[0], axes[2], Vector3d.CrossProduct(axes[0], axes[2]) };
            return (values, vectors);
        }

        public static double GetLamellaAngleOffset(Plane lamellaPlane, Vector3d[] eigenVectors)
        {
            // 1. The normal of your lamella is the Z-Axis of its plane (the stacking direction)
            Vector3d lamellaNormal = lamellaPlane.ZAxis;

            // 2. The normal of the Principal Strain Plane is the "Neutral" Eigenvector.
            // In bending, this is the direction of least deformation (eigenvalue ~ 1.0).
            // Based on your previous code, this is usually vectors[2].
            Vector3d principalNormal = eigenVectors[2];

            // 3. Compute the angle between the two normal vectors
            // Dot product: cos(theta) = (a · b) / (|a| * |b|)
            // Since our vectors are unitized, it's just a · b
            double dot = lamellaNormal * principalNormal;

            // 4. Clamp for numerical stability
            dot = Math.Max(-1.0, Math.Min(1.0, dot));

            // 5. Get the angle in Radians
            double angleRad = Math.Acos(dot);

            // 6. Since planes are symmetric, we want the acute angle (0 to 90 degrees)
            // If the angle is 120°, the offset is actually 60°.
            if (angleRad > Math.PI / 2.0)
            {
                angleRad = Math.PI - angleRad;
            }

            // Return in Degrees for easier interpretation
            return angleRad * (180.0 / Math.PI);
        }



        public static Mesh CreateMeshCube(Point3d[] points)
        {
            var mesh = new Mesh();
            mesh.Vertices.AddVertices(points);

            mesh.Faces.AddFace(0, 1, 2, 3);
            mesh.Faces.AddFace(4, 5, 6, 7);

            mesh.Faces.AddFace(0, 1, 5, 4);
            mesh.Faces.AddFace(1, 2, 6, 5);

            mesh.Faces.AddFace(2, 3, 7, 6);
            mesh.Faces.AddFace(3, 0, 4, 7);

            return mesh;
        }

        public static void CreateStrainCubes(Plane p0, Plane p1, double size, out Point3d[] points0, out Point3d[] points1)
        {
            var points = new Point3d[][]{
            new Point3d[8],
            new Point3d[8]
        };

            var xform = Transform.PlaneToPlane(p0, Plane.WorldXY);
            p0.Transform(xform);
            p1.Transform(xform);

            // var size = p0.Origin.DistanceTo(p1.Origin);
            var w = size / 2;

            points0 = points[0];
            points1 = points[1];

            for (int i = 0; i < 2; ++i)
            {
                points[i][0] = p0.PointAt(-w, -w, 0);
                points[i][1] = p0.PointAt(-w, w, 0);
                points[i][2] = p0.PointAt(w, w, 0);
                points[i][3] = p0.PointAt(w, -w, 0);
            }

            points[0][4] = p0.PointAt(-w, -w, size);
            points[0][5] = p0.PointAt(-w, w, size);
            points[0][6] = p0.PointAt(w, w, size);
            points[0][7] = p0.PointAt(w, -w, size);

            points[1][4] = p1.PointAt(-w, -w, 0);
            points[1][5] = p1.PointAt(-w, w, 0);
            points[1][6] = p1.PointAt(w, w, 0);
            points[1][7] = p1.PointAt(w, -w, 0);

        }

        public static Plane RotatePlaneForSymmetricEndpoints(
            Plane p1,
            Plane p2,
            double width)
        {
            // Vector between origins
            Vector3d D = p1.Origin - p2.Origin;

            // Project vectors into plane 2
            Vector3d Dp = D - Vector3d.Multiply(D * p2.ZAxis, p2.ZAxis);
            Vector3d X1p = p1.XAxis - Vector3d.Multiply(p1.XAxis * p2.ZAxis, p2.ZAxis);

            if (!Dp.Unitize() || !X1p.Unitize())
                return p2; // degenerate case

            // Current X axis of plane 2 (already in plane)
            Vector3d X2 = p2.XAxis;

            // Compute signed angles
            double a1 = Vector3d.VectorAngle(X1p, Dp, p2.ZAxis);
            double a2 = Vector3d.VectorAngle(X2, Dp, p2.ZAxis);

            double delta = a1 - a2;

            // Rotate plane 2
            Plane result = p2;
            result.Rotate(delta, p2.ZAxis, p2.Origin);

            return result;
        }

        public static double Trace(this Matrix A)
        {
            // The sum of the elements on the main diagonal
            return A[0, 0] + A[1, 1] + A[2, 2];
        }

        public static double Determinant(this Matrix A)
        {
            // Using Laplace expansion (cofactor expansion) along the first row
            return
                A[0, 0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) -
                A[0, 1] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0]) +
                A[0, 2] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]);
        }

        public static void PolarDecomposition(
            Matrix F,
            out Matrix R,
            out Matrix U)
        {
            var C = RightCauchyGreen(F);
            U = SymmetricSqrt(C);
            var Uinv = U.Duplicate();
            Uinv.Invert(1e-6);
            R = F * Uinv;
        }

        public static Matrix SymmetricSqrt(Matrix C, int iterations = 20)
        {
            var X = C;
            var Y = new Matrix(3, 3);
            Y.SetDiagonal(1);

            for (int i = 0; i < iterations; i++)
            {
                var Xinv = X.Duplicate();
                Xinv.Invert(1e-6);
                var Yinv = Y.Duplicate();
                Yinv.Invert(1e-6);

                X += Yinv;
                X.Scale(0.5);
                Y += Xinv;
                Y.Scale(0.5);
            }

            return X;
        }

        public static Matrix Identity3x3()
        {
            var matrix = new Matrix(3, 3);
            matrix.SetDiagonal(1);
            return matrix;
        }

        public static Matrix RightCauchyGreen(Matrix F)
        {
            var transposed = F.Duplicate();
            transposed.Transpose();

            return transposed * F;
        }

        public static Matrix BuildDeformationGradient(
            Point3d[] original,   // 8 vertices
            Point3d[] deformed)   // 8 vertices
        {
            // Use vertex 0 as origin
            Vector3d e1 = original[5] - original[4];
            Vector3d e2 = original[6] - original[4];
            Vector3d e3 = original[0] - original[4];

            Vector3d E1 = deformed[5] - deformed[4];
            Vector3d E2 = deformed[6] - deformed[4];
            Vector3d E3 = deformed[0] - deformed[4];

            var A = Utility.MatrixFromColumns(e1, e2, e3);
            var B = Utility.MatrixFromColumns(E1, E2, E3);
            A.Invert(1e-6);

            return B * A;
        }


    }
}
