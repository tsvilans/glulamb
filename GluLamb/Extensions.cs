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

using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Globalization;

namespace GluLamb
{
    public static class GluLambExtensionMethods
    {
        /// <summary>
        /// Modulus which works with negative numbers.
        /// </summary>
        /// <param name="x">Input value.</param>
        /// <param name="m">Domain value.</param>
        /// <returns></returns>
        public static int Modulus(this int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>
        /// Resize 2D array. From: https://stackoverflow.com/a/9059866
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original">Original array to resize.</param>
        /// <param name="x">New width.</param>
        /// <param name="y">New height.</param>
        /// <returns></returns>
        public static T[,] ResizeArray<T>(this T[,] original, int x, int y)
        {
            T[,] newArray = new T[x, y];
            int minX = Math.Min(original.GetLength(0), newArray.GetLength(0));
            int minY = Math.Min(original.GetLength(1), newArray.GetLength(1));

            for (int i = 0; i < minY; ++i)
                Array.Copy(original, i * original.GetLength(0), newArray, i * newArray.GetLength(0), minX);

            return newArray;
        }

        /// <summary>
        /// Calculate the normal deviation from the longitudinal direction of a glulam blank per vertex or mesh face. Can use acos to get the actual angle.
        /// </summary>
        /// <param name="mesh">Input mesh</param>
        /// <param name="curve">Curve to get tangent</param>
        /// <param name="faces">Calculate for faces instead of vertices</param>
        /// <returns>List of deviations between 0 and 1 for each mesh vertex or face.</returns>
        public static List<double> CalculateTangentDeviation(this Mesh mesh, Curve curve, bool faces = false)
        {
            List<double> deviations = new List<double>();

            if (faces)
            {
                mesh.FaceNormals.ComputeFaceNormals();

                Vector3d tangent = Vector3d.Unset;
                double t = 0.0;

                for (int i = 0; i < mesh.Faces.Count; ++i)
                {
                    curve.ClosestPoint(mesh.Faces.GetFaceCenter(i), out t);
                    tangent = curve.TangentAt(t);

                    deviations.Add(Math.Abs(tangent * mesh.FaceNormals[i]));
                }
            }
            else
            {
                mesh.Normals.ComputeNormals();

                Vector3d tangent = Vector3d.Unset;
                double t = 0.0;

                for (int i = 0; i < mesh.Vertices.Count; ++i)
                {
                    curve.ClosestPoint(mesh.Vertices[i], out t);
                    tangent = curve.TangentAt(t);

                    deviations.Add(Math.Abs(tangent * mesh.Normals[i]));
                }
            }

            return deviations;
        }

        public static List<Vector3d> CalculateTangentVector(this Mesh mesh, Curve curve, bool faces = false)
        {
            List<Vector3d> l_vectors = new List<Vector3d>();

            Point3d cp = Point3d.Unset;
            double t = 0.0;

            if (faces)
            {
                for (int i = 0; i < mesh.Faces.Count; ++i)
                {
                    curve.ClosestPoint(mesh.Faces.GetFaceCenter(i), out t);
                    l_vectors.Add(curve.TangentAt(t));
                }
            }
            else
            {
                for (int i = 0; i < mesh.Vertices.Count; ++i)
                {
                    curve.ClosestPoint(mesh.Vertices[i], out t);
                    l_vectors.Add(curve.TangentAt(t));
                }
            }

            return l_vectors;
        }

    }

    public static class PlaneExtensionMethods
    {
        /// <summary>
        /// Flip Plane around its X-axis (flip Y-axis).
        /// </summary>
        /// <param name="P"></param>
        public static Plane FlipAroundXAxis(this Plane P)
        {
            return new Plane(P.Origin, P.XAxis, -P.YAxis);
        }

        /// <summary>
        /// Flip Plane around its Y-axis (flip X-axis).
        /// </summary>
        /// <param name="P"></param>
        public static Plane FlipAroundYAxis(this Plane P)
        {
            return new Plane(P.Origin, -P.XAxis, P.YAxis);
        }

        public static Transform ProjectAlongVector(this Plane Pln, Vector3d V)
        {
            Transform oblique = new Transform(1);
            double[] eq = Pln.GetPlaneEquation();
            double a, b, c, d, dx, dy, dz, D;
            a = eq[0];
            b = eq[1];
            c = eq[2];
            d = eq[3];
            dx = V.X;
            dy = V.Y;
            dz = V.Z;
            D = a * dx + b * dy + c * dz;
            oblique.M00 = 1 - a * dx / D;
            oblique.M01 = -a * dy / D;
            oblique.M02 = -a * dz / D;
            oblique.M03 = 0;
            oblique.M10 = -b * dx / D;
            oblique.M11 = 1 - b * dy / D;
            oblique.M12 = -b * dz / D;
            oblique.M13 = 0;
            oblique.M20 = -c * dx / D;
            oblique.M21 = -c * dy / D;
            oblique.M22 = 1 - c * dz / D;
            oblique.M23 = 0;
            oblique.M30 = -d * dx / D;
            oblique.M31 = -d * dy / D;
            oblique.M32 = -d * dz / D;
            oblique.M33 = 1;
            oblique = oblique.Transpose();
            return oblique;
        }

        public static Vector3d Project(this Plane plane, Vector3d v) => new Vector3d(v - (plane.ZAxis * Vector3d.Multiply(plane.ZAxis, v)));

    }

    public static class PointExtensionMethods
    {
        /// <summary>
        /// Project point onto plane.
        /// </summary>
        /// <param name="p">Point to project.</param>
        /// <param name="pl">Plane to project onto.</param>
        /// <returns>Projected point.</returns>
        public static Point3d ProjectToPlane(this Point3d pt, Plane p)
        {
            Vector3d op = new Vector3d(pt - p.Origin);
            double dot = Vector3d.Multiply(p.ZAxis, op);
            Vector3d v = p.ZAxis * dot;
            return new Point3d(pt - v);
        }

        public static Polyline GetConvexHull(this List<Point3d> pts, Plane plane, bool transformed = true)
        {
            if (pts.Count < 1) return null;
            for (int i = 0; i < pts.Count; ++i)
            {
                plane.RemapToPlaneSpace(pts[i], out Point3d m_temp);
                pts[i] = m_temp;
            }
            pts = pts.OrderBy(x => x.X).ToList();

            Point3d poh = pts[0];
            Point3d ep = pts[0];
            List<Point3d> chpts = new List<Point3d>();
            int index = 0;

            do
            {
                chpts.Add(poh);
                ep = pts[index];
                for (int j = 0; j < pts.Count; ++j)
                {
                    if (ep == poh || (ep.X - chpts[index].X) * (pts[j].Y - chpts[index].Y) - (ep.Y - chpts[index].Y) * (pts[j].X - chpts[index].X) < 0)
                        ep = pts[j];
                }
                index++;
                poh = ep;
            }
            while (ep != pts[0] && index < pts.Count);

            Polyline poly = new Polyline(chpts);
            poly.Add(poly.First());

            if (transformed)
                poly.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));
            return poly;
        }
    }

    public static class CurveExtensionMethods
    {
        /// <summary>
        /// Gets a plane that is aligned to the curve start and end points, with the Z-axis point in the direction of most curvature.
        /// Kind of like a best-fit for a bounding box.
        /// </summary>
        /// <param name="c">Input curve.</param>
        /// <param name="Samples">Number of samples for calculating the Z-axis.</param>
        /// <returns>Decent-fit plane.</returns>
        public static Plane GetAlignedPlane(this Curve c, int Samples, out double Mag)
        {
            if (c.IsLinear())
            {
                Plane p;
                c.FrameAt(c.Domain.Min, out p);
                Mag = 0;
                return p;
            }

            Point3d start = c.PointAtStart;
            Point3d end = c.PointAtEnd;

            Vector3d YAxis = end - start;
            YAxis.Unitize();

            Plane SortingPlane = new Plane((end + start) / 2, YAxis);

            Point3d[] DivPts;
            c.DivideByCount(Samples, false, out DivPts);

            Vector3d ZAxis = new Vector3d();

            foreach (Point3d p in DivPts)
            {
                ZAxis += (p.ProjectToPlane(SortingPlane) - SortingPlane.Origin);
            }

            Mag = ZAxis.Length / Samples;
            YAxis.Unitize();

            Vector3d XAxis = Vector3d.CrossProduct(ZAxis, YAxis);

            return new Plane(start, XAxis, YAxis);
        }
    }

    public static class MeshExtensionMethods
    {
        public static Mesh FitToAxes(this Mesh m, Plane p, out Polyline convex_hull, ref Plane transform)
        {
            List<Point3d> pts = m.Vertices.Select(x => new Point3d(x)).ToList();

            convex_hull = pts.GetConvexHull(p, false);

            convex_hull.DeleteShortSegments(0.01);

            double angle = 0;
            int index = 0;
            double area = 0;
            double w, h;
            double temp = double.MaxValue;
            BoundingBox bb;

            for (int i = 0; i < convex_hull.SegmentCount; ++i)
            {
                Line seg = convex_hull.SegmentAt(i);
                Polyline ppoly = new Polyline(convex_hull);
                Vector3d dir = seg.Direction;
                dir.Unitize();
                //double temp_angle = Vector3d.VectorAngle(Vector3d.YAxis, dir);
                double temp_angle = Math.Atan2(dir.Y, dir.X);

                ppoly.Transform(Transform.Rotation(temp_angle, Vector3d.ZAxis, ppoly.CenterPoint()));
                bb = ppoly.BoundingBox;

                w = bb.Max.X - bb.Min.X;
                h = bb.Max.Y - bb.Min.Y;

                if (w < h) temp_angle += Math.PI / 2;
                area = w * h;

                if (Math.Abs(area - temp) < 0.01 && temp_angle < angle)
                {
                    temp = area;
                    index = i;
                    angle = temp_angle;
                }
                else if (area < temp)
                {
                    temp = area;
                    index = i;
                    angle = temp_angle;
                }
            }

            angle = -angle;

            convex_hull.Transform(Transform.Rotation(angle, Vector3d.ZAxis, convex_hull.CenterPoint()));
            Transform xform = Transform.Rotation(angle, p.ZAxis, p.Origin);
            m.Transform(xform);
            transform.Transform(xform);

            return m;
        }
    }

    public static class BrepExtensionMethods
    {
        public static Brep Cut(this Brep brep, IEnumerable<Brep> cutters, double tolerance = 0.01)
        {
            var solids = new List<Brep>();
            var surfs = new List<Brep>();
            if (brep == null) throw new Exception("Brep is null in CutBrep()");

            if (brep.SolidOrientation != BrepSolidOrientation.Outward)
                brep.Flip();

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                //brep.Surfaces[i].Reverse(0, true);

                //x.Faces.Flip(false);
                //if (brep.Faces[i].OrientationIsReversed)
                //    brep.Faces[i].Reverse(0, true);
            }

            foreach (Brep ctr in cutters)
            {
                if (ctr == null) continue;
                if (ctr.IsSolid)
                {
                    if (ctr.SolidOrientation != BrepSolidOrientation.Outward)
                        ctr.Flip();

                    solids.Add(ctr);
                }
                else
                {
                    surfs.Add(ctr);
                }
            }

            Brep[] pieces;
            if (surfs.Count > 0)
            {

                pieces = Brep.CreateBooleanSplit(new Brep[] { brep }, surfs, tolerance);
                if (pieces.Length < 1)
                    pieces = new Brep[] { brep };
            }
            else
            {
                pieces = new Brep[] { brep };
            }

            int index = -1;
            double volume = 0;

            for (int i = 0; i < pieces.Length; ++i)
            {
                if (pieces[i] == null || !pieces[i].IsSolid)
                    continue;

                var vmp = Rhino.Geometry.VolumeMassProperties.Compute(pieces[i], true, false, false, false);
                if (vmp == null) continue;

                if (Math.Abs(vmp.Volume) > volume)
                {
                    index = i;
                    volume = vmp.Volume;
                }
            }

            Brep largest;

            if (index >= 0)
                largest = pieces[index];
            else
                largest = brep;
                //largest = pieces[0];

            var diff = Brep.CreateBooleanDifference(new Brep[] { largest }, solids, 0.1);

            if (diff == null || diff.Length < 1)
                return largest;
            else
                return diff[0];

        }
    }
}
