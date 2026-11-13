/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
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

namespace GluLamb.Reconstruction
{
    /// <summary>
    /// How a centreline is run out past the part of it that was fitted.
    /// </summary>
    public enum EndExtension
    {
        /// <summary>Continue the curve's curvature and its rate of change, as measured over the
        /// last stretch of the curve (curvature changing linearly with length). Assumes nothing
        /// about the shape beyond that trend.</summary>
        Trend,

        /// <summary>Legacy: continue the curvature of the last stretch of the curve as a circle.
        /// Assumes constant curvature at the ends; kept only to compare with.</summary>
        LegacyArc,

        /// <summary>Continue straight along the end tangent.</summary>
        Straight,

        /// <summary>Rhino's smooth extension, from the curvature at the very end.</summary>
        Smooth
    }

    public static class CurveEnds
    {
        /// <summary>
        /// Extend a curve at both ends.
        /// </summary>
        /// <param name="curve">Curve to extend.</param>
        /// <param name="length">Length to add at each end.</param>
        /// <param name="style">How to extend it.</param>
        /// <param name="fitLength">For EndExtension.Trend and LegacyArc: the length of curve at each end that the
        /// arc's curvature is taken from (as a circle through its ends and middle), so that the
        /// last few millimetres don't decide it. 0: 15% of the curve's length.</param>
        public static Curve Extend(Curve curve, double length, EndExtension style, double fitLength = 0)
        {
            if (length <= 0) return curve.DuplicateCurve();

            if (style == EndExtension.Smooth)
                return curve.Extend(CurveEnd.Both, length, CurveExtensionStyle.Smooth) ?? curve.DuplicateCurve();
            if (style == EndExtension.Straight)
                return curve.Extend(CurveEnd.Both, length, CurveExtensionStyle.Line) ?? curve.DuplicateCurve();

            double total = curve.GetLength();
            if (fitLength <= 0) fitLength = 0.15 * total;
            fitLength = Math.Min(fitLength, 0.5 * total);

            var start = style == EndExtension.Trend ? EndTrend(curve, true, length, fitLength) : EndArc(curve, true, length, fitLength);
            var end = style == EndExtension.Trend ? EndTrend(curve, false, length, fitLength) : EndArc(curve, false, length, fitLength);

            var parts = new List<Curve>();
            if (start != null) parts.Add(start);
            parts.Add(curve.DuplicateCurve());
            if (end != null) parts.Add(end);

            var joined = Curve.JoinCurves(parts, Globals.FromMillimeters(0.01));
            if (joined == null || joined.Length != 1) return curve.Extend(CurveEnd.Both, length, CurveExtensionStyle.Line) ?? curve.DuplicateCurve();

            var result = joined[0];
            // JoinCurves can reverse the result; keep the input's direction
            if ((result.PointAtStart - curve.PointAtEnd).Length < (result.PointAtStart - curve.PointAtStart).Length
                && (result.PointAtEnd - curve.PointAtStart).Length < (result.PointAtEnd - curve.PointAtEnd).Length)
                result.Reverse();
            return result;
        }

        /// <summary>
        /// A curve of the given length that continues the curve from one end, following the trend
        /// of its curvature: the curvature vector is fitted as a straight-line function of length
        /// over the last fitLength of the curve, and the run-out is integrated along that, starting
        /// from the end point and tangent.
        /// </summary>
        static Curve EndTrend(Curve curve, bool atStart, double length, double fitLength)
        {
            double total = curve.GetLength();

            // Curvature vectors over the end stretch, against distance in from the end
            const int samples = 16;
            var us = new double[samples];
            var ks = new Vector3d[samples];
            double kMax = 0;
            for (int j = 0; j < samples; ++j)
            {
                double u = fitLength * j / (samples - 1);
                if (!curve.LengthParameter(atStart ? u : total - u, out double t)) return null;
                us[j] = u;
                ks[j] = curve.CurvatureAt(t);
                kMax = Math.Max(kMax, ks[j].Length);
            }

            // Least squares: k(u) = a + b u, for each coordinate
            double su = us.Sum(), suu = us.Sum(u => u * u), n = samples;
            double det = n * suu - su * su;
            Vector3d sk = Vector3d.Zero, suk = Vector3d.Zero;
            for (int j = 0; j < samples; ++j) { sk += ks[j]; suk += ks[j] * us[j]; }
            var b = det > 0 ? (n * suk - su * sk) / det : Vector3d.Zero;
            var a = (sk - b * su) / n;

            // A trend measured over a short stretch is uncertain: over the run-out the curvature
            // may change by at most half of the larger of the end's curvature and its average
            double change = b.Length * length, cap = 0.5 * Math.Max(a.Length, sk.Length / n);
            if (change > cap && change > 0) b *= cap / change;

            if (!curve.LengthParameter(atStart ? 0 : total, out double t0)) return null;
            var p = curve.PointAt(t0);
            var tangent = curve.TangentAt(t0);
            if (atStart) tangent.Reverse();

            // Integrate outwards (u goes negative past the end); the curvature is kept to a few
            // times the largest measured, so a steep trend can't curl the run-out up
            int steps = Math.Max(20, (int)Math.Ceiling(length / Globals.FromMillimeters(5)));
            double h = length / steps;
            double limit = 3 * Math.Max(kMax, 1e-12);
            var points = new List<Point3d>(steps + 1) { p };
            for (int i = 0; i < steps; ++i)
            {
                double u = -(i + 0.5) * h;
                var k = a + b * u;
                k -= tangent * (k * tangent);
                if (k.Length > limit) k *= limit / k.Length;

                // Midpoint step
                var mid = tangent + k * (0.5 * h);
                mid.Unitize();
                p += mid * h;
                tangent += k * h;
                tangent.Unitize();
                points.Add(p);
            }

            var runout = Curve.CreateInterpolatedCurve(points.Where((_, i) => i % 4 == 0 || i == points.Count - 1), 3);
            if (runout == null) return new LineCurve(points[0], points[points.Count - 1]);
            if (atStart) runout.Reverse();
            return runout;
        }

        /// <summary>
        /// An arc of the given length that continues the curve from one end: tangent to it, with
        /// the curvature of the circle through the end, and the points fitLength and fitLength / 2
        /// in from it. A straight line where that circle is (nearly) flat.
        /// </summary>
        static Curve EndArc(Curve curve, bool atStart, double length, double fitLength)
        {
            double total = curve.GetLength();
            if (!curve.LengthParameter(atStart ? 0 : total, out double t0)) return null;
            if (!curve.LengthParameter(atStart ? fitLength / 2 : total - fitLength / 2, out double t1)) return null;
            if (!curve.LengthParameter(atStart ? fitLength : total - fitLength, out double t2)) return null;

            var p = curve.PointAt(t0);
            var tangent = curve.TangentAt(t0);
            if (atStart) tangent.Reverse();

            var circle = new Circle(p, curve.PointAt(t1), curve.PointAt(t2));
            double radius = circle.IsValid ? circle.Radius : double.PositiveInfinity;

            if (double.IsInfinity(radius) || radius > 1000 * total)
                return new LineCurve(p, p + tangent * length);

            // Continue the circle itself: its tangent at the end, rather than the curve's, which the
            // last few millimetres of the curve can turn
            circle.ClosestParameter(p, out double a);
            var circleTangent = circle.TangentAt(a);
            if (circleTangent * tangent < 0) circleTangent.Reverse();
            if (circleTangent.Unitize()) tangent = circleTangent;

            var toCentre = circle.Center - p;
            toCentre -= tangent * (toCentre * tangent);
            if (!toCentre.Unitize()) return new LineCurve(p, p + tangent * length);
            var centre = p + toCentre * radius;

            double angle = length / radius;
            var far = centre + (p - centre) * Math.Cos(angle) + tangent * (radius * Math.Sin(angle));
            var arc = new Arc(p, tangent, far);
            if (!arc.IsValid) return new LineCurve(p, p + tangent * length);

            var arcCurve = new ArcCurve(arc);
            if (atStart) arcCurve.Reverse();
            return arcCurve;
        }
    }
}
