using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    /// <summary>
    /// Rounding the corners of steel plate outlines: only the inside (concave) corners, which the
    /// cutting tool rounds anyway and which carry stress. Outside corners stay sharp. (Plates whose
    /// corners sit in milled slot ends may need more; see the TODO list.)
    /// </summary>
    public static class PlateOutline
    {
        /// <summary>
        /// For each corner of a closed outline (points without the repeated first one), whether it
        /// is an inside (concave) corner, seen along the normal.
        /// </summary>
        public static bool[] InsideCorners(IList<Point3d> points, Vector3d normal)
        {
            int n = points.Count;
            var flags = new bool[n];

            // Orientation of the outline around the normal
            double area = 0;
            for (int i = 0; i < n; ++i)
                area += Vector3d.CrossProduct((Vector3d)points[i], (Vector3d)points[(i + 1) % n]) * normal;
            double sign = area >= 0 ? 1 : -1;

            for (int i = 0; i < n; ++i)
            {
                var a = points[(i - 1 + n) % n];
                var p = points[i];
                var b = points[(i + 1) % n];
                flags[i] = Vector3d.CrossProduct(p - a, b - p) * normal * sign < 0;
            }
            return flags;
        }

        /// <summary>
        /// A polyline (closed if the last point repeats the first) with the flagged corners rounded
        /// to radius (flags by point index; the end points of an open polyline aren't corners). A
        /// fillet is made smaller where the neighbouring segments are too short for it.
        /// </summary>
        public static Curve Round(IList<Point3d> points, IList<bool> flags, double radius, double tolerance)
        {
            bool closed = points.Count > 3 && points[0].DistanceTo(points[points.Count - 1]) < tolerance;
            var pts = closed ? points.Take(points.Count - 1).ToList() : points.ToList();
            int n = pts.Count;
            if (radius <= tolerance || n < 3)
                return new Polyline(points).ToNurbsCurve();

            // Where each corner's fillet starts and ends (or the corner itself)
            var ins = new Point3d[n];
            var outs = new Point3d[n];
            var arcs = new Arc?[n];
            for (int i = 0; i < n; ++i)
            {
                ins[i] = outs[i] = pts[i];
                bool corner = closed || (i > 0 && i < n - 1);
                if (!corner || !flags[i]) continue;

                var a = pts[(i - 1 + n) % n];
                var b = pts[(i + 1) % n];
                var u = a - pts[i];
                var v = b - pts[i];
                double lu = u.Length, lv = v.Length;
                if (!u.Unitize() || !v.Unitize()) continue;
                var angle = Vector3d.VectorAngle(u, v);
                if (angle < 1e-3 || Math.PI - angle < 1e-3) continue;

                // Half of each neighbouring segment at most, so neighbouring fillets don't overlap
                var d = radius / Math.Tan(angle * 0.5);
                d = Math.Min(d, Math.Min(lu, lv) * 0.5);
                if (d < tolerance) continue;

                ins[i] = pts[i] + u * d;
                outs[i] = pts[i] + v * d;
                arcs[i] = new Arc(ins[i], -u, outs[i]);
            }

            var curve = new PolyCurve();
            int count = closed ? n : n - 1;
            for (int i = 0; i < count; ++i)
            {
                int j = (i + 1) % n;
                var from = outs[i];
                var to = ins[j];
                if (from.DistanceTo(to) > tolerance)
                    curve.Append(new Line(from, to));
                if (arcs[j].HasValue && (closed || j < n - 1))
                    curve.Append(arcs[j].Value);
            }


            return curve.SegmentCount > 0 ? curve : new Polyline(points).ToNurbsCurve();
        }
    }
}
