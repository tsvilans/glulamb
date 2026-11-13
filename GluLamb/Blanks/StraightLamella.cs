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
    /// Whether a beam can be laminated from straight lamellas, for one stacking axis.
    /// </summary>
    public class StraightLamellaCheck
    {
        /// <summary>The beam's axis that the layers are stacked along: 1 Y, 0 X.</summary>
        public int StackAxis;

        public int Layers;
        public double LayerThickness, LayerWidth;

        /// <summary>Largest in-plane curvature of a layer, and the layer that has it.</summary>
        public double GeodesicCurvature;
        public int WorstLayer;

        /// <summary>Strain at the edges of a straight lamella that is bent sideways to that
        /// curvature: curvature x width / 2.</summary>
        public double EdgeBendStrain;

        /// <summary>Largest distance of a flat layer's centre curve from its chord. A straight
        /// board that is wider by twice this covers the layer.</summary>
        public double Bow;

        /// <summary>Largest strain from twist, and from bending out of plane in the press.</summary>
        public double TwistStrain, PressStrain;

        public double AllowedStrain, AllowedTwistStrain;

        /// <summary>Twist and press strains are within their limits.</summary>
        public bool TwistOk, PressOk;

        /// <summary>A straight lamella can be bent sideways into place: EdgeBendStrain, twist
        /// and press strains are within their limits.</summary>
        public bool Forced;

        /// <summary>A straight board with the spare width covers every layer, and twist and
        /// press strains are within their limits.</summary>
        public bool FromStraightBoard;

        /// <summary>Either way works.</summary>
        public bool Passed => Forced || FromStraightBoard;

        public override string ToString() =>
            $"Stack {(StackAxis != 0 ? "Y" : "X")}: {(Passed ? "straight lamellas OK" : !TwistOk ? "too much twist" : !PressOk ? "layers too thick for the bend" : "needs curved lamellas")} " +
            $"({Layers} x {LayerThickness:0.##}; edge bend {EdgeBendStrain * 1000:0.###} of {AllowedStrain * 1000:0.###} per mille, bow {Bow:0.##}, " +
            $"twist {TwistStrain * 1000:0.###} of {AllowedTwistStrain * 1000:0.###} per mille{(Forced ? "; forced" : "")}{(FromStraightBoard ? "; from a straight board" : "")})";
    }

    /// <summary>
    /// Tests whether a beam (a centreline with orientations) can be made from straight lamellas:
    /// no in-plane curvature to speak of, and little twist. It needs the layers of the blank, so
    /// it depends on the stacking axis. Every layer must pass, not only the middle one.
    /// </summary>
    public static class StraightLamella
    {
        /// <summary>
        /// The check for both stacking axes: Y first, then X.
        /// </summary>
        /// <param name="options">Thicknesses, rules and material. EdgeAllowance is the spare
        /// width on each side of a straight board. Null: the defaults.</param>
        public static StraightLamellaCheck[] Evaluate(Beam beam, TwoStepOptions options = null)
        {
            options ??= new TwoStepOptions();
            return new[] { Evaluate(beam, 1, options), Evaluate(beam, 0, options) };
        }

        public static StraightLamellaCheck Evaluate(Beam beam, int stackAxis, TwoStepOptions options = null)
        {
            options ??= new TwoStepOptions();
            var check = new StraightLamellaCheck { StackAxis = stackAxis };
            if (beam?.Centreline == null) return check;

            var layers = TwoStepLamination.Layers(beam, stackAxis, options, out double thickness, out double width, out double allowed, out double allowedTwist);
            check.Layers = layers.Count;
            check.LayerThickness = thickness;
            check.LayerWidth = width;
            check.AllowedStrain = allowed;
            check.AllowedTwistStrain = allowedTwist;

            foreach (var layer in layers)
            {
                double kg = layer.GeodesicCurvature.Max(Math.Abs);
                if (kg > check.GeodesicCurvature) { check.GeodesicCurvature = kg; check.WorstLayer = layer.Index; }
                check.Bow = Math.Max(check.Bow, Bow(layer.Developed));
                check.TwistStrain = Math.Max(check.TwistStrain, layer.TwistStrain);
                check.PressStrain = Math.Max(check.PressStrain, layer.PressStrain);
            }
            check.EdgeBendStrain = check.GeodesicCurvature * width / 2;

            const double slack = 1.0001;
            check.TwistOk = check.TwistStrain <= allowedTwist * slack;
            check.PressOk = check.PressStrain <= allowed * slack;
            check.Forced = check.TwistOk && check.PressOk && check.EdgeBendStrain <= allowed * slack;
            check.FromStraightBoard = check.TwistOk && check.PressOk && check.Bow <= options.EdgeAllowance + Globals.Tolerance;
            return check;
        }

        /// <summary>
        /// Largest distance of a flat polyline from the line through its ends.
        /// </summary>
        static double Bow(Polyline flat)
        {
            if (flat == null || flat.Count < 3) return 0;
            var chord = new Line(flat[0], flat[flat.Count - 1]);
            if (chord.Length <= 0) return 0;
            return flat.Max(p => chord.DistanceTo(p, false));
        }
    }
}
