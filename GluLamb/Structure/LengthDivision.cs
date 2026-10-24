using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace GluLamb
{
    /// <summary>
    /// Finds where to cut a centreline so that no piece, with its extensions, exceeds a
    /// maximum length. Everything is measured as arc length along the curve, so curved
    /// centrelines work the same as straight ones.
    /// </summary>
    public static class LengthDivision
    {
        /// <summary>
        /// Arc-length positions of the cuts on a curve, or null if no division fits.
        /// </summary>
        /// <param name="curve">Centreline to divide.</param>
        /// <param name="jointParameters">Curve parameters of existing joints. Cuts keep at least the clearance away from them.</param>
        /// <param name="maxLength">Maximum piece length, extensions included.</param>
        /// <param name="clearance">Minimum distance from a cut to a joint or to a curve end.</param>
        /// <param name="spliceAllowance">Extra length each piece needs at a cut (the splice's overlap or cut allowance).</param>
        /// <param name="startExtension">How far the first piece is extended at the curve start.</param>
        /// <param name="endExtension">How far the last piece is extended at the curve end.</param>
        public static double[] FindCuts(Curve curve, IEnumerable<double> jointParameters, double maxLength, double clearance,
            double spliceAllowance = 0, double startExtension = 0, double endExtension = 0)
        {
            var length = curve.GetLength();
            var joints = jointParameters.Select(t => curve.GetLength(new Interval(curve.Domain.Min, Math.Max(curve.Domain.Min, Math.Min(curve.Domain.Max, t)))));

            return FindCuts(length, joints, maxLength, clearance, spliceAllowance, startExtension, endExtension);
        }

        /// <summary>
        /// Splits a curve at the arc-length positions from FindCuts.
        /// </summary>
        public static Curve[] Divide(Curve curve, double[] cuts)
        {
            if (cuts == null || cuts.Length < 1) return new[] { curve.DuplicateCurve() };

            var parameters = new List<double>();
            foreach (var s in cuts)
                if (curve.LengthParameter(s, out double t))
                    parameters.Add(t);

            return curve.Split(parameters) ?? new[] { curve.DuplicateCurve() };
        }

        /// <summary>
        /// Arc-length positions of the cuts on a span of the given length, or null if no division fits.
        /// Uses the fewest pieces that fit, then spreads the cuts so the pieces (with their extensions)
        /// are as close to equal as the joint clearances allow.
        /// </summary>
        public static double[] FindCuts(double length, IEnumerable<double> joints, double maxLength, double clearance,
            double spliceAllowance = 0, double startExtension = 0, double endExtension = 0)
        {
            if (length <= 0 || maxLength <= 0) return null;
            if (length + startExtension + endExtension <= maxLength) return new double[0];

            clearance = Math.Max(0, clearance);
            spliceAllowance = Math.Max(0, spliceAllowance);

            var allowed = AllowedIntervals(length, joints, clearance);
            if (allowed.Count < 1) return null;

            // Every piece is at least as long as the shortest stretch between two cuts,
            // so no more pieces than this can help.
            double minReach = maxLength - 2 * spliceAllowance - Math.Max(startExtension, endExtension);
            int maxPieces = minReach > 0 ? (int)Math.Ceiling(length / minReach) + allowed.Count + 2 : 0;
            if (maxPieces < 2) return null;

            for (int n = 2; n <= maxPieces; ++n)
            {
                var cuts = Solve(length, n, allowed, maxLength, spliceAllowance, startExtension, endExtension);
                if (cuts != null) return cuts;
            }

            return null;
        }

        /// <summary>
        /// Blank length of each piece: its arc length plus its extensions.
        /// </summary>
        public static double[] PieceLengths(double length, double[] cuts, double spliceAllowance = 0, double startExtension = 0, double endExtension = 0)
        {
            int n = (cuts?.Length ?? 0) + 1;
            var lengths = new double[n];
            for (int i = 0; i < n; ++i)
            {
                double s0 = i == 0 ? 0 : cuts[i - 1];
                double s1 = i == n - 1 ? length : cuts[i];
                lengths[i] = s1 - s0 + (i == 0 ? startExtension : spliceAllowance) + (i == n - 1 ? endExtension : spliceAllowance);
            }
            return lengths;
        }

        static double[] Solve(double length, int n, List<Interval> allowed, double maxLength, double allowance, double e0, double e1)
        {
            // How far piece i (1-based) can reach between its two ends
            double Reach(int i) => maxLength - (i == 1 ? e0 : allowance) - (i == n ? e1 : allowance);

            // Earliest position of each cut that still lets the rest fit, working back from the end
            var earliest = new double[n + 1];
            earliest[n] = length;
            for (int i = n - 1; i >= 1; --i)
            {
                var x = SmallestAtLeast(allowed, earliest[i + 1] - Reach(i + 1));
                if (x == null || x.Value >= earliest[i + 1]) return null;
                earliest[i] = x.Value;
            }
            if (earliest[1] > Reach(1)) return null;

            // Aim for equal blank lengths, then place each cut as close to its target as the window allows
            double blank = (length + e0 + e1 + 2 * allowance * (n - 1)) / n;
            var cuts = new double[n - 1];
            double previous = 0;
            for (int i = 1; i < n; ++i)
            {
                double target = (blank - e0 - allowance) + (i - 1) * (blank - 2 * allowance);
                var x = Closest(allowed, target, Math.Max(earliest[i], previous), previous + Reach(i));
                if (x == null || x.Value <= previous) return null;
                cuts[i - 1] = x.Value;
                previous = x.Value;
            }

            return cuts;
        }

        /// <summary>
        /// Stretches of [0, length] where a cut may go: away from the ends and from every joint by the clearance.
        /// </summary>
        static List<Interval> AllowedIntervals(double length, IEnumerable<double> joints, double clearance)
        {
            var blocked = joints.Select(s => new Interval(s - clearance, s + clearance))
                .Append(new Interval(double.MinValue, clearance))
                .Append(new Interval(length - clearance, double.MaxValue))
                .OrderBy(x => x.T0);

            var allowed = new List<Interval>();
            double start = 0;
            foreach (var b in blocked)
            {
                if (b.T0 > start)
                    allowed.Add(new Interval(start, Math.Min(b.T0, length)));
                start = Math.Max(start, b.T1);
            }

            return allowed.Where(x => x.T1 > x.T0).ToList();
        }

        static double? SmallestAtLeast(List<Interval> allowed, double value)
        {
            foreach (var x in allowed)
                if (x.T1 >= value) return Math.Max(x.T0, value);
            return null;
        }

        static double? Closest(List<Interval> allowed, double target, double min, double max)
        {
            double? best = null;
            foreach (var x in allowed)
            {
                double lo = Math.Max(x.T0, min), hi = Math.Min(x.T1, max);
                if (lo > hi) continue;
                double candidate = Math.Max(lo, Math.Min(hi, target));
                if (best == null || Math.Abs(candidate - target) < Math.Abs(best.Value - target))
                    best = candidate;
            }
            return best;
        }
    }
}
