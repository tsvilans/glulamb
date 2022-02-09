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
    public static class RawLam
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

    }

}
#endif