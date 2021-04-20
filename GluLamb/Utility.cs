/*
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
    }

}
