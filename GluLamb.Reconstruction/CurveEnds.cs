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
        /// <summary>Continue the curvature of the last stretch of the curve (an arc tangent to
        /// it): a curved beam stays curved.</summary>
        Arc,

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
        /// <param name="fitLength">For EndExtension.Arc: the length of curve at each end that the
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

            var start = EndArc(curve, true, length, fitLength);
            var end = EndArc(curve, false, length, fitLength);

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

            // Centre on the side the curve bends to, at the circle's radius, so the arc is tangent
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
