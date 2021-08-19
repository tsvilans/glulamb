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
    public class StraightGlulam : Glulam
    {
        public StraightGlulam() : base()
        {

        }
        public StraightGlulam(Curve curve, CrossSectionOrientation orientation, GlulamData data) : base()
        {
            Data = data.Duplicate();
            Orientation = orientation;
            Centreline = curve.DuplicateCurve();
            //Centreline.Domain.MakeIncreasing();
        }

        public StraightGlulam(Curve curve, Plane[] planes, bool with_twist = false) : base()
        {

            Centreline = curve;
            //Centreline.Domain.MakeIncreasing();

            if (planes == null || planes.Length < 1)
            {
                Plane plane;
                Centreline.PerpendicularFrameAt(Centreline.Domain.Min, out plane);
                planes = new Plane[] { plane };
            }

            if (!Centreline.IsLinear(Tolerance)) throw new Exception("StraightGlulam only works with a linear centreline!");

            List<Vector3d> vectors = new List<Vector3d>();
            List<double> parameters = new List<double>();

            if (with_twist)
            {
                double t;
                foreach (var plane in planes)
                {
                    Centreline.ClosestPoint(plane.Origin, out t);

                    parameters.Add(t);
                    vectors.Add(plane.YAxis);
                }
                Orientation = new VectorListOrientation(Centreline, parameters, vectors);

            }
            else
            {
                var origin = Centreline.PointAtStart;
                var x_axis = Vector3d.CrossProduct(planes[0].YAxis, Centreline.TangentAtStart);
                var y_axis = Vector3d.CrossProduct(Centreline.TangentAtStart, x_axis);

                Orientation = new VectorOrientation(y_axis);
            }

        }

        public StraightGlulam(Curve curve) : base()
        {
            Centreline = curve;
            //Centreline.Domain.MakeIncreasing();

            Plane p;
            Centreline.PerpendicularFrameAt(Centreline.Domain.Min, out p);
            Orientation = new VectorOrientation(Vector3d.ZAxis);
        }

        public override void GenerateCrossSectionPlanes(int N, out Plane[] planes, out double[] t, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            //Curve CL = Centreline.Extend(CurveEnd.Both, offset, CurveExtensionStyle.Line);
            Curve CL = Centreline;
            t = new double[] { CL.Domain.Min, CL.Domain.Max };
            Array.Sort(t);

            planes = new Plane[] { GetPlane(t[0]), GetPlane(t[1]) };
        }

        public override Mesh ToMesh(double offset = 0.0, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            //Curve CL = Centreline.Extend(CurveEnd.Both, offset, CurveExtensionStyle.Line);

            Mesh m = new Mesh();
            double[] parameters;
            Plane[] frames;
            int N = 2;
            GenerateCrossSectionPlanes(N, out frames, out parameters, interpolation);

            double hW = Data.NumWidth * Data.LamWidth / 2 + offset;
            double hH = Data.NumHeight * Data.LamHeight / 2 + offset;

            Plane pplane;

            // vertex index and next frame vertex index
            int i4;
            int ii4;

            double Length = Centreline.GetLength() + offset * 2;
            double MaxT = parameters.Last() - parameters.First();
            double Width = Data.NumWidth * Data.LamWidth / 1000;
            double Height = Data.NumHeight * Data.LamHeight / 1000;


            for (int i = 0; i < parameters.Length; ++i)
            {
                i4 = i * 8;
                ii4 = i4 - 8;

                pplane = frames[i];

                for (int j = -1; j <= 1; j += 2)
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Point3d v = pplane.Origin + hW * j * pplane.XAxis + hH * k * pplane.YAxis;
                        m.Vertices.Add(v);
                        m.Vertices.Add(v);
                    }
                }

                //double DivV = DivParams[i] / MaxT;
                double DivV = parameters[i] / MaxT * Length / 1000;
                m.TextureCoordinates.Add(2 * Width + 2 * Height, DivV);
                m.TextureCoordinates.Add(0.0, DivV);

                m.TextureCoordinates.Add(Height, DivV);
                m.TextureCoordinates.Add(Height, DivV);

                m.TextureCoordinates.Add(2 * Height + Width, DivV);
                m.TextureCoordinates.Add(2 * Height + Width, DivV);

                m.TextureCoordinates.Add(Width + Height, DivV);
                m.TextureCoordinates.Add(Width + Height, DivV);


                if (i > 0)
                {

                    m.Faces.AddFace(i4 + 2,
                      ii4 + 2,
                      ii4 + 1,
                      i4 + 1);
                    m.Faces.AddFace(i4 + 6,
                      ii4 + 6,
                      ii4 + 3,
                      i4 + 3);
                    m.Faces.AddFace(i4 + 4,
                      ii4 + 4,
                      ii4 + 7,
                      i4 + 7);
                    m.Faces.AddFace(i4,
                      ii4,
                      ii4 + 5,
                      i4 + 5);
                }
            }

            // Start cap
            pplane = GetPlane(parameters.First());
            Point3d vc = pplane.Origin + hW * -1 * pplane.XAxis + hH * -1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * -1 * pplane.XAxis + hH * 1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * 1 * pplane.XAxis + hH * -1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * 1 * pplane.XAxis + hH * 1 * pplane.YAxis;
            m.Vertices.Add(vc);

            m.TextureCoordinates.Add(0, 0);
            m.TextureCoordinates.Add(0, Height);
            m.TextureCoordinates.Add(Width, 0);
            m.TextureCoordinates.Add(Width, Height);

            m.Faces.AddFace(m.Vertices.Count - 4,
              m.Vertices.Count - 3,
              m.Vertices.Count - 1,
              m.Vertices.Count - 2);

            // End cap
            pplane = GetPlane(parameters.Last());
            vc = pplane.Origin + hW * -1 * pplane.XAxis + hH * -1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * -1 * pplane.XAxis + hH * 1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * 1 * pplane.XAxis + hH * -1 * pplane.YAxis;
            m.Vertices.Add(vc);
            vc = pplane.Origin + hW * 1 * pplane.XAxis + hH * 1 * pplane.YAxis;
            m.Vertices.Add(vc);

            m.TextureCoordinates.Add(0, 0);
            m.TextureCoordinates.Add(0, Height);
            m.TextureCoordinates.Add(Width, 0);
            m.TextureCoordinates.Add(Width, Height);

            m.Faces.AddFace(m.Vertices.Count - 2,
              m.Vertices.Count - 1,
              m.Vertices.Count - 3,
              m.Vertices.Count - 4);

            m.Vertices.CullUnused();
            m.Compact();

            //m.UserDictionary.ReplaceContentsWith(GetArchivableDictionary());
            //m.UserDictionary.Set("glulam", GetArchivableDictionary());
            return m;
        }

        public override Brep ToBrep(double offset = 0.0)
        {
            //Curve CL = Centreline.Extend(CurveEnd.Both, offset, CurveExtensionStyle.Line);
            Curve CL = Centreline;
            double Length = CL.GetLength();
            double hW = Data.NumWidth * Data.LamWidth / 2 + offset;
            double hH = Data.NumHeight * Data.LamHeight / 2 + offset;
            double[] DivParams = new double[] { CL.Domain.Min, CL.Domain.Max };

            m_section_corners = CornerGenerator(offset);



            Curve[][] LoftCurves = new Curve[4][];

            for (int i = 0; i < 4; ++i)
                LoftCurves[i] = new Curve[2];

            Rhino.Geometry.Transform xform;
            Line l1 = new Line(m_section_corners[0], m_section_corners[1]);
            Line l2 = new Line(m_section_corners[1], m_section_corners[2]);
            Line l3 = new Line(m_section_corners[2], m_section_corners[3]);
            Line l4 = new Line(m_section_corners[3], m_section_corners[0]);
            Line temp;

            for (int i = 0; i < DivParams.Length; ++i)
            {
                Plane p = GetPlane(DivParams[i]);
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, p);
                temp = l1; temp.Transform(xform);
                LoftCurves[0][i] = temp.ToNurbsCurve();
                temp = l2; temp.Transform(xform);
                LoftCurves[1][i] = temp.ToNurbsCurve();
                temp = l3; temp.Transform(xform);
                LoftCurves[2][i] = temp.ToNurbsCurve();
                temp = l4; temp.Transform(xform);
                LoftCurves[3][i] = temp.ToNurbsCurve();
            }

            Brep brep = new Brep();

            for (int i = 0; i < 4; ++i)
            {
                Brep[] loft = Brep.CreateFromLoft(LoftCurves[i], Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                if (loft == null || loft.Length < 1)
                    continue;
                for (int j = 0; j < loft.Length; ++j)
                    brep.Append(loft[j]);
            }

            brep.JoinNakedEdges(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            brep = brep.CapPlanarHoles(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            //brep.UserDictionary.Set("glulam", GetArchivableDictionary());


            return brep;
        }

        public override List<Brep> GetLamellaeBreps()
        {
            double Length = Centreline.GetLength();
            double hW = Data.NumWidth * Data.LamWidth / 2;
            double hH = Data.NumHeight * Data.LamHeight / 2;
            double[] DivParams = new double[] { Centreline.Domain.Min, Centreline.Domain.Max };

            List<Curve>[,] LoftCurves = new List<Curve>[Data.NumWidth, Data.NumHeight];
            List<Brep> LamellaBreps = new List<Brep>();

            // Initialize curve lists
            for (int i = 0; i < Data.NumWidth; ++i)
                for (int j = 0; j < Data.NumHeight; ++j)
                    LoftCurves[i, j] = new List<Curve>();

            for (int i = 0; i < DivParams.Length; ++i)
            {
                Plane p = GetPlane(DivParams[i]);

                for (int j = 0; j < Data.NumWidth; ++j)
                {
                    for (int k = 0; k < Data.NumHeight; ++k)
                    {
                        Rectangle3d rec = new Rectangle3d(p,
                            new Interval(-hW + j * Data.LamWidth, -hW + (j + 1) * Data.LamWidth),
                            new Interval(-hH + k * Data.LamHeight, -hH + (k + 1) * Data.LamHeight));
                        LoftCurves[j, k].Add(rec.ToNurbsCurve());
                    }
                }
            }

            for (int i = 0; i < Data.NumWidth; ++i)
            {
                for (int j = 0; j < Data.NumHeight; ++j)
                {
                    Brep[] brep = Brep.CreateFromLoft(LoftCurves[i, j], Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    if (brep != null && brep.Length > 0)
                        LamellaBreps.Add(brep[0].CapPlanarHoles(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance));
                }
            }
            return LamellaBreps;
        }

        public override Curve[] GetEdgeCurves(double offset = 0.0)
        {
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            int numCorners = 4;
            GenerateCorners(offset);

            List<Point3d>[] crvPts = new List<Point3d>[numCorners];
            for (int i = 0; i < numCorners; ++i)
            {
                crvPts[i] = new List<Point3d>();
            }

            Transform xform;
            Point3d temp;

            for (int i = 0; i < parameters.Length; ++i)
            {
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);

                for (int j = 0; j < numCorners; ++j)
                {
                    temp = new Point3d(m_section_corners[j]);
                    temp.Transform(xform);
                    crvPts[j].Add(temp);
                }
            }

            Curve[] edges = new Curve[numCorners];

            for (int i = 0; i < numCorners; ++i)
            {
                edges[i] = new Line(crvPts[i][0], crvPts[i][1]).ToNurbsCurve();
                //edges[i] = Curve.CreateInterpolatedCurve(crvPts[i], 3, CurveKnotStyle.Chord, frames.First().ZAxis, frames.Last().ZAxis);
            }

            return edges;
        }
        public override double GetMaxCurvature(ref double width, ref double height)
        {
            width = 0.0;
            height = 0.0;
            return 0.0;
        }

        public override List<Mesh> GetLamellaeMeshes() => base.GetLamellaeMeshes();

        public override List<Curve> GetLamellaeCurves()
        {
            List<Curve> lam_crvs = new List<Curve>();

            double hWidth = Data.NumWidth * Data.LamWidth / 2;
            double hHeight = Data.NumHeight * Data.LamHeight / 2;
            Plane plane = Utility.PlaneFromNormalAndYAxis(
                Centreline.PointAtStart,
                Centreline.TangentAtStart,
                Orientation.GetOrientation(Centreline, Centreline.Domain.Min));

            double hLw = Data.LamWidth / 2;
            double hLh = Data.LamHeight / 2;

            Transform xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane);

            for (int i = 0; i < Data.NumHeight; ++i)
            {
                for (int j = 0; j < Data.NumWidth; ++j)
                {
                    Point3d p = new Point3d(j * Data.LamWidth - hWidth + hLw, i * Data.LamHeight - hHeight + hLh, 0.0);
                    Line l = new Line(p, Vector3d.ZAxis * Centreline.GetLength());
                    l.Transform(xform);

                    lam_crvs.Add(l.ToNurbsCurve());
                }
            }

            return lam_crvs;
        }

        public override Curve GetLamellaCurve(int i, int j)
        {
            double hWidth = Data.NumWidth * Data.LamWidth / 2;
            double hHeight = Data.NumHeight * Data.LamHeight / 2;
            Plane plane = Utility.PlaneFromNormalAndYAxis(
                Centreline.PointAtStart,
                Centreline.TangentAtStart,
                Orientation.GetOrientation(Centreline, Centreline.Domain.Min));

            double hLw = Data.LamWidth / 2;
            double hLh = Data.LamHeight / 2;

            Transform xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane);

            Point3d p = new Point3d(j * Data.LamWidth - hWidth + hLw, i * Data.LamHeight - hHeight + hLh, 0.0);
            Line l = new Line(p, Vector3d.ZAxis * Centreline.GetLength());
            l.Transform(xform);

            return l.ToNurbsCurve();
        }

        public override GlulamType Type() => GlulamType.Straight;

        public override string ToString() => "StraightGlulam";

        public override Mesh MapToCurveSpace(Mesh m)
        {
            Mesh mesh = m.DuplicateMesh();
            Vector3d v = Orientation.GetOrientation(Centreline, Centreline.Domain.Min);
            Plane p = Utility.PlaneFromNormalAndYAxis(Centreline.PointAtStart, Centreline.TangentAtStart, v);

            mesh.Transform(Rhino.Geometry.Transform.PlaneToPlane(p, Plane.WorldXY));
            return mesh;
        }

        public override bool InKLimitsComponent(out bool width, out bool height)
        {
            width = height = true;
            return true;
        }

        public override Curve CreateOffsetCurve(double x, double y, bool rebuild = false, int rebuild_pts = 20)
        {
            Vector3d v = Orientation.GetOrientation(Centreline, Centreline.Domain.Min);

            Plane p = Utility.PlaneFromNormalAndYAxis(Centreline.PointAtStart, Centreline.TangentAtStart, v);
            Curve copy = Centreline.DuplicateCurve();
            copy.Transform(Rhino.Geometry.Transform.Translation(p.XAxis * x + p.YAxis * y));
            return copy;
        }

        public override Curve CreateOffsetCurve(double x, double y, bool offset_start, bool offset_end, bool rebuild = false, int rebuild_pts = 20)
        {
            throw new NotImplementedException();
        }

        /*
        public override void CalculateLamellaSizes(double height, double width)
        {
            Data.LamHeight = height;
            Data.LamWidth = width;
            Data.Lamellae = new Stick[1, 1];
        }
        */
    }
}
