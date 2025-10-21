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
using static Rhino.Render.TextureGraphInfo;

namespace GluLamb
{
    [Serializable]
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

        public virtual int Samples
        {
            get; set;
        }

        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public Curve Centreline { get; set; }
        public CrossSectionOrientation Orientation { get; set; }

        public virtual Beam Duplicate()
        {
            return new Beam()
            {
                Centreline = Centreline.DuplicateCurve(),
                Orientation = Orientation.Duplicate(),
                Width = Width,
                Height = Height,
                OffsetX = OffsetX,
                OffsetY = OffsetY
            };
        }

        public static Beam StraightFromGeometry(Brep brep, Vector3d? xaxis = null)
        {
            var bplane = GluLamb.Utility.FindBestBasePlane(brep, xaxis.HasValue ? xaxis.Value : Vector3d.Unset);

            var bb = brep.GetBoundingBox(bplane, out Box worldBox);

            var cy = (bb.Min.Y + bb.Max.Y) / 2;
            var cz = (bb.Min.Z + bb.Max.Z) / 2;

            var start = bplane.PointAt(bb.Min.X, cy, cz);
            var end = bplane.PointAt(bb.Max.X, cy, cz);

            var centreline = new Line(start, end).ToNurbsCurve();

            var beam = new Beam()
            {
                Centreline = centreline,
                Width = bb.Max.Y - bb.Min.Y,
                Height = bb.Max.Z - bb.Min.Z,
                Orientation = new VectorOrientation(bplane.ZAxis)
            };

            return beam;
        }

        public Plane GetPlane(double t) => GetPlane(t, Centreline);

        public Plane GetPlane(double t, Curve curve) => Utility.PlaneFromNormalAndYAxis(
                                                        curve.PointAt(t),
                                                        curve.TangentAt(t),
                                                        Orientation.GetOrientation(curve, t));

        public Plane[] GetPlanes(IList<double> tt) => GetPlanes(tt, Centreline);

        public Plane[] GetPlanes(IList<double> tt, Curve curve)
        {
            var orientations = Orientation.GetOrientations(curve, tt);
            var planes = new Plane[tt.Count];

            Parallel.For(0, tt.Count, i =>
            {
                planes[i] = Utility.PlaneFromNormalAndYAxis(
                    curve.PointAt(tt[i]),
                    curve.TangentAt(tt[i]),
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

            if (Centreline.Domain.IncludesParameter(t))
                m_temp.Z = Centreline.GetLength(
                    new Interval(Centreline.Domain.Min, t));
 
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



            if (approximate)
            {
                double mu;

                var tt = Centreline.DivideByCount(num_samples, true);
                var lengths = tt.Select(x => Centreline.GetLength(new Interval(Centreline.Domain.Min, x))).ToArray();

                //for (int i = 0; i < pts.Count; ++i)
                Parallel.For(0, pts.Count, i =>

                {
                    Centreline.ClosestPoint(pts[i], out double t);
                    Plane m_plane = GetPlane(t);
                    m_plane.RemapToPlaneSpace(pts[i], out Point3d m_temp);

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
                    Centreline.ClosestPoint(pts[i], out double t);
                    Plane m_plane = GetPlane(t);
                    m_plane.RemapToPlaneSpace(pts[i], out Point3d m_temp);
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
        public Point3d FromBeamSpace(Point3d point, bool extend = true)
        {
            var curve = Centreline.DuplicateCurve();
            double length = curve.GetLength();

            if (extend)
            {
                double maxZ = 0;
                maxZ = Math.Max(maxZ, point.Z);

                if (maxZ > length)
                    curve = curve.Extend(CurveEnd.End, maxZ - length + 1, CurveExtensionStyle.Line);
            }


            if (curve.IsLinear())
            {
                curve.Domain = new Interval(0, length);

                    Plane m_plane;
                    m_plane = GetPlane(point.Z, curve);
                    return m_plane.PointAt(point.X, point.Y);
            }
            else
            {
                    Plane m_plane;
                    curve.LengthParameter(point.Z, out double t);
                    m_plane = GetPlane(t, curve);
                    return m_plane.PointAt(point.X, point.Y);
            }
        }

        /// <summary>
        /// Map points from beam space to world space.
        /// </summary>
        /// <param name="pts"></param>
        /// <returns></returns>
        public Plane FromBeamSpace(Plane plane, bool extend = true)
        {
            var curve = Centreline.DuplicateCurve();
            double length = curve.GetLength();

            if (extend)
            {
                double maxZ = 0;
                maxZ = Math.Max(maxZ, plane.Origin.Z);

                if (maxZ > length)
                    curve = curve.Extend(CurveEnd.End, maxZ - length + 1, CurveExtensionStyle.Line);
            }

            Plane m_plane;
            //Point3d origin;

            if (curve.IsLinear())
            {
                curve.Domain = new Interval(0, length);

                m_plane = GetPlane(plane.Origin.Z, curve);
                //origin = m_plane.PointAt(plane.Origin.X, plane.Origin.Y);
            }
            else
            {
                curve.LengthParameter(plane.Origin.Z, out double t);
                m_plane = GetPlane(t, curve);
                //origin = m_plane.PointAt(plane.Origin.X, plane.Origin.Y);
            }

            plane.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, m_plane));

            return plane;
        }

        /// <summary>
        /// Map points from beam space to world space.
        /// </summary>
        /// <param name="pts"></param>
        /// <returns></returns>
        public Plane[] FromBeamSpace(IList<Plane> planes, bool extend = true)
        {
            var curve = Centreline.DuplicateCurve();
            double length = curve.GetLength();
            var new_planes = new Plane[planes.Count];

            if (extend)
            {
                double maxZ = 0;
                foreach (var plane in planes)
                    maxZ = Math.Max(maxZ, plane.Origin.Z);

                if (maxZ > length)
                    curve = curve.Extend(CurveEnd.End, maxZ - length + 1, CurveExtensionStyle.Line);
            }

            Plane m_plane;
            //Point3d origin;

            if (curve.IsLinear())
            {
                for (int i = 0; i < planes.Count; ++i)
                {
                    curve.Domain = new Interval(0, length);

                    m_plane = GetPlane(planes[i].Origin.Z, curve);
                    //origin = m_plane.PointAt(plane.Origin.X, plane.Origin.Y);
                    planes[i].Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, m_plane));

                    new_planes[i] = planes[i];
                }
            }
            else
            {
                for (int i = 0; i < planes.Count; ++i)
                {
                    curve.LengthParameter(planes[i].Origin.Z, out double t);
                     m_plane = GetPlane(t, curve);
                    //origin = m_plane.PointAt(plane.Origin.X, plane.Origin.Y);
                    planes[i].Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, m_plane));

                    new_planes[i] = planes[i];
                }
            }

            return new_planes;
        }

        /// <summary>
        /// Map points from beam space to world space.
        /// </summary>
        /// <param name="pts"></param>
        /// <returns></returns>
        public Point3d[] FromBeamSpace(IList<Point3d> pts, bool extend=true)
        {
            Point3d[] m_output_pts = new Point3d[pts.Count];

            var curve = Centreline.DuplicateCurve();
            double length = curve.GetLength();

            if (extend)
            {
                double maxZ = 0;
                foreach(Point3d pt in pts)
                    maxZ = Math.Max(maxZ, pt.Z);

                if (maxZ > length)
                    curve = curve.Extend(CurveEnd.End, maxZ - length+1, CurveExtensionStyle.Line);
            }
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

            if (curve.IsLinear())
            {
                curve.Domain = new Interval(0, length);

                Parallel.For(0, pts.Count, i =>
                {
                    Plane m_plane;

                    //m_plane = Utility.PlaneFromNormalAndYAxis(
                    //Centreline.PointAtStart + Centreline.TangentAtStart * (pts[i].Z),
                    //Centreline.TangentAtStart,
                    //Orientation.GetOrientation(Centreline, pts[i].Z));
                    
                    m_plane = GetPlane(pts[i].Z, curve);

                    m_output_pts[i] = m_plane.PointAt(pts[i].X, pts[i].Y);
                });

            }

            else
            {
                Parallel.For(0, pts.Count, i =>
                {
                    //for (int i = 0; i < pts.Count; ++i)
                    //{
                    Plane m_plane;

                    curve.LengthParameter(pts[i].Z, out double t);
                    m_plane = GetPlane(t, curve);

                    m_output_pts[i] = m_plane.PointAt(pts[i].X, pts[i].Y);
                //}
                });
            }

            return m_output_pts;
        }

        /// <summary>
        /// Map mesh from beam space to world space.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public Mesh FromBeamSpace(Mesh mesh, bool extend = true)
        {
            Mesh m_mesh = mesh.DuplicateMesh();
            m_mesh.Vertices.Clear();
            m_mesh.Vertices.AddVertices(FromBeamSpace(mesh.Vertices.ToPoint3dArray(), extend));

            return m_mesh;
        }

        public virtual Brep ToBrep(double offset = 0.0)
        {
            if (Centreline.IsLinear() && Orientation.IsConstant())
            {
                var cplane = GetPlane(Centreline.Domain.Min);
                var rec = new Rectangle3d(cplane, new Interval(-Width * 0.5 + OffsetX, Width * 0.5 + OffsetX),
                  new Interval(-Height * 0.5 + OffsetY, Height * 0.5 + OffsetY)).ToNurbsCurve();

                return Extrusion.Create(rec, Centreline.GetLength(), true).ToBrep();
            }

            double[] tt;
            if (Centreline.IsLinear())
                tt = new double[] { Centreline.Domain.Min, Centreline.Domain.Max };
            else
                tt = Centreline.DivideByCount((int)Math.Ceiling(Centreline.GetLength() / 50.0), true);

            //var planes = GetPlanes(tt);
            int N = Math.Max(Samples, 6);

            BeamOps.GenerateCrossSectionPlanes(this, N, out Plane[] planes, out tt);

            var xsections = new List<Curve>();

            foreach (Plane plane in planes)
            {
                var rec = new Rectangle3d(plane, new Interval(-Width * 0.5 + OffsetX, Width * 0.5 + OffsetX),
                  new Interval(-Height * 0.5 + OffsetY, Height * 0.5 + OffsetY)).ToNurbsCurve();
                xsections.Add(rec);
            }

            var lofts = Brep.CreateFromLoft(xsections, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);

            if (lofts == null || lofts.Length < 1)
            {
                return null;
            }

            var loft = lofts[0];
            loft.Flip();

            var capped = loft.CapPlanarHoles(0.01);
            if (capped != null)
            {
                capped.Faces.SplitKinkyFaces(0.1);
                return capped;
            }

            return loft;
        }

        /// <summary>
        /// Evaluate torsion at curve parameter.
        /// </summary>
        /// <param name="t">Parameter to evaluate torsion at.</param>
        /// <param name="tolerance">Tolerance.</param>
        /// <returns>Torsion (radians per unit distance).</returns>
        public double EvaluateTorsion(double t, double tolerance = 0.001)
        {
            if (!Centreline.Domain.IncludesParameter(t)) return 0.0;

            double t0 = t - tolerance;
            double t1 = t + tolerance;

            if (!Centreline.Domain.IncludesParameter(t0)) return 0.0;
            if (!Centreline.Domain.IncludesParameter(t1)) return 0.0;

            var p0 = GetPlane(t0);
            var p1 = GetPlane(t1);

            var plane0 = new Plane(p0.Origin, p0.ZAxis, p0.YAxis);
            var plane1 = new Plane(p1.Origin, p1.ZAxis, p1.YAxis);

            double torsion = Math.Acos(Math.Min(1.0, Math.Max(-1.0, plane0.ZAxis * plane1.ZAxis))) / p0.Origin.DistanceTo(p1.Origin);

            return torsion;
        }

        public Brep ProfiledTopBottom(Brep topSrf, Brep btmSrf, out Curve[] edges, double sample_dist = 50.0, double maxSrfDistance = 500.0)
        {
            var length = Centreline.GetLength();
            var N = (int)Math.Ceiling(length / sample_dist);

            var tt = Centreline.DivideByCount(N, true);

            var planes = GetPlanes(tt.ToList());

            var xsections = new Curve[planes.Length];

            double hw = Width * 0.5;
            double hh = Height * 0.5;
            //double maxSrfDistance = 500.0;

            Brep[] topSrfs, btmSrfs;

            if (topSrf != null)
                topSrfs = new Brep[] { topSrf };
            if (btmSrf != null)
                btmSrfs = new Brep[] { btmSrf };

            /* Check surface direction */
            int sign = 1;
            var midN = N / 2;
            var midPlane = planes[midN];

            if (topSrf != null)
            {
                var tPt = topSrf.ClosestPoint(midPlane.Origin);
                if ((tPt - midPlane.Origin) * midPlane.YAxis < 0)
                    sign = -1;
            }
            else if (btmSrf != null)
            {
                var bPt = btmSrf.ClosestPoint(midPlane.Origin);
                if ((bPt - midPlane.Origin) * midPlane.YAxis > 0)
                    sign = -1;
            }
            //else
            //  throw new Exception("Neither surface is valid!");

            edges = new Curve[4];
            var verts = new List<Point3d>[4];
            for (int i = 0; i < 4; ++i)
                verts[i] = new List<Point3d>();


            for (int i = 0; i < planes.Length; ++i)
            {
                double top = topSrf != null ? maxSrfDistance * sign : hh * sign;
                double btm = btmSrf != null ? maxSrfDistance * -sign : hh * -sign;

                Line side0 = new Line(planes[i].PointAt(hw, btm), planes[i].PointAt(hw, top));
                Line side1 = new Line(planes[i].PointAt(-hw, btm), planes[i].PointAt(-hw, top));

                Curve[] overlapCrvs;
                Point3d[] xPts;
                Polyline xsection = new Polyline();

                if (topSrf != null)
                {
                    Rhino.Geometry.Intersect.Intersection.CurveBrep(side0.ToNurbsCurve(), topSrf, 0.01, out overlapCrvs, out xPts); xsection.Add(xPts[0]);
                    Rhino.Geometry.Intersect.Intersection.CurveBrep(side1.ToNurbsCurve(), topSrf, 0.01, out overlapCrvs, out xPts); xsection.Add(xPts[0]);
                }
                else
                {
                    xsection.Add(side0.To);
                    xsection.Add(side1.To);
                }
                if (btmSrf != null)
                {
                    Rhino.Geometry.Intersect.Intersection.CurveBrep(side1.ToNurbsCurve(), btmSrf, 0.01, out overlapCrvs, out xPts); xsection.Add(xPts[0]);
                    Rhino.Geometry.Intersect.Intersection.CurveBrep(side0.ToNurbsCurve(), btmSrf, 0.01, out overlapCrvs, out xPts); xsection.Add(xPts[0]);
                }
                else
                {
                    xsection.Add(side1.From);
                    xsection.Add(side0.From);
                }

                for (int j = 0; j < 4; ++j)
                {
                    verts[j].Add(xsection[j]);
                }

                xsection.Add(xsection[0]);

                xsections[i] = xsection.ToNurbsCurve();
            }

            for (int i = 0; i < 4; ++i)
            {
                edges[i] = Curve.CreateInterpolatedCurve(verts[i], 3);
            }

            Brep loft = Brep.CreateFromLoft(xsections, Point3d.Unset, Point3d.Unset, LoftType.Normal, false)[0];

            var capped = loft.CapPlanarHoles(0.01);
            if (capped != null)
            {
                capped.Faces.SplitKinkyFaces(0.1);
                return capped;
            }
            return loft;
        }

        public virtual Beam Trim(Interval domain, double overlap)
        {
            double l1 = Centreline.GetLength(new Interval(Centreline.Domain.Min, domain.Min));
            double l2 = Centreline.GetLength(new Interval(Centreline.Domain.Min, domain.Max));
            double t1, t2;

            if (!Centreline.LengthParameter(l1 - overlap, out t1)) t1 = domain.Min;
            if (!Centreline.LengthParameter(l2 + overlap, out t2)) t2 = domain.Max;

            domain = new Interval(
                Math.Max(t1, Centreline.Domain.Min),
                Math.Min(t2, Centreline.Domain.Max));

            double length = Centreline.GetLength(domain);

            if (domain.IsDecreasing || length < overlap || length < Glulam.OverlapTolerance)
                return null;

            double percentage = length / Centreline.GetLength();

            Curve trimmed_curve = Centreline.Trim(domain);

            CrossSectionOrientation trimmed_orientation = Orientation.Trim(domain);
            trimmed_orientation.Remap(Centreline, trimmed_curve);

            var beam = new Beam() { 
                Centreline = trimmed_curve, 
                Orientation = trimmed_orientation, 
                Width = Width, 
                Height = Height,
                OffsetX = OffsetX,
                OffsetY = OffsetY
            };

            return beam;
        }


    }
}
