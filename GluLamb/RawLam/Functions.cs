#if RAWLAM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb;

namespace GluLamb
{
    public static class RawLamProject
    {
        public static Curve[] SegmentCurve(Curve crv, double extension = 200, double max_length = 2600, double min_length = 500)
        {
            var segs = crv.DuplicateSegments().ToList();

            var tt = new List<double>();

            var spans = new List<Interval>();
            var lengths = new List<double>();
            var isLinear = new List<bool>();
            var isValid = new List<bool>();

            if (crv.Domain.IsDecreasing)
                crv.Domain.Reverse();

            foreach (var seg in segs)
            {
                double t0, t1;
                crv.ClosestPoint(seg.PointAtStart, out t0);
                crv.ClosestPoint(seg.PointAtEnd, out t1);

                var span = new Interval(t0, t1);
                var length = crv.GetLength(span);

                spans.Add(span);
                isLinear.Add(seg.IsLinear());
                lengths.Add(length);

                if (length < min_length && seg.IsLinear())
                    isValid.Add(false);
                else
                    isValid.Add(true);
            }

            // First and last segments should always be valid
            isValid[0] = true;
            isValid[isValid.Count - 1] = true;

            // Collapse short segments
            if (!isValid[0]) spans[1] = new Interval(spans[0].Min, spans[1].Max);

            for (int i = 1; i < spans.Count - 1; ++i)
            {
                if (!isValid[i])
                {
                    spans[i - 1] = new Interval(spans[i - 1].Min, spans[i].Mid);
                    spans[i + 1] = new Interval(spans[i].Mid, spans[i + 1].Max);
                }
            }

            // Clean up lists to remove invalid spans
            for (int i = spans.Count - 1; i >= 0; --i)
            {
                if (!isValid[i])
                {
                    spans.RemoveAt(i);
                    isValid.RemoveAt(i);
                    segs.RemoveAt(i);
                    isLinear.RemoveAt(i);
                    lengths.RemoveAt(i);
                }
            }

            // Extend curved elements into linear ones
            if (extension > 0)
            {
                for (int i = 1; i < spans.Count; ++i)
                {
                    double t;
                    var clength = crv.GetLength(new Interval(crv.Domain.Min, spans[i].Min));

                    if (isLinear[i - 1] && !isLinear[i])
                    {
                        clength = clength - extension;
                        crv.LengthParameter(clength, out t);
                    }
                    else if (isLinear[i] && !isLinear[i - 1])
                    {
                        clength = clength + extension;
                        crv.LengthParameter(clength, out t);
                    }
                    else
                        t = spans[i - 1].Max;

                    spans[i - 1] = new Interval(spans[i - 1].Min, t);
                    spans[i] = new Interval(t, spans[i].Max);
                }
            }

            // Add up all the segmentation parameters
            for (int i = 0; i < spans.Count; ++i)
            {
                tt.Add(spans[i].Min);
            }
            tt.Add(spans[spans.Count - 1].Max);


            var splits = crv.Split(tt);
            return splits;
        }

        public static void MatchStraightPieces(Element ele0, Element ele1)
        {
            var be0 = ele0 as BeamElement;
            var be1 = ele1 as BeamElement;
            if (be0 == null || be1 == null) return;
            var g0 = be0.Beam as Glulam;
            var g1 = be1.Beam as Glulam;

            if (g0 == null || g1 == null) return;

            if (g0 is StraightGlulam && g1 is FreeformGlulam)
            {
                var frame1 = g1.GetPlane(g1.Centreline.Domain.Min);
                bool vsection = g1.Width < g1.Height; // Is the curved glulam section flipped?

                Vector3d orivec;
                if (vsection)
                    orivec = frame1.YAxis;
                else
                    orivec = frame1.XAxis;
                be0.Beam.Orientation = new VectorOrientation(orivec);

                var handle = ele0.Handle;
                ele0.Handle = new Plane(handle.Origin, handle.XAxis, orivec);

            }
        }

        public static void ExtendGlulams(Structure structure, double extension)
        {
            for (int i = 0; i < structure.Elements.Count; ++i)
            {
                var be = structure.Elements[i] as BeamElement;
                if (be == null) continue;

                var glulam = be.Beam as Glulam;
                if (glulam == null) continue;

                if (be.Name.EndsWith("ST"))
                {
                    if (glulam.Centreline.PointAtStart.Z < glulam.Centreline.PointAtEnd.Z)
                    {
                        glulam.Extend(CurveEnd.End, extension, CurveExtensionStyle.Line);
                    }
                    else
                    {
                        glulam.Extend(CurveEnd.Start, extension, CurveExtensionStyle.Line);
                    }
                }
                else
                {
                    glulam.Extend(CurveEnd.Both, extension, CurveExtensionStyle.Line);
                }
            }
        }

        public static bool CreatePressingTemplate(Glulam glulam, out List<Curve> profiles, out Plane handle, double extension = 150)
        {
            profiles = new List<Curve>();
            //label_planes = new List<Plane>();

            var beam = glulam;


            //label_planes.Add(handle);

            var cl = beam.Centreline.DuplicateCurve();

            if (cl.IsLinear())
            {
                var gplane = beam.GetPlane(beam.Centreline.PointAtStart);
                handle = new Plane(gplane.Origin, gplane.ZAxis, gplane.YAxis);
            }
            else
            { 
            handle = new Plane(beam.Centreline.PointAtStart, beam.Centreline.PointAtStart - beam.Centreline.PointAtEnd,
                beam.Centreline.PointAt(beam.Centreline.Domain.Mid) - beam.Centreline.PointAtStart);
               }

            var startPt = cl.PointAtStart;
            var midPt = cl.PointAt(cl.Domain.Mid);
            var endPt = cl.PointAtEnd;

            if (!cl.IsLinear())
                cl = cl.Extend(CurveEnd.Both, extension, CurveExtensionStyle.Line);
            else
                cl = cl.Extend(CurveEnd.Both, 30, CurveExtensionStyle.Line);

            var offset0 = cl.Offset(handle, beam.Height * 0.5, 0.01, CurveOffsetCornerStyle.Sharp)[0];
            var offset1 = cl.Offset(handle, -beam.Height * 0.5, 0.01, CurveOffsetCornerStyle.Sharp)[0];

            var end0 = new Line(offset0.PointAtStart, offset1.PointAtStart).ToNurbsCurve();
            var end1 = new Line(offset0.PointAtEnd, offset1.PointAtEnd).ToNurbsCurve();

            var profile = Curve.JoinCurves(new Curve[] { offset0, offset1, end0, end1 });

            profiles.AddRange(profile);


            double circleRadius = 5.0;

            var handleCircle = new Circle(handle, circleRadius);
            profiles.Add(handleCircle.ToNurbsCurve());

            var startPlane = new Plane(startPt, handle.XAxis, handle.YAxis);
            var start = new Circle(startPlane, circleRadius);
            profiles.Add(start.ToNurbsCurve());

            /* Offset start circles */
            startPlane = beam.GetPlane(startPt);
            for (int j = -1; j < 2; j += 2)
            {
                var startOffset = new Circle(
                  new Plane(startPlane.Origin + startPlane.YAxis * j * beam.Height * 0.4,
                  startPlane.YAxis, startPlane.ZAxis), circleRadius);

                profiles.Add(startOffset.ToNurbsCurve());
            }
            //label_planes.Add(startPlane);


            var midPlane = new Plane(midPt, handle.XAxis, handle.YAxis);
            var mid = new Circle(midPlane, circleRadius);
            profiles.Add(mid.ToNurbsCurve());

            //label_planes.Add(midPlane);

            var endPlane = new Plane(endPt, handle.XAxis, handle.YAxis);

            var end = new Circle(endPlane, circleRadius);
            profiles.Add(end.ToNurbsCurve());

            /* Offset end circles */
            endPlane = beam.GetPlane(endPt);
            for (int j = -1; j < 2; j += 2)
            {
                var endOffset = new Circle(
                  new Plane(endPlane.Origin + endPlane.YAxis * j * beam.Height * 0.4,
                  endPlane.YAxis, endPlane.ZAxis), circleRadius);

                profiles.Add(endOffset.ToNurbsCurve());
            }

            //label_planes.Add(endPlane);

            return true;
        }

        /// <summary>
        /// Shorten glulam to just fit some geometry.
        /// </summary>
        /// <param name="glulam">Glulam to shorten.</param>
        /// <param name="mesh">Geometry to fit.</param>
        /// <returns></returns>
        public static Glulam CompactGlulam(Glulam glulam, Mesh mesh)
        {
            var crv = glulam.Centreline.DuplicateCurve();

            double tmin = crv.Domain.Max;
            double tmax = crv.Domain.Min;

            double t;

            if (crv.Domain.IsDecreasing)
                crv.Domain.Reverse();

            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                crv.ClosestPoint(mesh.Vertices[i], out t);
                tmin = Math.Min(tmin, t);
                tmax = Math.Max(tmax, t);
            }

            var ng = glulam.Duplicate();
            ng.Centreline = ng.Centreline.Trim(tmin, tmax);

            return ng;
        }

        /// <summary>
        /// Shorten glulam to just fit some geometry.
        /// </summary>
        /// <param name="glulam">Glulam to shorten.</param>
        /// <param name="mesh">Geometry to fit.</param>
        /// <returns></returns>
        public static Beam CompactBeam(Beam beam, Mesh mesh)
        {
            var crv = beam.Centreline.DuplicateCurve();

            double tmin = crv.Domain.Max;
            double tmax = crv.Domain.Min;

            double t;

            if (crv.Domain.IsDecreasing)
                crv.Domain.Reverse();

            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                crv.ClosestPoint(mesh.Vertices[i], out t);
                tmin = Math.Min(tmin, t);
                tmax = Math.Max(tmax, t);
            }

            var nb = beam.Duplicate();
            nb.Centreline = nb.Centreline.Trim(tmin, tmax);

            return nb;
        }

        /// <summary>
        /// Shorten glulam to just fit some geometry.
        /// </summary>
        /// <param name="glulam">Glulam to shorten.</param>
        /// <param name="brep">Geometry to fit.</param>
        /// <returns></returns>
        public static Glulam CompactGlulam(Glulam glulam, Brep brep)
        {
            var mesh = new Mesh();
            mesh.Append(Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh));


            var crv = glulam.Centreline.DuplicateCurve();

            double tmin = crv.Domain.Max;
            double tmax = crv.Domain.Min;

            double t;

            if (crv.Domain.IsDecreasing)
                crv.Domain.Reverse();

            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                crv.ClosestPoint(mesh.Vertices[i], out t);
                tmin = Math.Min(tmin, t);
                tmax = Math.Max(tmax, t);
            }

            var ng = glulam.Duplicate();
            ng.Centreline = ng.Centreline.Trim(tmin, tmax);

            return ng;
        }
    }

}
#endif