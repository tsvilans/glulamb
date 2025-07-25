﻿/*
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
using System.Linq;
using System.Collections.Generic;

using Rhino.Display;
using System.Drawing;
using Rhino.Geometry;
using System.ComponentModel;
using System.Collections;
using Rhino.DocObjects;
using Rhino;

namespace GluLamb
{
    public class Constants
    {
        public const double Tau = Math.PI * 2.0;
    }

    public enum Side
    {
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8,
        Front = 16,
        Back = 32
    }

    public static class Ease
    {
        public static double QuadOut(double t) =>
             -1.0 * t * (t - 2);

        public static double QuadIn(double t) =>
            t * t * t;

        public static double CubicIn(double t) =>
            t * t * t;

        public static double CubicOut(double t)
        {
            t--;
            return t * t * t + 1;
        }
    }
    public static class Mapping
    {
        public static Color4f VectorToColor1(Vector3d v)
        {
            return new Color4f((float)(v.X / 2 + 0.5), (float)(v.Y / 2 + 0.5), (float)(v.Z / 2 + 0.5), 1.0f);
        }

        public static Color4f Contrast(Color4f color, float contrast)
        {
            var red = ((color.R - 0.5f) * contrast) + 0.5f;
            var green = ((color.G - 0.5f) * contrast) + 0.5f;
            var blue = ((color.B - 0.5f) * contrast) + 0.5f;

            red = Math.Min(1.0f, Math.Max(0.0f, red));
            green = Math.Min(1.0f, Math.Max(0.0f, green));
            blue = Math.Min(1.0f, Math.Max(0.0f, blue));

            return Color4f.FromArgb(color.A, red, green, blue);
        }

        public static Color Contrast(Color color, double contrast)
        {
            var red = ((((color.R / 255.0) - 0.5) * contrast) + 0.5) * 255.0;
            var green = ((((color.G / 255.0) - 0.5) * contrast) + 0.5) * 255.0;
            var blue = ((((color.B / 255.0) - 0.5) * contrast) + 0.5) * 255.0;
            if (red > 255) red = 255;
            if (red < 0) red = 0;
            if (green > 255) green = 255;
            if (green < 0) green = 0;
            if (blue > 255) blue = 255;
            if (blue < 0) blue = 0;

            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }

        public static Color4f VectorToColor2(Vector3d v)
        {
            if (v.Z >= 0)
                return new Color4f((float)(v.X / 2 + 0.5), (float)(v.Y / 2 + 0.5), (float)v.Z, 1.0f);
            return new Color4f((float)(-v.X / 2 + 0.5), (float)(-v.Y / 2 + 0.5), (float)-v.Z, 1.0f);
        }

        public static Point3d FromBarycentricCoordinates(Point3d pt, Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            double x, y, z;

            x = (p1.X - p4.X) * pt.X + (p2.X - p4.X) * pt.Y + (p3.X - p4.X) * pt.Z + p4.X;
            y = (p1.Y - p4.Y) * pt.X + (p2.Y - p4.Y) * pt.Y + (p3.Y - p4.Y) * pt.Z + p4.Y;
            z = (p1.Z - p4.Z) * pt.Z + (p2.Z - p4.Z) * pt.Y + (p3.Z - p4.Z) * pt.Z + p4.Z;

            return new Point3d(x, y, z);
        }

        public static Point3d[] FromBarycentricCoordinates(ICollection<Point3d> pt, Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            return pt.Select(x => FromBarycentricCoordinates(x, p1, p2, p3, p4)).ToArray();
        }

        public static Point3d ToBarycentricCoordinates(Point3d pt, Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            return ToBarycentricCoordinates(new Point3d[] { pt }, p1, p2, p3, p4)[0];
        }

        public static Point3d[] ToBarycentricCoordinates(ICollection<Point3d> pt, Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            //Vector3d r1 = p2 - p1;
            //Vector3d r2 = p3 - p1;
            //Vector3d r3 = p4 - p1;

            //double J = Vector3d.CrossProduct(r1, r2) * r3;

            Transform xform = Transform.Identity;
            xform[0, 0] = p1.X - p4.X;
            xform[0, 1] = p2.X - p4.X;
            xform[0, 2] = p3.X - p4.X;

            xform[1, 0] = p1.Y - p4.Y;
            xform[1, 1] = p2.Y - p4.Y;
            xform[1, 2] = p3.Y - p4.Y;

            xform[2, 0] = p1.Z - p4.Z;
            xform[2, 1] = p2.Z - p4.Z;
            xform[2, 2] = p3.Z - p4.Z;


            xform.TryGetInverse(out Transform inverse);

            var output = new Point3d[pt.Count];

            int i = 0;
            foreach (Point3d p in pt)
            {
                output[i] = inverse * new Point3d(p - p4);
                ++i;
            }

            return output;
        }


    }

    public static class Interpolation
    {
        /// <summary>
        /// from http://paulbourke.net/miscellaneous/interpolation/
        /// Tension: 1 is high, 0 normal, -1 is low
        /// Bias: 0 is even,
        /// positive is towards first segment,
        /// negative towards the other
        /// </summary>
        /// <param name="y0"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="y3"></param>
        /// <param name="mu"></param>
        /// <param name="tension"></param>
        /// <param name="bias"></param>
        /// <returns></returns>
        public static double HermiteInterpolate(double y0, double y1, double y2, double y3, double mu, double tension = 0.0, double bias = 0.0)
        {
            double m0, m1, mu2, mu3;
            double a0, a1, a2, a3;

            mu2 = mu * mu;
            mu3 = mu2 * mu;
            m0 = (y1 - y0) * (1 + bias) * (1 - tension) / 2;
            m0 += (y2 - y1) * (1 - bias) * (1 - tension) / 2;
            m1 = (y2 - y1) * (1 + bias) * (1 - tension) / 2;
            m1 += (y3 - y2) * (1 - bias) * (1 - tension) / 2;
            a0 = 2 * mu3 - 3 * mu2 + 1;
            a1 = mu3 - 2 * mu2 + mu;
            a2 = mu3 - mu2;
            a3 = -2 * mu3 + 3 * mu2;

            return (a0 * y1 + a1 * m0 + a2 * m1 + a3 * y2);
        }

        /// <summary>
        /// from http://paulbourke.net/miscellaneous/interpolation/
        /// </summary>
        /// <param name="y0"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="y3"></param>
        /// <param name="mu"></param>
        /// <returns></returns>
        public static double CubicInterpolate(double y0, double y1, double y2, double y3, double mu)
        {
            double a0, a1, a2, a3, mu2;

            mu2 = mu * mu;
            a0 = y3 - y2 - y0 + y1;
            a1 = y0 - y1 - a0;
            a2 = y2 - y0;
            a3 = y1;

            return (a0 * mu * mu2 + a1 * mu2 + a2 * mu + a3);
        }

        /// <summary>
        /// from http://paulbourke.net/miscellaneous/interpolation/
        /// </summary>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="mu"></param>
        /// <returns></returns>
        public static double Lerp(double y1, double y2, double mu) =>
            y1 + (y2 - y1) * mu;
        //return y1 * (1 - mu) + y2 * mu;

        public static int Lerp(int y1, int y2, double mu) =>
            (int)(y1 + (y2 - y1) * mu);
        /*
        /// <summary>
        /// Simple lerp between two colors.
        /// </summary>
        /// <param name="c1">Color A.</param>
        /// <param name="c2">Color B.</param>
        /// <param name="t">t-value.</param>
        /// <returns>Interpolated color.</returns>
        public static Color Lerp(Color c1, Color c2, double t)
        {
            return Color.FromArgb(
                Lerp(c1.R, c2.R, t),
                Lerp(c1.G, c2.G, t),
                Lerp(c1.B, c2.B, t)
                );
        }
        */
        /// <summary>
        /// Simple linear interpolation between two points.
        /// </summary>
        /// <param name="pA"></param>
        /// <param name="pB"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Point3d Lerp(Point3d pA, Point3d pB, double t)
        {
            return pA + (pB - pA) * t;
        }

        /// <summary>
        /// Simple linear interpolation between two vectors.
        /// </summary>
        /// <param name="vA"></param>
        /// <param name="vB"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Vector3d Lerp(Vector3d vA, Vector3d vB, double t) =>
            vA + t * (vB - vA);

        public static double Unlerp(double a, double b, double c)
        {
            if (a > b)
                return 1.0 - (c - b) / (a - b);
            return (c - a) / (b - a);
        }

        /// <summary>
        /// from http://paulbourke.net/miscellaneous/interpolation/
        /// </summary>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="mu"></param>
        /// <returns></returns>
        public static double CosineInterpolate(double y1, double y2, double mu) =>
            Lerp(y1, y2, (1 - Math.Cos(mu * Math.PI)) / 2);


        /// <summary>
        /// Spherical interpolation using quaternions.
        /// </summary>
        /// <param name="qA">Quaternion A.</param>
        /// <param name="qB">Quaternion B.</param>
        /// <param name="t">t-value.</param>
        /// <returns></returns>
        public static Quaternion Slerp(Quaternion qA, Quaternion qB, double t)
        {
            if (t == 0) return qA;
            if (t == 1.0) return qB;

            Quaternion qC = new Quaternion();
            double cosHT = qA.A * qB.A + qA.B * qB.B + qA.C * qB.C + qA.D * qB.D;

            if (cosHT < 0.0)
            {
                qC.A = -qB.A;
                qC.B = -qB.B;
                qC.C = -qB.C;
                qC.D = -qB.D;
                cosHT = -cosHT;
            }
            else
                qC = qB;

            if (cosHT >= 1.0)
            {
                qC.A = qA.A;
                qC.B = qA.B;
                qC.C = qA.C;
                qC.D = qA.D;
                return qC;
            }
            double HT = Math.Acos(cosHT);
            double sinHT = Math.Sqrt(1.0 - cosHT * cosHT);

            if (Math.Abs(sinHT) < 0.001)
            {
                qC.A = 0.5 * (qA.A + qC.A);
                qC.B = 0.5 * (qA.B + qC.B);
                qC.C = 0.5 * (qA.C + qC.C);
                qC.D = 0.5 * (qA.D + qC.D);
                return qC;
            }

            double ratioA = Math.Sin((1 - t) * HT) / sinHT;
            double ratioB = Math.Sin(t * HT) / sinHT;

            qC.A = qA.A * ratioA + qC.A * ratioB;
            qC.B = qA.B * ratioA + qC.B * ratioB;
            qC.C = qA.C * ratioA + qC.C * ratioB;
            qC.D = qA.D * ratioA + qC.D * ratioB;
            return qC;
        }

        public static Vector3d Slerp(Vector3d v1, Vector3d v2, double t)
        {
            double dot = v1 * v2;
            if (dot >= 1.0) return v1;

            double theta = Math.Acos(dot) * t;
            Vector3d rel = v2 - v1 * dot;
            rel.Unitize();

            return ((v1 * Math.Cos(theta)) + rel * Math.Sin(theta));
        }

        /// <summary>
        /// Simple plane interpolation using interpolated vectors. Not ideal. 
        /// Fails spectacularly in extreme cases.
        /// </summary>
        /// <param name="A">Plane A.</param>
        /// <param name="B">Plane B.</param>
        /// <param name="t">t-value.</param>
        /// <returns></returns>
        public static Plane InterpolatePlanes(Plane A, Plane B, double t)
        {
            return new Plane(Lerp(A.Origin, B.Origin, t),
                                     Lerp(A.XAxis, B.XAxis, t),
                                     Lerp(A.YAxis, B.YAxis, t));
        }

        /// <summary>
        /// Better plane interpolation using quaternions.
        /// </summary>
        /// <param name="A">Plane A.</param>
        /// <param name="B">Plane B.</param>
        /// <param name="t">t-value.</param>
        /// <returns></returns>
        public static Plane InterpolatePlanes2(Plane A, Plane B, double t)
        {
            Quaternion qA = Quaternion.Rotation(Plane.WorldXY, A);
            Quaternion qB = Quaternion.Rotation(Plane.WorldXY, B);

            Quaternion qC = Slerp(qA, qB, t);
            Point3d p = Lerp(A.Origin, B.Origin, t);

            qC.GetRotation(out Plane plane);
            plane.Origin = p;

            return plane;
        }

        public static Plane CalculateFrenetFrame(Curve c, double t)
        {
            Vector3d z = c.TangentAt(t);
            Vector3d y = c.CurvatureAt(t);
            return new Plane(c.PointAt(t), Vector3d.CrossProduct(z, y), z);
        }

        /// <summary>
        /// Calculate RMF for curve. Adapted from https://math.stackexchange.com/a/2847887 and based on
        /// the paper https://dl.acm.org/doi/10.1145/1330511.1330513
        /// </summary>
        /// <param name="c"></param>
        /// <param name="steps"></param>
        /// <returns></returns>
        public static List<Plane> CalculateRMF(Curve c, int steps)
        {
            List<Plane> frames = new List<Plane>();
            double c1, c2, step = 1.0 / steps, t0, t1;
            Vector3d v1, v2, riL, tiL, riN, siN;
            Plane x0, x1;

            // n = YAxis
            // r = XAxis
            // t = ZAxis

            // Start off with the standard tangent/axis/normal frame
            // associated with the curve just prior the Bezier interval.
            t0 = -step;
            frames.Add(CalculateFrenetFrame(c, t0));

            // start constructing RM frames
            for (; t0 < 1.0; t0 += step)
            {
                // start with the previous, known frame
                x0 = frames[frames.Count - 1];

                // get the next frame: we're going to throw away its axis and normal
                t1 = t0 + step;
                x1 = CalculateFrenetFrame(c, t1);

                // First we reflect x0's tangent and axis onto x1, through
                // the plane of reflection at the point midway x0--x1
                v1 = x1.Origin - x0.Origin;
                c1 = v1 * v1;
                riL = x0.XAxis - v1 * (2 / c1 * (v1 * x0.XAxis));
                tiL = x0.ZAxis - v1 * (2 / c1 * (v1 * x0.ZAxis));

                // Then we reflection a second time, over a plane at x1
                // so that the frame tangent is aligned with the curve tangent:
                v2 = x1.ZAxis - tiL;
                c2 = v2 * v2;
                riN = riL - v2 * (2 / c2 * (v2 * riL));
                siN = Vector3d.CrossProduct(x1.ZAxis, riN);
                x1.YAxis = siN;
                x1.XAxis = riN;

                // we record that frame, and move on
                frames.Add(x1);
            }

            // and before we return, we throw away the very first frame,
            // because it lies outside the Bezier interval.
            frames.RemoveAt(0);

            return frames;
        }


    }

    public static class Utility
    {

        static readonly Random random = new Random();

        public static Mesh Create2dMeshGrid(double width, double length, double resolution = 50.0)
        {
            double w = width;
            double hw = width / 2;

            int Nx = (int)Math.Ceiling(width / resolution);
            int Nz = (int)Math.Ceiling(length / resolution);

            double stepX = width / Nx;
            double stepZ = length / Nz;

            Nx++; Nz++;

            Mesh lmesh = new Mesh();

            // Make mesh data for body
            for (int i = 0; i < Nz; ++i)
            {
                double posX = i * stepZ;
                for (int k = 0; k < Nx; ++k)
                    lmesh.Vertices.Add(-hw + k * stepX, 0, posX);
            }

            for (int i = 0; i < Nz - 1; ++i)
            {
                for (int j = 0; j < Nx - 1; ++j)
                {
                    lmesh.Faces.AddFace(
                      (i + 1) * Nx + j,
                      (i + 1) * Nx + j + 1,
                      i * Nx + j + 1,
                      i * Nx + j
                      );
                }
            }

            return lmesh;
        }
        public static Mesh Create3dMeshGrid(double width, double thickness, double length, double resolution = 50.0)
        {
            double w = width;
            double h = thickness;

            double hw = width / 2;
            double hh = thickness / 2;

            int Nx = (int)Math.Ceiling(width / resolution);
            int Ny = (int)Math.Ceiling(thickness / resolution);
            int Nz = (int)Math.Ceiling(length / resolution);

            double stepX = width / Nx;
            double stepY = thickness / Ny;
            double stepZ = length / Nz;

            int Nloop = Nx * 2 + Ny * 2; // Num verts in a loop

            Nx++; Ny++; Nz++;

           
            Mesh lmesh = new Mesh();

            // Make mesh data for body
            for (int i = 0; i < Nz; ++i)
            {
                double posX = i * stepZ;

                for (int j = 0; j < Ny; ++j)
                    lmesh.Vertices.Add(-hw, -hh + j * stepY, posX);

                for (int k = 1; k < Nx; ++k)
                    lmesh.Vertices.Add(-hw + k * stepX, -hh + h, posX);

                for (int j = Ny - 2; j >= 0; --j)
                    lmesh.Vertices.Add(-hw + w, -hh + j * stepY, posX);

                for (int k = Nx - 2; k > 0; --k)
                    lmesh.Vertices.Add(-hw + k * stepX, -hh, posX);
            }

            for (int i = 0; i < Nz - 1; ++i)
            {
                for (int j = 0; j < Nloop - 1; ++j)
                {
                    lmesh.Faces.AddFace(
                      (i + 1) * Nloop + j,
                      (i + 1) * Nloop + j + 1,
                      i * Nloop + j + 1,
                      i * Nloop + j
                      );
                }
                lmesh.Faces.AddFace(
                  (i + 1) * Nloop + Nloop - 1,
                  (i + 1) * Nloop,
                  i * Nloop,
                  i * Nloop + Nloop - 1
                  );
            }

            // Make mesh data for end faces

            int c = lmesh.Vertices.Count;

            for (int i = 0; i < Ny; ++i)
                for (int j = 0; j < Nx; ++j)
                {
                    lmesh.Vertices.Add(-hw + stepX * j, -hh + stepY * i, 0);
                }

            for (int i = 0; i < Ny - 1; ++i)
                for (int j = 0; j < Nx - 1; ++j)
                {
                    lmesh.Faces.AddFace(
                      c + Nx * i + j,
                      c + Nx * (i + 1) + j,
                      c + Nx * (i + 1) + j + 1,
                      c + Nx * i + j + 1
                      );
                }

            c = lmesh.Vertices.Count;

            for (int i = 0; i < Ny; ++i)
                for (int j = 0; j < Nx; ++j)
                {
                    lmesh.Vertices.Add(-hw + stepX * j, -hh + stepY * i, length);
                }

            for (int i = 0; i < Ny - 1; ++i)
                for (int j = 0; j < Nx - 1; ++j)
                {
                    lmesh.Faces.AddFace(
                      c + Nx * i + j + 1,
                      c + Nx * (i + 1) + j + 1,
                      c + Nx * (i + 1) + j,
                      c + Nx * i + j
                      );
                }

            return lmesh;
        }

        public static Plane PlaneFromNormalAndYAxis(Point3d origin, Vector3d normal, Vector3d yaxis)
        {
            return new Plane(origin, Vector3d.CrossProduct(yaxis, normal), yaxis);
        }

        /// <summary>
        /// Create frames that are aligned with a Brep. The input curve does not
        /// necessarily have to lie on the Brep.
        /// </summary>
        /// <param name="curve">Input centreline of the glulam.</param>
        /// <param name="brep">Brep to align the glulam orientation to.</param>
        /// <param name="num_samples">Number of orientation frames to use for alignment.</param>
        /// <returns>New Glulam oriented to the brep.</returns>
        public static Plane[] FramesNormalToSurface(Curve curve, Brep brep, int num_samples = 20)
        {
            num_samples = Math.Max(num_samples, 2);
            double[] t = curve.DivideByCount(num_samples - 1, true);
            Plane[] planes = new Plane[num_samples];
            Vector3d xaxis, yaxis, zaxis;
            Point3d pt;
            ComponentIndex ci;

            for (int i = 0; i < t.Length; ++i)
            {
                brep.ClosestPoint(curve.PointAt(t[i]), out pt, out ci, out double u, out double v, 0, out yaxis);

                // From: https://discourse.mcneel.com/t/brep-closestpoint-normal-is-not-normal/15147/8
                // If the closest point is found on an edge, average the face normals
                if (ci.ComponentIndexType == ComponentIndexType.BrepEdge)
                {
                    BrepEdge edge = brep.Edges[ci.Index];
                    int[] faces = edge.AdjacentFaces();
                    yaxis = Vector3d.Zero;
                    for (int j = 0; j < faces.Length; ++j)
                    {
                        BrepFace bf = edge.Brep.Faces[j];
                        if (bf.ClosestPoint(pt, out u, out v))
                        {
                            Vector3d faceNormal = bf.NormalAt(u, v);
                            yaxis += faceNormal;
                        }
                    }
                    yaxis.Unitize();
                }

                zaxis = curve.TangentAt(t[i]);
                xaxis = Vector3d.CrossProduct(zaxis, yaxis);
                planes[i] = new Plane(pt, xaxis, yaxis);
            }

            return planes;
        }

        public static double ApproximateAtan2(double y, double x)
        {
            int o = 0;
            if (y < 0) { x = -x; y = -y; o |= 4; }
            if (x <= 0) { double t = x; x = y; y = -t; o |= 2; }
            if (x <= y) { double t = y - x; x += y; y = t; o |= 1; }
            return o + y / x;
        }

        /// <summary>
        /// Sorts vectors around a point and vector.
        /// </summary>
        /// <param name="V">Input vectors.</param>
        /// <param name="p">Point to sort around.</param>
        /// <param name="normal">Normal vector to sort around.</param>
        /// <param name="sorted_indices">(out) List of vector indices in order.</param>
        /// <returns>Sorted angles of vectors.</returns>
        public static List<double> SortVectorsAroundPoint(List<Vector3d> V, Point3d p, Vector3d normal, out List<int> sorted_indices)
        {
            Plane plane = new Plane(p, normal);

            List<Tuple<double, int>> angles = new List<Tuple<double, int>>();

            sorted_indices = new List<int>();
            List<double> angleValues = new List<double>();

            for (int i = 0; i < V.Count; ++i)
            {
                double dx = Vector3d.Multiply(V[i], plane.XAxis);
                double dy = Vector3d.Multiply(V[i], plane.YAxis);

                angles.Add(new Tuple<double, int>(Math.Atan2(dy, dx), i));
            }

            angles.Sort();

            foreach (Tuple<double, int> t in angles)
            {
                sorted_indices.Add(t.Item2);
                angleValues.Add(t.Item1);
            }

            return angleValues;
        }

        public static Interval OverlapCurves(Curve c0, Curve c1)
        {
            Point3d min, max;

            if (c0.PointAtStart.DistanceTo(c1.PointAtStart) < c0.PointAtStart.DistanceTo(c1.PointAtEnd))
            {
                min = c1.PointAtStart;
                max = c1.PointAtEnd;
            }
            else
            {
                min = c1.PointAtEnd;
                max = c1.PointAtStart;
            }

            double t0, t1;
            c0.ClosestPoint(min, out t0);
            c0.ClosestPoint(max, out t1);

            return new Interval(Math.Max(t0, c0.Domain.Min), Math.Min(t1, c0.Domain.Max));
        }

        /// <summary>
        /// Given 3 points, calculate the maximum fillet radius possible between them.
        /// </summary>
        /// <param name="p0">Point 1</param>
        /// <param name="p1">Point 2 (vertex)</param>
        /// <param name="p2">Point 3</param>
        /// <param name="radius">Output radius</param>
        /// <param name="offset">Length offset to factor into calculation.</param>
        /// <returns></returns>
        public static bool MaxFilletRadius(Point3d p0, Point3d p1, Point3d p2, out double radius, double offset = 0)
        {
            var angle = Vector3d.VectorAngle(p0 - p1, p2 - p1);

            var l0 = p0.DistanceTo(p1);
            var l1 = p2.DistanceTo(p1);
            var length = Math.Min(l0, l1);
            if (length < offset)
            {
                radius = 0;
                return false; // Length is too small for offset
            }

            length = length - offset;
            radius = Math.Tan(angle / 2) * length;
            return true;
        }

        /// <summary>
        /// Given 3 points and a radius, calculate the minimum distance that the 3rd point needs to be away from the 2nd
        /// point (the vertex) in order to successfully create a fillet of that radius.
        /// </summary>
        /// <param name="p0">Point 1</param>
        /// <param name="p1">Point 2 (vertex)</param>
        /// <param name="p2">Point 3</param>
        /// <param name="radius">Radius of fillet</param>
        /// <param name="length">Output minimum length</param>
        /// <param name="offset">Additional offset to add to length</param>
        /// <returns></returns>
        public static bool ArmLengthFromRadius(Point3d p0, Point3d p1, Point3d p2, double radius, out double length, double offset = 0)
        {
            var angle = Vector3d.VectorAngle(p0 - p1, p2 - p1);
            length = radius / Math.Tan(angle / 2) + offset;

            //length = Math.Max(length, p1.DistanceTo(p2));

            //var vec = p2 - p1; vec.Unitize();

            //p3 = p1 + vec * length;
            return true;
        }

        /// <summary>
        /// Align a plane such that its X-axis lies on the XY plane. Useful for 5-axis machining.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="v"></param>
        /// <param name="plane"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static bool AlignedPlane(Point3d origin, Vector3d v, out Plane plane, out double angle)
        {
            Vector3d xaxis, yaxis;
            // Handle case where the vector is pointing straight up or down
            double dot = Vector3d.ZAxis * v;
            if (dot >= 1)
                xaxis = Vector3d.XAxis;
            else if (dot <= -1)
                xaxis = -Vector3d.XAxis;
            else
                xaxis = Vector3d.CrossProduct(Vector3d.ZAxis, v);

            yaxis = Vector3d.CrossProduct(xaxis, v);

            plane = new Plane(origin, xaxis, yaxis);

            var sign = v * Vector3d.ZAxis > 0 ? 1 : -1;
            angle = Vector3d.VectorAngle(yaxis, -Vector3d.ZAxis) * sign;

            return true;
        }

        public static Plane AdjustPlaneToBrep(Brep brep, Plane plane, double tolerance = 0.01, double threshold = 0.9)
        {
            Vector3d normal = Vector3d.Unset;
            double best = 0;

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                var face = brep.Faces[i];
                if (!face.TryGetPlane(out Plane fplane, tolerance))
                {
                    continue;
                }

                var dot = fplane.ZAxis * plane.ZAxis;

                if (Math.Abs(dot) > threshold
                    && Math.Abs(dot) > best)
                {
                    best = Math.Abs(dot);
                    if (dot < 0)
                        normal = -fplane.ZAxis;
                    else
                        normal = fplane.ZAxis;
                }
            }
            
            if (normal == Vector3d.Unset)
            {
                return Plane.Unset;
            }

            var yaxis = Vector3d.CrossProduct(normal, plane.XAxis);
            var xaxis = Vector3d.CrossProduct(yaxis, normal);

            return new Plane(plane.Origin, xaxis, yaxis);
        }

        public static Vector3d ClosestAxis(Plane plane, Vector3d vector, Plane? guide = null)
        {
            var axes = new Vector3d[]
            {
                plane.XAxis,
                plane.YAxis,
                -plane.XAxis,
                -plane.YAxis
            };

            var (number, index) = axes.Select(x => vector * x).Select((n, i) => (n, i)).Max();
            
            if (guide!= null && guide.HasValue && guide.Value.IsValid)
                return new Vector3d[] { guide.Value.XAxis, guide.Value.YAxis, -guide.Value.XAxis, -guide.Value.YAxis }[index];
            return axes[index];
        }

        public static int ClosestDimension2D(Plane plane, Vector3d vector)
        {
            var projected = plane.Project(vector);
            return Math.Abs(projected * plane.XAxis) > Math.Abs(projected * plane.YAxis) ? 0 : 1;
            //return Math.Abs(plane.Project(vector) * plane.XAxis) > 0.5 ? 0 : 1;
        }

        /// <summary>
        /// Flip a plane around the bounding box of a geometry, effectively flipping the 
        /// object.
        /// </summary>
        /// <param name="geo">Geometry to flip.</param>
        /// <param name="plane">Baseplane of geometry.</param>
        /// <returns>New baseplane.</returns>
        public static Plane FlipBasePlane(GeometryBase geo, Plane plane)
        {
            geo.GetBoundingBox(plane, out Box box);
            return new Plane(box.GetCorners()[7], plane.XAxis, -plane.YAxis);
        }

        /// <summary>
        /// Find most reasonable baseplane for a Brep. This attempts to align the longest axis of the Brep
        /// with the X-axis, and the largest face with the Z-axis. Perfect for laying out flat pieces for
        /// machining. The baseplane will sit at the bottom left corner of the Brep's bounding box.
        /// </summary>
        /// <param name="brep">Input Brep.</param>
        /// <returns>A baseplane for the Brep.</returns>
        public static Plane FindBestBasePlane(Brep brep, Vector3d optx, double linearTolerance = 0.001, double dotTolerance = 0.1, bool biggestPlane = true)
        {
            Vector3d vec = Vector3d.XAxis;
            Vector3d xaxis = Vector3d.XAxis, zaxis = Vector3d.ZAxis;
            BoundingBox bb = BoundingBox.Empty;
            Plane plane = Plane.Unset;

            if (optx.IsValid)
                xaxis = optx;
            else
            {

                var edge_vectors = new List<Vector3d>();

                foreach (var edge in brep.Edges)
                {
                    if (edge.IsLinear(linearTolerance))
                    {
                        xaxis = edge.TangentAtStart;
                        xaxis *= edge.GetLength();

                        if (xaxis * vec < 0)
                            xaxis.Reverse();

                        vec += xaxis;
                        edge_vectors.Add(xaxis);
                    }
                }

                double[] distances = edge_vectors.Select(x => Math.Abs(x * vec)).ToArray();
                var max_distance = distances.Max();
                var min_index = Array.IndexOf(distances, max_distance);

                xaxis = edge_vectors[min_index];
            }

            zaxis = biggestPlane ? GetBestCrossVector2(brep, xaxis, dotTolerance) :  GetBestCrossVector(brep, xaxis, dotTolerance);

            plane = new Plane(Point3d.Origin, xaxis, Vector3d.CrossProduct(zaxis, xaxis));

            Box box;
            bb = brep.GetBoundingBox(plane, out box);

            plane = new Plane(box.GetCorners()[0], plane.XAxis, plane.YAxis);

            return plane;
        }

        /// <summary>
        /// Given a vector, calculates the best normal that makes the Brep lie flattest.
        /// </summary>
        /// <param name="brep">Input Brep.</param>
        /// <param name="fwd">Main direction.</param>
        /// <returns>A perpendicular direction that is normal to the largest, flattest face of the Brep.</returns>
        /// <exception cref="Exception"></exception>
        public static Vector3d GetBestCrossVector(Brep brep, Vector3d fwd, double tolerance = 0.1)
        {
            var candidates = new List<Tuple<double, double, BrepFace, Vector3d>>();

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                var face = brep.Faces[i];
                var zaxis = Vector3d.Unset;
                if (face.IsPlanar())
                {
                    Plane plane;
                    face.TryGetPlane(out plane);
                    zaxis = plane.ZAxis;
                }
                else
                {
                    var midU = face.Domain(0).Mid;
                    var midV = face.Domain(1).Mid;

                    zaxis = face.NormalAt(midU, midV);
                }

                var amp = AreaMassProperties.Compute(face);
                if (amp == null || amp.Area <= 0)
                {
                    continue;
                }
                //throw new Exception("Bad face area.");


                var dot = Math.Abs(zaxis * fwd);
                if (dot < tolerance)
                {
                    candidates.Add(new Tuple<double, double, BrepFace, Vector3d>(1 / Math.Abs(amp.Area), dot, face, zaxis));
                }
            }

            if (candidates.Count < 1)
            {
                return Math.Abs(fwd * Vector3d.ZAxis) == 1.0 ? Vector3d.YAxis : Vector3d.ZAxis;
            }

            candidates = candidates.OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();

            return candidates[0].Item4;
        }

        /// <summary>
        /// Given a vector, calculates the best normal that makes the Brep lie flattest.
        /// </summary>
        /// <param name="brep">Input Brep.</param>
        /// <param name="fwd">Main direction.</param>
        /// <returns>A perpendicular direction that is normal to the largest, flattest face of the Brep.</returns>
        /// <exception cref="Exception"></exception>
        public static Vector3d GetBestCrossVector2(Brep brep, Vector3d fwd, double tolerance = 0.1)
        {
            var planes = new Dictionary<int[], double>(new IntArrayComparer());
            var planesActual = new Dictionary<int[], Plane>(new IntArrayComparer());

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                var face = brep.Faces[i];
                Plane plane;

                if (face.IsPlanar())
                {
                    face.TryGetPlane(out plane);
                }
                else
                {
                    var midU = face.Domain(0).Mid;
                    var midV = face.Domain(1).Mid;

                    plane = new Plane(face.PointAt(midU, midV), face.NormalAt(midU, midV));
                }

                var dot = Math.Abs(plane.ZAxis * fwd);
                if (dot > tolerance)
                {
                    continue;
                }

                var n = plane.ZAxis;
                var origin = plane.Origin;

                double d = (-n.X * origin.X - n.Y * n.Y - n.Z * n.Z);

                var scalarPlane = new int[] { (int)(n.X * 1000), (int)(n.Y * 1000), (int)(n.Z * 1000), (int)(d * 1000) };

                var amp = AreaMassProperties.Compute(face);
                if (amp == null || amp.Area <= 0)
                {
                    continue;
                }
                // throw new Exception("Bad face area.");

                if (!planes.ContainsKey(scalarPlane))
                {
                    planes.Add(scalarPlane, 0);
                    planesActual.Add(scalarPlane, plane);
                }

                planes[scalarPlane] += amp.Area;

            }

            if (planes.Count < 1)
            {
                return Math.Abs(fwd * Vector3d.ZAxis) == 1.0 ? Vector3d.YAxis : Vector3d.ZAxis;
            }

            int[] biggest = new int[] { 0, 0, 0, 0 };
            double biggestArea = 0;

            foreach (var kvp in planes)
            {
                if (kvp.Value > biggestArea)
                {
                    biggest = kvp.Key;
                    biggestArea = kvp.Value;
                }
            }

            return planesActual[biggest].ZAxis;
        }

        public static double CrossSectionArea(IEnumerable<Point3d> polygon)
        {
            var X = polygon.Select(point => point.X).ToArray();
            var Y = polygon.Select(point => point.Y).ToArray();

            var N = X.Length;
            double A = 0;

            for (int i = 0; i < N - 1; ++i)
            {
                A += X[i] * Y[i + 1] - X[i + 1] * Y[i];
            }
            return A * 0.5;
        }

        public static Point3d CrossSectionCentroid(IEnumerable<Point3d> polygon)
        {
            var x = polygon.Select(point => point.X).ToArray();
            var y = polygon.Select(point => point.Y).ToArray();

            var N = y.Length;
            double A = 0;
            double cx = 0, cy = 0;

            for (int i = 0; i < N - 1; ++i)
            {
                A += x[i] * y[i + 1] - x[i + 1] * y[i];
                cx += (x[i] + x[i + 1]) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
                cy += (y[i] + y[i + 1]) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
            }

            A *= 0.5;
            cx /= 6 * A;
            cy /= 6 * A;

            return new Point3d(cx, cy, 0);
        }

        public static void CrossSectionInertia(IEnumerable<Point3d> polygon, out double Ixx, out double Iyy, out double Ixy)
        {
            var x = polygon.Select(point => point.X).ToArray();
            var y = polygon.Select(point => point.Y).ToArray();

            var N = y.Length;
            double A = 0;
            double cx = 0, cy = 0;

            for (int i = 0; i < N - 1; ++i)
            {
                A += x[i] * y[i + 1] - x[i + 1] * y[i];
                cx += (x[i] + x[i + 1]) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
                cy += (y[i] + y[i + 1]) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
            }

            A *= 0.5;
            cx /= 6 * A;
            cy /= 6 * A;

            double sxx = 0, syy = 0, sxy = 0;

            for (int i = 0; i < N - 1; ++i)
            {
                sxx += (Math.Pow(y[i], 2) + y[i] * y[i + 1] + Math.Pow(y[i + 1], 2)) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
                syy += (Math.Pow(x[i], 2) + x[i] * x[i + 1] + Math.Pow(x[i + 1], 2)) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
                sxy += (x[i] * y[i + 1] + 2 * x[i] * y[i] + 2 * x[i + 1] * y[i + 1] + x[i + 1] * y[i]) * (x[i] * y[i + 1] - x[i + 1] * y[i]);
            }

            Ixx = sxx / 12 - A * Math.Pow(cy, 2);
            Iyy = syy / 12 - A * Math.Pow(cx, 2);
            Ixy = sxy / 24 - A * cx * cy;
        }

        public static void PrincipalDirections(double Ixx, double Iyy, double Ixy, out double I1, out double I2, out double theta)
        {
            double avg = (Ixx + Iyy) / 2;
            double diff = (Ixx - Iyy) / 2;
            double ddii = Math.Sqrt(diff * diff + Ixy * Ixy);
            I1 = avg + ddii;
            I2 = avg - ddii;
            theta = Math.Atan2(-Ixy, diff) / 2;
        }

        /// <summary>
        /// Freeform Press
        /// Calculate how much the curve should be extended to fully cover a collection
        /// of geometry. Iteratively projects geometry vertices onto extensions of the curve
        /// until none lie past its ends.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="breps"></param>
        /// <param name="nIterations"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static Curve ExtendCentreline(Curve curve, List<Brep> breps, int nIterations = 10, double threshold = 0.001)
        {
            var temp = curve.DuplicateCurve();

            double extension = 0;
            bool kill = false;
            for (int i = 0; i < nIterations; ++i)
            {
                extension = 0;

                foreach (var surface in breps)
                {
                    foreach (var vertex in surface.Vertices)
                    {
                        var vec = vertex.Location - temp.PointAtEnd;
                        extension = Math.Max(extension, vec * temp.TangentAtEnd);
                    }
                }
                //Console.WriteLine($"Extension: {extension}");

                temp = temp.Extend(CurveEnd.End, extension, CurveExtensionStyle.Smooth);

                if (extension < threshold)
                {
                    //Console.WriteLine($"Took {i} iterations to converge to threshold of {threshold}.");
                    //Console.WriteLine($"Final extension: {extension}");
                    kill = true;
                    break;
                }
            }

            return temp;
        }

        /// <summary>
        /// Freeform Press
        /// Calculate how much the curve should be offset in both directions to fully encompass
        /// a collection of geometry. The curve and geometry should be planar, ideally in the 
        /// world XY plane.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="breps"></param>
        /// <param name="widthLeft"></param>
        /// <param name="widthRight"></param>
        /// <returns></returns>
        public static double CalculateEncompassingOffset(Curve curve, List<Brep> breps, out double widthLeft, out double widthRight, Vector3d? zaxis = null)
        {
            if (zaxis == null)
                zaxis = Vector3d.ZAxis;

            widthLeft = 0;
            widthRight = 0;

            foreach (var surface in breps)
            {
                foreach (var vertex in surface.Vertices)
                {
                    curve.ClosestPoint(vertex.Location, out double t);
                    var cp = curve.PointAt(t);
                    var vec = vertex.Location - cp;

                    var binormal = Vector3d.CrossProduct(curve.TangentAt(t), zaxis.Value);
                    var dot = binormal * vec;
                    widthLeft = Math.Max(widthLeft, binormal * vec);
                    widthRight = Math.Min(widthRight, binormal * vec);
                }
            }

            return Math.Max(widthLeft, Math.Abs(widthRight));
        }

        /// <summary>
        /// Freeform Press
        /// From top, middle, and bottom surfaces of a beam, calculate the widest surface that
        /// encompasses the whole volume when it is unrolled.
        /// </summary>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="middle"></param>
        /// <param name="curve"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static Brep[] GetGlulamBlankSurfaces(Brep top, Brep bottom, Brep middle, Curve curve, double tolerance = 1e-4)
        {
            Curve[] unrolledCurves;
            Point3d[] unrolledPoints;
            TextDot[] unrolledDots;

            var unrollerMiddle = new Unroller(middle);
            unrollerMiddle.AddFollowingGeometry(curve);
            var unrolledMiddle = unrollerMiddle.PerformUnroll(out unrolledCurves, out unrolledPoints, out unrolledDots).FirstOrDefault();
            var unrolledCentreline = unrolledCurves[0];

            var unrollerTop = new Unroller(top);
            var unrolledTop = unrollerTop.PerformUnroll(out unrolledCurves, out unrolledPoints, out unrolledDots).FirstOrDefault();

            var unrollerBottom = new Unroller(bottom);
            var unrolledBottom = unrollerBottom.PerformUnroll(out unrolledCurves, out unrolledPoints, out unrolledDots).FirstOrDefault();

            var layers = new List<Brep> { unrolledTop, unrolledBottom };
            var extendedCentreline = ExtendCentreline(unrolledCentreline, layers);
            double offsetLeft, offsetRight;

            var width = CalculateEncompassingOffset(extendedCentreline, layers, out offsetLeft, out offsetRight);

            var edgeLeft = extendedCentreline.Offset(Plane.WorldXY, offsetLeft, tolerance, CurveOffsetCornerStyle.None).FirstOrDefault();
            var edgeRight = extendedCentreline.Offset(Plane.WorldXY, offsetRight, tolerance, CurveOffsetCornerStyle.None).FirstOrDefault();

            var loft = Brep.CreateFromLoft(new Curve[] { edgeLeft, edgeRight },
                Point3d.Unset, Point3d.Unset, LoftType.Normal, false).FirstOrDefault();

            if (loft == null)
            {
                throw new Exception($"Lofting extended edges failed.");
            }

            var morphedTop = loft.DuplicateBrep();
            var morphTop = new Rhino.Geometry.Morphs.SporphSpaceMorph(unrolledTop.Surfaces.First(), top.Surfaces.First(),
                new Point2d(0, 0), new Point2d(0, 0));

            var morphedBottom = loft.DuplicateBrep();
            var morphBottom = new Rhino.Geometry.Morphs.SporphSpaceMorph(unrolledBottom.Surfaces.First(), bottom.Surfaces.First(),
                new Point2d(0, 0), new Point2d(0, 0));

            morphTop.Morph(morphedTop);
            morphBottom.Morph(morphedBottom);

            return new Brep[] { morphedTop, morphedBottom };
        }

        /// <summary>
        /// Freeform Press
        /// Calculate true shape of top, middle, and bottom surfaces of a 
        /// beam, if fabricated using the 2-step method (in-plane bending, out-of-plane bending).
        /// </summary>
        /// <param name="beam"></param>
        /// <returns></returns>
        public static Brep[] GetTrueGlulamBlank(Beam beam, bool flipMiddle=false)
        {
            Brep top, bottom, middle;

            if (true)
            {
                top = BeamOps.GetFace(beam, Side.Top);
                bottom = BeamOps.GetFace(beam, Side.Bottom);
                middle = BeamOps.GetSideSurface(beam, 1, 0, beam.Width);
                if (flipMiddle)
                    middle.Flip();
            }
            else
            {
                top = BeamOps.GetFace(beam, Side.Left);
                bottom = BeamOps.GetFace(beam, Side.Right);
                middle = BeamOps.GetSideSurface(beam, 0, 0, beam.Height);
            }

            return GetGlulamBlankSurfaces(top, bottom, middle, beam.Centreline);
        }

    }

    public static class DocUtility
    {
        public static Plane GetElementPlane(RhinoObject rhinoObject, bool userDictionary = false)
        {
            string originString, xaxisString, yaxisString;

            if (userDictionary)
            {
                rhinoObject.UserDictionary.TryGetString("plane.Origin", out originString);
                rhinoObject.UserDictionary.TryGetString("plane.XAxis", out xaxisString);
                rhinoObject.UserDictionary.TryGetString("plane.YAxis", out yaxisString);
            }
            else
            {
                originString = rhinoObject.Attributes.GetUserString("plane.Origin");
                xaxisString = rhinoObject.Attributes.GetUserString("plane.XAxis");
                yaxisString = rhinoObject.Attributes.GetUserString("plane.YAxis");
            }

            if (string.IsNullOrEmpty(originString) || string.IsNullOrEmpty(xaxisString) || string.IsNullOrEmpty(yaxisString))
                return Plane.Unset;

            if (Point3d.TryParse(originString, out Point3d origin) && Point3d.TryParse(xaxisString, out Point3d xaxis) &&
                Point3d.TryParse(yaxisString, out Point3d yaxis))
                return new Plane(origin, new Vector3d(xaxis), new Vector3d(yaxis));

            return Plane.Unset;
        }

#if RHINO8
        public static int GetOrCreateLayer(RhinoDoc doc, string layerPath)
        {
            var layerIndex = doc.Layers.FindByFullPath(layerPath, -1);
            if (layerIndex < 0)
            {
                layerIndex = doc.Layers.AddPath(layerPath);
            }
            return layerIndex;
        }
#endif
    }

}
