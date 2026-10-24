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
using System.Linq;

using Rhino.Geometry;

using GluLamb.Blanks;

namespace GluLamb
{
    /// <summary>
    /// A glulam as a blank.
    /// </summary>
    public abstract partial class Glulam : IBlank
    {
        Rectangle3d IBlank.GetSection(double t) =>
            new Rectangle3d(GetPlane(t), new Interval(-Width * 0.5, Width * 0.5), new Interval(-Height * 0.5, Height * 0.5));

        /// <summary>
        /// The strength class, species and allowance from Data. Setting it sets those on Data (a
        /// species of "Composite" or "Homogeneous" leaves the lamellae's species as they are).
        /// </summary>
        BlankMaterial IBlank.Material
        {
            get => new BlankMaterial(Data.StrengthClass, Data.Species, Data.Allowance);
            set
            {
                Data.StrengthClass = value.StrengthClass ?? "";
                if (value.Species != "Composite" && value.Species != "Homogeneous")
                    Data.SetSpecies(value.Species);
                Data.Allowance = value.Allowance;
            }
        }

        /// <summary>
        /// The lamella layers through the height. A layer of several lamellae across the width
        /// reads "Mixed" where their grades or species differ.
        /// </summary>
        IReadOnlyList<BlankLayer> IBlank.Layers
        {
            get
            {
                double length = Centreline.GetLength();
                var layers = new List<BlankLayer>();
                for (int j = 0; j < Data.NumHeight; ++j)
                {
                    var sticks = Enumerable.Range(0, Data.NumWidth).Select(i => Data.Lamellae[i, j]).Where(x => x != null).ToList();
                    string Same(IEnumerable<string> values)
                    {
                        var distinct = values.Distinct().ToList();
                        return distinct.Count == 1 ? distinct[0] : distinct.Count == 0 ? "" : "Mixed";
                    }
                    layers.Add(new BlankLayer(Data.LamHeight, 0, length, Same(sticks.Select(x => x.Species)), Same(sticks.Select(x => x.Grade ?? ""))));
                }
                return layers;
            }
        }

        Brep IBlank.ToBrep() => ToBrep();

        double IBlank.GetVolume() => GetVolume();

        IBlank IBlank.DuplicateBlank() => DuplicateGlulam();
    }
}
