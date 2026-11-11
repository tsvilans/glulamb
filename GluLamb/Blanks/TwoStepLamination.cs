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

namespace GluLamb.Blanks
{
    /// <summary>
    /// Options for TwoStepLamination.
    /// </summary>
    public class TwoStepOptions
    {
        /// <summary>The beam's section axis the final layers are stacked along: 1 its Y-axis
        /// (height), 0 its X-axis (width).</summary>
        public int StackAxis = 1;

        /// <summary>Finished thickness of the final layers (G3), and of the boards of the
        /// single-curved glulam (G1). 0: the largest the radius ratio allows (rounded so that whole
        /// layers or boards fill the depth).</summary>
        public double LayerThickness = 0;
        public double BoardThickness = 0;

        /// <summary>Thickest layer or board, however gentle the curvature.</summary>
        public double MaxThickness = Globals.FromMillimeters(45);

        /// <summary>Saw kerf between the slices of G1, and what planing takes off each slice
        /// (both faces together).</summary>
        public double Kerf = Globals.FromMillimeters(3);
        public double Planing = Globals.FromMillimeters(2);

        /// <summary>Extra width on each edge of a layer, and extra length at each end.</summary>
        public double EdgeAllowance = Globals.FromMillimeters(5);
        public double EndAllowance = Globals.FromMillimeters(50);

        /// <summary>Smallest radius over lamella thickness (Eurocode: 200). The allowed strain,
        /// for bending and for twist, is 1 / (2 x this): the bending strain of a lamella at that
        /// limit.</summary>
        public double RadiusRatio = Glulam.RadiusMultiplier;

        /// <summary>Spacing of the samples along the beam.</summary>
        public double SampleSpacing = Globals.FromMillimeters(20);
    }

    /// <summary>
    /// One layer of the final blank (G3) and the flat-cut, curved lamella (G2) it is pressed from.
    /// </summary>
    public class DevelopedLayer
    {
        public int Index;

        /// <summary>Offset of the layer's centre from the blank's centre, along the stacking axis.</summary>
        public double Offset;

        /// <summary>The layer's centre curve and its two edges in the final blank, and its surface.</summary>
        public Polyline Curve, LeftEdge, RightEdge;
        public Brep Surface;

        /// <summary>The layer developed flat: its centre curve and closed outline (with the
        /// allowances), in the world XY plane, lined up with the other layers and lifted to the
        /// height of its slice in G1.</summary>
        public Polyline Developed;
        public Polyline Outline;

        /// <summary>Along the layer: arc length, in-plane (geodesic) curvature (made in step 1),
        /// out-of-plane (normal) curvature (made in step 3), and geodesic torsion, the twist of
        /// the layer (made in step 3; zero all along for a developable layer).</summary>
        public double[] S, GeodesicCurvature, NormalCurvature, Twist;

        /// <summary>Largest strains: bending in step 3 (the layer's thickness), twist (the edge
        /// stretch of a twisted strip, which is also how far the layer is from developable, and the
        /// shear of its faces), all against the allowed strain.</summary>
        public double PressStrain, TwistStrain;

        public bool Developable;
    }

    /// <summary>
    /// The geometry of a two-step lamination: flat boards bent into a single-curved glulam (G1),
    /// G1 sliced into curved lamellas (G2), the lamellas pressed out of plane into a free-form
    /// blank (G3). Each layer of G3 is developed from the beam's frame: its in-plane curvature
    /// comes from step 1, its out-of-plane curvature and twist from step 3.
    /// </summary>
    public class TwoStepResult
    {
        /// <summary>The final blank: the beam's section centreline run out by the end allowance,
        /// with whole layers across and along the stacking axis.</summary>
        public Glulam Blank;
        public List<DevelopedLayer> Layers = new List<DevelopedLayer>();

        /// <summary>The single-curved glulam, in the world XY plane: its height lies in the
        /// plane and holds the boards; its width (along Z) is the stack of slices.</summary>
        public Glulam SingleCurved;

        /// <summary>Board thickness and the length of each board of G1 (G0).</summary>
        public double BoardThickness;
        public List<double> BoardLengths = new List<double>();

        public double LayerThickness;

        /// <summary>Allowed strain (1 / (2 x radius ratio)), and the largest strains of step 1 (the
        /// boards of G1 at its inner face), step 3 and twist.</summary>
        public double AllowedStrain, BendStrain, PressStrain, TwistStrain;

        public List<string> Messages = new List<string>();
        public bool Success => Blank != null && SingleCurved != null;
    }

    public static class TwoStepLamination
    {
        public static TwoStepResult Develop(Beam beam, TwoStepOptions options = null)
        {
            options ??= new TwoStepOptions();
            var result = new TwoStepResult();
            if (beam?.Centreline == null) { result.Messages.Add("No beam."); return result; }

            double allowed = 1.0 / (2 * options.RadiusRatio);
            result.AllowedStrain = allowed;

            // The final blank's centreline: the beam's section centreline, run out straight by the
            // end allowance (a few centimetres; the beam itself ends inside it)
            var centreline = BeamOps.SectionCentreline(beam, out var orientation);
            var source = new Beam { Centreline = centreline, Orientation = orientation, Width = beam.Width, Height = beam.Height };
            var extended = options.EndAllowance > 0
                ? centreline.Extend(CurveEnd.Both, options.EndAllowance, CurveExtensionStyle.Line) ?? centreline.DuplicateCurve()
                : centreline.DuplicateCurve();
            extended.Domain = new Interval(0, 1);

            // Frames along the beam. The layers are measured and developed over the beam's own
            // length; the end allowance is added flat to the developed lamellas, where it is waste,
            // so that it adds no kink (an orientation held still past a twisting end would)
            double length = centreline.GetLength();
            int count = Math.Max(20, (int)Math.Ceiling(length / options.SampleSpacing) + 1);
            var tt = centreline.DivideByCount(count - 1, true);
            var frames = source.GetPlanes(tt);

            // Stacking direction N, and B = N x T across the layers
            bool stackY = options.StackAxis != 0;
            double depth = stackY ? beam.Height : beam.Width;
            double across = (stackY ? beam.Width : beam.Height) + 2 * options.EdgeAllowance;
            var normals = frames.Select(f => stackY ? f.YAxis : f.XAxis).ToArray();

            // Layer thickness: the largest the out-of-plane curvature allows, over all layers
            double t2 = options.LayerThickness;
            if (t2 <= 0)
            {
                double kMax = 0;
                foreach (double y in new[] { -depth / 2, 0, depth / 2 })
                {
                    var probe = Measure(OffsetPoints(frames, normals, y), normals);
                    kMax = Math.Max(kMax, probe.Kn.Max(Math.Abs));
                }
                double tMax = Math.Min(options.MaxThickness, kMax > 1e-12 ? 1 / kMax / options.RadiusRatio : depth);
                t2 = depth / Math.Max(1, (int)Math.Ceiling(depth / tMax - 1e-9));
            }
            int layers = Math.Max(1, (int)Math.Round(depth / t2));
            if (Math.Abs(layers * t2 - depth) > 1e-6 * depth)
            {
                layers = (int)Math.Ceiling(depth / t2 - 1e-9);
                result.Messages.Add($"{layers} layers of {t2:0.##} make {layers * t2:0.##}, more than the beam's {depth:0.##}.");
            }
            result.LayerThickness = t2;

            // The final blank as a glulam with whole layers along the stacking axis, on the
            // extended centreline (its orientation held at the beam's end frames past its ends)
            var et = extended.DivideByCount(count - 1, true);
            var vectors = et.Select(t =>
            {
                centreline.ClosestPoint(extended.PointAt(t), out double u);
                return source.Orientation.GetOrientation(centreline, u);
            }).ToList();
            var blankOrientation = new VectorListOrientation(extended, et, vectors);
            var data = stackY
                ? new GlulamData(1, layers, across, t2)
                : new GlulamData(layers, 1, t2, across);
            result.Blank = Glulam.CreateGlulam(extended, blankOrientation, data);

            // Develop each layer
            for (int k = 0; k < layers; ++k)
            {
                double offset = -layers * t2 / 2 + (k + 0.5) * t2;
                var layer = DevelopLayer(frames, normals, offset, across, t2, allowed, options.EndAllowance);
                layer.Index = k;
                result.Layers.Add(layer);
            }

            // Line the developed layers up with the middle one (they are cut from one G1)
            var reference = result.Layers[layers / 2].Developed;
            foreach (var layer in result.Layers)
            {
                var fit = Fit2d(layer.Developed, reference);
                layer.Developed.Transform(fit);
                layer.Outline.Transform(fit);
            }

            // G1: the reference curve, moved across to the middle of all the outlines and run to
            // their ends, with all the outlines inside it
            result.SingleCurved = SingleCurved(result, reference, layers, t2, options, out double g1Height);
            if (result.SingleCurved == null) { result.Messages.Add("Couldn't make the single-curved glulam."); return result; }

            // Lift each outline to the height of its slice in G1
            double pitch = t2 + options.Planing + options.Kerf;
            double bottom = -(layers * (t2 + options.Planing) + (layers - 1) * options.Kerf) / 2 + (t2 + options.Planing) / 2;
            foreach (var layer in result.Layers)
            {
                var lift = Transform.Translation(0, 0, bottom + layer.Index * pitch);
                layer.Developed.Transform(lift);
                layer.Outline.Transform(lift);
            }

            // Strains
            result.PressStrain = result.Layers.Max(l => l.PressStrain);
            result.TwistStrain = result.Layers.Max(l => l.TwistStrain);
            int twisted = result.Layers.Count(l => !l.Developable);

            result.Messages.Add($"{layers} layers of {t2:0.##}, {across:0.##} wide; G1 {result.SingleCurved.Width:0.##} x {g1Height:0.##} from {result.BoardLengths.Count} boards of {result.BoardThickness:0.##}.");
            result.Messages.Add($"Strain allowed {allowed * 1000:0.###} per mille; step 1 {result.BendStrain * 1000:0.###}, step 3 {result.PressStrain * 1000:0.###}, twist {result.TwistStrain * 1000:0.###}.");
            if (twisted > 0)
                result.Messages.Add($"{twisted} of {layers} layers are not developable (they twist); the largest twist strain is {result.TwistStrain / allowed:0.##} x the allowed strain.");
            if (result.BendStrain > allowed * 1.0001) result.Messages.Add("Step 1 exceeds the allowed strain: thinner boards are needed.");
            if (result.PressStrain > allowed * 1.0001) result.Messages.Add("Step 3 exceeds the allowed strain: thinner layers are needed.");
            if (result.TwistStrain > allowed * 1.0001) result.Messages.Add("Twist exceeds the allowed strain: narrower or thinner layers, or less twist (see Least Twist Orientation).");

            return result;
        }

        /// <summary>
        /// A layer at an offset along the stacking axis: its curve and edges in 3D, its curvatures
        /// and twist, and its development in the plane (centre curve at the origin along +X).
        /// </summary>
        static DevelopedLayer DevelopLayer(Plane[] frames, Vector3d[] normals, double offset, double width, double thickness, double allowed, double endAllowance)
        {
            var points = OffsetPoints(frames, normals, offset);
            var m = Measure(points, normals);
            int n = points.Length;

            var layer = new DevelopedLayer
            {
                Offset = offset,
                Curve = new Polyline(points),
                S = m.S, GeodesicCurvature = m.Kg, NormalCurvature = m.Kn, Twist = m.Tg
            };

            var left = new Point3d[n];
            var right = new Point3d[n];
            for (int i = 0; i < n; ++i) { left[i] = points[i] + m.B[i] * (width / 2); right[i] = points[i] - m.B[i] * (width / 2); }
            layer.LeftEdge = new Polyline(left);
            layer.RightEdge = new Polyline(right);
            layer.Surface = Brep.CreateFromLoft(new Curve[] { new PolylineCurve(left), new PolylineCurve(right) }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)?.FirstOrDefault();

            // Development: the heading turns by the in-plane (geodesic) turning angle at each point
            var developed = new Point3d[n];
            var heading = new double[n];
            developed[0] = Point3d.Origin;
            for (int i = 1; i < n; ++i)
            {
                heading[i] = heading[i - 1] + (i > 1 ? m.Turn[i - 1] : 0);
                double step = points[i].DistanceTo(points[i - 1]);
                developed[i] = developed[i - 1] + new Vector3d(Math.Cos(heading[i]), Math.Sin(heading[i]), 0) * step;
            }

            // Outline: the edges either side (B develops to the tangent turned by +90 degrees),
            // square to the curve's direction at each point (halfway between its two segments)
            var across = new Vector3d[n];
            for (int i = 0; i < n; ++i)
            {
                double h = i == 0 ? heading[Math.Min(1, n - 1)] : heading[i] + 0.5 * m.Turn[i];
                across[i] = new Vector3d(-Math.Sin(h), Math.Cos(h), 0) * (width / 2);
            }
            // The end allowance, straight on from each end
            var flat = developed.ToList();
            var sides = across.ToList();
            if (endAllowance > 0 && n > 1)
            {
                var d0 = developed[0] - developed[1]; d0.Unitize();
                var d1 = developed[n - 1] - developed[n - 2]; d1.Unitize();
                flat.Insert(0, developed[0] + d0 * endAllowance);
                flat.Add(developed[n - 1] + d1 * endAllowance);
                sides.Insert(0, across[0]);
                sides.Add(across[n - 1]);
            }
            layer.Developed = new Polyline(flat);

            var outline = new Polyline();
            for (int i = 0; i < flat.Count; ++i) outline.Add(flat[i] + sides[i]);
            for (int i = flat.Count - 1; i >= 0; --i) outline.Add(flat[i] - sides[i]);
            outline.Add(outline[0]);
            layer.Outline = outline;

            // Strains: bending of the layer's thickness in step 3; twist as the stretch of the edges
            // of a twisted strip (w/2)^2 tau^2 / 2 (also the in-plane strain a non-developable layer
            // needs) or the shear of its faces t tau / 2, whichever is larger
            double kn = m.Kn.Max(Math.Abs), tau = m.Tg.Max(Math.Abs);
            layer.PressStrain = thickness * kn / 2;
            layer.TwistStrain = Math.Max(0.5 * (width / 2) * (width / 2) * tau * tau, thickness * tau / 2);
            layer.Developable = 0.5 * (width / 2) * (width / 2) * tau * tau <= 0.01 * allowed;
            return layer;
        }

        static Point3d[] OffsetPoints(Plane[] frames, Vector3d[] normals, double offset)
            => frames.Select((f, i) => f.Origin + normals[i] * offset).ToArray();

        struct Measures
        {
            public double[] S, Kg, Kn, Tg, Turn;
            public Vector3d[] B;
        }

        /// <summary>
        /// Arc length, geodesic and normal curvature and geodesic torsion along a polyline on a
        /// strip with the given normals (the strip's direction across, B = N x T, is square to its
        /// tangent). Turn is the in-plane turning angle at each inner point.
        /// </summary>
        static Measures Measure(Point3d[] p, Vector3d[] normals)
        {
            int n = p.Length;
            var m = new Measures { S = new double[n], Kg = new double[n], Kn = new double[n], Tg = new double[n], Turn = new double[n], B = new Vector3d[n] };
            var T = new Vector3d[n];
            var N = new Vector3d[n];
            for (int i = 0; i < n; ++i)
            {
                var t = p[Math.Min(i + 1, n - 1)] - p[Math.Max(i - 1, 0)];
                t.Unitize();
                T[i] = t;
                var b = Vector3d.CrossProduct(normals[i], t);
                b.Unitize();
                m.B[i] = b;
                var nn = Vector3d.CrossProduct(t, b);
                nn.Unitize();
                N[i] = nn;
                if (i > 0) m.S[i] = m.S[i - 1] + p[i].DistanceTo(p[i - 1]);
            }

            for (int i = 1; i < n - 1; ++i)
            {
                var d0 = p[i] - p[i - 1];
                var d1 = p[i + 1] - p[i];
                double h = 0.5 * (d0.Length + d1.Length);

                // In-plane turning angle: both segments in the tangent plane at i, angle about N
                var a = d0 - N[i] * (d0 * N[i]);
                var c = d1 - N[i] * (d1 * N[i]);
                m.Turn[i] = Math.Atan2(Vector3d.CrossProduct(a, c) * N[i], a * c);
                m.Kg[i] = m.Turn[i] / h;

                var u0 = d0 / d0.Length; var u1 = d1 / d1.Length;
                m.Kn[i] = ((u1 - u0) / h) * N[i];

                // Geodesic torsion: how fast N turns about T, seen across the strip
                m.Tg[i] = ((N[i + 1] - N[i - 1]) / (m.S[i + 1] - m.S[i - 1])) * m.B[i];
            }
            if (n > 2)
            {
                m.Kg[0] = m.Kg[1]; m.Kg[n - 1] = m.Kg[n - 2];
                m.Kn[0] = m.Kn[1]; m.Kn[n - 1] = m.Kn[n - 2];
                m.Tg[0] = m.Tg[1]; m.Tg[n - 1] = m.Tg[n - 2];
            }
            return m;
        }

        /// <summary>
        /// The rigid move in the XY plane that best fits a polyline's points onto another's, point
        /// for point (the same places along the beam).
        /// </summary>
        static Transform Fit2d(Polyline from, Polyline to)
        {
            int n = Math.Min(from.Count, to.Count);
            double fx = 0, fy = 0, tx = 0, ty = 0;
            for (int i = 0; i < n; ++i) { fx += from[i].X; fy += from[i].Y; tx += to[i].X; ty += to[i].Y; }
            fx /= n; fy /= n; tx /= n; ty /= n;

            double sin = 0, cos = 0;
            for (int i = 0; i < n; ++i)
            {
                double ax = from[i].X - fx, ay = from[i].Y - fy, bx = to[i].X - tx, by = to[i].Y - ty;
                cos += ax * bx + ay * by;
                sin += ax * by - ay * bx;
            }
            double angle = Math.Atan2(sin, cos);
            return Transform.Translation(tx, ty, 0) * Transform.Rotation(angle, Vector3d.ZAxis, Point3d.Origin) * Transform.Translation(-fx, -fy, 0);
        }

        /// <summary>
        /// G1: a single-curved glulam in the XY plane around all the developed outlines. Its
        /// centreline follows the reference layer's developed curve, moved across to the middle of
        /// the outlines and run to their ends; its height holds them all; its width is the stack of
        /// slices. Boards as thick as its tightest inner radius allows, and their lengths.
        /// </summary>
        static Glulam SingleCurved(TwoStepResult result, Polyline reference, int layers, double t2, TwoStepOptions options, out double height)
        {
            height = 0;
            var refCurve = Curve.CreateInterpolatedCurve(reference, 3);
            if (refCurve == null) return null;
            var ext = refCurve.Extend(CurveEnd.Both, 0.25 * refCurve.GetLength(), CurveExtensionStyle.Line) ?? refCurve;

            // Where every outline point sits: along the extended reference curve and across it
            double ta = double.MaxValue, tb = double.MinValue, dMin = double.MaxValue, dMax = double.MinValue;
            foreach (var layer in result.Layers)
                foreach (var q in layer.Outline)
                {
                    if (!ext.ClosestPoint(q, out double t)) continue;
                    var p = ext.PointAt(t);
                    var tan = ext.TangentAt(t);
                    var side = new Vector3d(-tan.Y, tan.X, 0);
                    double d = (q - p) * side;
                    ta = Math.Min(ta, t); tb = Math.Max(tb, t);
                    dMin = Math.Min(dMin, d); dMax = Math.Max(dMax, d);
                }
            if (tb <= ta) return null;

            height = dMax - dMin;
            double shift = 0.5 * (dMin + dMax);
            var trimmed = ext.Trim(ta, tb) ?? ext;

            // The centreline moved across by the shift
            var tt = trimmed.DivideByCount(Math.Max(20, reference.Count), true);
            var shifted = tt.Select(t =>
            {
                var tan = trimmed.TangentAt(t);
                return trimmed.PointAt(t) + new Vector3d(-tan.Y, tan.X, 0) * shift;
            }).ToList();
            var g1Curve = Curve.CreateInterpolatedCurve(shifted, 3);
            if (g1Curve == null) return null;

            // Boards: as thick as the tightest inner radius allows
            double kMax = g1Curve.DivideByCount(200, true).Max(t => g1Curve.CurvatureAt(t).Length);
            double inner = kMax > 1e-12 ? 1 / kMax - height / 2 : double.MaxValue;
            double t1Max = Math.Min(options.MaxThickness, inner > 0 && inner < double.MaxValue ? inner / options.RadiusRatio : height);
            double t1 = options.BoardThickness > 0 ? options.BoardThickness : height / Math.Max(1, (int)Math.Ceiling(height / t1Max - 1e-9));
            int boards = Math.Max(1, (int)Math.Ceiling(height / t1 - 1e-9));
            result.BoardThickness = t1;
            result.BendStrain = inner > 0 && inner < double.MaxValue ? t1 / (2 * inner) : (kMax > 1e-12 ? double.PositiveInfinity : 0);

            double width = layers * (t2 + options.Planing) + (layers - 1) * options.Kerf;
            var data = new GlulamData(1, boards, width, t1);
            var glulam = Glulam.CreateGlulam(g1Curve, new PlanarOrientation(Plane.WorldXY), data);

            // Board lengths: each board's centre, offset across the centreline
            var samples = g1Curve.DivideByCount(400, true);
            for (int j = 0; j < boards; ++j)
            {
                double y = -boards * t1 / 2 + (j + 0.5) * t1;
                var pts = samples.Select(t => glulam.GetPlane(t).PointAt(0, y)).ToArray();
                result.BoardLengths.Add(new Polyline(pts).Length);
            }
            return glulam;
        }
    }
}
