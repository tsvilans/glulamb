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
    public enum TwistMode
    {
        /// <summary>No twist: the rotation-minimizing frame, turned by the one angle that keeps it
        /// closest to the design. Every layer is developable.</summary>
        None,

        /// <summary>Even twist: the design's orientation at both ends, turning evenly between them
        /// relative to the rotation-minimizing frame.</summary>
        Even,

        /// <summary>Limited twist: the orientation closest to the design whose twist stays within
        /// the allowed strain for the layer width and thickness.</summary>
        Limited
    }

    public class LeastTwistResult
    {
        /// <summary>The beam with the suggested orientation.</summary>
        public Beam Beam;

        /// <summary>Along the centreline: arc length, the design's and the suggestion's angle from
        /// the rotation-minimizing frame (radians), and the suggestion's twist (radians per unit
        /// length), which is the geodesic torsion of its layers.</summary>
        public double[] S, DesignAngle, Angle, Twist;

        /// <summary>Largest turn of the suggestion from the design (radians), the design's own
        /// largest twist, and the suggestion's.</summary>
        public double MaxDeviation, DesignTwist, MaxTwist;

        /// <summary>Largest twist the allowed strain lets a layer of this width and thickness take.</summary>
        public double AllowedTwist;

        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// Orientations that make the layers of a two-step blank developable (no twist) or twist as
    /// little as the design allows. A layer whose normal follows the curve's rotation-minimizing
    /// frame has no geodesic torsion: it is a developable strip that bends from a flat-cut lamella
    /// without stretching. Measured from that frame, the twist of any orientation is the rate at
    /// which its angle changes along the curve.
    /// </summary>
    public static class LeastTwist
    {
        /// <param name="beam">The design.</param>
        /// <param name="mode">What to suggest.</param>
        /// <param name="stackAxis">The beam's axis the layers are stacked along (1 Y, 0 X).</param>
        /// <param name="layerWidth">Width of a layer across (0: the beam's), and its thickness, for the twist limit.</param>
        /// <param name="radiusRatio">Smallest radius over lamella thickness; the allowed strain is 1 / (2 x this).</param>
        public static LeastTwistResult Suggest(Beam beam, TwistMode mode, int stackAxis = 1, double layerWidth = 0, double layerThickness = 0, double radiusRatio = 0, double sampleSpacing = 0)
        {
            var result = new LeastTwistResult();
            if (beam?.Centreline == null) { result.Messages.Add("No beam."); return result; }
            if (radiusRatio <= 0) radiusRatio = Glulam.RadiusMultiplier;
            if (sampleSpacing <= 0) sampleSpacing = Globals.FromMillimeters(20);
            bool stackY = stackAxis != 0;
            if (layerWidth <= 0) layerWidth = stackY ? beam.Width : beam.Height;

            var curve = beam.Centreline;
            int count = Math.Max(20, (int)Math.Ceiling(curve.GetLength() / sampleSpacing) + 1);
            var tt = curve.DivideByCount(count - 1, true);
            var rmf = curve.GetPerpendicularFrames(tt);
            if (rmf == null || rmf.Length != tt.Length) { result.Messages.Add("Couldn't find the rotation-minimizing frames."); return result; }

            // The design's stacking direction, as an angle from the RMF's Y-axis about the tangent,
            // made continuous
            int n = tt.Length;
            var s = new double[n];
            var design = new double[n];
            for (int i = 0; i < n; ++i)
            {
                if (i > 0) s[i] = s[i - 1] + curve.PointAt(tt[i]).DistanceTo(curve.PointAt(tt[i - 1]));
                var frame = beam.GetPlane(tt[i]);
                var normal = stackY ? frame.YAxis : frame.XAxis;
                double a = Math.Atan2(normal * rmf[i].XAxis, normal * rmf[i].YAxis);
                if (i > 0) a += Math.Round((design[i - 1] - a) / (2 * Math.PI)) * 2 * Math.PI;
                design[i] = a;
            }
            result.S = s;
            result.DesignAngle = design;

            // Allowed twist: the edge stretch (w/2)^2 tau^2 / 2 and the face shear t tau / 2 within
            // the allowed strain
            double allowed = 1.0 / (2 * radiusRatio);
            double tauEdge = Math.Sqrt(2 * allowed) / (layerWidth / 2);
            double tauShear = layerThickness > 0 ? 2 * allowed / layerThickness : double.MaxValue;
            result.AllowedTwist = Math.Min(tauEdge, tauShear);

            var angle = new double[n];
            switch (mode)
            {
                case TwistMode.None:
                    {
                        double mid = 0.5 * (design.Min() + design.Max());
                        for (int i = 0; i < n; ++i) angle[i] = mid;
                        break;
                    }
                case TwistMode.Even:
                    {
                        double length = s[n - 1];
                        for (int i = 0; i < n; ++i) angle[i] = design[0] + (design[n - 1] - design[0]) * s[i] / length;
                        break;
                    }
                default:
                    {
                        // The closest curve (largest difference) whose slope is within the allowed
                        // twist: halfway between the lowest curve above the design and the highest
                        // curve below it with that slope
                        double tau = result.AllowedTwist;
                        for (int i = 0; i < n; ++i)
                        {
                            double upper = double.MaxValue, lower = double.MinValue;
                            for (int j = 0; j < n; ++j)
                            {
                                double d = tau * Math.Abs(s[i] - s[j]);
                                upper = Math.Min(upper, design[j] + d);
                                lower = Math.Max(lower, design[j] - d);
                            }
                            angle[i] = 0.5 * (upper + lower);
                        }
                        break;
                    }
            }
            result.Angle = angle;

            result.Twist = Rate(angle, s);
            result.DesignTwist = Rate(design, s).Max(Math.Abs);
            result.MaxTwist = result.Twist.Max(Math.Abs);
            result.MaxDeviation = Enumerable.Range(0, n).Max(i => Math.Abs(angle[i] - design[i]));

            // The suggested orientation: the beam's Y-axis turned so that the stacking axis takes
            // the suggested angle
            var vectors = new List<Vector3d>(n);
            for (int i = 0; i < n; ++i)
            {
                var normal = rmf[i].YAxis * Math.Cos(angle[i]) + rmf[i].XAxis * Math.Sin(angle[i]);
                var y = stackY ? normal : Vector3d.CrossProduct(rmf[i].ZAxis, normal);
                vectors.Add(y);
            }
            result.Beam = new Beam
            {
                Centreline = curve.DuplicateCurve(),
                Orientation = new VectorListOrientation(curve, tt, vectors),
                Width = beam.Width,
                Height = beam.Height,
                OffsetX = beam.OffsetX,
                OffsetY = beam.OffsetY
            };

            result.Messages.Add($"{mode}: turns up to {Rhino.RhinoMath.ToDegrees(result.MaxDeviation):0.##} deg from the design; " +
                $"twist {Rhino.RhinoMath.ToDegrees(result.MaxTwist) * Globals.FromMillimeters(1000):0.##} deg/m (design {Rhino.RhinoMath.ToDegrees(result.DesignTwist) * Globals.FromMillimeters(1000):0.##} deg/m, allowed {Rhino.RhinoMath.ToDegrees(result.AllowedTwist) * Globals.FromMillimeters(1000):0.##} deg/m).");
            return result;
        }

        static double[] Rate(double[] a, double[] s)
        {
            int n = a.Length;
            var r = new double[n];
            for (int i = 0; i < n; ++i)
            {
                int i0 = Math.Max(0, i - 1), i1 = Math.Min(n - 1, i + 1);
                r[i] = s[i1] > s[i0] ? (a[i1] - a[i0]) / (s[i1] - s[i0]) : 0;
            }
            return r;
        }
    }
}
