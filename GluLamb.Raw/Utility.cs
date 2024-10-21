using Accord;
using Accord.Math;
using Accord.Math.Optimization;

namespace GluLamb.Raw
{
    public static class Utility
    {
        /// <summary>
        /// Get eigenvalues and eigenvectors for a list of XYZ coordinates, effectively finding the principal directions of a set of
        /// points or vertices. Useful for finding the plane-of-best-fit for free-form beam elements.
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Z"></param>
        /// <param name="eigenValues"></param>
        /// <param name="eigenVectors"></param>
        /// <exception cref="Exception"></exception>
        public static void GetEigenVectors(double[] X, double[] Y, double[] Z, out double[] eigenValues, out double[,] eigenVectors)
        {

            //var pts = mesh.Vertices.ToPoint3dArray();
            if (X.Length != Y.Length || X.Length != Z.Length) { throw new Exception("Arrays are not even!"); };

            int N = X.Length;

            double[] mean = new double[3];
            for (int i = 0; i < N; ++i)
            {
                mean[0] += X[i];
                mean[1] += Y[i];
                mean[2] += Z[i];
            }

            // Get the mean
            mean[0] = mean[0] / N;
            mean[1] = mean[1] / N;
            mean[2] = mean[2] / N;

            // Centre the data
            for (int i = 0; i < N; ++i)
            {
                X[i] -= mean[0];
                Y[i] -= mean[1];
                Z[i] -= mean[2];
            }

            var cov = new double[3, 3];

            // Calculate variance
            for (int i = 0; i < N; ++i)
            {
                cov[0, 0] += X[i] * X[i];
                cov[1, 1] += Y[i] * Y[i];
                cov[2, 2] += Z[i] * Z[i];
            }

            cov[0, 0] /= N - 1;
            cov[1, 1] /= N - 1;
            cov[2, 2] /= N - 1;

            // Calculate covariance
            for (int i = 0; i < N; ++i)
            {
                cov[0, 1] += X[i] * Y[i];
                cov[0, 2] += X[i] * Z[i];
                cov[1, 2] += Y[i] * Z[i];
            }

            cov[0, 1] /= N - 1;
            cov[0, 2] /= N - 1;
            cov[1, 2] /= N - 1;

            cov[1, 0] = cov[0, 1];
            cov[2, 0] = cov[0, 2];
            cov[2, 1] = cov[1, 2];

            var div = cov[0, 0];
            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    cov[i, j] /= div;
                }
            }

            var eigen = new Accord.Math.Decompositions.EigenvalueDecomposition(cov);

            eigenValues = eigen.RealEigenvalues;
            //eigenValues = eigen.ImaginaryEigenvalues;

            eigenVectors = eigen.Eigenvectors;
        }

        public static void GetPlanarTCP(double[][] frames, out double[] tcp)
        {
            tcp = new double[3];

            var transformationMatrices = new Matrix4x4[3];
            for (int i=0; i < frames.GetLength(0); ++i)
            {
                var v0 = new Vector4((float)frames[i][0], (float)frames[i][1], (float)frames[i][2], (float)frames[i][3]);
                var v1 = new Vector4((float)frames[i][4], (float)frames[i][5], (float)frames[i][6], (float)frames[i][7]);
                var v2 = new Vector4((float)frames[i][8], (float)frames[i][9], (float)frames[i][10], (float)frames[i][11]);
                var v3 = new Vector4((float)frames[i][12], (float)frames[i][13], (float)frames[i][14], (float)frames[i][15]);
                transformationMatrices[i] = Matrix4x4.CreateFromRows(v0, v1, v2, v3);
            }
            double[] initialGuess = { 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0 };

            LeastSquaresFunction function = (parameters, residualsArray) =>
            {
                residualsArray = new double[3];
                Vector4 tcp = new Vector4( (float)parameters[0], (float)parameters[1], (float)parameters[2], 1.0f );  // TCP (x, y, z)
                Vector3 planeNormal = new Vector3( (float)parameters[3], (float)parameters[4], (float)parameters[5] ); // Plane normal (a, b, c)
                double planeD = parameters[6];  // Plane distance (d)

                for (int i = 0; i < transformationMatrices.Length; i++)
                {
                    // Extract rotation matrix and translation vector from the 4x4 matrix
                    Matrix4x4 T = transformationMatrices[i];

                    Matrix3x3 rotation = Matrix3x3.CreateFromRows(
                        T.GetRow(0).ToVector3(),
                        T.GetRow(1).ToVector3(),
                        T.GetRow(2).ToVector3());

                    Vector3 translation = T.GetColumn(3).ToVector3(); // Last column (translation)

                    // Transform the TCP from tool frame to world frame

                    Vector4 tcpWorld = Matrix4x4.Multiply(transformationMatrices[i], tcp);

                    // Calculate the plane equation: a * x + b * y + c * z + d = 0
                    double planeEq = Vector3.Dot(planeNormal, tcpWorld.ToVector3()) + planeD;

                    // Store the residual (this should be close to 0)
                    residualsArray[i] = planeEq;
                }

                return 0;
            };

            // Create a Levenberg-Marquardt solver
            var lm = new LevenbergMarquardt(parameters: 7);
            lm.Function = function;

            

            // Optimize the parameters (TCP and plane parameters)
            var success = lm.Minimize(frames, tcp);


        }
    }
}