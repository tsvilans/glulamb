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
    [Serializable]
    public class SingleCurvedGlulam : FreeformGlulam
    {
        public SingleCurvedGlulam() : base()
        {

        }
        public SingleCurvedGlulam(Curve curve, CrossSectionOrientation orientation, GlulamData data) : base()
        {
            Data = data.Duplicate();
            Orientation = orientation.Duplicate();
            Centreline = curve.DuplicateCurve();
            //Centreline.Domain.MakeIncreasing();

        }

        public SingleCurvedGlulam(Curve centreline, Plane[] planes, bool with_twist = false) : base()
        {
            if (planes == null)
            {
                Plane p;
                if (centreline.TryGetPlane(out p))
                {
                    double midT = centreline.Domain.Mid;
                    planes = new Plane[] {
                        new Plane(centreline.PointAtStart,
                        p.ZAxis,
                        Vector3d.CrossProduct(centreline.TangentAtStart, p.ZAxis)),
                        new Plane(centreline.PointAt(midT),
                        p.ZAxis,
                        Vector3d.CrossProduct(centreline.TangentAt(midT), p.ZAxis)
                        ),
                        new Plane(centreline.PointAtEnd,
                        p.ZAxis,
                        Vector3d.CrossProduct(centreline.TangentAtEnd, p.ZAxis)
                        )};
                }
                else
                {
                    planes = new Plane[] { centreline.GetAlignedPlane(20, out double mag) };
                }
            }

            Centreline = centreline;
            //Centreline.Domain.MakeIncreasing();

            //Frames = new List<Tuple<double, Plane>>();
            double t;

            List<Vector3d> vectors = new List<Vector3d>();
            List<double> parameters = new List<double>();

            for (int i = 0; i < planes.Length; ++i)
            {
                Centreline.ClosestPoint(planes[i].Origin, out t);

                parameters.Add(t);
                vectors.Add(planes[i].YAxis);
                //Frames.Add(new Tuple<double, Plane>(t, planes[i]));
            }

            Orientation = new VectorListOrientation(centreline, parameters, vectors);

            //SortFrames();
            //RecalculateFrames();
        }

        public override GlulamType Type() => GlulamType.SingleCurved;

        public override string ToString() => "SingleCurvedGlulam";

        public override bool InKLimitsComponent(out bool width, out bool height)
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

        public override Mesh MapToCurveSpace(Mesh m)
        {
            Plane cp, cpp = Plane.Unset;
            if (!Centreline.TryGetPlane(out cp, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                throw new Exception("SingleCurvedGlulam: Centreline is not planar!");
            double t, l;

            Mesh mesh = new Mesh();

            for (int i = 0; i < m.Vertices.Count; ++i)
            {
                Point3d p = new Point3d(m.Vertices[i]);
                Centreline.ClosestPoint(p, out t);
                l = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));
                //Vector3d xaxis = Vector3d.CrossProduct(cp.ZAxis, Centreline.TangentAt(t));
                //cpp = new Plane(Centreline.PointAt(t), xaxis, cp.ZAxis);

                cpp = GetPlane(t);
                p.Transform(Rhino.Geometry.Transform.PlaneToPlane(cpp, Plane.WorldXY));
                p.Z = l;

                mesh.Vertices.Add(p);
            }

            mesh.Faces.AddFaces(m.Faces);
            mesh.FaceNormals.ComputeFaceNormals();

            return mesh;
        }

        /*
        public override void CalculateLamellaSizes(double width, double height)
        {
            Plane cPlane;
            Centreline.TryGetPlane(out cPlane);
            var normal = cPlane.ZAxis;

            var tt = Centreline.DivideByCount(Data.Samples, true);

            double k = 0.0;
            Vector3d kVec;
            for (int i = 0; i < tt.Length; ++i)
            {
                kVec = Centreline.CurvatureAt(tt[i]);
                k = Math.Max(k, kVec.Length);
            }

            double lh = Math.Floor((1 / Math.Abs(k)) * Glulam.RadiusMultiplier);

            Data.LamWidth = width;


            int num_height = (int)Math.Ceiling(height / lh);
            Data.LamHeight = height / Data.LamHeight;

            if (num_height < 2)
            {
                num_height = 2;
                Data.LamHeight /= 2;
            }

            Data.Lamellae.ResizeArray(Data.NumWidth, num_height);

        }
        */
    }
}
