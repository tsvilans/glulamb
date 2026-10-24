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
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// A grade per layer, bottom (-Y) to top (+Y). If the glulam has a different number of
        /// lamellae, each lamella takes the grade at its relative height.
        /// </summary>
        public static Layup Layers(IList<string> grades, string species = null) => new LayeredLayup(grades, species);

        /// <summary>
        /// A layup from a list of grades: one grade, homogeneous; two grades and an outer count above
        /// 0, combined (outer, core); otherwise one grade per layer. Null for no grades.
        /// </summary>
        public static Layup FromGrades(IList<string> grades, int outerCount = 0, string species = null)
        {
            var list = grades?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (list == null || list.Count == 0) return null;
            if (list.Count == 1) return Homogeneous(list[0], species);
            if (list.Count == 2 && outerCount > 0) return Combined(list[0], outerCount, list[1], species);
            return Layers(list, species);
        }

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

    public class LayeredLayup : Layup
    {
        public readonly string[] Grades;
        public readonly string Species;

        public LayeredLayup(IList<string> grades, string species = null)
        {
            if (grades == null || grades.Count == 0) throw new ArgumentException("A layered layup needs at least one grade.", nameof(grades));
            Grades = grades.ToArray();
            Species = species;
        }

        public override void Apply(GlulamData data)
        {
            int n = data.NumHeight;
            for (int i = 0; i < data.NumWidth; ++i)
                for (int j = 0; j < n; ++j)
                    Set(data, i, j, Grades[Math.Min(Grades.Length - 1, j * Grades.Length / n)], Species);
        }

        public override string ToString() => $"Layers {string.Join(" ", Grades)}";
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

        /// <summary>
        /// From values in the order inside, outside, sides, start, end; missing ones are 0.
        /// </summary>
        public static MachiningAllowance FromValues(IList<double> values)
        {
            double At(int i) => values != null && i < values.Count ? values[i] : 0;
            return new MachiningAllowance(At(0), At(1), At(2), At(3), At(4));
        }

        public double[] ToValues() => new[] { Inside, Outside, Sides, Start, End };

        public override string ToString() => $"Inside {Inside:0.###}, Outside {Outside:0.###}, Sides {Sides:0.###}, Start {Start:0.###}, End {End:0.###}";
    }
}
