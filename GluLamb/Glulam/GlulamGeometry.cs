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

using Rhino.Collections;

namespace GluLamb
{
    public abstract partial class Glulam
    {


        public List<Point3d> DiscretizeCentreline(bool adaptive = true)
        {
            if (adaptive)
            {
                var pCurve = Centreline.ToPolyline(Glulam.Tolerance, Glulam.AngleTolerance, 0.0, 0.0);
                return pCurve.ToPolyline().ToList();
            }

            var tt = Centreline.DivideByCount(Data.Samples, true);
            return tt.Select(x => Centreline.PointAt(x)).ToList();
        }

        public List<Point3d>[] GetEdgePoints(double offset = 0.0)
        {
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            m_section_corners = CornerGenerator(offset);
            int numCorners = m_section_corners.Length;

            List<Point3d>[] edge_points = m_section_corners.Select(x => frames.Select(y => y.PointAt(x.X, x.Y)).ToList()).ToArray();

            return edge_points;
        }

        /// <summary>
        /// Get a glulam curve at some arbitrary point.
        /// </summary>
        /// <param name="g"></param>
        /// <param name="offset">Offset in cross-section space.</param>
        /// <returns></returns>
        public List<Point3d> GetCurvePoints(Point3d offset)
        {
            int N = Math.Max(Data.Samples, 6);

            Plane[] frames;
            double[] parameters;
            GenerateCrossSectionPlanes(N, out frames, out parameters, Data.InterpolationType);

            List<Point3d> edge_points = frames.Select(y => y.PointAt(offset.X, offset.Y)).ToList();

            return edge_points;
        }

        public virtual Mesh ToMesh(double offset = 0.0, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            return new Mesh();
        }

        //public virtual Brep ToBrep(double offset = 0.0)
        //{
        //    return new Brep();
        //}

        public virtual List<Curve> GetLamellaeCurves()
        {
            return new List<Curve>();
        }

        public virtual Curve GetLamellaCurve(int i, int j)
        {
            throw new NotImplementedException("GetLamellaCurve() not implemented.");
        }

        public virtual List<Mesh> GetLamellaeMeshes()
        {
            return new List<Mesh>();
        }

        public virtual List<Brep> GetLamellaeBreps()
        {
            return new List<Brep>();
        }

        public virtual Curve[] GetEdgeCurves(double offset = 0.0)
        {
            return new Curve[0];
        }

        public abstract void GenerateCrossSectionPlanes(int N, out Plane[] planes, out double[] t, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR);

        public List<Polyline> GetUnbentLamellaOutlines()
        {
            var lam_crvs = GetLamellaeCurves();

            var outlines = new List<Polyline>();

            return outlines;
        }

        public List<Mesh> GetUnbentLamellaeMeshes(double resolution = 50.0, bool thick = true)
        {
            var xforms = new List<Transform>();
            var lams = GetUnbentLamellaeMeshes(out xforms, resolution, thick);

            for (int i = 0; i < lams.Count; ++i)
            {
                lams[i].Transform(xforms[i]);
            }

            return lams;
        }

        public List<Mesh> GetUnbentLamellaeMeshes(out List<Transform> xforms, double resolution=50.0, bool thick = true)
        {
            xforms = new List<Transform>();
            var lams = new List<Mesh>();

            double width = Width;
            double hwidth = width / 2;

            double height = Height;
            double hheight = height / 2;

            var lam_crvs = GetLamellaeCurves();

            for (int i = 0; i < Data.NumHeight; ++i)
            {

                for (int j = 0; j < Data.NumWidth; ++j)
                {
                    Mesh lmesh;

                    if (thick)
                        lmesh = GluLamb.Utility.Create3dMeshGrid(Data.LamWidth, Data.LamHeight, lam_crvs[i].GetLength(), resolution);
                    else
                        lmesh = GluLamb.Utility.Create2dMeshGrid(Data.LamWidth, lam_crvs[i].GetLength(), resolution);

                    xforms.Add(Rhino.Geometry.Transform.Translation(
                        Data.LamWidth * j - hwidth + Data.LamWidth * 0.5, 
                        Data.LamHeight * i - hheight + Data.LamHeight * 0.5, 0));
                    lams.Add(lmesh);
                }
            }
            return lams;
        }
    }
}