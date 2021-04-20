/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2021 Tom Svilans
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
    public class Beam
    {
        public Beam() { }
        public virtual double Width
        {
            get; set;
        }

        public virtual double Height
        {
            get; set;
        }

        public Curve Centreline { get; set; }
        public CrossSectionOrientation Orientation;

        public Plane GetPlane(double t) => Utility.PlaneFromNormalAndYAxis(
                                                        Centreline.PointAt(t),
                                                        Centreline.TangentAt(t),
                                                        Orientation.GetOrientation(Centreline, t));
        public Plane GetPlane(Point3d pt)
        {
            Centreline.ClosestPoint(pt, out double t);
            return GetPlane(t);
        }
        public void Transform(Transform x)
        {
            Centreline.Transform(x);
            Orientation.Transform(x);
        }

        public Point3d[] ToBeamSpace(IList<Point3d> pts)
        {
            Point3d[] m_output_pts = new Point3d[pts.Count];

            Plane m_plane;
            Point3d m_temp;
            double t;
            for (int i = 0; i < pts.Count; ++i)
            {
                Centreline.ClosestPoint(pts[i], out t);
                m_plane = GetPlane(t);
                m_plane.RemapToPlaneSpace(pts[i], out m_temp);
                m_temp.Z = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

                m_output_pts[i] = m_temp;
            }

            return m_output_pts;
        }

        public Mesh ToBeamSpace(Mesh mesh)
        {
            Mesh m_mesh = mesh.DuplicateMesh();
            m_mesh.Vertices.Clear();
            m_mesh.Vertices.AddVertices(ToBeamSpace(mesh.Vertices.ToPoint3dArray()));

            return m_mesh;
        }

    }
}
