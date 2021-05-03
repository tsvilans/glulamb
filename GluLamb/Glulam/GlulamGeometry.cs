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


    }
}