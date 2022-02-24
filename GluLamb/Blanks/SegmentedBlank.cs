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
}
