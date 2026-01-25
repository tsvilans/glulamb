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
        public static (double[] values, Vector3d[] vectors)
                EigenDecompositionSymmetric(Matrix U)
        {
            double[] values = EigenvaluesSymmetric(U);
            Vector3d[] vectors = new Vector3d[3];

            for (int i = 0; i < 3; i++)
                vectors[i] = EigenvectorFromEigenvalue(U, values[i]);

            // Orthonormalize (numerical safety)
            vectors[0].Unitize();
            vectors[1] -= Vector3d.Multiply(
                Vector3d.Multiply(vectors[1], vectors[0]), vectors[0]);
            vectors[1].Unitize();
            vectors[2] = Vector3d.CrossProduct(vectors[0], vectors[1]);

            return (values, vectors);
        }

        public static Vector3d EigenvectorFromEigenvalue(Matrix U, double lambda)
        {
            var M = SubtractLambdaI(U, lambda);

            Vector3d r0 = new Vector3d(M[0, 0], M[0, 1], M[0, 2]);
            Vector3d r1 = new Vector3d(M[1, 0], M[1, 1], M[1, 2]);
            Vector3d r2 = new Vector3d(M[2, 0], M[2, 1], M[2, 2]);

            Vector3d v0 = Vector3d.CrossProduct(r0, r1);
            Vector3d v1 = Vector3d.CrossProduct(r0, r2);
            Vector3d v2 = Vector3d.CrossProduct(r1, r2);

            double d0 = v0.SquareLength;
            double d1 = v1.SquareLength;
            double d2 = v2.SquareLength;

            Vector3d v =
                (d0 > d1 && d0 > d2) ? v0 :
                (d1 > d2) ? v1 :
                                    v2;

            if (!v.Unitize())
                throw new Exception("Degenerate eigenvector");

            return v;
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


        public static Matrix SubtractLambdaI(Matrix matrix, double lambda)
        {
            var mat = matrix.Duplicate();

            mat[0, 0] -= lambda;
            mat[1, 1] -= lambda;
            mat[2, 2] -= lambda;

            return mat;
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

        public static double[] EigenvaluesSymmetric(Matrix A)
        {
            // Step 1: Calculate the mean of the diagonal (the "average" eigenvalue)
            double m = Trace(A) / 3.0;

            // Step 2: Calculate the deviatoric matrix B = A - m*I
            // This shifts the eigenvalues so their sum (trace) is zero.
            Matrix B = new Matrix(3, 3);
            B[0, 0] = A[0, 0] - m; B[0, 1] = A[0, 1]; B[0, 2] = A[0, 2];
            B[1, 0] = A[1, 0]; B[1, 1] = A[1, 1] - m; B[1, 2] = A[1, 2];
            B[2, 0] = A[2, 0]; B[2, 1] = A[2, 1]; B[2, 2] = A[2, 2] - m;

            // Step 3: Calculate the "norm" of the deviatoric matrix
            double p2 =
                B[0, 0] * B[0, 0] +
                B[1, 1] * B[1, 1] +
                B[2, 2] * B[2, 2] +
                2.0 * (B[0, 1] * B[0, 1] + B[0, 2] * B[0, 2] + B[1, 2] * B[1, 2]);

            double p = Math.Sqrt(p2 / 6.0);

            // Step 4: Scale the matrix and find the determinant for the trig identity
            Matrix C = B.Duplicate();
            C.Scale(1.0 / p);

            double r = Determinant(C) / 2.0;

            // Clamp for numerical safety (acos requires values between -1 and 1)
            r = Math.Max(-1.0, Math.Min(1.0, r));

            double phi = Math.Acos(r) / 3.0;

            // Step 5: Solve for the three roots (the eigenvalues)
            // The roots are spaced by 120 degrees (2π/3 radians)
            double eig1 = m + 2.0 * p * Math.Cos(phi);
            double eig2 = m + 2.0 * p * Math.Cos(phi + 2.0 * Math.PI / 3.0);
            double eig3 = m + 2.0 * p * Math.Cos(phi + 4.0 * Math.PI / 3.0);

            return new[] { eig1, eig2, eig3 };
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
