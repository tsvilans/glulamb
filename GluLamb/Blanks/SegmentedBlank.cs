using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Geometry;

namespace GluLamb.Blanks
{
    /// <summary>
    /// A segmented blank developed during the Helen and Hard DAC project.
    /// </summary>
    public class SegmentedBlank
    {
        public static double AngleLimit = RhinoMath.ToRadians(5);
        public static double CentrelineDeviation = 20;

        public string Name;
        public List<BlankSegment> Segments;
        public Curve Centreline;
        public Plane Plane;
        public BoundingBox Bounds;
        public int SimpleDivision = 0;

        public List<Plane> DivisionPlanes;

        public double Height = 60;
        public double LayerThickness = 20;

        public Curve[] EdgeCurves;

        public double Offset0;
        public double Offset1;

        private Curve[] m_offsets;
        public Curve InnerOffset
        {
            get { return m_offsets[0]; }
            set { m_offsets[0] = value; }
        }
        public Curve OuterOffset
        {
            get { return m_offsets[1]; }
            set { m_offsets[1] = value; }
        }

        public List<Plane> Planes;
        public List<object> debug;

        public SegmentedBlank(Curve centreline, Curve[] edge_curves, Plane plane, double width0, double width1, double height, double layer_thickness)
        {
            if (plane == Plane.Unset)
                if (!Centreline.TryGetPlane(out plane))
                    plane = Plane.WorldXY;

            this.Plane = plane;
            debug = new List<object>();


            // Orient centreline
            int sign = 1;

            if (centreline.TangentAtStart * Vector3d.XAxis < 0)
            {
                centreline.Reverse();
                sign = -1;
            }

            Centreline = centreline;

            Offset0 = width0;
            Offset1 = width1;

            // Make blank side offsets
            m_offsets = new Curve[2];
            m_offsets[0] = Centreline.Offset(Plane, width0 * sign, 0.1, CurveOffsetCornerStyle.None)[0];
            m_offsets[1] = Centreline.Offset(Plane, width1 * -sign, 0.1, CurveOffsetCornerStyle.None)[0];

            var bb_inner = InnerOffset.GetBoundingBox(Plane);
            var bb_outer = OuterOffset.GetBoundingBox(Plane);

            Bounds = BoundingBox.Union(bb_inner, bb_outer);
            var origin = Bounds.Max;
            origin = Plane.PointAt(origin.X, origin.Y, origin.Z);
            Bounds.Transform(Transform.Translation(new Vector3d(-Bounds.Max)));

            this.Plane.Origin = origin;

            EdgeCurves = edge_curves;
            DivisionPlanes = new List<Plane>();
            Height = height;
            LayerThickness = layer_thickness;

        }

        public void CreateDivisionPlanes(IList<double> tt)
        {
            DivisionPlanes = tt.Select(x => new Plane(Centreline.PointAt(x), Plane.ZAxis, Vector3d.CrossProduct(Centreline.TangentAt(x), Plane.ZAxis))).ToList();
        }
        public void TrimOffsetsToDivisionPlanes()
        {
            var res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, DivisionPlanes.First(), 0.01);
            var res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, DivisionPlanes.Last(), 0.01);
            if (res0 != null && res0.Count > 0 && res1 != null && res1.Count > 0)
            {
                InnerOffset = InnerOffset.Trim(res0[0].ParameterA, res1[0].ParameterA);
            }

            res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, DivisionPlanes.First(), 0.01);
            res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, DivisionPlanes.Last(), 0.01);
            if (res0 != null && res0.Count > 0 && res1 != null && res1.Count > 0)
            {
                OuterOffset = OuterOffset.Trim(res0[0].ParameterA, res1[0].ParameterA);
            }
        }
        public List<double> SegmentCurve(double min, double max, double angle)
        {
            var divs = new List<double>();

            var length = Centreline.GetLength();
            var N = (int)Math.Ceiling(length / 3.0);

            if (length < max * 2)
            {
                max = length / 3;
            }

            var tt = Centreline.DivideByCount(N, true);

            double finalT, firstT;

            Centreline.LengthParameter(length - min, out finalT);
            Centreline.LengthParameter(min, out firstT);

            double lastT = tt[0];

            divs.Add(tt[0]);

            Vector3d lastVector = Centreline.TangentAtStart;

            //divs.Add(firstT);

            for (int i = 1; i < tt.Length; ++i)
            {
                var tan = Centreline.TangentAt(tt[i]);

                var segLength = Centreline.GetLength(new Interval(lastT, tt[i]));
                if (segLength > max || // if the maximum segment length is reached
                  (Vector3d.VectorAngle(tan, lastVector) > angle * 0.5 && segLength > min * 0.5) && tt[i] > firstT)
                {
                    divs.Add(tt[i - 1]);
                    lastT = tt[i - 1];
                    lastVector = Centreline.TangentAt(tt[i - 1]);
                    i--;
                }

                if (tt[i] >= finalT)
                {
                    if (Vector3d.VectorAngle(lastVector, Centreline.TangentAt(tt[tt.Length - 1])) > angle)
                        divs.Add((tt[i - 1] + tt[tt.Length - 1]) * 0.5);

                    divs.Add(finalT);

                    divs.Add(tt[tt.Length - 1]);
                    break;
                }
            }

            return divs;
        }

        public List<double> SegmentCentreline(double min, double max, bool alternate = false)
        {
            var tlist = new List<double>();
            double length = Centreline.GetLength();

            if (Centreline.IsLinear())
            {
                int N = (int)Math.Ceiling(length / (max * 0.5));
                var tt = Centreline.DivideByCount(N, true);

                tlist.AddRange(tt);
            }
            else
            {
                var width = Math.Max(Offset0, Offset1) * 2;

                //var polyline = Centreline.ToPolyline(CentrelineDeviation, AngleLimit * 0.5, min, max * 0.5).ToPolyline();
                //var polyline = Centreline.ToPolyline(0, 0, AngleLimit, Math.Tan(AngleLimit / 10), 0, CentrelineDeviation, min, max * 0.5, true).ToPolyline();
                var polyline = Centreline.ToPolyline(0, 0, AngleLimit, 100, 0, CentrelineDeviation, min * 0.5, max * 0.5, true).ToPolyline();
                polyline.CollapseShortSegments(min);

                double t;
                foreach (var pt in polyline)
                {
                    Centreline.ClosestPoint(pt, out t);
                    tlist.Add(t);
                }
            }
            return tlist;
        }

        public List<double> SegmentCentreline2(double min, double max, bool alternate = false)
        {
            if (Centreline.IsLinear())
            {
                List<double> tlist = new List<double>();

                double length = Centreline.GetLength();
                int N = (int)Math.Ceiling(length / max) * 2;
                var tt = Centreline.DivideByCount(N, false);

                var flag = alternate;
                tlist.Add(Centreline.Domain.Min);

                foreach (double t in tt)
                {
                    if (flag || true)
                        tlist.Add(t);
                    flag = !flag;
                }

                tlist.Add(Centreline.Domain.Max);

                return tlist;
            }
            var polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), 0, max).ToPolyline();
            polyline.CollapseShortSegments(min);

            if (!alternate || true)
            {

                double[] tt = new double[polyline.Count * 2 - 1];
                var psegs = polyline.GetSegments();

                //for (int k = 0; k < polyline.Count; ++k)
                ////{
                //  Centreline.ClosestPoint(polyline[k], out tt[k]);
                //}

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From, out tt[k * 2]);
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt[k * 2 + 1]);
                }

                tt[tt.Length - 1] = Centreline.Domain.Max;

                return tt.ToList();
            }
            else
            {

                var psegs = polyline.GetSegments();
                double[] tt2 = new double[polyline.SegmentCount + 2];

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt2[k + 1]);
                }

                tt2[0] = Centreline.Domain.Min;
                tt2[tt2.Length - 1] = Centreline.Domain.Max;

                return tt2.ToList();
            }
        }
        public List<double> SegmentCentreline2b(double min, double max, bool alternate = false)
        {
            if (Centreline.IsLinear())
            {
                List<double> tlist = new List<double>();

                double length = Centreline.GetLength();
                int N = (int)Math.Ceiling(length / max) * 2;
                var tt = Centreline.DivideByCount(N, false);

                var flag = alternate;
                tlist.Add(Centreline.Domain.Min);

                foreach (double t in tt)
                {
                    if (flag || true)
                        tlist.Add(t);
                    flag = !flag;
                }

                tlist.Add(Centreline.Domain.Max);

                return tlist;
            }
            var polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), 0, max).ToPolyline();
            polyline.CollapseShortSegments(min);

            if (!alternate || true)
            {

                double[] tt = new double[polyline.Count * 2 - 1];
                var psegs = polyline.GetSegments();

                //for (int k = 0; k < polyline.Count; ++k)
                ////{
                //  Centreline.ClosestPoint(polyline[k], out tt[k]);
                //}

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From, out tt[k * 2]);
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt[k * 2 + 1]);
                }

                tt[tt.Length - 1] = Centreline.Domain.Max;

                return tt.ToList();
            }
            else
            {

                var psegs = polyline.GetSegments();
                double[] tt2 = new double[polyline.SegmentCount + 2];

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt2[k + 1]);
                }

                tt2[0] = Centreline.Domain.Min;
                tt2[tt2.Length - 1] = Centreline.Domain.Max;

                return tt2.ToList();
            }
        }

        public List<double> SegmentCurve3(double min, double max, double angle)
        {
            Curve c = Centreline;
            Plane plane;
            c.TryGetPlane(out plane);

            int N = 100;

            var tlist = new List<double>();

            var tt = c.DivideByCount(N, true);

            tlist.Add(tt[0]);

            Vector3d tmp = c.TangentAt(tt[0]);
            for (int i = 0; i < tt.Length; ++i)
            {
                var vec = c.TangentAt(tt[i]);
                if (Vector3d.VectorAngle(vec, tmp) > angle)
                {
                    tlist.Add(tt[i - 1]);
                    tmp = vec;
                }
            }

            tlist.Add(tt[tt.Length - 1]);

            var doms = new List<Interval>();

            for (int i = 0; i < tlist.Count - 1; ++i)
            {
                var dom = new Interval(tlist[i], tlist[i + 1]);
                doms.Add(dom);
            }

            for (int i = 0; i < doms.Count; ++i)
            {
                var length = c.GetLength(doms[i]);

                if (length > max)
                {
                    int ndiv = (int)Math.Ceiling(length / max);
                    for (int j = 0; j < ndiv; ++j)
                    {
                        //tlist.Insert(i, doms[i].Mid);
                        tlist.Insert(i, doms[i].Min + doms[i].Length / ndiv * j);

                    }
                }
            }

            tlist.Sort();

            return tlist;

        }

        public List<Line> CreateDivisionLines()
        {
            if (InnerOffset == null || OuterOffset == null) throw new Exception("SegmentedBlank: Offsets not defined.");

            var lines = new List<Line>();

            foreach (Plane plane in DivisionPlanes)
            {
                var res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, plane, 0.1);
                var res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, plane, 0.1);

                var line = new Line(res0[0].PointA, res1[0].PointA);

                //var line = new Line(plane.PointAt(0, Offset0), plane.PointAt(0, -Offset1));

                lines.Add(line);
            }

            return lines;
        }

        public List<List<BlankSegment>> CreateSegments()
        {
            // Simple output list of geometry
            var output = new List<List<BlankSegment>>();

            int Nlayers = (int)(Height / LayerThickness);

            int N = DivisionPlanes.Count;

            if (N < 2) throw new Exception("No division planes!");


            var tt0 = new double[N];
            var tt1 = new double[N];
            var pt0 = new Point3d[N];
            var pt1 = new Point3d[N];

            tt0[0] = m_offsets[0].Domain.Min; tt0[N - 1] = m_offsets[0].Domain.Max;
            tt1[0] = m_offsets[1].Domain.Min; tt1[N - 1] = m_offsets[1].Domain.Max;
            pt0[0] = m_offsets[0].PointAtStart; pt0[N - 1] = m_offsets[0].PointAtEnd;
            pt1[0] = m_offsets[1].PointAtStart; pt1[N - 1] = m_offsets[1].PointAtEnd;



            for (int i = 0; i < Nlayers; ++i)
            {
                double zheight = -Height * 0.5 + i * LayerThickness;

                output.Add(new List<BlankSegment>());

                int mod = i.Modulus(2);

                for (int j = 1; j < DivisionPlanes.Count - 1; ++j)
                {
                    var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(m_offsets[0], DivisionPlanes[j], 0.1);
                    tt0[j] = res[0].ParameterA;
                    pt0[j] = res[0].PointA;

                    res = Rhino.Geometry.Intersect.Intersection.CurvePlane(m_offsets[1], DivisionPlanes[j], 0.1);
                    tt1[j] = res[0].ParameterA;
                    pt1[j] = res[0].PointA;

                }

                Curve[] segs;
                for (int j = -mod; j < N - 1; j += 2)
                {
                    var x = Math.Max(j, 0);
                    var y = Math.Min(j + 2, N - 1);

                    if (x == y) continue;

                    segs = new Curve[4];
                    segs[0] = new Line(pt1[x], pt0[x]).ToNurbsCurve();
                    segs[1] = m_offsets[0].Trim(tt0[x], tt0[y]);
                    segs[2] = new Line(pt0[y], pt1[y]).ToNurbsCurve();
                    segs[3] = m_offsets[1].Trim(tt1[x], tt1[y]);

                    if (segs[1] == null || segs[3] == null)
                    {
                        debug.Add(DivisionPlanes[x]);
                        debug.Add(DivisionPlanes[y]);
                        debug.AddRange(segs);
                        debug.Add(pt1[x]);
                        debug.Add(pt0[x]);
                        debug.Add(pt1[y]);
                        debug.Add(pt0[y]);
                        continue;
                    }

                    var loop = Curve.JoinCurves(segs)[0];
                    if (loop == null)
                    {
                        debug.AddRange(segs);
                        debug.Add(pt1[x]);
                        debug.Add(pt0[x]);
                        continue;
                    }
                    if (loop.IsClosable(0.5))
                        loop.MakeClosed(0.5);

                    //var loft = Brep.CreateFromLoft(segs, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                    //Brep[] outBlends, outWalls;
                    //var ext = Brep.CreateOffsetBrep(loft, thickness, true, true, 0.1, out outBlends, out outWalls)[0];

                    Plane extrusion_plane;
                    loop.TryGetPlane(out extrusion_plane);
                    if (extrusion_plane.ZAxis * this.Plane.ZAxis < 0)
                        loop.Reverse();

                    var ext = Extrusion.Create(loop, LayerThickness, true);
                    if (ext == null)
                        continue;
                    var brep = ext.ToBrep();

                    var bseg = new BlankSegment();
                    bseg.Geometry = brep;
                    bseg.InsideEdge = segs[1];
                    bseg.OutsideEdge = segs[3];
                    bseg.PlaneAtStart = DivisionPlanes[x];
                    bseg.PlaneAtEnd = DivisionPlanes[y];

                    var po0 = DivisionPlanes[x].Origin;
                    var po1 = DivisionPlanes[y].Origin;
                    var xaxis = po1 - po0;
                    var yaxis = Vector3d.CrossProduct(xaxis, Plane.ZAxis);

                    bseg.Handle = new Plane((po0 + po1) * 0.5, xaxis, yaxis);

                    bseg.Transform(Transform.Translation(Plane.ZAxis * zheight));

                    output[i].Add(bseg);
                }
            }
            return output;
        }
    }

    public class BlankSegment
    {
        public Brep Geometry;
        Curve[] m_curves;

        public Curve InsideEdge
        {
            get { return m_curves[0]; }
            set { m_curves[0] = value; }
        }
        public Curve OutsideEdge
        {
            get { return m_curves[1]; }
            set { m_curves[1] = value; }
        }

        public Plane PlaneAtStart;
        public Plane PlaneAtEnd;

        public Plane Handle;

        public double Thickness;
        public double Width;

        public BlankSegment()
        {
            m_curves = new Curve[2];
        }

        public BlankSegment Duplicate()
        {
            var bs = new BlankSegment();
            bs.Geometry = Geometry.DuplicateBrep();
            bs.Width = Width;
            bs.Thickness = Thickness;
            bs.Handle = Handle;

            bs.PlaneAtStart = PlaneAtStart;
            bs.PlaneAtEnd = PlaneAtEnd;

            bs.InsideEdge = InsideEdge.DuplicateCurve();
            bs.OutsideEdge = OutsideEdge.DuplicateCurve();

            return bs;
        }

        public void Transform(Transform xform)
        {
            Handle.Transform(xform);
            m_curves[0].Transform(xform);
            m_curves[1].Transform(xform);

            if (Geometry != null)
                Geometry.Transform(xform);
            PlaneAtStart.Transform(xform);
            PlaneAtEnd.Transform(xform);
        }
    }

    public class SegmentedBlankX
    {
        public static double AngleLimit = RhinoMath.ToRadians(5);
        public static double CentrelineDeviation = 20;

        public List<BlankSegment> Segments;
        public Curve Centreline;
        public Plane Plane;
        public BoundingBox Bounds;
        public int SimpleDivision = 0;
        public string Name = "";

        public List<Plane> DivisionPlanes;

        public double Height = 60;
        public double LayerThickness = 20;

        public Curve[] EdgeCurves;

        public double Offset0;
        public double Offset1;

        private Curve[] m_offsets;
        public Curve InnerOffset
        {
            get { return m_offsets[0]; }
            set { m_offsets[0] = value; }
        }
        public Curve OuterOffset
        {
            get { return m_offsets[1]; }
            set { m_offsets[1] = value; }
        }

        public List<Plane> Planes;
        public List<object> debug;

        public SegmentedBlankX(SegmentedBlankX sb)
        {
            Name = sb.Name;
            Centreline = sb.Centreline;
            Plane = sb.Plane;
            Bounds = sb.Bounds;

            m_offsets = new Curve[2];

            InnerOffset = sb.InnerOffset.DuplicateCurve();
            OuterOffset = sb.OuterOffset.DuplicateCurve();
            Height = sb.Height;
            SimpleDivision = sb.SimpleDivision;
            LayerThickness = sb.LayerThickness;



            if (sb.EdgeCurves != null)
            {
                EdgeCurves = new Curve[sb.EdgeCurves.Length];
                for (int i = 0; i < EdgeCurves.Length; i++)
                    EdgeCurves[i] = sb.EdgeCurves[i].DuplicateCurve();
            }

            if (sb.Segments != null)
            {
                Segments = new List<BlankSegment>();
                for (int i = 0; i < sb.Segments.Count; i++)
                    Segments.Add(sb.Segments[i].Duplicate());
            }

            if (sb.DivisionPlanes != null)
            {
                DivisionPlanes = new List<Plane>(sb.DivisionPlanes);
            }

            if (sb.Planes != null)
            {
                Planes = new List<Plane>(sb.Planes);
            }


        }

        public SegmentedBlankX(Curve centreline, Curve[] edge_curves, Curve[] offset_curves, Plane plane, double height, double layer_thickness)
        {
            if (plane == Plane.Unset)
                if (!Centreline.TryGetPlane(out plane))
                    plane = Plane.WorldXY;

            this.Plane = plane;
            debug = new List<object>();

            // Orient centreline
            int sign = 1;

            if (centreline.TangentAtStart * plane.XAxis > 0)
            {
                centreline.Reverse();
                sign = -1;
            }

            Centreline = centreline;

            Offset0 = 0;
            Offset1 = 0;

            // Inject blank side offsets
            if (offset_curves.Length < 2) throw new Exception("SegmentBlankX requires 2 offset curves!");
            m_offsets = new Curve[2];
            m_offsets[0] = offset_curves[0];
            m_offsets[1] = offset_curves[1];

            var bb_inner = InnerOffset.GetBoundingBox(Plane);
            var bb_outer = OuterOffset.GetBoundingBox(Plane);

            Bounds = BoundingBox.Union(bb_inner, bb_outer);
            var origin = Bounds.Max;
            origin = Plane.PointAt(origin.X, origin.Y, origin.Z);

            Bounds.Transform(Rhino.Geometry.Transform.Translation(new Vector3d(-Bounds.Max)));

            this.Plane.Origin = origin;

            EdgeCurves = edge_curves;
            DivisionPlanes = new List<Plane>();
            Height = height;
            LayerThickness = layer_thickness;
        }
        public SegmentedBlankX(Curve centreline, Curve[] edge_curves, Plane plane, double width0, double width1, double height, double layer_thickness)
        {
            if (plane == Plane.Unset)
                if (!Centreline.TryGetPlane(out plane))
                    plane = Plane.WorldXY;

            this.Plane = plane;
            debug = new List<object>();
            //debug.Add(centreline.PointAtStart);
            //debug.Add(new Line(centreline.PointAtStart, centreline.TangentAtStart * 200));

            // Orient centreline
            int sign = 1;

            if (centreline.TangentAtStart * plane.XAxis > 0)
            {
                centreline.Reverse();
                sign = -1;
            }

            Centreline = centreline;

            Offset0 = width0;
            Offset1 = width1;

            // Make blank side offsets
            m_offsets = new Curve[2];
            m_offsets[0] = Centreline.Offset(Plane, width0 * sign, 0.1, CurveOffsetCornerStyle.None)[0];
            m_offsets[1] = Centreline.Offset(Plane, width1 * -sign, 0.1, CurveOffsetCornerStyle.None)[0];

            var bb_inner = InnerOffset.GetBoundingBox(Plane);
            var bb_outer = OuterOffset.GetBoundingBox(Plane);

            Bounds = BoundingBox.Union(bb_inner, bb_outer);
            var origin = Plane.PointAt(Bounds.Max.X, Bounds.Max.Y, Bounds.Max.Z);

            Bounds.Transform(Rhino.Geometry.Transform.Translation(new Vector3d(-Bounds.Max)));

            this.Plane.Origin = origin;

            EdgeCurves = edge_curves;
            DivisionPlanes = new List<Plane>();
            Height = height;
            LayerThickness = layer_thickness;
        }

        public void CreateDivisionPlanes(IList<double> tt)
        {
            DivisionPlanes = tt.Select(x => new Plane(Centreline.PointAt(x), Plane.ZAxis, Vector3d.CrossProduct(Centreline.TangentAt(x), Plane.ZAxis))).ToList();

            /*
            if (Math.Abs(tt[0] - Centreline.Domain.Min) > 0.1)
              DivisionPlanes.Insert(0, new Plane(Centreline.PointAtStart, Centreline.TangentAtStart));
            if (Math.Abs(tt[tt.Count - 1] - Centreline.Domain.Max) > 0.1)
              DivisionPlanes.Add(new Plane(Centreline.PointAtEnd, Centreline.TangentAtEnd));
            */
        }

        public void TrimOffsetsToDivisionPlanes()
        {
            var res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, DivisionPlanes.First(), 0.01);
            var res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, DivisionPlanes.Last(), 0.01);
            if (res0 != null && res0.Count > 0 && res1 != null && res1.Count > 0)
            {
                InnerOffset = InnerOffset.Trim(res0[0].ParameterA, res1[0].ParameterA);
            }

            res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, DivisionPlanes.First(), 0.01);
            res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, DivisionPlanes.Last(), 0.01);
            if (res0 != null && res0.Count > 0 && res1 != null && res1.Count > 0)
            {
                OuterOffset = OuterOffset.Trim(res0[0].ParameterA, res1[0].ParameterA);
            }

            var bb_inner = InnerOffset.GetBoundingBox(Plane);
            var bb_outer = OuterOffset.GetBoundingBox(Plane);

            Bounds = BoundingBox.Union(bb_inner, bb_outer);
            var origin = Plane.PointAt(Bounds.Max.X, Bounds.Max.Y, Bounds.Max.Z);

            Bounds.Transform(Rhino.Geometry.Transform.Translation(new Vector3d(-Bounds.Max)));

            this.Plane.Origin = origin;
        }

        public List<double> SegmentCurve(double min, double max, double angle)
        {
            var divs = new List<double>();

            var length = Centreline.GetLength();
            var N = (int)Math.Ceiling(length / 3.0);

            if (length < max * 2)
            {
                max = length / 3;
            }

            var tt = Centreline.DivideByCount(N, true);

            double finalT, firstT;

            Centreline.LengthParameter(length - min, out finalT);
            Centreline.LengthParameter(min, out firstT);

            double lastT = firstT;

            divs.Add(tt[0]);

            Vector3d lastVector = Centreline.TangentAtStart;

            divs.Add(firstT);

            for (int i = 1; i < tt.Length; ++i)
            {
                if (tt[i] < firstT) continue;

                var tan = Centreline.TangentAt(tt[i]);

                var segLength = Centreline.GetLength(new Interval(lastT, tt[i]));
                if (segLength > max || // if the maximum segment length is reached
                  (Vector3d.VectorAngle(tan, lastVector) > angle * 0.5 && segLength > min * 0.5) && tt[i] > firstT)
                {
                    divs.Add(tt[i - 1]);
                    lastT = tt[i - 1];
                    lastVector = Centreline.TangentAt(tt[i - 1]);
                    i--;
                }

                if (tt[i] >= finalT)
                {
                    if (Vector3d.VectorAngle(lastVector, Centreline.TangentAt(tt[tt.Length - 1])) > angle)
                        divs.Add((tt[i - 1] + tt[tt.Length - 1]) * 0.5);

                    divs.Add(finalT);

                    divs.Add(tt[tt.Length - 1]);
                    break;
                }
            }

            return divs;
        }

        public List<double> SegmentCentreline(double min, double max, bool alternate = false)
        {
            var tlist = new List<double>();
            double length = Centreline.GetLength();

            if (Centreline.IsLinear())
            {
                int N = (int)Math.Ceiling(length / (max * 0.5));
                var tt = Centreline.DivideByCount(N, true);

                tlist.AddRange(tt);
            }
            else
            {
                var width = Math.Max(Offset0, Offset1) * 2;

                //var polyline = Centreline.ToPolyline(CentrelineDeviation, AngleLimit * 0.5, min, max * 0.5).ToPolyline();
                //var polyline = Centreline.ToPolyline(0, 0, AngleLimit, Math.Tan(AngleLimit / 10), 0, CentrelineDeviation, min, max * 0.5, true).ToPolyline();
                var polyline = Centreline.ToPolyline(0, 0, AngleLimit, 100, 0, CentrelineDeviation, min * 0.5, max * 0.5, true).ToPolyline();
                polyline.CollapseShortSegments(min);

                double t;
                foreach (var pt in polyline)
                {
                    Centreline.ClosestPoint(pt, out t);
                    tlist.Add(t);
                }
            }

            return tlist;
        }

        public List<double> SegmentCentreline2b(double min, double max, bool alternate = false)
        {
            if (Centreline.IsLinear())
            {
                List<double> tlist = new List<double>();

                double length = Centreline.GetLength();
                int N = (int)Math.Ceiling(length / max) * 2;
                var tt = Centreline.DivideByCount(N, false);

                var flag = alternate;
                tlist.Add(Centreline.Domain.Min);

                foreach (double t in tt)
                {
                    if (flag || true)
                        tlist.Add(t);
                    flag = !flag;
                }

                tlist.Add(Centreline.Domain.Max);

                return tlist;
            }
            var polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), 0, max).ToPolyline();
            polyline.CollapseShortSegments(min);

            if (!alternate || true)
            {

                double[] tt = new double[polyline.Count * 2 - 1];
                var psegs = polyline.GetSegments();

                //for (int k = 0; k < polyline.Count; ++k)
                ////{
                //  Centreline.ClosestPoint(polyline[k], out tt[k]);
                //}

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From, out tt[k * 2]);
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt[k * 2 + 1]);
                }

                tt[tt.Length - 1] = Centreline.Domain.Max;

                return tt.ToList();
            }
            else
            {

                var psegs = polyline.GetSegments();
                double[] tt2 = new double[polyline.SegmentCount + 2];

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt2[k + 1]);
                }

                tt2[0] = Centreline.Domain.Min;
                tt2[tt2.Length - 1] = Centreline.Domain.Max;

                return tt2.ToList();
            }
        }

        public List<double> SegmentCentreline2(double min, double max, bool alternate = false, bool old_method = false)
        {
            if (Centreline.IsLinear(0.001) && false)
            {
                List<double> tlist = new List<double>();

                double length = Centreline.GetLength();
                int N = (int)Math.Ceiling(length / max) * 2;
                var tt = Centreline.DivideByCount(N, false);

                var flag = alternate;
                tlist.Add(Centreline.Domain.Min);

                foreach (double t in tt)
                {
                    if (flag || true)
                        tlist.Add(t);
                    flag = !flag;
                }

                tlist.Add(Centreline.Domain.Max);

                return tlist;
            }
            Polyline polyline;
            if (old_method)
                polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), 0, max).ToPolyline();
            else
                polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), min, max).ToPolyline();
            //polyline = Centreline.ToPolyline(1.0, RhinoMath.ToRadians(0.1), 0, max).ToPolyline();

            polyline.CollapseShortSegments(min * 0.5);


            if (!alternate || true)
            {

                double[] tt = new double[polyline.Count * 2 - 1];
                var psegs = polyline.GetSegments();

                //for (int k = 0; k < polyline.Count; ++k)
                ////{
                //  Centreline.ClosestPoint(polyline[k], out tt[k]);
                //}

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From, out tt[k * 2]);
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt[k * 2 + 1]);
                }

                tt[tt.Length - 1] = Centreline.Domain.Max;

                return tt.ToList();
            }
            else
            {

                var psegs = polyline.GetSegments();
                double[] tt2 = new double[polyline.SegmentCount + 2];

                for (int k = 0; k < polyline.SegmentCount; ++k)
                {
                    Centreline.ClosestPoint(psegs[k].From + psegs[k].Direction * 0.5, out tt2[k + 1]);
                }

                tt2[0] = Centreline.Domain.Min;
                tt2[tt2.Length - 1] = Centreline.Domain.Max;

                return tt2.ToList();
            }
        }

        public List<double> SegmentCurve3(double min, double max, double angle)
        {
            Curve c = Centreline;
            Plane plane;
            c.TryGetPlane(out plane);

            int N = 100;

            var tlist = new List<double>();

            var tt = c.DivideByCount(N, true);

            tlist.Add(tt[0]);

            Vector3d tmp = c.TangentAt(tt[0]);
            for (int i = 0; i < tt.Length; ++i)
            {
                var vec = c.TangentAt(tt[i]);
                if (Vector3d.VectorAngle(vec, tmp) > angle)
                {
                    tlist.Add(tt[i - 1]);
                    tmp = vec;
                }
            }

            tlist.Add(tt[tt.Length - 1]);

            var doms = new List<Interval>();

            for (int i = 0; i < tlist.Count - 1; ++i)
            {
                var dom = new Interval(tlist[i], tlist[i + 1]);
                doms.Add(dom);
            }

            for (int i = 0; i < doms.Count; ++i)
            {
                var length = c.GetLength(doms[i]);

                if (length > max)
                {
                    int ndiv = (int)Math.Ceiling(length / max);
                    for (int j = 0; j < ndiv; ++j)
                    {
                        //tlist.Insert(i, doms[i].Mid);
                        tlist.Insert(i, doms[i].Min + doms[i].Length / ndiv * j);

                    }
                }
            }

            tlist.Sort();

            return tlist;

        }

        public List<Line> CreateDivisionLines()
        {
            if (InnerOffset == null || OuterOffset == null) throw new Exception("SegmentedBlank: Offsets not defined.");

            var lines = new List<Line>();

            try
            {
                foreach (Plane plane in DivisionPlanes)
                {
                    if (!plane.IsValid) continue;
                    var res0 = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, plane, 0.1);
                    var res1 = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, plane, 0.1);

                    var line = new Line(res0[0].PointA, res1[0].PointA);

                    //var line = new Line(plane.PointAt(0, Offset0), plane.PointAt(0, -Offset1));

                    lines.Add(line);
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("{0}: {1}", Name, e.Message));
            }

            return lines;
        }

        public List<List<BlankSegment>> CreateSegments()
        {

            // Simple output list of geometry
            var output = new List<List<BlankSegment>>();

            int Nlayers = (int)(Height / LayerThickness);

            int N = DivisionPlanes.Count;

            if (N < 2) throw new Exception("No division planes!");


            var tt0 = new double[N];
            var tt1 = new double[N];
            var pt0 = new Point3d[N];
            var pt1 = new Point3d[N];

            tt0[0] = m_offsets[0].Domain.Min; tt0[N - 1] = m_offsets[0].Domain.Max;
            tt1[0] = m_offsets[1].Domain.Min; tt1[N - 1] = m_offsets[1].Domain.Max;
            pt0[0] = m_offsets[0].PointAtStart; pt0[N - 1] = m_offsets[0].PointAtEnd;
            pt1[0] = m_offsets[1].PointAtStart; pt1[N - 1] = m_offsets[1].PointAtEnd;



            for (int i = 0; i < Nlayers; ++i)
            {
                double zheight = -Height * 0.5 + i * LayerThickness;

                output.Add(new List<BlankSegment>());

                int mod = i.Modulus(2);

                for (int j = 1; j < DivisionPlanes.Count - 1; ++j)
                {
                    var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(m_offsets[0], DivisionPlanes[j], 0.1);
                    tt0[j] = res[0].ParameterA;
                    pt0[j] = res[0].PointA;

                    res = Rhino.Geometry.Intersect.Intersection.CurvePlane(m_offsets[1], DivisionPlanes[j], 0.1);
                    tt1[j] = res[0].ParameterA;
                    pt1[j] = res[0].PointA;

                }

                Curve[] segs;
                for (int j = -mod; j < N - 1; j += 2)
                {
                    var x = Math.Max(j, 0);
                    var y = Math.Min(j + 2, N - 1);

                    if (x == y) continue;

                    segs = new Curve[4];
                    segs[0] = new Line(pt1[x], pt0[x]).ToNurbsCurve();
                    segs[1] = m_offsets[0].Trim(tt0[x], tt0[y]);
                    segs[2] = new Line(pt0[y], pt1[y]).ToNurbsCurve();
                    segs[3] = m_offsets[1].Trim(tt1[x], tt1[y]);

                    if (segs[1] == null || segs[3] == null)
                    {
                        debug.Add(DivisionPlanes[x]);
                        debug.Add(DivisionPlanes[y]);
                        debug.AddRange(segs);
                        debug.Add(pt1[x]);
                        debug.Add(pt0[x]);
                        debug.Add(pt1[y]);
                        debug.Add(pt0[y]);
                        continue;
                    }

                    var loop = Curve.JoinCurves(segs)[0];
                    if (loop == null)
                    {
                        debug.AddRange(segs);
                        debug.Add(pt1[x]);
                        debug.Add(pt0[x]);

                        continue;
                    }
                    if (loop.IsClosable(0.5))
                        loop.MakeClosed(0.5);

                    //debug.Add(loop);
                    //debug.Add(loop.IsClosed);

                    //var loft = Brep.CreateFromLoft(segs, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
                    //Brep[] outBlends, outWalls;
                    //var ext = Brep.CreateOffsetBrep(loft, thickness, true, true, 0.1, out outBlends, out outWalls)[0];

                    Plane extrusion_plane;
                    loop.TryGetPlane(out extrusion_plane);
                    if (extrusion_plane.ZAxis * this.Plane.ZAxis < 0)
                        loop.Reverse();

                    var ext = Extrusion.Create(loop, LayerThickness, true);
                    if (ext == null)
                    {
                        //throw new Exception("Fucked");
                        continue;
                    }
                    var brep = ext.ToBrep();

                    var bseg = new BlankSegment();
                    bseg.Geometry = brep;
                    bseg.InsideEdge = segs[1];
                    bseg.OutsideEdge = segs[3];
                    bseg.PlaneAtStart = DivisionPlanes[x];
                    bseg.PlaneAtEnd = DivisionPlanes[y];

                    var po0 = DivisionPlanes[x].Origin;
                    var po1 = DivisionPlanes[y].Origin;
                    var xaxis = po1 - po0;
                    var yaxis = Vector3d.CrossProduct(xaxis, Plane.ZAxis);

                    bseg.Handle = new Plane((po0 + po1) * 0.5, xaxis, yaxis);

                    bseg.Transform(Rhino.Geometry.Transform.Translation(Plane.ZAxis * zheight));

                    output[i].Add(bseg);
                }
            }

            return output;
        }

        public void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Centreline.Transform(xform);

            InnerOffset.Transform(xform);
            OuterOffset.Transform(xform);

            if (DivisionPlanes != null)
                for (int i = 0; i < DivisionPlanes.Count; ++i)
                {
                    var temp = DivisionPlanes[i];
                    temp.Transform(xform);

                    DivisionPlanes[i] = temp;
                }

            if (Planes != null)
                for (int i = 0; i < Planes.Count; ++i)
                {
                    var temp = Planes[i];
                    temp.Transform(xform);

                    Planes[i] = temp;
                }

            if (Segments != null)
                for (int i = 0; i < Segments.Count; ++i)
                    Segments[i].Transform(xform);

            if (EdgeCurves != null)
                for (int i = 0; i < EdgeCurves.Length; ++i)
                    EdgeCurves[i].Transform(xform);
        }

        public SegmentedBlankX Duplicate()
        {
            return new SegmentedBlankX(this);
        }
    }

    public class CixHelper
    {
        public Dictionary<string, double> Variables;
        public CixHelper()
        {
        }

        public void Load(string path)
        {
            Variables = new Dictionary<string, double>();

            var lines = System.IO.File.ReadAllLines(path);


            for (int i = 0; i < lines.Length; ++i)
            {
                var line = lines[i].Trim();
                var tok = line.Split('=');
                if (tok.Length < 2) continue;

                Variables[tok[0]] = double.Parse(tok[1]);
            }
        }

        public List<Line> GetSegmentLines()
        {
            var lines = new List<Line>();

            int N = (int)Variables["SEC_N"];

            double x0, x1, y0, y1;

            x0 = Variables["BL_E_1_IN_X"];
            y0 = Variables["BL_E_1_IN_Y"];

            x1 = Variables["BL_E_1_OUT_X"];
            y1 = Variables["BL_E_1_OUT_Y"];

            lines.Add(new Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0)));

            for (int i = 1; i < N + 1; ++i)
            {
                x0 = Variables[string.Format("BL_SEC_{0}_{1}_IN_X", i, i + 1)];
                y0 = Variables[string.Format("BL_SEC_{0}_{1}_IN_Y", i, i + 1)];

                x1 = Variables[string.Format("BL_SEC_{0}_{1}_OUT_X", i, i + 1)];
                y1 = Variables[string.Format("BL_SEC_{0}_{1}_OUT_Y", i, i + 1)];

                var line = new Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0));
                if (line.IsValid)
                    lines.Add(line);
            }

            x0 = Variables["BL_E_2_IN_X"];
            y0 = Variables["BL_E_2_IN_Y"];

            x1 = Variables["BL_E_2_OUT_X"];
            y1 = Variables["BL_E_2_OUT_Y"];

            lines.Add(new Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0)));

            return lines;
        }

        public Line[] GetEndLines()
        {
            var e1 = new Line(
                new Point3d(Variables["BL_SEC_E_1_SEC_1_IN_X"], Variables["BL_SEC_E_1_SEC_1_IN_Y"], 0),
                new Point3d(Variables["BL_SEC_E_1_SEC_1_OUT_X"], Variables["BL_SEC_E_1_SEC_1_OUT_Y"], 0));

            var e2 = new Line(
                 new Point3d(Variables["BL_SEC_E_2_SEC_N_IN_X"], Variables["BL_SEC_E_2_SEC_N_IN_Y"], 0),
                 new Point3d(Variables["BL_SEC_E_2_SEC_N_OUT_X"], Variables["BL_SEC_E_2_SEC_N_OUT_Y"], 0));

            return new Line[] { e1, e2 };
        }

        public Curve[] GetBlankOffsets(int num_divs=45)
        {
            int N = num_divs;

            var inList = new Point3d[N];
            var outList = new Point3d[N];

            for (int i = 0; i < N; ++i)
            {
                double ix, iy, ox, oy;
                ix = Variables[string.Format("BL_IN_CURVE_P_{0}_X", i + 1)];
                iy = Variables[string.Format("BL_IN_CURVE_P_{0}_Y", i + 1)];
                inList[i] = new Point3d(ix, iy, 0);

                ox = Variables[string.Format("BL_OUT_CURVE_P_{0}_X", i + 1)];
                oy = Variables[string.Format("BL_OUT_CURVE_P_{0}_Y", i + 1)];
                outList[i] = new Point3d(ox, oy, 0);
            }

            var blank_offsets = new Curve[2];
            //blank_offsets[0] = Curve.CreateControlPointCurve(inList, 3);
            //blank_offsets[1] = Curve.CreateControlPointCurve(outList, 3); 
            
            blank_offsets[0] = Curve.CreateInterpolatedCurve(inList, 3);
            blank_offsets[1] = Curve.CreateInterpolatedCurve(outList, 3);
            return blank_offsets;
        }

        public Point3d GetOrigin()
        {
            return new Point3d(
                Variables["ORIGO_X"], Variables["ORIGO_Y"], 0);
        }

        public Curve[] GetEdgeCurves(int num_divs=25)
        {
            var prefixes = new string[] { "TOP_OUT_SPL_P_", "BOTTOM_OUT_SPL_P_", "BOTTOM_IN_SPL_P_", "TOP_IN_SPL_P_" };
            int N = num_divs;

            var points = new Point3d[N];
            var curves = new Curve[prefixes.Length];

            double x, y, z;

            for (int i = 0; i < prefixes.Length; ++i)
            {
                for (int j = 0; j < N; ++j)
                {
                    Variables.TryGetValue(string.Format("{0}{1}_X", prefixes[i], j + 1), out x);
                    Variables.TryGetValue(string.Format("{0}{1}_Y", prefixes[i], j + 1), out y);
                    Variables.TryGetValue(string.Format("{0}{1}_Z", prefixes[i], j + 1), out z);

                    points[j] = new Point3d(x, y, -z);
                }

                //curves[i] = Curve.CreateControlPointCurve(points, 3);
                curves[i] = Curve.CreateInterpolatedCurve(points, 3);
            }

            return curves;
        }

        public Point3d GetBounds()
        {
            return new Point3d(
                Variables["BL_L"], Variables["BL_W"], 0);
        }

        public bool GetCleanCuts(out List<Line> lines)
        {
            double x0, x1, y0, y1;

            lines = new List<Line> { Line.Unset, Line.Unset };
            if (Variables.ContainsKey("E_1_RENSKAER_PKT_1_X"))
            {
                x0 = Variables["E_1_RENSKAER_PKT_1_X"];
                y0 = Variables["E_1_RENSKAER_PKT_1_Y"];
                x1 = Variables["E_1_RENSKAER_PKT_2_X"];
                y1 = Variables["E_1_RENSKAER_PKT_2_Y"];

                lines[0] = new Line(
                    new Point3d(x0, y0, 0), 
                    new Point3d(x1, y1, 0));
            }


            if (Variables.ContainsKey("E_2_RENSKAER_PKT_1_X"))
            {
                x0 = Variables["E_2_RENSKAER_PKT_1_X"];
                y0 = Variables["E_2_RENSKAER_PKT_1_Y"];
                x1 = Variables["E_2_RENSKAER_PKT_2_X"];
                y1 = Variables["E_2_RENSKAER_PKT_2_Y"];

                lines[1] = new Line(
                    new Point3d(x0, y0, 0),
                    new Point3d(x1, y1, 0));
            }
            return true;
        }

        public bool GetCrossCuts(out List<Plane> crosscuts)
        {
            double x0, x1, y0, y1, z0, z1, alpha;

            crosscuts = new List<Plane>();

            for (int e = 1; e <= 2; ++e)
            {
                for (int i = 1; i <= 2; ++i)
                {
                    if (Variables.ContainsKey(string.Format("E_{0}_CUT_{1}", e, i)))
                    {
                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_1_X", e, i), out x0);
                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_1_Y", e, i), out y0);
                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_1_Z", e, i), out z0);

                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_2_X", e, i), out x1);
                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_2_Y", e, i), out y1);
                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_LINE_PKT_2_Z", e, i), out z1);

                        Variables.TryGetValue(string.Format("E_{0}_CUT_{1}_ALFA", e, i), out alpha);
                        alpha = RhinoMath.ToRadians(alpha);

                        var pt0 = new Point3d(x0, y0, -z0);
                        var pt1 = new Point3d(x1, y1, -z1);

                        var xaxis = new Vector3d(pt1 - pt0);
                        var yaxis = Vector3d.ZAxis;

                        yaxis.Transform(Transform.Rotation(-alpha, xaxis, pt0));

                        crosscuts.Add(new Plane(pt0, xaxis, yaxis));
                    }
                }
            }

            return true;
        }

        public bool GetEndDrillings(out List<Plane> planes, out List<Line> axes, out List<Circle> outlines)
        {
            double x0, x1, y0, y1, z0, z1, alpha;

            axes = new List<Line>();
            planes = new List<Plane>();
            outlines = new List<Circle>();

            for (int e = 1; e <= 2; ++e)
            {
                for (int i = 1; i <= 2; ++i)
                {
                    if (Variables.ContainsKey(string.Format("E_{0}_HUL_{1}", e, i)))
                    {
                        double active;
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}", e, i), out active);
                        if ((int)active == 0) continue;

                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_1_X", e, i), out x0);
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_1_Y", e, i), out y0);
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_1_Z", e, i), out z0);

                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_2_X", e, i), out x1);
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_2_Y", e, i), out y1);
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_PKT_2_Z", e, i), out z1);


                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_PL_ALFA", e, i), out alpha);
                        alpha = RhinoMath.ToRadians(alpha);

                        var pt0 = new Point3d(x0, y0, z0);
                        var pt1 = new Point3d(x1, y1, z1);

                        var xaxis = new Vector3d(pt1 - pt0);
                        var yaxis = -Vector3d.ZAxis;

                        yaxis.Transform(Transform.Rotation(-alpha, xaxis, pt0));
                        var plane = new Plane(pt0, xaxis, yaxis);

                        planes.Add(plane);


                        double num_holes;
                        Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_N", e, i), out num_holes);
                        int N = (int)num_holes;

                        for (int j = 1; j <= N; ++j)
                        {
                            bool success = true;

                            double hx, hy, hdia, hdepth;
                            success &= Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_{2}_X", e, i, j), out hx);
                            success &= Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_{2}_Y", e, i, j), out hy);
                            success &= Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_{2}_DIA", e, i, j), out hdia);
                            success &= Variables.TryGetValue(string.Format("E_{0}_HUL_{1}_{2}_DYBDE", e, i, j), out hdepth);

                            if (!success) throw new Exception(string.Format("Shit is fucked: hx {0:0.##} hy {1:0.##} hdia {2:0.##} hdepth {3:0.##}", hx, hy, hdia, hdepth));

                            //var hpt = new Point3d(hx, hy, 0);
                            var hpt = plane.PointAt(hx, hy);

                            var axis = new Line(hpt, plane.ZAxis * hdepth);
                            var circle = new Circle(new Plane(hpt, plane.XAxis, plane.YAxis), hdia * 0.5);

                            axes.Add(axis);
                            outlines.Add(circle);
                        }
                    }
                }
            }

            return true;
        }

        public bool GetTopDrillings(out List<Plane> planes, out List<Line> axes, out List<Circle> outlines)
        {
            double x0, x1, y0, y1, z0, z1, alpha;

            axes = new List<Line>();
            planes = new List<Plane>();
            outlines = new List<Circle>();

            for (int i = 1; i <= 2; ++i)
            {
                if (Variables.ContainsKey(string.Format("TOP_HUL_{0}", i)))
                {
                    double active;
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}", i), out active);
                    if ((int)active == 0) continue;

                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_1_X", i), out x0);
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_1_Y", i), out y0);
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_1_Z", i), out z0);

                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_2_X", i), out x1);
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_2_Y", i), out y1);
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_PKT_2_Z", i), out z1);


                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_PL_ALFA", i), out alpha);
                    alpha = RhinoMath.ToRadians(alpha);

                    var pt0 = new Point3d(x0, y0, z0);
                    var pt1 = new Point3d(x1, y1, z1);

                    var xaxis = new Vector3d(pt1 - pt0);
                    var yaxis = -Vector3d.ZAxis;

                    yaxis.Transform(Transform.Rotation(-alpha, xaxis, pt0));
                    var plane = new Plane(pt0, xaxis, yaxis);

                    planes.Add(plane);


                    double num_holes;
                    Variables.TryGetValue(string.Format("TOP_HUL_{0}_N", i), out num_holes);
                    int N = (int)num_holes;
                    for (int j = 1; j <= N; ++j)
                    {
                        double hx, hy, hdia, hdepth;
                        Variables.TryGetValue(string.Format("TOP_HUL_{0}_{1}_X", i, j), out hx);
                        Variables.TryGetValue(string.Format("TOP_HUL_{0}_{1}_Y", i, j), out hy);
                        Variables.TryGetValue(string.Format("TOP_HUL_{0}_{1}_DIA", i, j), out hdia);
                        Variables.TryGetValue(string.Format("TOP_HUL_{0}_{1}_DYBDE", i, j), out hdepth);

                        //var hpt = new Point3d(hx, hy, 0);
                        var hpt = plane.PointAt(hx, hy);

                        var axis = new Line(hpt, plane.ZAxis * hdepth);
                        var circle = new Circle(new Plane(hpt, plane.XAxis, plane.YAxis), hdia * 0.5);

                        axes.Add(axis);
                        outlines.Add(circle);
                    }
                }
            }

            return true;
        }

        public bool GetTopDowelHoles(out List<Line> axes, out List<Circle> outlines)
        {
            double x0, y0;
            double diameter = 0;
            double depth = 0;

            axes = new List<Line>();
            outlines = new List<Circle>();

            if (Variables.ContainsKey("TOP_DYVELHUL_DIA"))
                diameter = Variables["TOP_DYVELHUL_DIA"];

            if (Variables.ContainsKey("TOP_DYVELHUL_DYBDE"))
                depth = Variables["TOP_DYVELHUL_DYBDE"];

            for (int e = 1; e <= 2; ++e)
            {

                if (Variables.ContainsKey(string.Format("TOP_DYVELHUL_E_{0}", e)))
                {
                    int active = (int)Variables[string.Format("TOP_DYVELHUL_E_{0}", e)];
                    int num_holes = (int)Variables[string.Format("TOP_DYVELHUL_E_{0}_N", e)];

                    for (int i = 1; i <= num_holes; ++i)
                    {
                        x0 = Variables[string.Format("TOP_DYVELHUL_E_{0}_HUL_{1}_X", e, i)];
                        y0 = Variables[string.Format("TOP_DYVELHUL_E_{0}_HUL_{1}_Y", e, i)];

                        axes.Add(new Line(new Point3d(x0, y0, 0), Vector3d.ZAxis * -depth));
                        outlines.Add(new Circle(new Point3d(x0, y0, 0), diameter * 0.5));
                    }
                }
            }

            return true;
        }

    }

}
