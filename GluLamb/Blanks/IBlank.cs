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

using System.Collections.Generic;

using Rhino.Geometry;

namespace GluLamb.Blanks
{
    /// <summary>
    /// A blank: the material body a beam is made from (a glulam, a segmented blank), as opposed to
    /// the beam, which is the design object. A blank has its own id, material and geometry, and a
    /// beam sits in it through a Placement. See docs/blank-model.md.
    /// </summary>
    public interface IBlank
    {
        string Id { get; set; }

        /// <summary>
        /// The blank's reference curve (for a pressed blank, along its form).
        /// </summary>
        Curve Centreline { get; }

        /// <summary>
        /// The section plane at a centreline parameter: Z along the centreline, Y the stacking
        /// direction of the layers (the section's height), X across them (its width).
        /// </summary>
        Plane GetPlane(double t);

        /// <summary>
        /// The blank's section at a centreline parameter, as a rectangle in the section plane. The
        /// section can change along the blank.
        /// </summary>
        Rectangle3d GetSection(double t);

        BlankMaterial Material { get; set; }

        /// <summary>
        /// The layers through the blank's height, bottom (-Y) to top (+Y), or null if it has none.
        /// </summary>
        IReadOnlyList<BlankLayer> Layers { get; }

        Brep ToBrep();

        double GetVolume();

        void Transform(Transform xform);

        IBlank DuplicateBlank();
    }

    /// <summary>
    /// Material data shared by every kind of blank.
    /// </summary>
    public struct BlankMaterial
    {
        /// <summary>
        /// Strength class, e.g. GL24h or GL28c (empty: not given).
        /// </summary>
        public string StrengthClass;

        /// <summary>
        /// Species of the blank (for a glulam with mixed lamellae, "Composite").
        /// </summary>
        public string Species;

        public MachiningAllowance Allowance;

        public BlankMaterial(string strengthClass, string species, MachiningAllowance allowance)
        {
            StrengthClass = strengthClass;
            Species = species;
            Allowance = allowance;
        }

        public override string ToString() => $"{StrengthClass} {Species}; allowance {Allowance}".Trim();
    }

    /// <summary>
    /// One layer of a blank: a lamella, or a course of segments.
    /// </summary>
    public struct BlankLayer
    {
        public double Thickness;

        /// <summary>
        /// Where the layer starts and ends, as lengths along the blank's centreline. A layer that
        /// doesn't run the full length (e.g. in a stepped layup) starts after 0 or ends before the end.
        /// </summary>
        public double Start, End;

        public string Species, Grade;

        public BlankLayer(double thickness, double start, double end, string species, string grade)
        {
            Thickness = thickness;
            Start = start;
            End = end;
            Species = species;
            Grade = grade;
        }

        public override string ToString() => $"{Thickness:0.###} {Species} {Grade} [{Start:0.#}, {End:0.#}]".Replace("  ", " ");
    }

    /// <summary>
    /// Where a beam sits in a blank: the transform from the blank to the beam, and for a beam cut
    /// from a longer blank, where along the blank it starts.
    /// </summary>
    public class Placement
    {
        public string BlankId;
        public string BeamId;

        /// <summary>
        /// Index of the beam in the input it came from.
        /// </summary>
        public int BeamIndex = -1;

        /// <summary>
        /// Transform from the blank to the beam.
        /// </summary>
        public Transform Transform = Transform.Identity;

        /// <summary>
        /// Whether the beam is shorter than the blank and cut from part of it.
        /// </summary>
        public bool Partial;

        /// <summary>
        /// Where the beam starts along the blank's reference member (a length; 0 unless partial).
        /// </summary>
        public double Offset;

        /// <summary>
        /// Whether the beam runs against the blank's direction.
        /// </summary>
        public bool Reversed;

        /// <summary>
        /// Largest deviation of the beam from the blank's reference member.
        /// </summary>
        public double Deviation;

        public override string ToString() =>
            $"Placement {BeamId} in {BlankId}" + (Partial ? $" from {Offset:0.#}" : "") + (Reversed ? ", reversed" : "") + $", deviation {Deviation:0.###}";
    }
}
