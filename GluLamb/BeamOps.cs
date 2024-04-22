using Rhino;
using Rhino.Geometry;
using Rhino.Render.ChangeQueue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public static class BeamOps
    {
        public static void GenerateCrossSectionPlanes(Beam beam, int N, out Plane[] frames, out double[] parameters, GlulamData.Interpolation interpolation = GlulamData.Interpolation.LINEAR)
        {
            Curve curve = beam.Centreline;

            double multiplier = RhinoMath.UnitScale(UnitSystem.Millimeters, Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);

            //PolylineCurve discrete = curve.ToPolyline(Glulam.Tolerance * 10, Glulam.AngleTolerance, 0.0, 0.0);
            PolylineCurve discrete = curve.ToPolyline(
                multiplier * Globals.Tolerance, 
                Globals.AngleTolerance, 
                multiplier * Globals.MininumSegmentLength, 
                curve.GetLength() / Globals.MinimumNumSegments);

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

            var vectors = beam.Orientation.GetOrientations(curve, parameters);

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

        public static Curve CreateOffsetCurve(Beam beam, double x, double y, bool rebuild = false, int rebuild_pts = 20)
        {
            List<Point3d> pts = new List<Point3d>();

            int N = Math.Max(6, 40);

            GenerateCrossSectionPlanes(beam, N, out Plane[] planes, out double[] parameters, GlulamData.Interpolation.LINEAR);

            for (int i = 0; i < planes.Length; ++i)
            {
                Plane p = planes[i];
                pts.Add(p.Origin + p.XAxis * x + p.YAxis * y);
            }

            Curve new_curve = Curve.CreateInterpolatedCurve(pts, 3, CurveKnotStyle.Uniform,
                beam.Centreline.TangentAtStart, beam.Centreline.TangentAtEnd);

            if (new_curve == null)
                throw new Exception("Beam::CreateOffsetCurve:: Failed to create interpolated curve!");

            double len = new_curve.GetLength();
            new_curve.Domain = new Interval(0.0, len);

            if (rebuild)
                new_curve = new_curve.Rebuild(rebuild_pts, new_curve.Degree, true);

            return new_curve;
        }

        public static Brep GetFace(Beam beam, Side side)
        {
            Plane[] planes;
            double[] parameters;

            int N = Math.Max(40, 6); // At some point I need to fold Samples into Beam

            GenerateCrossSectionPlanes(beam, N, out planes, out parameters, GlulamData.Interpolation.LINEAR);

            double hWidth = beam.Width / 2;
            double hHeight = beam.Height / 2;
            double x1, y1, x2, y2;
            x1 = y1 = x2 = y2 = 0;
            Rectangle3d face;

            // beam.GetSectionOffset(out double offsetX, out double offsetY);
            double offsetX = beam.OffsetX, offsetY = beam.OffsetY;

            switch (side)
            {
                case (Side.Back):
                    face = new Rectangle3d(planes.First(), new Interval(-hWidth + offsetX, hWidth + offsetX), new Interval(-hHeight + offsetY, hHeight + offsetY));
                    return Brep.CreateFromCornerPoints(face.Corner(0), face.Corner(1), face.Corner(2), face.Corner(3), 0.001);
                case (Side.Front):
                    face = new Rectangle3d(planes.Last(), new Interval(-hWidth + offsetX, hWidth + offsetX), new Interval(-hHeight + offsetY, hHeight + offsetY));
                    return Brep.CreateFromCornerPoints(face.Corner(0), face.Corner(1), face.Corner(2), face.Corner(3), 0.001);
                case (Side.Left):
                    x1 = hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
                case (Side.Right):
                    x1 = -hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
                case (Side.Top):
                    x1 = hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = hHeight + offsetY;
                    break;
                case (Side.Bottom):
                    x1 = hWidth + offsetX; y1 = -hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
            }

            Curve[] rules = new Curve[parameters.Length];
            for (int i = 0; i < planes.Length; ++i)
                rules[i] = new Line(
                    planes[i].Origin + planes[i].XAxis * x1 + planes[i].YAxis * y1,
                    planes[i].Origin + planes[i].XAxis * x2 + planes[i].YAxis * y2
                    ).ToNurbsCurve();

            Brep[] loft = Brep.CreateFromLoft(rules, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (loft == null || loft.Length < 1) throw new Exception("Beam::GetBeamFace::Loft failed!");

            Brep brep = loft[0];

            return brep;
        }

        public static Brep[] GetFaces(Beam beam, int mask)
        {
            bool[] flags = new bool[6];
            List<Brep> breps = new List<Brep>();

            for (int i = 0; i < 6; ++i)
            {
                if ((mask & (1 << i)) > 0)
                    breps.Add(GetFace(beam, (Side)(1 << i)));
            }

            return breps.ToArray();
        }

        public static Point3d[] GenerateCorners(Beam beam, double offset = 0.0)
        {
            return new Point3d[]
            {
                new Point3d(-beam.Width / 2 + beam.OffsetX, -beam.Height / 2, 0),
                new Point3d(-beam.Width / 2 + beam.OffsetX, beam.Height / 2, 0),
                new Point3d(beam.Width / 2 + beam.OffsetX, beam.Height / 2, 0),
                new Point3d(beam.Width / 2 + beam.OffsetX, -beam.Height / 2, 0)
            };
            double x = beam.Width;
            double y = beam.Height;

            double x0 = 0, x1 = x, y0 = 0, y1 = y;
            double hx = x / 2, hy = y / 2;

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

        public static Brep GetSideSurface(Beam beam, int side, double offset, double width, double extension = 0.0, bool flip = false)
        {
            // TODO: Create access for Glulam ends, with offset (either straight or along Centreline).

            side = side.Modulus(2);
            double w2 = width / 2;

            Curve c = beam.Centreline.DuplicateCurve();
            if (extension > 0.0)
                c = c.Extend(CurveEnd.Both, extension, CurveExtensionStyle.Smooth);

            int N = Math.Max(6, Globals.CurvatureSamples);
                
            BeamOps.GenerateCrossSectionPlanes(beam, N, out Plane[] planes, out double[] parameters, GlulamData.Interpolation.LINEAR);

            Curve[] rules = new Curve[planes.Length];

            for (int i = 0; i < planes.Length; ++i)
            {
                Plane p = planes[i];
                if (side == 0)
                    rules[i] = new Line(
                        p.Origin + p.XAxis * (offset + beam.OffsetX) + p.YAxis * (w2 + beam.OffsetY),
                        p.Origin + p.XAxis * (offset + beam.OffsetX) - p.YAxis * (w2 - beam.OffsetY)
                        ).ToNurbsCurve();
                else
                    rules[i] = new Line(
                        p.Origin + p.YAxis * (offset + beam.OffsetY) + p.XAxis * (w2 + beam.OffsetX),
                        p.Origin + p.YAxis * (offset + beam.OffsetY) - p.XAxis * (w2 - beam.OffsetX)
                        ).ToNurbsCurve();

            }

            Brep[] loft = Brep.CreateFromLoft(rules, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (loft == null || loft.Length < 1) throw new Exception("Glulam::GetSideSurface::Loft failed!");

            Brep brep = loft[0];

            Point3d pt = brep.Faces[0].PointAt(brep.Faces[0].Domain(0).Mid, brep.Faces[0].Domain(1).Mid);
            Vector3d nor = brep.Faces[0].NormalAt(brep.Faces[0].Domain(0).Mid, brep.Faces[0].Domain(1).Mid);

            double ct;
            beam.Centreline.ClosestPoint(pt, out ct);
            Vector3d nor2 = beam.Centreline.PointAt(ct) - pt;
            nor2.Unitize();

            if (nor2 * nor < 0.0)
            {
                brep.Flip();
            }

            if (flip)
                brep.Flip();

            return brep;
        }

    }
}
