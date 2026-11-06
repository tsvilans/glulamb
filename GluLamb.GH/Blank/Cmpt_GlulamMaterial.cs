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
using Grasshopper.Kernel;

namespace GluLamb.GH.Components
{
    public class Cmpt_GlulamMaterial : GH_Component
    {
        public Cmpt_GlulamMaterial()
          : base("Glulam Material", "GMat",
              "Set a glulam's material data: strength class, species, lamella grades (layup) and machining allowances. " +
              "Inputs left empty keep the glulam's current values. Delaminate reads them back.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GlulamStraight;
        public override Guid ComponentGuid => new Guid("8b2d4f6a-1c3e-4a57-9d80-5e7f2a1b3c94");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam to set the material of.", GH_ParamAccess.item);
            pManager.AddTextParameter("Strength Class", "SC", "Strength class, e.g. GL24h or GL28c.", GH_ParamAccess.item);
            pManager.AddTextParameter("Species", "S", "Species of all the lamellae, e.g. Spruce.", GH_ParamAccess.item);
            pManager.AddTextParameter("Grades", "Gr", "Lamella grades: one grade for a homogeneous layup; two grades (outer, core) with an Outer Count for a combined layup; " +
                "otherwise one grade per layer, bottom (-Y) to top (+Y).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Outer Count", "OC", "With two grades: number of outer lamellae on each face.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Allowance", "A", "Machining allowances: inside, outside, sides, start, end (missing values are 0).", GH_ParamAccess.list);

            for (int i = 1; i < 6; ++i)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "A copy of the glulam with the material data set (same id).", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "The glulam's material data.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object input = null;
            if (!DA.GetData(0, ref input)) return;
            var glulam = (input is GH_Glulam goo ? goo.Value : input as Glulam)?.DuplicateGlulam();
            if (glulam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Glulam needs a glulam (e.g. from Create Blank or Cluster Beams).");
                return;
            }

            string strengthClass = null, species = null;
            int outerCount = 0;
            var grades = new List<string>();
            var allowance = new List<double>();
            DA.GetData(1, ref strengthClass);
            DA.GetData(2, ref species);
            DA.GetDataList(3, grades);
            DA.GetData(4, ref outerCount);
            DA.GetDataList(5, allowance);

            var data = glulam.Data;
            if (strengthClass != null)
                data.StrengthClass = strengthClass.Trim();
            var layup = Layup.FromGrades(grades, outerCount, species);
            if (layup != null)
                data.Layup = layup;
            else
                data.SetSpecies(species);
            if (allowance.Count > 0)
                data.Allowance = MachiningAllowance.FromValues(allowance);

            if (grades.Count == 2 && outerCount <= 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Two grades without an Outer Count are taken as one grade per layer.");

            DA.SetData(0, new GH_Glulam(glulam));
            DA.SetData(1, $"{data.StrengthClass} {data.Species} {data.Layup}; allowance {data.Allowance}".Trim());
        }
    }
}
