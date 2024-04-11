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
    public enum GlulamType
    {
        Straight,
        SingleCurved,
        DoubleCurved
    }

    [Serializable]
    public abstract partial class Glulam : Beam
    {
        public static double RadiusMultiplier = 200.0;  // This is the Eurocode 5 formula: lamella thickness cannot exceed 1/200th of the curvature radius.
        public static int CurvatureSamples = 100;       // Number of samples to samples curvature at.
        public static double RadiusTolerance = 0.00001; // For curvature calculations: curvature radius and lamella thickness cannot exceed this
        public static double MininumSegmentLength = 30.0; // Minimum length of discretized segment when creating glulam geometry (mm).
        public static int MinimumNumSegments = 25;

        #region Static variables and methods
        public static double Tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
        public static double OverlapTolerance = 1.0 * Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Millimeters);
        public static double AngleTolerance = Rhino.RhinoMath.ToRadians(2.5);

        #endregion

        public Guid Id { private set; get; }
        public GlulamData Data;

        // Protected
        protected Point3d[] m_section_corners = null; // Cached section corners
        protected Point3d[] m_lamella_centers = null; // Cached centerpoints for lamellae

        public Func<double, Point3d[]> CornerGenerator;

        /// <summary>
        /// Get total width of glulam.
        /// </summary>
        public override double Width
        {
            get
            {
                return Data.LamWidth * Data.NumWidth;
            }
        }
        /// <summary>
        /// Get total height of glulam.
        /// </summary>
        public override double Height
        {
            get
            {
                return Data.LamHeight * Data.NumHeight;
            }
        }

        public Dictionary<string, object> GetProperties()
        {
            Dictionary<string, object> props = new Dictionary<string, object>();

            props.Add("id", Id);
            props.Add("centreline", Centreline);
            props.Add("width", Width);
            props.Add("height", Height);
            props.Add("length", Centreline.GetLength());
            props.Add("lamella_width", Data.LamWidth);
            props.Add("lamella_height", Data.LamHeight);
            props.Add("lamella_count_width", Data.NumWidth);
            props.Add("lamella_count_height", Data.NumHeight);
            props.Add("volume", GetVolume());
            props.Add("samples", Data.Samples);
            //props.Add("frames", GetAllPlanes());

            double max_kw = 0.0, max_kh = 0.0;
            props.Add("max_curvature", GetMaxCurvature(ref max_kw, ref max_kh));
            props.Add("max_curvature_width", max_kw);
            props.Add("max_curvature_height", max_kh);
            props.Add("type", ToString());
            props.Add("type_id", (int)Type());
            props.Add("orientation", Orientation);

            return props;
        }

        public override bool Equals(object obj)
        {
            if (obj is Glulam && (obj as Glulam).Id == Id)
                return true;
            return false;
        }

        /// <summary>
        /// Duplicate glulam data.
        /// </summary>
        /// <returns></returns>
        public new Glulam DuplicateGlulam() => CreateGlulam(Centreline.DuplicateCurve(), Orientation.Duplicate(), Data.Duplicate());
        public override Beam Duplicate() => DuplicateGlulam();

        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => "Glulam";
        public virtual GlulamType Type() => GlulamType.Straight;

        /// <summary>
        /// Reverse direction of glulam.
        /// </summary>
        public void Reverse()
        {
            Curve Reversed = Centreline.DuplicateCurve();
            Reversed.Reverse();

            Orientation.Remap(Centreline, Reversed);
            Centreline = Reversed;
        }

        public virtual double GetVolume(bool accurate = false)
        {
            if (accurate)
            {
                Rhino.Geometry.VolumeMassProperties vmp = VolumeMassProperties.Compute(ToBrep());
                return vmp.Volume;
            }
            return Centreline.GetLength() * Width * Height;
        }

        /// <summary>
        /// Join a glulam onto another one. Returns null if join is not possible.
        /// </summary>
        /// <param name="glulam"></param>
        /// <returns></returns>
        public Glulam Join(Glulam glulam)
        {
            Rhino.Geometry.Intersect.CurveIntersections ci;
            ci = Rhino.Geometry.Intersect.Intersection.CurveCurve(Centreline, glulam.Centreline, Tolerance, OverlapTolerance);
            if (ci.Count != 1) return null;
            if (ci[0].IsOverlap) return null;
            if (Math.Abs(Centreline.TangentAt(ci[0].ParameterA) * glulam.Centreline.TangentAt(ci[0].ParameterB)) < AngleTolerance) return null;

            Curve[] NewCentreline = Curve.JoinCurves(new Curve[] { Centreline, glulam.Centreline });
            if (NewCentreline.Length != 1) return null;

            CrossSectionOrientation NewOrientation = Orientation.Duplicate();
            NewOrientation.Join(glulam.Orientation);

            Glulam new_glulam = CreateGlulam(NewCentreline[0], NewOrientation, Data.Duplicate());

            new_glulam.Data.Samples = Data.Samples + glulam.Data.Samples;

            return new_glulam;
        }

        public bool Extend(CurveEnd end, double length, CurveExtensionStyle style = CurveExtensionStyle.Smooth)
        {
            Curve c = Centreline.Extend(end, length, style);
            if (c == null) return false;

            Centreline = c;
            return true;
        }

        /// <summary>
        /// Split glulam into two at parameter t.
        /// </summary>
        /// <param name="t">Curve parameter to split glulam at.</param>
        /// <returns>List of new glulams.</returns>
        public List<Glulam> Split(double t)
        {
            if (!Centreline.Domain.IncludesParameter(t)) return null;

            double percentage = (t - Centreline.Domain.Min) / (Centreline.Domain.Max - Centreline.Domain.Min);

            Plane split_plane = GetPlane(t);
            Curve[] split_curves = Centreline.Split(t);
            if (split_curves == null || split_curves.Length != 2) return null;

            GlulamData Data1 = Data.Duplicate();
            Data1.Samples = (int)(Data.Samples * percentage);

            CrossSectionOrientation[] SplitOrientations = Orientation.Split(new double[] { t });

            Glulam Blank1 = CreateGlulam(split_curves[0], SplitOrientations[0], Data1);

            GlulamData Data2 = Data.Duplicate();
            Data2.Samples = (int)(Data.Samples * (1 - percentage));

            Glulam Blank2 = CreateGlulam(split_curves[1], SplitOrientations[1], Data2);

            List<Glulam> blanks = new List<Glulam>() { Blank1, Blank2 };
            return blanks;
        }

        public override Beam Trim(Interval domain, double overlap)
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

            GlulamData data = Data.Duplicate();
            data.Samples = Math.Max(6, (int)(data.Samples * percentage));


            Curve trimmed_curve = Centreline.Trim(domain);

            CrossSectionOrientation trimmed_orientation = Orientation.Trim(domain);
            trimmed_orientation.Remap(Centreline, trimmed_curve);

            Glulam glulam = CreateGlulam(trimmed_curve, trimmed_orientation, data);

            return glulam;
        }

        /// <summary>
        /// Split glulam into two at parameter t, with an overlap of a certain length.
        /// </summary>
        /// <param name="t">Curve parameter to split glulam at.</param>
        /// <param name="overlap">Amount of overlap.</param>
        /// <returns>List of new glulams.</returns>
        public List<Glulam> Split(double t, double overlap)
        {
            if (overlap < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) return Split(t);

            if (!Centreline.Domain.IncludesParameter(t)) return null;
            double split_length = Centreline.GetLength(new Interval(Centreline.Domain.Min, t));

            double t1;
            double t2;

            if (!Centreline.LengthParameter(split_length + (overlap / 2), out t1)) return null;
            if (!Centreline.LengthParameter(split_length - (overlap / 2), out t2)) return null;

            if (!Centreline.Domain.IncludesParameter(t1) || !Centreline.Domain.IncludesParameter(t2)) return null;

            Curve[] split_curves;
            Plane split_plane;
            double percentage;

            Glulam Blank1, Blank2;
            GlulamData Data1, Data2;
            {
                percentage = (t1 - Centreline.Domain.Min) / (Centreline.Domain.Max - Centreline.Domain.Min);
                split_plane = GetPlane(t1);
                split_curves = Centreline.Split(t1);
                if (split_curves == null || split_curves.Length != 2) return null;

                var SplitOrientation = Orientation.Split(new double[] { t1 });

                Data1 = Data.Duplicate();
                Data1.Samples = Math.Max(2, (int)(Data.Samples * percentage));

                Blank1 = CreateGlulam(split_curves[0], SplitOrientation[0], Data1);
            }
            {
                percentage = (t2 - Centreline.Domain.Min) / (Centreline.Domain.Max - Centreline.Domain.Min);
                split_plane = GetPlane(t2);
                split_curves = Centreline.Split(t2);
                if (split_curves == null || split_curves.Length != 2) return null;

                var SplitOrientation = Orientation.Split(new double[] { t2 });

                Data2 = Data.Duplicate();
                Data2.Samples = Math.Max(2, (int)(Data.Samples * (1 - percentage)));

                Blank2 = CreateGlulam(split_curves[1], SplitOrientation[1], Data2);
            }

            List<Glulam> blanks = new List<Glulam>() { Blank1, Blank2 };
            return blanks;
        }

        public Glulam[] Split(IList<double> t, double overlap = 0.0)
        {
            if (t.Count < 1)
                return new Glulam[] { this.DuplicateGlulam() };
            if (t.Count < 2)
            {
                if (Centreline.Domain.IncludesParameter(t[0]))
                    return Split(t[0]).ToArray();
                else
                    return new Glulam[] { this.DuplicateGlulam() };
            }


            Glulam temp = this;

            List<double> parameters = new List<double>();
            foreach (double p in t)
            {
                if (Centreline.Domain.IncludesParameter(p))
                    parameters.Add(p);
            }
            parameters.Sort();


            //Curve[] centrelines = Centreline.Split(t);
            //GlulamOrientation[] orientations = Orientation.Split(t);

            Glulam[] glulams = new Glulam[parameters.Count];

            int num_splits = 0;
            for (int i = 1; i < parameters.Count - 1; ++i)
            {
                List<Glulam> splits = temp.Split(parameters[i], overlap);

                if (splits == null || splits.Count < 2)
                    continue;

                if (splits[0] != null)
                {
                    glulams[i - 1] = splits[0];
                    num_splits++;
                }
                temp = splits[1];
            }

            if (temp != null)
                glulams[glulams.Length - 1] = temp;

            return glulams;
        }

        /// <summary>
        /// Overbend glulam to account for springback.
        /// </summary>
        /// <param name="t">Amount to overbend (1.0 is no effect. Less than 1.0 relaxes the curvature, more than 1.0 increases curvature.</param>
        /// <returns>New overbent glulam.</returns>
        public virtual Glulam Overbend(double t)
        {
            return this;
        }

        /// <summary>
        /// Maps a mesh onto the curve space of the glulam. This makes other analysis much easier.
        /// </summary>
        /// <param name="m">Mesh to map.</param>
        /// <returns>New mesh that is mapped onto curve space (Y-axis is axis of curve).</returns>
        public abstract Mesh MapToCurveSpace(Mesh m);

        /// <summary>
        /// Create a new Curve which is offset from the Centreline according to the Glulam frames. This means 
        /// that the new Curve will follow the orientation and twisting of this Glulam.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public abstract Curve CreateOffsetCurve(double x, double y, bool rebuild = false, int rebuild_pts = 20);
        public abstract Curve CreateOffsetCurve(double x, double y, bool offset_start, bool offset_end, bool rebuild = false, int rebuild_pts = 20);

        public void GetSectionOffset(out double offsetX, out double offsetY)
        {
            double x = Width;
            double y = Height;

            offsetX = OffsetX;
            offsetY = OffsetY;
            return;

            //double x0 = 0, y0 = 0;
            double hx = x / 2, hy = y / 2;

            offsetX = 0; offsetY = 0;

            switch (Data.SectionAlignment)
            {
                case (GlulamData.CrossSectionPosition.MiddleCentre):
                    offsetX = 0; offsetY = 0;
                    //x0 = -hx; y0 = -hy; 
                    break;

                case (GlulamData.CrossSectionPosition.TopLeft):
                    offsetX = hx; offsetY = -hy;
                    break;

                case (GlulamData.CrossSectionPosition.TopCentre):
                    offsetX = 0; offsetY = -hy;
                    //x0 = -hx; y0 = -y; 
                    break;

                case (GlulamData.CrossSectionPosition.TopRight):
                    offsetX = -hx; offsetY = -hy;
                    //x0 = -x; y0 = -y;
                    break;

                case (GlulamData.CrossSectionPosition.MiddleLeft):
                    offsetX = hx; offsetY = 0;
                    //y0 = -hy;
                    break;

                case (GlulamData.CrossSectionPosition.MiddleRight):
                    offsetX = -hx; offsetY = 0;
                    //x0 = -x; y0 = -hy; 
                    break;

                case (GlulamData.CrossSectionPosition.BottomLeft):
                    offsetX = hx; offsetY = hy;
                    break;

                case (GlulamData.CrossSectionPosition.BottomCentre):
                    offsetX = 0; offsetY = hy;
                    //x0 = -hx;
                    break;

                case (GlulamData.CrossSectionPosition.BottomRight):
                    offsetX = -hx; offsetY = hy;
                    //x0 = -x; 
                    break;
            }
        }

        public Point3d[] GenerateCorners(double offset = 0.0)
        {
            double x = Width;
            double y = Height;
            double hx = Width * 0.5, hy = Height * 0.5;

            double x0 = -hx + OffsetX, x1 = hx + OffsetX, y0 = -hy + OffsetY, y1 = hy + OffsetY;

            int numCorners = 4;

            Point3d[] section_corners = new Point3d[numCorners];

            //m_section_corners = new Point3d[numCorners];
            /*
            switch (Data.SectionAlignment)
            {
                case (GlulamData.CrossSectionPosition.MiddleCentre):
                    x0 -= hx; y0 -= hy; x1 -= hx; y1 -= hy;
                    break;

                case (GlulamData.CrossSectionPosition.TopLeft):
                    y0 -= y; y1 -= y;
                    break;

                case (GlulamData.CrossSectionPosition.TopCentre):
                    x0 -= hx; y0 -= y; x1 -= hx; y1 -= y;
                    break;

                case (GlulamData.CrossSectionPosition.TopRight):
                    x0 -= x; y0 -= y; x1 -= x; y1 -= y;
                    break;

                case (GlulamData.CrossSectionPosition.MiddleLeft):
                    y0 -= hy; y1 -= hy;
                    break;

                case (GlulamData.CrossSectionPosition.MiddleRight):
                    x0 -= x; y0 -= hy; x1 -= x; y1 -= hy;
                    break;

                case (GlulamData.CrossSectionPosition.BottomLeft):
                    break;

                case (GlulamData.CrossSectionPosition.BottomCentre):
                    x0 -= hx; x1 -= hx;
                    break;

                case (GlulamData.CrossSectionPosition.BottomRight):
                    x0 -= x; x1 -= x;
                    break;
            }
            */

            section_corners[0] = new Point3d(x0 - offset, y0 - offset, 0);
            section_corners[1] = new Point3d(x0 - offset, y1 + offset, 0);
            section_corners[2] = new Point3d(x1 + offset, y1 + offset, 0);
            section_corners[3] = new Point3d(x1 + offset, y0 - offset, 0);

            return section_corners;
        }

        List<Curve> LamellaOutlines(Glulam g)
        {
            double[] t = g.Centreline.DivideByCount(g.Data.Samples, true);

            List<Plane> planes = t.Select(x => g.GetPlane(x)).ToList();

            Point3d[][] pts = new Point3d[4][];
            pts[0] = new Point3d[g.Data.NumWidth + 1];
            pts[1] = new Point3d[g.Data.NumHeight + 1];

            pts[2] = new Point3d[g.Data.NumWidth + 1];
            pts[3] = new Point3d[g.Data.NumHeight + 1];

            double hWidth = g.Width / 2;
            double hHeight = g.Height / 2;

            // Create points for lamella corners
            for (int i = 0; i <= g.Data.NumWidth; ++i)
            {
                pts[0][i] = new Point3d(-hWidth + g.Data.LamWidth * i, -hHeight, 0);
                pts[2][i] = new Point3d(-hWidth + g.Data.LamWidth * i, hHeight, 0);
            }

            for (int i = 0; i <= g.Data.NumHeight; ++i)
            {
                pts[1][i] = new Point3d(-hWidth, -hHeight + g.Data.LamHeight * i, 0);
                pts[3][i] = new Point3d(hWidth, -hHeight + g.Data.LamHeight * i, 0);
            }

            List<Point3d>[][] crv_pts = new List<Point3d>[4][];

            crv_pts[0] = new List<Point3d>[g.Data.NumWidth + 1];
            crv_pts[1] = new List<Point3d>[g.Data.NumHeight + 1];
            crv_pts[2] = new List<Point3d>[g.Data.NumWidth + 1];
            crv_pts[3] = new List<Point3d>[g.Data.NumHeight + 1];

            Transform xform;
            Point3d pt;

            // Create curve points
            foreach (Plane p in planes)
            {
                xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, p);
                for (int i = 0; i <= g.Data.NumWidth; ++i)
                {
                    pt = new Point3d(pts[0][i]);

                    pt.Transform(xform);
                    if (crv_pts[0][i] == null)
                        crv_pts[0][i] = new List<Point3d>();
                    crv_pts[0][i].Add(pt);

                    pt = new Point3d(pts[2][i]);

                    pt.Transform(xform);
                    if (crv_pts[2][i] == null)
                        crv_pts[2][i] = new List<Point3d>();
                    crv_pts[2][i].Add(pt);
                }

                for (int i = 0; i <= g.Data.NumHeight; ++i)
                {
                    pt = new Point3d(pts[1][i]);

                    pt.Transform(xform);
                    if (crv_pts[1][i] == null)
                        crv_pts[1][i] = new List<Point3d>();
                    crv_pts[1][i].Add(pt);

                    pt = new Point3d(pts[3][i]);

                    pt.Transform(xform);
                    if (crv_pts[3][i] == null)
                        crv_pts[3][i] = new List<Point3d>();
                    crv_pts[3][i].Add(pt);
                }
            }

            // Create lamella side curves
            List<Curve> crvs = new List<Curve>();
            for (int i = 0; i <= g.Data.NumWidth; ++i)
            {
                crvs.Add(Curve.CreateInterpolatedCurve(crv_pts[0][i], 3));
                crvs.Add(Curve.CreateInterpolatedCurve(crv_pts[2][i], 3));
            }

            for (int i = 0; i <= g.Data.NumHeight; ++i)
            {
                crvs.Add(Curve.CreateInterpolatedCurve(crv_pts[1][i], 3));
                crvs.Add(Curve.CreateInterpolatedCurve(crv_pts[3][i], 3));
            }

            // Create lamella end curves
            Point3d p0, p1;

            xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, planes.First());

            for (int i = 0; i <= g.Data.NumWidth; ++i)
            {
                p0 = new Point3d(pts[0][i]);
                p0.Transform(xform);

                p1 = new Point3d(pts[2][i]);
                p1.Transform(xform);

                crvs.Add(new Line(p0, p1).ToNurbsCurve());
            }

            for (int i = 0; i <= g.Data.NumHeight; ++i)
            {
                p0 = new Point3d(pts[1][i]);
                p0.Transform(xform);

                p1 = new Point3d(pts[3][i]);
                p1.Transform(xform);

                crvs.Add(new Line(p0, p1).ToNurbsCurve());
            }


            xform = Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, planes.Last());

            for (int i = 0; i <= g.Data.NumWidth; ++i)
            {
                p0 = new Point3d(pts[0][i]);
                p0.Transform(xform);

                p1 = new Point3d(pts[2][i]);
                p1.Transform(xform);

                crvs.Add(new Line(p0, p1).ToNurbsCurve());
            }

            for (int i = 0; i <= g.Data.NumHeight; ++i)
            {
                p0 = new Point3d(pts[1][i]);
                p0.Transform(xform);

                p1 = new Point3d(pts[3][i]);
                p1.Transform(xform);

                crvs.Add(new Line(p0, p1).ToNurbsCurve());
            }

            return crvs;
        }
    }
}
