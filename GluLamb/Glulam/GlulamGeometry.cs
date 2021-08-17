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

        public virtual Brep ToBrep(double offset = 0.0)
        {
            return new Brep();
        }

        public virtual List<Curve> GetLamellaeCurves()
        {
            return new List<Curve>();
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

        public List<Mesh> GetUnbentLamellaeMeshes(double resolution=50.0)
        {

            var lams = new List<Mesh>();
            //var xforms = new List<Transform>();

            double length = Centreline.GetLength();
            int Nz = (int)Math.Ceiling(length / resolution) + 1;
            int Nx = (int)Math.Ceiling(Width / resolution) + 1;
            int Ny = (int)Math.Ceiling(Height / resolution) + 1;

            double sz = length / (Nz - 1);
            double sx = Width / (Nx - 1);

            double width = Width;
            double hwidth = width / 2;

            double height = Height;
            double hheight = height / 2;

            var lam_crvs = GetLamellaeCurves();

            for (int i = 0; i < Data.NumHeight; ++i)
            {
                sz = lam_crvs[i].GetLength() / (Nz - 1);

                //var xform = Rhino.Geometry.Transform.Translation(new Vector3d(0, i * Data.LamHeight + (Data.LamHeight / 2), 0));
                double ycoord = i * Data.LamHeight + (Data.LamHeight / 2) - hheight;
                //ycoord = -hheight;

                var mesh = new Mesh();
                for (int z = 0; z < Nz; ++z)
                {
                    for (int x = 0; x < Nx; ++x)
                    {
                        double xcoord = sx * x - hwidth;
                        mesh.Vertices.Add(xcoord, ycoord, sz * z);
                    }
                }

                for (int z = 0; z < Nz - 1; ++z)
                    for (int x = 0; x < Nx - 1; ++x)
                    {
                        int a = z * Nx + x,
                          b = z * Nx + x + 1,
                          c = (z + 1) * Nx + x + 1,
                          d = (z + 1) * Nx + x;

                        mesh.Faces.AddFace(
                          a, b, c, d);
                    }

                lams.Add(mesh);
                //xforms.Add(xform);
            }

            return lams;
        }
    }
}