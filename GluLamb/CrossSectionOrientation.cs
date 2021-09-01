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
using Rhino.Collections;

namespace GluLamb
{
    public abstract class CrossSectionOrientation
    {
        public abstract Vector3d GetOrientation(Curve crv, double t);

        public Vector3d GetOrientation(Curve crv, Point3d pt)
        {
            double t;
            crv.ClosestPoint(pt, out t);
            return GetOrientation(crv, t);
        }

        public abstract Vector3d[] GetOrientations(Curve crv, IList<double> t);

        public abstract void Remap(Curve old_curve, Curve new_curve);

        public virtual CrossSectionOrientation[] Split(IList<double> t)
        {
            CrossSectionOrientation[] new_orientations = new CrossSectionOrientation[t.Count + 1];
            for (int i = 0; i < new_orientations.Length; ++i)
            {
                new_orientations[i] = this.Duplicate();
            }

            return new_orientations;
        }

        public abstract CrossSectionOrientation Join(CrossSectionOrientation orientation);

        public virtual CrossSectionOrientation Duplicate()
        {
            throw new NotImplementedException();
        }

        public virtual CrossSectionOrientation Trim(Interval domain)
        {
            return this.Duplicate();
        }

        public abstract void Transform(Transform x);

        public abstract object GetDriver();

        protected Vector3d NormalizeVector(Curve crv, double t, Vector3d v)
        {
            v.Unitize();
            Vector3d tan = crv.TangentAt(t);
            Vector3d xprod = Vector3d.CrossProduct(tan, v);

            return Vector3d.CrossProduct(xprod, tan);
        }
    }

    /// <summary>
    /// An orientation consisting of a single direction vector. The 
    /// vector should not be parallel to the curve at any point.
    /// </summary>
    public class VectorOrientation : CrossSectionOrientation
    {
        private Vector3d m_vector;

        public VectorOrientation(Vector3d v)
        {
            m_vector = v;
        }

        public override string ToString()
        {
            return "VectorOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            return NormalizeVector(crv, t, m_vector);
        }

        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            return t.Select(x => GetOrientation(crv, x)).ToArray();
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            return;
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new VectorOrientation(m_vector);
        }

        public override object GetDriver()
        {
            return m_vector;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            throw new NotImplementedException();
        }

        public override void Transform(Transform x)
        {
            m_vector.Transform(x);
        }
    }

    /// <summary>
    /// Simple class to group together curve parameter, direction vector, and angular offset
    /// for the VectorListOrientation class.
    /// </summary>
    public struct VectorParameter : IComparable, IComparable<VectorParameter>, IComparable<double>
    {
        public Vector3d Direction;
        public double Parameter;
        public double AngularOffset;

        public static VectorParameter Unset => new VectorParameter { Parameter = double.NaN, AngularOffset = double.NaN, Direction = Vector3d.Unset };

        public void CalculateAngularOffset(Curve curve)
        {
            Plane RMF;
            if (Math.Abs(Parameter - curve.Domain.Min) < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, curve.Domain.Max }).First();
            else if (Math.Abs(Parameter - curve.Domain.Max) < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, curve.Domain.Max }).Last();
            else if (Parameter > curve.Domain.Min)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, Parameter }).Last();
            else
                RMF = curve.GetPerpendicularFrames(new double[] { Parameter, curve.Domain.Max }).First();

            //curve.PerpendicularFrameAt(Parameter, out Plane RMF);
            AngularOffset = Vector3d.VectorAngle(RMF.YAxis, Direction, RMF);
        }

        public int CompareTo(VectorParameter vp)
        {
            if (vp.Parameter == double.NaN) return 1;

            if (Parameter == vp.Parameter) return 0;

            return Parameter > vp.Parameter ? 1 : -1;
        }

        public int CompareTo(double t)
        {
            if (Parameter == t) return 0;
            return Parameter > t ? 1 : -1;
        }

        public int CompareTo(object obj)
        {
            return Parameter.CompareTo(obj);
        }

        public VectorParameter Duplicate() => new VectorParameter { Parameter = Parameter, Direction = Direction, AngularOffset = AngularOffset };

    }

    public class VectorParameterComparer : IComparer<VectorParameter>
    {
        public int Compare(VectorParameter x, VectorParameter y)
        {
            return x.CompareTo(y.Parameter);
        }
        public int Compare(VectorParameter x, double y)
        {
            return x.CompareTo(y);
        }
    }


    /// <summary>
    /// An orientation consisting of a list of vectors arranged at specific parameters
    /// along a curve. Vectors should be perpendicular to the curve upon input, 
    /// otherwise it might give funny results.
    /// </summary>
    public class VectorListOrientation : CrossSectionOrientation
    {
        protected VectorParameter[] m_guides;
        protected Curve m_curve;

        public List<Vector3d> Vectors { get { return m_guides.Select(x => x.Direction).ToList(); } }
        public List<double> Parameters { get { return m_guides.Select(x => x.Parameter).ToList(); } }
        public List<double> AngularOffsets { get { return m_guides.Select(x => x.AngularOffset).ToList(); } }

        public VectorListOrientation(Curve curve, IList<VectorParameter> guides)
        {
            m_guides = new VectorParameter[guides.Count];
            Array.Copy(guides.ToArray(), m_guides, guides.Count);
            m_curve = curve;

            //RecalculateVectors();

            for (int i = 0; i < m_guides.Length; ++i)
            {
                if (m_guides[i].AngularOffset == double.NaN)
                {
                    //m_guides[i].CalculateAngularOffset(curve);
                }
            }

            if (m_guides.Length > 1)
                for (int i = 1; i < m_guides.Length; ++i)
                {
                    /*
                    double diff = m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset;
                    if (Math.Abs(diff) > Math.PI)
                    {
                        m_guides[i].AngularOffset -= Constants.Tau * Math.Sign(diff); ;
                    }
                    */

                    if (m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset > Math.PI)
                        m_guides[i].AngularOffset -= Constants.Tau;
                    else if (m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset < -Math.PI)
                        m_guides[i].AngularOffset += Constants.Tau;

                }

            Array.Sort(m_guides, (a, b) => a.Parameter.CompareTo(b.Parameter));
        }

        public override string ToString()
        {
            return "VectorListOrientation";
        }

        public VectorListOrientation(Curve curve, IList<double> parameters, IList<Vector3d> vectors)
        {
            int N = Math.Min(parameters.Count, vectors.Count);

            m_curve = curve;
            m_guides = new VectorParameter[N];

            //Plane[] RMFs = curve.GetPerpendicularFrames(parameters);

            //Plane RMF;
            int index = 0;
            for (int i = 0; i < N; ++i)
            {
                if (vectors[i].IsValid && !vectors[i].IsZero)
                {
                    //RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, parameters[i] })[1];
                    //curve.PerpendicularFrameAt(parameters[i], out RMF);
                    m_guides[index] = new VectorParameter { Direction = vectors[i], Parameter = parameters[i], AngularOffset = double.NaN };
                    m_guides[index].CalculateAngularOffset(curve);
                    index++;
                }
            }

            Array.Resize(ref m_guides, index);

            if (index > 0)
            {
                for (int i = 1; i < index; ++i)
                {
                    double diff = m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset;
                    if (Math.Abs(diff) > Math.PI)
                    {
                        m_guides[i].AngularOffset -= Constants.Tau * Math.Sign(diff); ;
                    }

                    /*
                    if ((m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset) > Math.PI)
                        m_guides[i].AngularOffset -= Constants.Tau;
                    else if ((m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset) < -Math.PI)
                        m_guides[i].AngularOffset += Constants.Tau;
                    */
                }
            }
            else
                throw new Exception("No valid vectors or parameters.");

            Array.Sort(m_guides, (a, b) => a.Parameter.CompareTo(b.Parameter));

            RecalculateVectors();
        }

        public override Vector3d GetOrientation(Curve curve, double t)
        {
            double angle;
            if (t <= m_guides.First().Parameter)
            {
                angle = m_guides.First().AngularOffset;
            }
            else if (t >= m_guides.Last().Parameter)
            {
                angle = m_guides.Last().AngularOffset;
            }

            int res = Array.BinarySearch(m_guides.ToArray(), t);

            int max = m_guides.Length - 1;
            double mu;

            if (res < 0)
            {
                res = ~res;
                res--;
            }

            if (res >= 0 && res < max)
            {
                mu = (t - m_guides[res].Parameter) / (m_guides[res + 1].Parameter - m_guides[res].Parameter);
                angle = Interpolation.Lerp(m_guides[res].AngularOffset, m_guides[res + 1].AngularOffset, mu);
            }
            else if (res < 0)
            {
                angle = m_guides.First().AngularOffset;
            }
            else if (res >= max)
            {
                angle = m_guides.Last().AngularOffset;
            }
            else
            {
                throw new Exception("It shouldn't come to this...");
                //angle = 0.0;
            }

            Plane RMF;

            if (Math.Abs(t - curve.Domain.Min) < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, curve.Domain.Max }).First();
            else if (Math.Abs(t - curve.Domain.Max) < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, curve.Domain.Max }).Last();
            else if (t > curve.Domain.Min)
                RMF = curve.GetPerpendicularFrames(new double[] { curve.Domain.Min, t }).Last();
            else
                RMF = curve.GetPerpendicularFrames(new double[] { t, curve.Domain.Max }).First();

            //crv.PerpendicularFrameAt(t, out RMF);
            Vector3d vec = RMF.YAxis;

            vec.Transform(Rhino.Geometry.Transform.Rotation(angle, RMF.ZAxis, RMF.Origin));

            return NormalizeVector(curve, t, vec);
        }

        /// <summary>
        /// This particular one can be optimized as in the older FreeformGlulam code
        /// </summary>
        /// <param name="crv"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            return t.Select(x => GetOrientation(crv, x)).ToArray();
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            Point3d pt;
            double t;
            for (int i = 0; i < m_guides.Length; ++i)
            {
                pt = old_curve.PointAt(m_guides[i].Parameter);
                new_curve.ClosestPoint(pt, out t);

                m_guides[i].Parameter = t;
                m_guides[i].CalculateAngularOffset(new_curve);
            }

            Array.Sort(m_guides, (a, b) => a.Parameter.CompareTo(b.Parameter));

            while (m_guides[0].AngularOffset > Math.PI)
                m_guides[0].AngularOffset -= Math.PI * 2;
            while (m_guides[0].AngularOffset < -Math.PI)
                m_guides[0].AngularOffset += Math.PI * 2;

            for (int i = 1; i < m_guides.Length; ++i)
            {
                while (m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset < -Math.PI)
                    m_guides[i].AngularOffset += Math.PI * 2;
                while (m_guides[i].AngularOffset - m_guides[i - 1].AngularOffset > Math.PI)
                    m_guides[i].AngularOffset -= Math.PI * 2;
            }

        }

        public override CrossSectionOrientation Duplicate()
        {
            return new VectorListOrientation(m_curve, m_guides);
        }

        public override CrossSectionOrientation Trim(Interval domain)
        {
            if (domain.IsDecreasing)
                domain.Swap();

            int plusMin = 0, plusMax = 0;

            if (domain.Min > m_curve.Domain.Min)
                plusMin++;
            if (domain.Max < m_curve.Domain.Max)
                plusMax++;


            int iMin = Array.BinarySearch(m_guides, domain.Min);

            if (iMin < 0)
                iMin = ~iMin;

            int iMax = Array.BinarySearch(m_guides, domain.Max);

            if (iMax < 0)
                iMax = ~iMax;
            else
                iMax++;

            int length = iMax - iMin + plusMin + plusMax;

            VectorParameter[] vp = new VectorParameter[length];
            Array.Copy(m_guides, iMin, vp, plusMin, iMax - iMin);

            if (plusMin > 0)
            {
                vp[0] = new VectorParameter { Parameter = domain.Min, Direction = GetOrientation(m_curve, domain.Min) };
                vp[0].CalculateAngularOffset(m_curve);
            }
            if (plusMax > 0)
            {
                vp[vp.Length - 1] = new VectorParameter { Parameter = domain.Max, Direction = GetOrientation(m_curve, domain.Max) };
                vp[vp.Length - 1].CalculateAngularOffset(m_curve);
            }

            return new VectorListOrientation(m_curve, vp);
        }

        public override CrossSectionOrientation[] Split(IList<double> t)
        {
            CrossSectionOrientation[] new_orientations = new CrossSectionOrientation[t.Count + 1];

            if (t.Count < 1)
            {
                new_orientations[0] = this.Duplicate();
                return new_orientations;
            }

            int prev = 0;
            double prev_param = 0.0;
            bool flag = false;

            VectorParameter[] new_guides;

            int index = 0;
            foreach (double param in t)
            {
                int res = Array.BinarySearch(m_guides, param);

                if (res < 0)
                {
                    res = ~res;

                    new_guides = new VectorParameter[res - prev + 1];
                    Array.Copy(m_guides, prev, new_guides, 0, res - prev);

                    new_guides[new_guides.Length - 1] = new VectorParameter { Parameter = param, Direction = GetOrientation(m_curve, param) };
                    new_guides[new_guides.Length - 1].CalculateAngularOffset(m_curve);

                    if (prev > 0 || flag)
                    {
                        VectorParameter[] temp = new VectorParameter[new_guides.Length + 1];
                        temp[0] = new VectorParameter { Parameter = prev_param, Direction = GetOrientation(m_curve, prev_param) };
                        temp[0].CalculateAngularOffset(m_curve);

                        Array.Copy(new_guides, 0, temp, 1, new_guides.Length);
                        new_guides = temp;
                    }

                    new_orientations[index] = new VectorListOrientation(
                        m_curve,
                        new_guides);

                    prev_param = param;
                    prev = res;
                    flag = true;
                }
                else
                {
                    new_guides = new VectorParameter[res - prev + 1];
                    Array.Copy(m_guides, prev, new_guides, 0, res - prev + 1);

                    new_orientations[index] = new VectorListOrientation(
                        m_curve,
                        new_guides);

                    prev = res;
                }

                index++;
            }

            new_guides = new VectorParameter[m_guides.Length - prev + 1];
            Array.Copy(m_guides, prev, new_guides, 1, m_guides.Length - prev);

            new_guides[0] = new VectorParameter { Parameter = t.Last(), Direction = GetOrientation(m_curve, t.Last()) };
            new_guides[0].CalculateAngularOffset(m_curve);

            new_orientations[index] = new VectorListOrientation(
                m_curve,
                new_guides);

            return new_orientations;
        }

        public Curve GetCurve()
        {
            return m_curve;
        }


        public override object GetDriver()
        {
            return m_guides;
        }

        public void RecalculateVectors()
        {
            for (int i = 0; i < m_guides.Length; ++i)
            {
                m_guides[i].Direction.Unitize();
                m_guides[i].Direction = NormalizeVector(m_curve, m_guides[i].Parameter, m_guides[i].Direction);
            }
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            if (orientation is VectorListOrientation)
            {
                VectorListOrientation vlo = orientation as VectorListOrientation;
                VectorParameter[] temp = new VectorParameter[m_guides.Length + vlo.m_guides.Length];
                Array.Copy(m_guides, temp, m_guides.Length);
                Array.Copy(vlo.m_guides, 0, temp, m_guides.Length, vlo.m_guides.Length);
                Array.Sort(temp, (a, b) => a.Parameter.CompareTo(b.Parameter));

                m_guides = temp;
            }

            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            for (int i = 0; i < m_guides.Length; ++i)
            {
                m_guides[i].Direction.Transform(x);
            }
        }
    }


    public class SurfaceOrientation : CrossSectionOrientation
    {
        private Brep m_surface;

        public SurfaceOrientation(Brep srf)
        {
            m_surface = srf;
        }

        public override string ToString()
        {
            return "SurfaceOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            Point3d pt = crv.PointAt(t);
            double u, v;
            ComponentIndex ci;
            Point3d cp;
            Vector3d normal;

            m_surface.ClosestPoint(pt, out cp, out ci, out u, out v, 0, out normal);

            if (ci.ComponentIndexType == ComponentIndexType.BrepEdge)
            {
                int fi = m_surface.Edges[ci.Index].AdjacentFaces().First();
                m_surface.Faces[fi].ClosestPoint(cp, out u, out v);
                normal = m_surface.Faces[fi].NormalAt(u, v);
            }
            else if (ci.ComponentIndexType == ComponentIndexType.BrepVertex)
            {
                int ei = m_surface.Vertices[ci.Index].EdgeIndices().First();
                int fi = m_surface.Edges[ei].AdjacentFaces().First();
                m_surface.Faces[fi].ClosestPoint(cp, out u, out v);
                normal = m_surface.Faces[fi].NormalAt(u, v);
            }

            return NormalizeVector(crv, t, normal);
        }


        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            return t.Select(x => GetOrientation(crv, x)).ToArray();
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            return;
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new SurfaceOrientation(m_surface);
        }


        public override object GetDriver()
        {
            return m_surface;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            return;
        }
    }

    public class RailCurveOrientation : CrossSectionOrientation
    {
        private Curve m_curve;
        private bool m_closest;

        /// <summary>
        /// Use the closest curve point. Otherwise, use the same parameter
        /// or curve length.
        /// </summary>
        public bool ClosestPoint
        {
            get { return m_closest; }
            set { m_closest = value; }
        }

        public RailCurveOrientation(Curve curve)
        {
            m_curve = curve.DuplicateCurve();
            m_closest = true;
        }
        public override string ToString()
        {
            return "RailCurveOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            Vector3d v;
            Point3d pt = crv.PointAt(t);
            if (m_closest)
            {
                double t2;
                m_curve.ClosestPoint(pt, out t2);
                v = m_curve.PointAt(t2) - pt;
            }
            else
            {
                v = m_curve.PointAt(t) - pt;
            }

            return NormalizeVector(crv, t, v);
        }

        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            return t.Select(x => GetOrientation(crv, x)).ToArray();
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            //m_curve.Reverse();
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new RailCurveOrientation(m_curve);
        }

        public override object GetDriver()
        {
            return m_curve;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            return;
        }
    }

    /// <summary>
    /// Orientation that does nothing. Direction vector is the 
    /// normalized curvature vector of the queried curve (Frenet-Serret 
    /// Frame). If querying multiple orientations, the vectors
    /// are reverse if necessary to avoid flips.
    /// </summary>
    public class KCurveOrientation : CrossSectionOrientation
    {

        public KCurveOrientation()
        {
        }

        public override string ToString()
        {
            return "KCurveOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            Vector3d k = crv.CurvatureAt(t);
            k.Unitize();
            return k;
        }
        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            var v = t.Select(x => GetOrientation(crv, x)).ToArray();
            if (t.Count > 0)
                for (int i = 1; i < v.Length; ++i)
                {
                    if (v[i] * v[i - 1] < 0)
                        v[i].Reverse();
                }

            return v;
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            return;
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new KCurveOrientation();
        }


        public override object GetDriver()
        {
            return null;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            return;
        }
    }

    /// <summary>
    /// Orientation that uses the rotation minimizing frame of the curve. 
    /// Direction vector is the Y-axis of the RMF.
    /// </summary>
    public class RmfOrientation : CrossSectionOrientation
    {

        public RmfOrientation()
        {
        }

        public override string ToString()
        {
            return "RmfOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            crv.Domain.MakeIncreasing();

            if (t <= crv.Domain.Min)
            {
                return crv.GetPerpendicularFrames(new double[] { t, crv.Domain.Max })[0].YAxis;
            }
            else if (t >= crv.Domain.Max)
            {
                return crv.GetPerpendicularFrames(new double[] { crv.Domain.Min, t })[1].YAxis;
            }

            return crv.GetPerpendicularFrames(new double[] { crv.Domain.Min, t })[1].YAxis;
        }
        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            IEnumerable<double> sorted_t = t.OrderBy(x => x).Where(y => crv.Domain.IncludesParameter(y));
            try
            {
                Plane[] planes = crv.GetPerpendicularFrames(sorted_t);
                return planes.Select(x => x.YAxis).ToArray();
            }
            catch
            {
                throw new Exception("RmfOrientation::GetOrientations() failed.");
            }
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            return;
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new RmfOrientation();
        }

        public override CrossSectionOrientation[] Split(IList<double> t)
        {
            CrossSectionOrientation[] new_orientations = new CrossSectionOrientation[t.Count + 1];
            for (int i = 0; i < new_orientations.Length; ++i)
            {
                new_orientations[i] = this.Duplicate();
            }

            return new_orientations;
        }

        public override object GetDriver()
        {
            return null;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            return;
        }
    }

    public class PlanarOrientation : CrossSectionOrientation
    {
        public Plane Plane;
        public PlanarOrientation(Plane plane)
        {
            Plane = plane;
        }

        public override string ToString()
        {
            return "PlanarOrientation";
        }

        public override Vector3d GetOrientation(Curve crv, double t)
        {
            return Vector3d.CrossProduct(Plane.Normal, crv.TangentAt(t));
        }
        public override Vector3d[] GetOrientations(Curve crv, IList<double> t)
        {
            return t.Select(x => Vector3d.CrossProduct(Plane.Normal, crv.TangentAt(x))).ToArray();
        }

        public override void Remap(Curve old_curve, Curve new_curve)
        {
            return;
        }

        public override CrossSectionOrientation Duplicate()
        {
            return new PlanarOrientation(Plane);
        }

        public override CrossSectionOrientation[] Split(IList<double> t)
        {
            CrossSectionOrientation[] new_orientations = new CrossSectionOrientation[t.Count + 1];
            for (int i = 0; i < new_orientations.Length; ++i)
            {
                new_orientations[i] = this.Duplicate();
            }

            return new_orientations;
        }

        public override object GetDriver()
        {
            return Plane;
        }

        public override CrossSectionOrientation Join(CrossSectionOrientation orientation)
        {
            return this.Duplicate();
        }

        public override void Transform(Transform x)
        {
            Plane.Transform(x);
            return;
        }
    }
}
