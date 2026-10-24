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

using System;

namespace GluLamb
{
    /// <summary>
    /// How the lamella grades are laid up through a glulam's height (its Y-axis, the stacking
    /// direction). Set on GlulamData.Layup, it fills the lamellae's grades, and fills them again
    /// whenever the lamella count changes.
    /// </summary>
    public abstract class Layup
    {
        /// <summary>
        /// Set the grades (and species, if the layup has one) of the lamellae.
        /// </summary>
        public abstract void Apply(GlulamData data);

        /// <summary>
        /// Every lamella the same grade, e.g. a GL24h layup of T14 lamellae.
        /// </summary>
        public static Layup Homogeneous(string grade, string species = null) => new HomogeneousLayup(grade, species);

        /// <summary>
        /// Outer lamellae of a higher grade on both faces (top and bottom), the core of a lower one,
        /// e.g. a GL28c layup: 2 outer lamellae of T22 on each face, a T14 core.
        /// </summary>
        /// <param name="outerGrade">Grade of the outer lamellae.</param>
        /// <param name="outerCount">Number of outer lamellae on each face.</param>
        /// <param name="coreGrade">Grade of the core lamellae.</param>
        /// <param name="species">Optional species for all the lamellae.</param>
        public static Layup Combined(string outerGrade, int outerCount, string coreGrade, string species = null) =>
            new CombinedLayup(outerGrade, outerCount, coreGrade, species);

        protected static void Set(GlulamData data, int i, int j, string grade, string species)
        {
            var stick = data.Lamellae[i, j] ?? (data.Lamellae[i, j] = new Stick());
            stick.Grade = grade;
            if (!string.IsNullOrWhiteSpace(species))
                stick.Species = species;
        }
    }

    public class HomogeneousLayup : Layup
    {
        public readonly string Grade, Species;

        public HomogeneousLayup(string grade, string species = null)
        {
            Grade = grade;
            Species = species;
        }

        public override void Apply(GlulamData data)
        {
            for (int i = 0; i < data.NumWidth; ++i)
                for (int j = 0; j < data.NumHeight; ++j)
                    Set(data, i, j, Grade, Species);
        }

        public override string ToString() => $"Homogeneous {Grade}";
    }

    public class CombinedLayup : Layup
    {
        public readonly string OuterGrade, CoreGrade, Species;
        public readonly int OuterCount;

        public CombinedLayup(string outerGrade, int outerCount, string coreGrade, string species = null)
        {
            if (outerCount < 0) throw new ArgumentOutOfRangeException(nameof(outerCount));
            OuterGrade = outerGrade;
            OuterCount = outerCount;
            CoreGrade = coreGrade;
            Species = species;
        }

        public override void Apply(GlulamData data)
        {
            int n = data.NumHeight;
            for (int i = 0; i < data.NumWidth; ++i)
                for (int j = 0; j < n; ++j)
                {
                    bool outer = j < OuterCount || j >= n - OuterCount;
                    Set(data, i, j, outer ? OuterGrade : CoreGrade, Species);
                }
        }

        public override string ToString() => $"Combined {OuterGrade} x {OuterCount} / {CoreGrade}";
    }

    /// <summary>
    /// Extra material on a blank for machining, like a CIX blank's OverInside, OverOutside, OverE1 and OverE2.
    /// </summary>
    public struct MachiningAllowance
    {
        /// <summary>
        /// On the inside and outside faces: the lamella faces, towards and away from the centre of
        /// curvature (for a straight blank, the -Y and +Y faces).
        /// </summary>
        public double Inside, Outside;

        /// <summary>
        /// On each of the two side faces (the lamella edges, ±X).
        /// </summary>
        public double Sides;

        /// <summary>
        /// At the start and end of the blank.
        /// </summary>
        public double Start, End;

        public MachiningAllowance(double inside, double outside, double sides, double start, double end)
        {
            Inside = inside;
            Outside = outside;
            Sides = sides;
            Start = start;
            End = end;
        }

        public override string ToString() => $"Inside {Inside:0.###}, Outside {Outside:0.###}, Sides {Sides:0.###}, Start {Start:0.###}, End {End:0.###}";
    }
}
