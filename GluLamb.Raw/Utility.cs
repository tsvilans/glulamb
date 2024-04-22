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
    }
}