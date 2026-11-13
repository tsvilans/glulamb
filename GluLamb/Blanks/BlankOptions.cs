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

using GluLamb.Constraints;

namespace GluLamb.Blanks
{
    public enum BlankKind
    {
        /// <summary>A straight blank around the beam.</summary>
        Straight,

        /// <summary>A blank that bends in one plane.</summary>
        SingleCurved,

        /// <summary>A blank along the beam's centreline that does not twist. Its section follows
        /// the rotation-minimizing frame, so its lamellas are developable.</summary>
        Untwisted,

        /// <summary>A blank with the beam's own section: no waste, but it twists if the beam twists.</summary>
        AsBeam
    }

    /// <summary>
    /// One way to make the blank of a beam, with its cost in material and in work.
    /// </summary>
    public class BlankOption
    {
        public BlankKind Kind;

        /// <summary>The blank. It encloses the beam.</summary>
        public Glulam Blank;

        public double Width, Height, Length, Volume;

        /// <summary>Fraction of the blank that is not the beam.</summary>
        public double Waste;

        /// <summary>Number of lamellae across the width and the height. More than one in a
        /// direction: the blank bends in that direction.</summary>
        public int LamellaeX, LamellaeY;

        /// <summary>Largest twist of the blank (radians per unit length). Zero: developable lamellas.</summary>
        public double Twist;

        /// <summary>A plane to orient the blank for the press. See PressAlignment.</summary>
        public Plane Handle;

        public string Summary;
    }

    /// <summary>
    /// The blanks that a beam can be cut from, from simple to exact: straight, single-curved,
    /// double-curved without twist, and the beam's own shape. The blank's orientation is free of
    /// the beam's: each blank is the smallest of its kind that encloses the beam.
    /// </summary>
    public static class BlankOptions
    {
        /// <param name="allowance">Extra material on each side of the section.</param>
        /// <param name="rules">The rules that limit the lamella thickness. Null: Eurocode.</param>
        public static List<BlankOption> Evaluate(Beam beam, double allowance = 0, RuleSet rules = null, Context material = null)
        {
            var options = new List<BlankOption>();
            if (beam?.Centreline == null) return options;
            rules ??= LaminationRules.Eurocode();

            // The beam centred on its section, and the corners of its sections
            var curve = BeamOps.SectionCentreline(beam, out var orientation);
            var centred = new Beam { Centreline = curve, Orientation = orientation, Width = beam.Width, Height = beam.Height };
            double length = curve.GetLength();
            int count = Math.Clamp((int)Math.Ceiling(length / Globals.FromMillimeters(50)) + 1, 20, 1000);
            var tt = curve.DivideByCount(count - 1, true);
            var frames = centred.GetPlanes(tt);
            double hw = beam.Width / 2, hh = beam.Height / 2;
            var corners = frames.SelectMany(f => new[] { f.PointAt(hw, hh), f.PointAt(-hw, hh), f.PointAt(-hw, -hh), f.PointAt(hw, -hh) }).ToList();
            double beamVolume = beam.Width * beam.Height * length;

            void Add(BlankKind kind, Beam envelope)
            {
                if (envelope == null) return;
                envelope.Width += 2 * allowance;
                envelope.Height += 2 * allowance;

                var glulam = Glulam.CreateGlulam(envelope, envelope.Orientation);
                glulam.Data.Compute(envelope, Standards.Standard.None, 100, rules, material);
                double l = glulam.Centreline.GetLength();
                var option = new BlankOption
                {
                    Kind = kind,
                    Blank = glulam,
                    Width = envelope.Width,
                    Height = envelope.Height,
                    Length = l,
                    Volume = envelope.Width * envelope.Height * l,
                    LamellaeX = glulam.Data.NumWidth,
                    LamellaeY = glulam.Data.NumHeight,
                    Twist = kind == BlankKind.Straight || kind == BlankKind.SingleCurved ? 0
                        : LeastTwist.Suggest(envelope, TwistMode.None).DesignTwist,
                    Handle = PressAlignment.Handle(glulam.Centreline, glulam.GetPlane(glulam.Centreline.Domain.Mid).YAxis)
                };
                option.Waste = option.Volume > 0 ? Math.Max(0, 1 - beamVolume / option.Volume) : 0;
                option.Summary = $"{kind}: {option.Width:0.#} x {option.Height:0.#} x {option.Length:0}, waste {option.Waste * 100:0.#}%, " +
                    $"lamellae {option.LamellaeX} x {option.LamellaeY}, twist {Rhino.RhinoMath.ToDegrees(option.Twist) * Globals.FromMillimeters(1000):0.##} deg/m";
                options.Add(option);
            }

            Add(BlankKind.Straight, Straight(corners));
            if (!curve.IsLinear(Globals.Tolerance))
            {
                Add(BlankKind.SingleCurved, SingleCurved(curve, corners));
                Add(BlankKind.Untwisted, Untwisted(curve, tt, frames, hw, hh));
            }
            Add(BlankKind.AsBeam, new Beam { Centreline = curve.DuplicateCurve(), Orientation = orientation.Duplicate(), Width = beam.Width, Height = beam.Height });
            return options;
        }

        /// <summary>
        /// The smallest straight beam around the points: along their best-fit line, turned about
        /// it to the smallest section.
        /// </summary>
        static Beam Straight(List<Point3d> points)
        {
            if (!Line.TryFitLineToPoints(points, out Line line)) return null;
            var z = line.Direction;
            if (!z.Unitize()) return null;
            var origin = line.From;
            var basis = new Plane(origin, z);

            var flat = points.Select(p => new Point2d((p - origin) * basis.XAxis, (p - origin) * basis.YAxis)).ToList();
            SmallestRectangle(flat, out double angle, out Interval x, out Interval y);

            var xAxis = basis.XAxis * Math.Cos(angle) + basis.YAxis * Math.Sin(angle);
            var yAxis = Vector3d.CrossProduct(z, xAxis);
            double z0 = points.Min(p => (p - origin) * z), z1 = points.Max(p => (p - origin) * z);
            var centre = origin + xAxis * x.Mid + yAxis * y.Mid;

            return new Beam
            {
                Centreline = new LineCurve(centre + z * z0, centre + z * z1),
                Orientation = new VectorOrientation(yAxis),
                Width = x.Length,
                Height = y.Length
            };
        }

        /// <summary>
        /// A beam around the points that bends in one plane: along the centreline projected to
        /// its best-fit plane, with its height in the plane.
        /// </summary>
        static Beam SingleCurved(Curve centreline, List<Point3d> points)
        {
            var samples = centreline.DivideByCount(100, true).Select(t => centreline.PointAt(t));
            if (Plane.FitPlaneToPoints(samples, out Plane plane) != PlaneFitResult.Success) return null;
            var planar = Curve.ProjectToPlane(centreline, plane);
            if (planar == null || planar.IsLinear(Globals.Tolerance)) return null;

            // The points in the curved space of a beam along the extended curve
            var extended = planar.Extend(CurveEnd.Both, 0.1 * planar.GetLength(), CurveExtensionStyle.Line) ?? planar;
            extended.Domain = new Interval(0, 1);
            var probe = new Beam { Centreline = extended, Orientation = new PlanarOrientation(plane) };
            var box = new BoundingBox(probe.ToBeamSpace(points, true, 200));

            double total = extended.GetLength();
            if (!extended.LengthParameter(Math.Max(0, box.Min.Z), out double t0) || !extended.LengthParameter(Math.Min(total, box.Max.Z), out double t1) || t1 <= t0) return null;
            var trimmed = extended.Trim(t0, t1);
            if (trimmed == null) return null;

            var offset = new Beam
            {
                Centreline = trimmed,
                Orientation = new PlanarOrientation(plane),
                OffsetX = 0.5 * (box.Min.X + box.Max.X),
                OffsetY = 0.5 * (box.Min.Y + box.Max.Y)
            };
            var centred = BeamOps.SectionCentreline(offset, out _);
            return new Beam
            {
                Centreline = centred,
                Orientation = new PlanarOrientation(plane),
                Width = box.Max.X - box.Min.X,
                Height = box.Max.Y - box.Min.Y
            };
        }

        /// <summary>
        /// The smallest beam around the beam's sections that follows its centreline and does not
        /// twist: its section keeps one angle to the rotation-minimizing frame.
        /// </summary>
        static Beam Untwisted(Curve centreline, double[] tt, Plane[] frames, double hw, double hh)
        {
            var rmf = centreline.GetPerpendicularFrames(tt);
            if (rmf == null || rmf.Length != tt.Length) return null;

            // The section corners of every station, in the RMF's coordinates
            var flat = new List<Point2d>(4 * tt.Length);
            for (int i = 0; i < tt.Length; ++i)
                foreach (var c in new[] { frames[i].PointAt(hw, hh), frames[i].PointAt(-hw, hh), frames[i].PointAt(-hw, -hh), frames[i].PointAt(hw, -hh) })
                    flat.Add(new Point2d((c - rmf[i].Origin) * rmf[i].XAxis, (c - rmf[i].Origin) * rmf[i].YAxis));
            SmallestRectangle(flat, out double angle, out Interval x, out Interval y);

            var vectors = rmf.Select(f => -f.XAxis * Math.Sin(angle) + f.YAxis * Math.Cos(angle)).ToList();
            var offset = new Beam
            {
                Centreline = centreline.DuplicateCurve(),
                Orientation = new VectorListOrientation(centreline, tt, vectors),
                OffsetX = x.Mid,
                OffsetY = y.Mid
            };
            var centred = BeamOps.SectionCentreline(offset, out var orientation);
            return new Beam { Centreline = centred, Orientation = orientation, Width = x.Length, Height = y.Length };
        }

        /// <summary>
        /// The rectangle with the smallest area around points in a plane. Tries each turn in
        /// steps of a quarter degree. angle: the turn of the rectangle's X-axis. x, y: its extents
        /// along its own axes.
        /// </summary>
        static void SmallestRectangle(List<Point2d> points, out double angle, out Interval x, out Interval y)
        {
            angle = 0; x = y = Interval.Unset;
            double best = double.MaxValue;
            for (int step = 0; step < 360; ++step)
            {
                double a = step * Math.PI / 720, c = Math.Cos(a), s = Math.Sin(a);
                double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
                foreach (var p in points)
                {
                    double u = p.X * c + p.Y * s, v = -p.X * s + p.Y * c;
                    if (u < x0) x0 = u; if (u > x1) x1 = u;
                    if (v < y0) y0 = v; if (v > y1) y1 = v;
                }
                double area = (x1 - x0) * (y1 - y0);
                if (area < best * (1 - 1e-9)) { best = area; angle = a; x = new Interval(x0, x1); y = new Interval(y0, y1); }
            }
        }
    }

    /// <summary>
    /// Planes and moves that lay a curved part along the X-axis on average, for the press.
    /// </summary>
    public static class PressAlignment
    {
        /// <summary>
        /// A handle plane for a curve. Origin: the start. X: towards the end (the chord), so the
        /// curve runs along X on average. Y: towards the point of the curve that is furthest from
        /// the chord, so the curve bulges to +Y. A straight curve: Y from fallback.
        /// </summary>
        public static Plane Handle(Curve curve, Vector3d fallback)
        {
            var origin = curve.PointAtStart;
            var x = curve.PointAtEnd - origin;
            if (!x.Unitize()) { x = curve.TangentAtStart; x.Unitize(); }

            var y = Vector3d.Zero;
            double furthest = 0;
            foreach (double t in curve.DivideByCount(50, true))
            {
                var d = curve.PointAt(t) - origin;
                d -= x * (d * x);
                if (d.Length > furthest) { furthest = d.Length; y = d; }
            }
            if (furthest < Globals.Tolerance) y = fallback - x * (fallback * x);
            if (!y.Unitize()) y = Vector3d.CrossProduct(Vector3d.ZAxis, x);
            if (!y.Unitize()) y = Vector3d.YAxis;
            return new Plane(origin, x, y);
        }

        /// <summary>
        /// The move that puts a handle plane on the world XY plane.
        /// </summary>
        public static Transform ToPress(Plane handle) => Transform.PlaneToPlane(handle, Plane.WorldXY);

        /// <summary>
        /// The turn and move in the XY plane that puts a flat curve along the X-axis on average:
        /// its start at the origin, its end on the +X axis. No mirror.
        /// </summary>
        public static Transform FlatToPress(Curve curve)
        {
            var origin = curve.PointAtStart;
            var x = curve.PointAtEnd - origin;
            x.Z = 0;
            if (!x.Unitize()) return Transform.Translation(-origin.X, -origin.Y, 0);
            double angle = Math.Atan2(x.Y, x.X);
            return Transform.Rotation(-angle, Vector3d.ZAxis, Point3d.Origin) * Transform.Translation(-origin.X, -origin.Y, 0);
        }
    }
}
