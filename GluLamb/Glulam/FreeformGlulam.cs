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
using Eto.Forms;
using Grasshopper.Kernel.Geometry.Delaunay;
using Rhino;
using Rhino.Geometry;

namespace GluLamb
{
    public abstract class FreeformGlulam : Glulam
    {
        /// <summary>
        /// Generate a series of planes on the glulam cross-section. TODO: Re-implement as GlulamOrientation function
        /// </summary>
        /// <param name="N">Number of planes to extract.</param>
        /// <param name="extension">Extension of the centreline curve</param>
        /// <param name="frames">Output cross-section planes.</param>
        /// <param name="parameters">Output t-values along centreline curve.</param>
        /// <param name="interpolation">Type of interpolation to use (default is Linear).</param>
        public override void GenerateCrossSectionPlanes(int N, out Plane[] frames, out double[] parameters, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            Curve curve = Centreline;

            double multiplier = RhinoMath.UnitScale(UnitSystem.Millimeters, Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);

            //PolylineCurve discrete = curve.ToPolyline(Glulam.Tolerance * 10, Glulam.AngleTolerance, 0.0, 0.0);
            PolylineCurve discrete = curve.ToPolyline(multiplier * Tolerance, AngleTolerance, multiplier * MininumSegmentLength, curve.GetLength() / MinimumNumSegments);
            if (discrete == null)
            {
                parameters = curve.DivideByCount(N - 1, true).ToArray();

                //discrete = new PolylineCurve(new Point3d[] { curve.PointAtStart, curve.PointAtEnd });
            }
            else
            {
                if (discrete.TryGetPolyline(out Polyline discrete2))
                {
                    N = discrete2.Count;
                    parameters = new double[N];

                    for (int i = 0; i < N; ++i)
                        curve.ClosestPoint(discrete2[i], out parameters[i]);
                }
                else
                {
                    parameters = curve.DivideByCount(N - 1, true).ToArray();
                }
            }

            //frames = parameters.Select(x => GetPlane(x)).ToArray();
            //return;

            frames = new Plane[parameters.Length];

            var vectors = Orientation.GetOrientations(curve, parameters);

            Plane temp;
            for (int i = 0; i < parameters.Length; ++i)
            {
                temp = Utility.PlaneFromNormalAndYAxis(
                    curve.PointAt(parameters[i]),
                    curve.TangentAt(parameters[i]),
                    vectors[i]);

                if (temp.IsValid)
                    frames[i] = temp;
                else
                    throw new Exception(string.Format("Plane is invalid: vector {0} tangent {1}", vectors[i], curve.TangentAt(parameters[i])));
            }

            return;
        }

        public override Mesh ToMesh(double offset = 0.0, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            Mesh m = new Mesh();

            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            GetSectionOffset(out double offsetX, out double offsetY);
            Point3d[] m_corners = GenerateCorners();

            double hW = Data.NumWidth * Data.LamWidth / 2 + offset;
            double hH = Data.NumHeight * Data.LamHeight / 2 + offset;

            // vertex index and next frame vertex index
            int i4;
            int ii4;

            //double texLength = (Centreline.GetLength() + offset * 2) / 1000;
            //double MaxT = parameters.Last() - parameters.First();

            double texWidth = Width / 1000.0; // Width in meters
            double texHeight = Height / 1000.0; // Height in meters

            for (int i = 0; i < frames.Length; ++i)
            {
                i4 = i * 8;
                ii4 = i4 - 8;

                double texLength = Centreline.GetLength(
                  new Interval(Centreline.Domain.Min, parameters[i])) / 1000;

                for (int j = 0; j < m_corners.Length; ++j)
                {
                    Point3d v = frames[i].PointAt(m_corners[j].X, m_corners[j].Y);
                    m.Vertices.Add(v);
                    m.Vertices.Add(v);
                }

                //double DivV = parameters[i] / MaxT * Length / 1000;
                m.TextureCoordinates.Add(texLength, 2 * texWidth + 2 * texHeight);
                m.TextureCoordinates.Add(texLength, 0.0);

                m.TextureCoordinates.Add(texLength, texHeight);
                m.TextureCoordinates.Add(texLength, texHeight);

                m.TextureCoordinates.Add(texLength, 2 * texHeight + texWidth);
                m.TextureCoordinates.Add(texLength, 2 * texHeight + texWidth);

                m.TextureCoordinates.Add(texLength, texWidth + texHeight);
                m.TextureCoordinates.Add(texLength, texWidth + texHeight);

                if (i > 0)
                {
                    m.Faces.AddFace(i4 + 2,
                      ii4 + 2,
                      ii4 + 1,
                      i4 + 1);
                    m.Faces.AddFace(i4 + 5,
                      ii4 + 5,
                      ii4 + 3,
                      i4 + 3);
                    m.Faces.AddFace(i4 + 7,
                      ii4 + 7,
                      ii4 + 4,
                      i4 + 4);

                    m.Faces.AddFace(i4,
                      ii4,
                      ii4 + 7,
                      i4 + 7);
                }
            }

            Plane pplane;

            // Start cap
            pplane = frames.First();

            //m_section_corners.Select(x => frames.Select(y => m.Vertices.Add(y.PointAt(x.X, x.Y))));

            //for (int j = 0; j < m_corners.Length; ++j)
            //    m.Vertices.Add(pplane.PointAt(m_corners[j].X, m_corners[j].Y));

            var start_profile = new Polyline(m_corners);
            start_profile.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames.First()));
            var start_mesh = new Mesh();
            start_mesh.Vertices.AddVertices(start_profile);

            start_profile.Add(start_profile[0]);
            start_mesh.Faces.AddFaces(start_profile.TriangulateClosedPolyline());
            start_mesh.Faces.ConvertTrianglesToQuads(RhinoMath.ToRadians(2), 0.875);

            var end_profile = new Polyline(m_corners);
            end_profile.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames.Last()));
            var end_mesh = new Mesh();
            end_mesh.Vertices.AddVertices(end_profile);

            end_profile.Add(end_profile[0]);
            end_mesh.Faces.AddFaces(end_profile.TriangulateClosedPolyline());
            end_mesh.Faces.ConvertTrianglesToQuads(RhinoMath.ToRadians(2), 0.875);

            m.Append(start_mesh);
            m.Append(end_mesh);

            return m;

            m.TextureCoordinates.Add(0, 0);
            m.TextureCoordinates.Add(0, texHeight);
            m.TextureCoordinates.Add(texWidth, 0);
            m.TextureCoordinates.Add(texWidth, texHeight);

            m.Faces.AddFace(m.Vertices.Count - 4,
              m.Vertices.Count - 3,
              m.Vertices.Count - 2,
              m.Vertices.Count - 1);

            // End cap
            pplane = frames.Last();
            for (int j = 0; j < m_corners.Length; ++j)
                m.Vertices.Add(pplane.PointAt(m_corners[j].X, m_corners[j].Y));

            m.TextureCoordinates.Add(0, 0);
            m.TextureCoordinates.Add(0, texHeight);
            m.TextureCoordinates.Add(texWidth, 0);
            m.TextureCoordinates.Add(texWidth, texHeight);

            m.Faces.AddFace(m.Vertices.Count - 1,
              m.Vertices.Count - 2,
              m.Vertices.Count - 3,
              m.Vertices.Count - 4);
            //m.UserDictionary.ReplaceContentsWith(GetArchivableDictionary());
            //m.UserDictionary.Set("glulam", GetArchivableDictionary());

            return m;

        }

        public Polyline[] GetCrossSections(double offset = 0.0)
        {
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] tt, Data.InterpolationType);

            GenerateCorners(offset);

            Polyline[] cross_sections = new Polyline[N];

            Transform xform;

            Polyline pl = new Polyline(
                new Point3d[] {
                    m_section_corners[0],
                    m_section_corners[1],
                    m_section_corners[2],
                    m_section_corners[3],
                    m_section_corners[0]
                });

            Polyline temp;

            for (int i = 0; i < frames.Length; ++i)
            {
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);
                temp = new Polyline(pl);
                temp.Transform(xform);
                cross_sections[i] = temp;
            }

            return cross_sections;
        }

#if SLOWBREP
        public override Brep ToBrep(double offset = 0.0)
        {
            
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

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
                //frames[i] = frames[i].FlipAroundYAxis();
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);

                for (int j = 0; j < numCorners; ++j)
                {
                    temp = new Point3d(m_section_corners[j]);
                    temp.Transform(xform);
                    crvPts[j].Add(temp);
                }
            }
            

            var edge_points = GetEdgePoints(offset);
            int numCorners = m_section_corners.Length;
            var num_points = edge_points[0].Count;

            NurbsCurve[] edges = new NurbsCurve[numCorners + 4];
            var edge_parameters = new List<double>[numCorners];
            double t;

            for (int i = 0; i < numCorners; ++i)
            {
                //edges[i] = NurbsCurve.CreateInterpolatedCurve(edge_points[i], 3, CurveKnotStyle.Chord, Centreline.TangentAtStart, Centreline.TangentAtEnd);
                edges[i] = NurbsCurve.Create(false, 3, edge_points[i]);
                edge_parameters[i] = new List<double>();

                foreach (Point3d pt in edge_points[i])
                {
                    edges[i].ClosestPoint(pt, out t);
                    edge_parameters[i].Add(t);
                }
            }

            edges[numCorners + 0] = new Line(edge_points[3].First(), edge_points[0].First()).ToNurbsCurve();
            edges[numCorners + 1] = new Line(edge_points[2].First(), edge_points[1].First()).ToNurbsCurve();

            edges[numCorners + 2] = new Line(edge_points[2].Last(), edge_points[1].Last()).ToNurbsCurve();
            edges[numCorners + 3] = new Line(edge_points[3].Last(), edge_points[0].Last()).ToNurbsCurve();

            Brep[] sides = new Brep[numCorners + 2];
            int ii = 0;
            for (int i = 0; i < numCorners; ++i)
            {
                ii = (i + 1).Modulus(numCorners);

                List<Point2d> rulings = new List<Point2d>();
                for (int j = 0; j < num_points; ++j)
                    rulings.Add(new Point2d(edge_parameters[i][j], edge_parameters[ii][j]));

                sides[i] = Brep.CreateDevelopableLoft(edges[i], edges[ii], rulings).First();
                //sides[i] = Brep.CreateFromLoft(
                //  new Curve[] { edges[i], edges[ii] },
                //  Point3d.Unset, Point3d.Unset, LoftType.Normal, false)[0];
            }

            // Make ends

            sides[numCorners + 0] = Brep.CreateFromLoft(
              new Curve[] {
                  edges[numCorners + 0],
                  edges[numCorners + 1] },
              Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

            sides[numCorners + 1] = Brep.CreateFromLoft(
              new Curve[] {
                  edges[numCorners + 2],
                  edges[numCorners + 3] },
              Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

            // Join Breps

            Brep brep = Brep.JoinBreps(
              sides,
              Tolerance
              )[0];

            //brep.UserDictionary.Set("glulam", GetArchivableDictionary());

            return brep;
        }
#else

        public override Brep ToBrep(double offset = 0.0)
        {
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            var cross_section_profile = new Polyline(GenerateCorners(offset));
            cross_section_profile.Add(cross_section_profile[0]);

            Transform xform;

            var profiles = new List<Curve>();

            for (int i = 0; i < parameters.Length; ++i)
            {
                //frames[i] = frames[i].FlipAroundYAxis();
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);
                var temp_profile = cross_section_profile.Duplicate().ToNurbsCurve();
                temp_profile.Transform(xform);
                profiles.Add(temp_profile);
            }

            if (profiles.Count < 1)
                throw new Exception("FreeformGlulam.ToBrep(): profiles was null");

            var body = Brep.CreateFromLoft(profiles, Point3d.Unset, Point3d.Unset, LoftType.Tight, false);
            var start_cap = Brep.CreatePlanarBreps(profiles.First(), Tolerance);
            var end_cap = Brep.CreatePlanarBreps(profiles.Last(), Tolerance);

            if (body == null)
                throw new Exception("FreeformGlulam.ToBrep(): body was null");
            else if (start_cap == null)
                throw new Exception("FreeformGlulam.ToBrep(): start_cap was null");
            else if (end_cap == null)
                throw new Exception("FreeformGlulam.ToBrep(): end_cap was null");

            var joined = Brep.JoinBreps(body.Concat(start_cap).Concat(end_cap), Tolerance);
            if (joined.Length > 0)
                return joined[0];
            else throw new Exception("FreeformGlulam.ToBrep(): Joined Brep failed.");
        }
#endif
        public List<Point3d>[] Test_GetBoundingBrepPoints()
        {
            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            int numCorners = 4;
            GenerateCorners(0.0);

            List<Point3d>[] crvPts = new List<Point3d>[numCorners];
            for (int i = 0; i < numCorners; ++i)
            {
                crvPts[i] = new List<Point3d>();
            }

            Transform xform;
            Point3d temp;

            for (int i = 0; i < parameters.Length; ++i)
            {
                //frames[i] = frames[i].FlipAroundYAxis();
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);

                for (int j = 0; j < numCorners; ++j)
                {
                    temp = new Point3d(m_section_corners[j]);
                    temp.Transform(xform);
                    crvPts[j].Add(temp);
                }
            }

            return crvPts;
            /*
            Curve[] edges = new Curve[numCorners + 4];

            for (int i = 0; i < numCorners; ++i)
            {
                edges[i] = Curve.CreateInterpolatedCurve(crvPts[i], 3);
            }

            edges[4] = new Line(crvPts[3].First(), crvPts[0].First()).ToNurbsCurve();
            edges[5] = new Line(crvPts[2].First(), crvPts[1].First()).ToNurbsCurve();

            edges[6] = new Line(crvPts[2].Last(), crvPts[1].Last()).ToNurbsCurve();
            edges[7] = new Line(crvPts[3].Last(), crvPts[0].Last()).ToNurbsCurve();

            return edges;
            */
        }

        public override List<Brep> GetLamellaeBreps()
        {
            double Length = Centreline.GetLength();
            double hW = Data.NumWidth * Data.LamWidth / 2;
            double hH = Data.NumHeight * Data.LamHeight / 2;
            //double[] DivParams = Centreline.DivideByCount(Data.Samples - 1, true);

            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            GetSectionOffset(out double offsetX, out double offsetY);

            Point3d[,,] AllPoints = new Point3d[Data.NumWidth + 1, Data.NumHeight + 1, parameters.Length];
            Point3d[,] CornerPoints = new Point3d[Data.NumWidth + 1, Data.NumHeight + 1];

            for (int x = 0; x <= Data.NumWidth; ++x)
            {
                for (int y = 0; y <= Data.NumHeight; ++y)
                {
                    CornerPoints[x, y] = new Point3d(
                        -hW + offsetX + x * Data.LamWidth,
                        -hH + offsetY + y * Data.LamHeight,
                        0);
                }
            }



            Transform xform;
            Point3d temp;
            for (int i = 0; i < frames.Length; ++i)
            {
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);

                for (int x = 0; x <= Data.NumWidth; ++x)
                {
                    for (int y = 0; y <= Data.NumHeight; ++y)
                    {
                        temp = new Point3d(CornerPoints[x, y]);
                        temp.Transform(xform);
                        AllPoints[x, y, i] = temp;
                    }
                }
            }

            Curve[,] EdgeCurves = new Curve[Data.NumWidth + 1, Data.NumHeight + 1];
            for (int x = 0; x <= Data.NumWidth; ++x)
            {
                for (int y = 0; y <= Data.NumHeight; ++y)
                {
                    Point3d[] pts = new Point3d[frames.Length];
                    for (int z = 0; z < frames.Length; ++z)
                    {
                        pts[z] = AllPoints[x, y, z];
                    }

                    EdgeCurves[x, y] = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Chord, frames.First().ZAxis, frames.Last().ZAxis);
                }
            }

            List<Brep> LamellaBreps = new List<Brep>();

            for (int x = 0; x < Data.NumWidth; ++x)
            {
                for (int y = 0; y < Data.NumHeight; ++y)
                {
                    Curve[] edges = new Curve[8];
                    edges[4] = new Line(AllPoints[x, y, 0], AllPoints[x + 1, y, 0]).ToNurbsCurve();
                    edges[5] = new Line(AllPoints[x, y + 1, 0], AllPoints[x + 1, y + 1, 0]).ToNurbsCurve();

                    edges[6] = new Line(AllPoints[x, y, frames.Length - 1], AllPoints[x + 1, y, frames.Length - 1]).ToNurbsCurve();
                    edges[7] = new Line(AllPoints[x, y + 1, frames.Length - 1], AllPoints[x + 1, y + 1, frames.Length - 1]).ToNurbsCurve();

                    Brep[] sides = new Brep[6];

                    sides[0] = Brep.CreateFromLoft(
                          new Curve[] { EdgeCurves[x, y], EdgeCurves[x + 1, y] },
                          Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                    sides[1] = Brep.CreateFromLoft(
                          new Curve[] { EdgeCurves[x + 1, y], EdgeCurves[x + 1, y + 1] },
                          Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                    sides[2] = Brep.CreateFromLoft(
                      new Curve[] { EdgeCurves[x + 1, y + 1], EdgeCurves[x, y + 1] },
                      Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                    sides[3] = Brep.CreateFromLoft(
                      new Curve[] { EdgeCurves[x, y + 1], EdgeCurves[x, y] },
                      Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

                    sides[4] = Brep.CreateFromLoft(
                      new Curve[] { edges[4], edges[5] },
                      Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

                    sides[5] = Brep.CreateFromLoft(
                      new Curve[] { edges[6], edges[7] },
                      Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

                    Brep brep = Brep.JoinBreps(
                      sides,
                      Tolerance
                      )[0];

                    LamellaBreps.Add(brep);
                }
            }

            return LamellaBreps;
        }

        public override List<Mesh> GetLamellaeMeshes() => base.GetLamellaeMeshes();

        public override List<Curve> GetLamellaeCurves()
        {
            int N = Math.Max(Data.Samples, 6);
            GenerateCrossSectionPlanes(N, out Plane[] frames, out double[] parameters, Data.InterpolationType);

            List<Point3d>[] crvPts = new List<Point3d>[Data.Lamellae.Length];
            for (int i = 0; i < Data.Lamellae.Length; ++i)
            {
                crvPts[i] = new List<Point3d>();
            }

            // ****************

            Transform xform;
            Point3d temp;

            double hWidth = Data.NumWidth * Data.LamWidth / 2;
            double hHeight = Data.NumHeight * Data.LamHeight / 2;
            double hLw = Data.LamWidth / 2;
            double hLh = Data.LamHeight / 2;

            GetSectionOffset(out double offsetX, out double offsetY);

            List<Point3d> LamellaPoints = new List<Point3d>();

            for (int x = 0; x < Data.Lamellae.GetLength(0); ++x)
            {
                for (int y = 0; y < Data.Lamellae.GetLength(1); ++y)
                {
                    LamellaPoints.Add(
                        new Point3d(
                            -hWidth + offsetX + hLw + x * Data.LamWidth,
                            -hHeight + offsetY + hLh + y * Data.LamHeight,
                            0));
                }
            }

            for (int i = 0; i < frames.Length; ++i)
            {
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, frames[i]);

                for (int j = 0; j < Data.Lamellae.Length; ++j)
                {
                    temp = new Point3d(LamellaPoints[j]);
                    temp.Transform(xform);
                    crvPts[j].Add(temp);
                }
            }

            Curve[] LamellaCentrelines = new Curve[Data.Lamellae.Length];

            for (int i = 0; i < Data.Lamellae.Length; ++i)
            {
                LamellaCentrelines[i] = Curve.CreateInterpolatedCurve(crvPts[i], 3, CurveKnotStyle.Chord, frames.First().ZAxis, frames.Last().ZAxis);
            }

            return LamellaCentrelines.ToList();
            /*

            List<Rhino.Geometry.Curve> crvs = new List<Rhino.Geometry.Curve>();


            Rhino.Geometry.Plane plane;
            //Centreline.PerpendicularFrameAt(Centreline.Domain.Min, out plane);



            List<List<List<Rhino.Geometry.Point3d>>> verts;

            verts = new List<List<List<Point3d>>>();
            for (int i = 0; i < Data.NumHeight; ++i)
            {
                verts.Add(new List<List<Point3d>>());
                for (int j = 0; j < Data.NumWidth; ++j)
                {
                    verts[i].Add(new List<Rhino.Geometry.Point3d>());
                }
            }
            double t;
            for (int i = 0; i < xPlanes.Length; ++i)
            {
                plane = xPlanes[i];
                var xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, plane);


                for (int j = 0; j < Data.NumHeight; ++j)
                {
                    for (int k = 0; k < Data.NumWidth; ++k)
                    {
                        Rhino.Geometry.Point3d p = new Rhino.Geometry.Point3d(
                            k * Data.LamWidth - hWidth + hLw, 
                            j * Data.LamHeight - hHeight + hLh, 
                            0.0);
                        p.Transform(xform);
                        verts[j][k].Add(p);
                    }
                }
            }

            for (int i = 0; i < Data.NumHeight; ++i)
            {
                for (int j = 0; j < Data.NumWidth; ++j)
                {
                    crvs.Add(new Rhino.Geometry.Polyline(verts[i][j]).ToNurbsCurve());
                }
            }

            return crvs;
            */
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
                edges[i] = Curve.CreateInterpolatedCurve(crvPts[i], 3, CurveKnotStyle.Chord, frames.First().ZAxis, frames.Last().ZAxis);
            }

            return edges;
        }

        /*
        public override Glulam Overbend(double t)
        {
            PolyCurve pc = Centreline.DuplicateCurve() as PolyCurve;
            if (pc == null) return this;

            // fix this domain issue... not working for some reason
            PolyCurve pco = pc.OverBend(t);
            pco.Domain = pc.Domain;

            FreeformGlulam g = this.Duplicate() as FreeformGlulam;
            g.Centreline = pco;

            return g;
        }
        */
        public abstract override GlulamType Type();

        public override double GetMaxCurvature(ref double width, ref double height)
        {
            double[] t = Centreline.DivideByCount(CurvatureSamples, false);
            double max_kw = 0.0, max_kh = 0.0, max_k = 0.0;
            Plane temp;
            Vector3d k;
            for (int i = 0; i < t.Length; ++i)
            {
                temp = GetPlane(t[i]);

                k = Centreline.CurvatureAt(t[i]);
                max_kw = Math.Max(max_kw, Math.Abs(k * temp.XAxis));
                max_kh = Math.Max(max_kh, Math.Abs(k * temp.YAxis));
                max_k = Math.Max(max_k, k.Length);
            }
            width = max_kw;
            height = max_kh;
            return max_k;
        }

        public override string ToString() => "FreeformGlulam";

        public override Mesh MapToCurveSpace(Mesh m)
        {
            throw new NotImplementedException();
        }

        public override Curve CreateOffsetCurve(double x, double y, bool rebuild = false, int rebuild_pts = 20)
        {
            List<Point3d> pts = new List<Point3d>();

            int N = Math.Max(6, Data.Samples);

            GenerateCrossSectionPlanes(N, out Plane[] planes, out double[] parameters, Data.InterpolationType);

            for (int i = 0; i < planes.Length; ++i)
            {
                Plane p = planes[i];
                pts.Add(p.Origin + p.XAxis * x + p.YAxis * y);
            }

            Curve new_curve = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Uniform,
                Centreline.TangentAtStart, Centreline.TangentAtEnd);

            if (new_curve == null)
                throw new Exception("FreeformGlulam::CreateOffsetCurve:: Failed to create interpolated curve!");

            double len = new_curve.GetLength();
            new_curve.Domain = new Interval(0.0, len);

            if (rebuild)
                new_curve = new_curve.Rebuild(rebuild_pts, new_curve.Degree, true);

            return new_curve;
        }

        public override Curve CreateOffsetCurve(double x, double y, bool offset_start, bool offset_end, bool rebuild = false, int rebuild_pts = 20)
        {
            if (!offset_start && !offset_end) return Centreline.DuplicateCurve();
            if (offset_start && offset_end) return CreateOffsetCurve(x, y, rebuild, rebuild_pts);

            List<Point3d> pts = new List<Point3d>();
            double[] t = Centreline.DivideByCount(this.Data.Samples, true);

            double tmin = offset_start ? t.First() : t.Last();
            double tmax = offset_end ? t.Last() : t.First();

            for (int i = 0; i < t.Length; ++i)
            {
                Plane p = GetPlane(t[i]);
                double l = Ease.QuadOut(Interpolation.Unlerp(tmin, tmax, t[i]));
                pts.Add(p.Origin + p.XAxis * l * x + p.YAxis * l * y);
            }

            Curve new_curve = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Uniform,
                Centreline.TangentAtStart, Centreline.TangentAtEnd);

            if (new_curve == null)
                throw new Exception(this.ToString() + "::CreateOffsetCurve:: Failed to create interpolated curve!");

            double len = new_curve.GetLength();
            new_curve.Domain = new Interval(0.0, len);

            if (rebuild)
                new_curve = new_curve.Rebuild(rebuild_pts, new_curve.Degree, true);

            return new_curve;
        }
    }
}
