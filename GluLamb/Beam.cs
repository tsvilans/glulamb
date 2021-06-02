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
        public CrossSectionOrientation Orientation { get; set; }

        public Plane GetPlane(double t) => Utility.PlaneFromNormalAndYAxis(
                                                        Centreline.PointAt(t),
                                                        Centreline.TangentAt(t),
                                                        Orientation.GetOrientation(Centreline, t));

        public Plane[] GetPlanes(IList<double> tt)
        {
            var orientations = Orientation.GetOrientations(Centreline, tt);
            var planes = new Plane[tt.Count];

            Parallel.For(0, tt.Count, i =>
            {
                planes[i] = Utility.PlaneFromNormalAndYAxis(
                    Centreline.PointAt(tt[i]),
                    Centreline.TangentAt(tt[i]),
                    orientations[i]);

            });

            return planes;
        }
        public Plane[] GetPlanes(IList<Point3d> pts)
        {
            var tt = new double[pts.Count];
            Parallel.For(0, pts.Count, i =>
            {
                Centreline.ClosestPoint(pts[i], out tt[i]);
            });

            var orientations = Orientation.GetOrientations(Centreline, tt);
            var planes = new Plane[tt.Length];

            Parallel.For(0, tt.Length, i =>
            {
                planes[i] = Utility.PlaneFromNormalAndYAxis(
                    Centreline.PointAt(tt[i]),
                    Centreline.TangentAt(tt[i]),
                    orientations[i]);
            });

            return planes;
        }

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

        // MAPPING METHODS

        public Point3d ToBeamSpace(Point3d pt)
        {
            Plane m_plane;
            Point3d m_temp;
            double t;

            Centreline.ClosestPoint(pt, out t);
            m_plane = GetPlane(t);
            m_plane.RemapToPlaneSpace(pt, out m_temp);
            m_temp.Z = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

            return m_temp;
        }

        public Plane ToBeamSpace(Plane plane)
        {
            Centreline.ClosestPoint(plane.Origin, out double t);
            Plane m_plane = GetPlane(t);
            plane.Transform(Rhino.Geometry.Transform.PlaneToPlane(m_plane, Plane.WorldXY));
            plane.OriginZ = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

            return plane;
        }

        public Point3d[] ToBeamSpace(IList<Point3d> pts, bool approximate=false, int num_samples=100)
        {
            Point3d[] m_output_pts = new Point3d[pts.Count];

            Plane m_plane;
            Point3d m_temp;
            double t;

            if (approximate)
            {
                double mu;

                var tt = Centreline.DivideByCount(num_samples, true);
                var lengths = tt.Select(x => Centreline.GetLength(new Interval(Centreline.Domain.Min, x))).ToArray();

                //for (int i = 0; i < pts.Count; ++i)
                Parallel.For(0, pts.Count, i =>

                {
                    Centreline.ClosestPoint(pts[i], out t);
                    m_plane = GetPlane(t);
                    m_plane.RemapToPlaneSpace(pts[i], out m_temp);

                    var res = Array.BinarySearch(tt, t);
                    if (res < 0)
                    {
                        res = ~res;
                        res--;
                    }

                    if (res >= 0 && res < tt.Length - 1)
                    {
                        mu = (t - tt[res]) / (tt[res + 1] - tt[res]);
                        m_temp.Z = Interpolation.Lerp(lengths[res], lengths[res + 1], mu);
                    }
                    else if (res < 0)
                    {
                        m_temp.Z = lengths.First();
                    }
                    else if (res >= (tt.Length - 1))
                    {
                        m_temp.Z = lengths.Last();
                    }

                    m_output_pts[i] = m_temp;
                }
                );
            }
            else
            {
                //for (int i = 0; i < pts.Count; ++i)
                Parallel.For(0, pts.Count, i =>
                {
                    Centreline.ClosestPoint(pts[i], out t);
                    m_plane = GetPlane(t);
                    m_plane.RemapToPlaneSpace(pts[i], out m_temp);
                    m_temp.Z = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

                    m_output_pts[i] = m_temp;
                }
                );
            }

            return m_output_pts;
        }

        public Plane[] ToBeamSpace(IList<Plane> planes)
        {
            Plane[] m_output_planes = new Plane[planes.Count];

            Plane m_plane;
            Plane m_temp;
            double t;

            for (int i = 0; i < planes.Count; ++i)
            {
                Centreline.ClosestPoint(planes[i].Origin, out t);
                m_plane = GetPlane(t);
                m_temp = planes[i];
                m_temp.Transform(Rhino.Geometry.Transform.PlaneToPlane(m_plane, Plane.WorldXY));
                m_temp.OriginZ = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

                m_output_planes[i] = m_temp;
            }

            return m_output_planes;
        }

        public Mesh ToBeamSpace(Mesh mesh)
        {
            Mesh m_mesh = mesh.DuplicateMesh();
            m_mesh.Vertices.Clear();
            m_mesh.Vertices.AddVertices(ToBeamSpace(mesh.Vertices.ToPoint3dArray()));

            return m_mesh;
        }

        /// <summary>
        /// Map points from beam space to world space.
        /// </summary>
        /// <param name="pts"></param>
        /// <returns></returns>
        public Point3d[] FromBeamSpace(IList<Point3d> pts)
        {
            Point3d[] m_output_pts = new Point3d[pts.Count];
            /*
            double min_z = double.MaxValue;
            double max_z = double.MinValue;

            foreach (var pt in pts)
            {
                min_z = Math.Min(min_z, pt.Z);
                max_z = Math.Max(max_z, pt.Z);
            }

            double ext_min = Math.Min(0, Centreline.Domain.Min - min_z);
            double ext_max = Math.Max(0, max_z - Centreline.Domain.Max);
            double tolerance = 5.0;

            double ext = Math.Max(ext_min, ext_max) + tolerance;

            Curve c = Centreline.Extend(CurveEnd.Both, ext, CurveExtensionStyle.Line);
            */

            if (Centreline.IsLinear())
            {
                Parallel.For(0, pts.Count, i =>
                {
                    Plane m_plane;

                    //m_plane = Utility.PlaneFromNormalAndYAxis(
                    //Centreline.PointAtStart + Centreline.TangentAtStart * (pts[i].Z),
                    //Centreline.TangentAtStart,
                    //Orientation.GetOrientation(Centreline, pts[i].Z));
                    
                    m_plane = GetPlane(pts[i].Z);

                    m_output_pts[i] = m_plane.PointAt(pts[i].X, pts[i].Y);
                });

            }

            else
            {
                //Parallel.For(0, pts.Count, i =>
                //{
                    for (int i = 0; i < pts.Count; ++i)
                    {
                    Plane m_plane;

                    Centreline.LengthParameter(pts[i].Z, out double t);
                    m_plane = GetPlane(t);

                    m_output_pts[i] = m_plane.PointAt(pts[i].X, pts[i].Y);
                }
                //});
            }

            return m_output_pts;
        }

        /// <summary>
        /// Map mesh from beam space to world space.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public Mesh FromBeamSpace(Mesh mesh)
        {
            Mesh m_mesh = mesh.DuplicateMesh();
            m_mesh.Vertices.Clear();
            m_mesh.Vertices.AddVertices(FromBeamSpace(mesh.Vertices.ToPoint3dArray()));

            return m_mesh;
        }



    }
}
