/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2020 Tom Svilans
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb
{
    public abstract partial class Glulam : BeamBase
    {
        /*
        public abstract void CalculateLamellaSizes(double width, double height);
        */

        public virtual double GetMaxCurvature(ref double width, ref double height)
        {
            return 0.0;
        }

        /// <summary>
        /// Checks the glulam to see if its lamella sizes are appropriate for its curvature.
        /// </summary>
        /// <returns>True if glulam is within curvature limits.</returns>
        public bool InKLimits()
        {
            double t;
            return InKLimits(out t);
        }

        /// <summary>
        /// Checks the glulam to see if its lamella sizes are appropriate for its curvature.
        /// </summary>
        /// <param name="param">Parameter with maximum curvature.</param>
        /// <returns>True if glulam is within curvature limits.</returns>
        public bool InKLimits(out double param)
        {
            double[] t = Centreline.DivideByCount(CurvatureSamples, false);
            double max_k = 0.0;
            int index = 0;
            double temp;
            for (int i = 0; i < t.Length; ++i)
            {
                temp = Centreline.CurvatureAt(t[i]).Length;
                if (temp > max_k)
                {
                    max_k = temp;
                    index = i;
                }
            }

            param = t[index];

            double ratio = (1 / max_k) / RadiusMultiplier;
            if (Math.Abs(ratio - Data.LamHeight) > RadiusTolerance || Math.Abs(ratio - Data.LamWidth) > RadiusTolerance)
                return false;
            return true;
        }

        /// <summary>
        /// Checks the glulam to see if its lamella sizes are appropriate for its curvature.
        /// </summary>
        /// <param name="width">True if the lamella width is OK.</param>
        /// <param name="height">True if the lamella height is OK.</param>
        /// <returns>True if both dimensions are OK.</returns>
        public virtual bool InKLimitsComponent(out bool width, out bool height)
        {
            width = height = false;
            double[] t = Centreline.DivideByCount(CurvatureSamples, false);
            double max_kw = 0.0, max_kh = 0.0;
            Plane temp;
            Vector3d k;
            for (int i = 0; i < t.Length; ++i)
            {
                temp = GetPlane(t[i]);

                k = Centreline.CurvatureAt(t[i]);
                max_kw = Math.Max(max_kw, Math.Abs(k * temp.XAxis));
                max_kh = Math.Max(max_kh, Math.Abs(k * temp.YAxis));
            }

            double rw = (1 / max_kw) / RadiusMultiplier;
            double rh = (1 / max_kh) / RadiusMultiplier;

            if (rw - Data.LamWidth > -RadiusTolerance || double.IsInfinity(1 / max_kw))
                width = true;
            if (rh - Data.LamHeight > -RadiusTolerance || double.IsInfinity(1 / max_kh))
                height = true;

            return width && height;
        }

        /// <summary>
        /// Returns a list of mesh face indices that are outside of the fibre cutting angle limit.
        /// </summary>
        /// <param name="m">Input mesh to check against fibre cutting angle.</param>
        /// <param name="angle">Fibre cutting angle (in radians, default is 5 degrees (0.0872665 radians)).</param>
        /// <returns>Mesh face indices of faces outside of the fibre cutting angle.</returns>
        public int[] CheckFibreCuttingAngle(Mesh m, double angle = 0.0872665)
        {
            Mesh mm = MapToCurveSpace(m);

            List<int> fi = new List<int>();

            for (int i = 0; i < mm.Faces.Count; ++i)
            {
                double dot = Math.Abs(mm.FaceNormals[i] * Vector3d.ZAxis);
                if (dot > Math.Sin(angle))
                {
                    fi.Add(i);
                }
            }

            return fi.ToArray();
        }

        /// <summary>
        /// Gets fibre direction throughout this Glulam, given another Glulam that contains it.
        /// </summary>
        /// <param name="blank">Glulam blank to compare against.</param>
        /// <param name="angles">List of angles. The fibre direction deviates by this much from the centreline.</param>
        /// <param name="divX">Number of sampling divisions in X.</param>
        /// <param name="divY">Number of sampling divisions in Y.</param>
        /// <param name="divZ">Number of sampling divisions along the length of the Glulam.</param>
        /// <returns></returns>
        public List<Ray3d> FibreDeviation(Glulam blank, out List<double> angles, int divX = 8, int divY = 8, int divZ = 50)
        {
            double stepX = Data.LamWidth * Data.NumWidth / (divX + 1);
            double stepY = Data.LamHeight * Data.NumHeight / (divY + 1);

            List<Ray3d> rays = new List<Ray3d>();
            angles = new List<double>();

            double[] tt = this.Centreline.DivideByCount(divZ, true);
            double t;

            for (int z = 0; z < tt.Length; ++z)
            {
                for (int y = -divY / 2; y <= divY / 2; ++y)
                {
                    for (int x = -divX / 2; x <= divX / 2; ++x)
                    {
                        Plane BePlane = this.GetPlane(tt[z]);
                        Point3d pt = BePlane.Origin + BePlane.YAxis * stepY * y + BePlane.XAxis * stepX * x;

                        blank.Centreline.ClosestPoint(pt, out t);

                        Vector3d tanBl = blank.Centreline.TangentAt(t);
                        Vector3d tanBe = this.Centreline.TangentAt(tt[z]);

                        double angle = Math.Acos(Math.Abs(tanBl * tanBe));

                        rays.Add(new Ray3d(pt, tanBl));
                        angles.Add(angle);
                    }
                }
            }

            return rays;
        }

        public List<Ray3d> FibreDeviation(Glulam blank, int divX = 8, int divY = 8, int divZ = 50)
        {
            return FibreDeviation(blank, out List<double> angles, divX, divY, divZ);
        }

    }



}
