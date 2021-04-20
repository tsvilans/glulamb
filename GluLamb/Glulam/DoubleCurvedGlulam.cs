/*
 * tasTools
 * A personal PhD research toolkit.
 * Copyright 2017-2018 Tom Svilans
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
    public class DoubleCurvedGlulam : FreeformGlulam
    {
        public DoubleCurvedGlulam() : base()
        {

        }

        public DoubleCurvedGlulam(Curve curve, CrossSectionOrientation orientation, GlulamData data) : base()
        {
            Data = data.Duplicate();
            Orientation = orientation;
            Centreline = curve.DuplicateCurve();
            //Centreline.Domain.MakeIncreasing();

        }

        public DoubleCurvedGlulam(Curve centreline, Plane[] planes) : base()
        {
            Centreline = centreline;
            //Centreline.Domain.MakeIncreasing();


            if (planes != null)
            {
                List<Vector3d> vectors = new List<Vector3d>();
                List<double> parameters = new List<double>();
                double t;

                for (int i = 0; i < planes.Length; ++i)
                {
                    Centreline.ClosestPoint(planes[i].Origin, out t);
                    parameters.Add(t);
                    vectors.Add(planes[i].YAxis);
                }

                Orientation = new VectorListOrientation(Centreline, parameters, vectors);
            }
            else
            {
                Orientation = new KCurveOrientation();
            }
        }

        public override GlulamType Type() => GlulamType.DoubleCurved;

        public override string ToString() => "DoubleCurvedGlulam";

        public override Mesh MapToCurveSpace(Mesh m)
        {
            Plane cp;
            double t, l;

            Mesh mesh = new Mesh();

            List<Point3d> verts = new List<Point3d>(m.Vertices.Count);
            object m_lock = new object();

            Parallel.For(0, m.Vertices.Count, i =>
            //for (int i = 0; i < m.Vertices.Count; ++i)
            {
                Point3d temp1, temp2;

                temp1 = m.Vertices[i];
                Centreline.ClosestPoint(temp1, out t);
                l = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));
                cp = GetPlane(t);
                //Centreline.PerpendicularFrameAt(t, out cp);
                //p.Transform(Rhino.Geometry.Transform.PlaneToPlane(cp, Plane.WorldXY));
                cp.RemapToPlaneSpace(temp1, out temp2);
                temp2.Z = l;

                //lock(m_lock)
                //{
                verts[i] = temp2;
                //}
                //}
            });
            /*
            for (int i = 0; i < m.Vertices.Count; ++i)
            {
                Point3d p = new Point3d(m.Vertices[i]);
                Centreline.ClosestPoint(p, out t);
                l = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));
                cp = GetPlane(t);
                //Centreline.PerpendicularFrameAt(t, out cp);
                p.Transform(Rhino.Geometry.Transform.PlaneToPlane(cp, Plane.WorldXY));
                p.Z = l;

            }
            */

            mesh.Vertices.AddVertices(verts);
            mesh.Faces.AddFaces(m.Faces);
            mesh.FaceNormals.ComputeFaceNormals();

            return mesh;
        }

        /*
        public override void CalculateLamellaSizes(double width, double height)
        {
            var tt = Centreline.DivideByCount(Data.Samples, true);

            Plane kPlane;
            Vector3d kVec;
            double maxKX = 0.0, maxKY = 0.0;
            double dotKX, dotKY;

            for (int i = 0; i < tt.Length; ++i)
            {
                kPlane = GetPlane(tt[i]);
                kVec = Centreline.CurvatureAt(tt[i]);

                dotKX = kVec * kPlane.XAxis;
                dotKY = kVec * kPlane.YAxis;

                maxKX = Math.Max(dotKX, maxKX);
                maxKY = Math.Max(dotKY, maxKY);
            }

            double lh = Math.Floor((1 / Math.Abs(maxKY)) * Glulam.RadiusMultiplier);
            double lw = Math.Floor((1 / Math.Abs(maxKX)) * Glulam.RadiusMultiplier);

            int num_height = (int)Math.Ceiling(height / lh);
            Data.LamHeight = height / num_height;

            int num_width = (int)Math.Ceiling(width / lw);
            Data.LamWidth = width / num_width;

            if (Data.NumHeight < 2)
            {
                num_height = 2;
                Data.LamHeight /= 2;
            }

            if (Data.NumWidth < 2)
            {
                num_width = 2;
                Data.LamWidth /= 2;
            }

            Data.Lamellae.ResizeArray(num_width, num_height);
        }
        */

        /*
        public override Curve CreateOffsetCurve(double x, double y, bool rebuild = false, int rebuild_pts = 20)
        {
            List<Point3d> pts = new List<Point3d>();
            double[] t = Centreline.DivideByCount(this.Data.Samples, true);

            for (int i = 0; i < t.Length; ++i)
            {
                Plane p = GetPlane(t[i]);
                pts.Add(p.Origin + p.XAxis * x + p.YAxis * y);
            }

            Curve new_curve = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Uniform,
                Centreline.TangentAtStart, Centreline.TangentAtEnd);

            if (new_curve == null)
                throw new Exception("SingleCurvedGlulam::CreateOffsetCurve:: Failed to create interpolated curve!");

            double len = new_curve.GetLength();
            new_curve.Domain = new Interval(0.0, len);

            if (rebuild)
                new_curve = new_curve.Rebuild(rebuild_pts, new_curve.Degree, true);

            return new_curve;
        }
        */
    }

}
