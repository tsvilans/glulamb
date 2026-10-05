// Proposal (v2, against the real Connector.cs): a more robust Connector.CutConnectors
// -------------------------------------------------------------------------------
// Drop-in replacement for CutConnectors plus private helpers, inside `class Connector`.
// The signature is unchanged except for two trailing optional parameters, so existing
// callers still compile.
//
// Why the current version fails on less-than-clean models:
//   * It cuts with the raw axes. Connectors that came through IntersectConnectorsRay were
//     extended by exactly r*tan(theta), which puts the lowest point of the cap rim *on*
//     the surface (a tangent touch), and the angle there is measured against the bounding
//     box sides of "Geometry", while the cut is made on "DetailedGeometry". Tangent or
//     near-coincident contact is the classic way BooleanDifference fails.
//   * One BooleanDifference for all cutters: one bad cutter (or one that misses the part)
//     sinks the whole cut, and result.First() can return a sliver if a cutter splits it.
//
// What this does instead, per connector, against the Brep actually being cut:
//   1. Crosses the axis (as an infinite line) with the Brep and uses crossing parity to
//      tell whether the start / end lies in material. Works on Breps that aren't quite
//      closed.
//   2. START penetrates if the start is outside and the axis enters before the end, or the
//      start is inside but within breakthroughEpsilon of the surface behind it (same
//      meaning and default as in IntersectConnectorsRay). END is the same test on the
//      flipped axis; BOTH is both.
//   3. Extends that end past the crossing by r*tan(theta) + clearance, theta measured
//      against the real face normal at the crossing (clamped at 80 degrees).
//   4. Checks the cap rim on the real surface and keeps growing until it is clear,
//      which covers curved, chamfered or kinked faces.
//   5. Never extends back into another piece of the same part (L-shapes, slots).
//   It is idempotent: connectors already extended by IntersectConnectorsRay just get the
//   extra clearance, if they need it.
//
// Then the boolean: all cutters at once; if that fails, one at a time with retries
// (looser tolerance, cutter rotated about its axis to move the seam) and skip-and-warn on
// a connector that still fails, so you get every hole that *can* be cut.

using D2P_Core;
using D2P_Core.Interfaces;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace G2PComponents
{
    public partial class Connector // (paste the members into the existing class; `partial` only so this file stands alone)
    {
        public static Brep CutConnectors(
            IComponent component,
            IEnumerable<Connector> connectors,
            RhinoDoc doc = null,
            double tolerance = 1e-3,
            double clearance = 1.0,
            double breakthroughEpsilon = 0.5)
        {
            // --- Get geometry (unchanged lookup order) ---
            GeometryBase geometry =
                Utility.GetMember(component, "DetailedGeometry", doc).FirstOrDefault() ??
                Utility.GetMember(component, "Geometry", doc).FirstOrDefault();

            Brep target = ToBrep(geometry);
            if (target == null)
            {
                if (geometry != null)
                    RhinoApp.WriteLine($"-- WARNING: CutConnectors can't cut a {geometry.ObjectType}.");
                return null;
            }

            PrepareTarget(target, tolerance);
            double docTol = (doc ?? RhinoDoc.ActiveDoc)?.ModelAbsoluteTolerance ?? tolerance;

            // --- Extend each connector so its caps clear the surface, then build cutters ---
            BoundingBox bounds = target.GetBoundingBox(true);
            double reach = bounds.Diagonal.Length;
            var cutters = new List<(Brep Brep, Line Axis, string Name)>();

            foreach (var connector in connectors)
            {
                if (connector.Axis.Length < tolerance || connector.Diameter <= 0) continue;

                double r = connector.Diameter * 0.5;
                var box = new BoundingBox(new[] { connector.Axis.From, connector.Axis.To });
                box.Inflate(r);
                if (!Overlaps(box, bounds)) continue; // a cutter that misses can make the whole boolean return null

                Line axis = ExtendToClear(target, connector.Axis, r, reach + connector.Axis.Length,
                                          tolerance, clearance, breakthroughEpsilon);

                var cutter = new Connector(axis, connector.Diameter, connector.Name).ToCylinder().ToBrep(true, true);
                if (cutter != null && cutter.IsValid) cutters.Add((cutter, axis, connector.Name));
            }

            if (cutters.Count < 1)
                return target;

            // --- Fast path: all at once ---
            var result = Brep.CreateBooleanDifference(new[] { target }, cutters.Select(c => c.Brep), tolerance);
            if (IsGoodResult(result, target))
                return PickResult(result);

            // --- Robust path: one at a time, with retries ---
            foreach (var (cutter, axis, name) in cutters)
            {
                Brep next = TryDifference(target, cutter, axis, tolerance, docTol);
                if (next != null)
                    target = next;
                else
                    RhinoApp.WriteLine($"-- WARNING: Failed to cut connector {name}, skipped.");
            }

            return target;
        }

        // ---------------------------------------------------------------------------------
        // Start / end penetration and extension
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Returns the axis with its start and/or end pushed out so the cylinder's caps clear
        /// the surface. Ends that sit deeper than breakthroughEpsilon inside the material
        /// (blind holes) are left alone.
        /// </summary>
        static Line ExtendToClear(Brep target, Line axis, double r, double reach,
                                  double tol, double clearance, double breakthroughEpsilon)
        {
            Vector3d d = axis.Direction;
            d.Unitize();

            double startExt = EndExtension(target, axis, r, reach, tol, clearance, breakthroughEpsilon);
            double endExt = EndExtension(target, new Line(axis.To, axis.From), r, reach, tol, clearance, breakthroughEpsilon);

            return new Line(axis.From - d * startExt, axis.To + d * endExt);
        }

        /// <summary>
        /// Looks only at axis.From. Returns how far to push it back (along -direction), or 0
        /// if that end doesn't penetrate the surface. s = distance along the axis from From.
        /// </summary>
        static double EndExtension(Brep target, Line axis, double r, double reach,
                                   double tol, double clearance, double breakthroughEpsilon)
        {
            Vector3d d = axis.Direction;
            if (!d.Unitize()) return 0;
            double length = axis.Length;

            List<double> xs = Crossings(target, axis.From, d, reach, tol);

            double sc; // the surface crossing this end has to clear
            if (IsInside(xs, 0))
            {
                var behind = xs.Where(x => x < 0).ToList();
                if (behind.Count == 0) return 0;
                sc = behind.Max();
                if (-sc > breakthroughEpsilon) return 0;   // blind: starts/stops inside material
            }
            else
            {
                var ahead = xs.Where(x => x > 0 && x < length).ToList();
                if (ahead.Count == 0) return 0;            // never enters from this side
                sc = ahead.Min();
            }

            // Don't push into material further back (stop halfway across the gap).
            double limit = double.NegativeInfinity;
            var further = xs.Where(x => x < sc - 10 * tol).ToList();
            if (further.Count > 0) limit = 0.5 * (further.Max() + sc);

            double allowance = TiltAllowance(target, axis.From + d * sc, d, r, clearance);
            double s = Math.Max(Math.Min(0, sc - allowance), limit);

            // Real surfaces aren't planes: check the rim and grow until it's clear.
            double step = Math.Max(0.5 * r, clearance);
            for (int i = 0; i < 6 && !CapIsClear(target, axis.From + d * s, d, r, reach, tol, clearance); i++)
            {
                double next = s - step;
                if (next < limit) { s = limit; break; }
                s = next;
            }

            return Math.Max(0, -s);
        }

        /// <summary>
        /// Same r*tan(theta) as the tilt compensation in IntersectConnectorsRay, but against the
        /// actual face normal at the hit, plus clearance so the rim doesn't just touch.
        /// </summary>
        static double TiltAllowance(Brep target, Point3d hit, Vector3d d, double r, double clearance)
        {
            if (!target.ClosestPoint(hit, out _, out _, out _, out _, 0, out Vector3d n) || !n.Unitize())
                return r + clearance; // conservative (= 45 degrees)

            double dot = Math.Min(1, Math.Abs(d * n));
            double theta = Math.Min(Math.Acos(dot), RhinoMath.ToRadians(80));
            return r * Math.Tan(theta) + clearance;
        }

        static bool CapIsClear(Brep target, Point3d center, Vector3d d, double r, double reach,
                               double tol, double clearance)
        {
            const int samples = 16;
            var circle = new Circle(new Plane(center, d), r);

            for (int i = 0; i <= samples; i++)
            {
                Point3d p = i == samples ? center : circle.PointAt(2 * Math.PI * i / samples);
                List<double> xs = Crossings(target, p, d, reach, tol);

                if (IsInside(xs, 0)) return false;
                if (xs.Any(x => Math.Abs(x) < 0.5 * clearance)) return false; // rim (almost) on the surface
            }
            return true;
        }

        /// <summary>Signed distances along d from origin where the infinite line crosses the Brep.</summary>
        static List<double> Crossings(Brep target, Point3d origin, Vector3d d, double reach, double tol)
        {
            var probe = new LineCurve(origin - d * reach, origin + d * reach);
            Intersection.CurveBrep(probe, target, tol, out _, out Point3d[] points);

            var result = new List<double>();
            foreach (double x in (points ?? Array.Empty<Point3d>()).Select(p => (p - origin) * d).OrderBy(x => x))
                if (result.Count == 0 || x - result[result.Count - 1] > 10 * tol) // a hit on a shared edge comes back twice
                    result.Add(x);
            return result;
        }

        /// <summary>Ray parity: inside if an odd number of crossings lie ahead. Doesn't need IsSolid.</summary>
        static bool IsInside(List<double> crossings, double s) => crossings.Count(x => x > s) % 2 == 1;

        // ---------------------------------------------------------------------------------
        // Boolean robustness
        // ---------------------------------------------------------------------------------

        static Brep ToBrep(GeometryBase geometry)
        {
            switch (geometry)
            {
                case Brep b: return b.DuplicateBrep();       // don't modify the component's own geometry
                case Extrusion e: return e.ToBrep(true);
                default: return null;                         // Mesh / SubD: no Brep boolean possible
            }
        }

        static void PrepareTarget(Brep b, double tol)
        {
            b.Faces.SplitKinkyFaces(RhinoMath.DefaultAngleTolerance, true);
            b.MergeCoplanarFaces(tol);
            if (b.SolidOrientation == BrepSolidOrientation.Inward) b.Flip();
            b.Compact();
        }

        static Brep TryDifference(Brep target, Brep cutter, Line axis, double tol, double docTol)
        {
            double[] tolerances = { tol, tol * 10, docTol };
            double[] seamRotations = { 0.0, 0.137, 0.291 }; // radians; odd values so the seam avoids part edges

            foreach (double angle in seamRotations)
            {
                Brep c = cutter.DuplicateBrep();
                if (angle != 0) c.Rotate(angle, axis.Direction, axis.From);

                foreach (double t in tolerances.Distinct())
                {
                    var result = Brep.CreateBooleanDifference(target, c, t);
                    if (IsGoodResult(result, target)) return PickResult(result);
                }
            }
            return null;
        }

        static bool IsGoodResult(Brep[] result, Brep input)
        {
            if (result == null || result.Length == 0) return false;
            if (result.Any(b => b == null || !b.IsValid)) return false;
            if (input.IsSolid && result.Any(b => !b.IsSolid)) return false;

            // BooleanDifference sometimes "succeeds" by handing back the input unchanged.
            double before = VolumeMassProperties.Compute(input)?.Volume ?? 0;
            double after = result.Sum(b => VolumeMassProperties.Compute(b)?.Volume ?? 0);
            return before <= 0 || after < before;
        }

        /// <summary>If a cutter splits the part, keep the biggest piece rather than whichever comes first.</summary>
        static Brep PickResult(Brep[] result) =>
            result.Length == 1
                ? result[0]
                : result.OrderByDescending(b => VolumeMassProperties.Compute(b)?.Volume ?? 0).First();

        static bool Overlaps(BoundingBox a, BoundingBox b) =>
            a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
            a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
            a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
    }
}
