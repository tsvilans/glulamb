/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2020 Tom Svilans
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
using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.DocObjects;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateBlank : GH_Component, IGH_VariableParameterComponent
    {
        public Cmpt_CreateBlank()
          : base("Create Blank", "Blank",
              "Create a glulam blank for a beam.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GlulamCurved;
        public override Guid ComponentGuid => new Guid("f82e5284-63e3-460f-9f90-0a1aa8f608aa");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        readonly IGH_Param[] parameters = new IGH_Param[2]
        {
            new Param_Number() { Name = "LamellaWidth", NickName = "LW", Description = "Lamella width (X-axis).", Optional = true },
            new Param_Number() { Name = "LamellaHeight", NickName = "LH", Description = "Lamella height (Y-axis).", Optional = true },
        };

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Lamella dims", AddLamellaDimensions, true, Params.Input.Any(x => x.Name == "LamellaWidth") && Params.Input.Any(x => x.Name == "LamellaHeight"));
        }

        private void AddLamellaDimensions(object sender, EventArgs e)
        {
            AddParams(new int[] { 0, 1 });
        }

        private void AddParams(int[] indices)
        {
            foreach (var index in indices)
            {
                IGH_Param parameter = parameters[index];

                if (Params.Input.Any(x => x.Name == parameter.Name))
                {
                    Params.UnregisterInputParameter(Params.Input.First(x => x.Name == parameter.Name), true);
                }
                else
                {
                    int insertIndex = Params.Input.Count;
                    for (int i = 0; i < Params.Input.Count; i++)
                    {
                        int otherIndex = Array.FindIndex(parameters, x => x.Name == Params.Input[i].Name);
                        if (otherIndex > index)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                    Params.RegisterInputParam(parameter, insertIndex);
                }
            }
            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "A free-form or straight beam.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam blank.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData("Beam", ref beam)) return;

            double lamellaWidth = 0, lamellaHeight = 0;
            bool hasDimensions = Params.Input.Any(x => x.Name == "LamellaWidth");

            if (hasDimensions)
            {
                DA.GetData("LamellaWidth", ref lamellaWidth);
                DA.GetData("LamellaHeight", ref lamellaHeight);

                if (lamellaWidth <= 0 || lamellaHeight <= 0) hasDimensions = false;
            }

            Glulam glulam;

            if (!hasDimensions)
            {
                glulam = Glulam.CreateGlulam(beam, beam.Orientation, Standards.Standard.Eurocode);
            }
            else
            {
                var Nx = (int)Math.Ceiling(beam.Width / lamellaWidth);
                var Ny = (int)Math.Ceiling(beam.Height / lamellaHeight);

                glulam = Glulam.CreateGlulam(beam.Centreline, beam.Orientation, new GlulamData(Nx, Ny, lamellaWidth, lamellaHeight));
                glulam.OffsetX = beam.OffsetX;
                glulam.OffsetY = beam.OffsetY;
            }

            DA.SetData("Glulam", new GH_Glulam(glulam));
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index) => false;
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index) => false;
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index) => null;
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index) => false;
        void IGH_VariableParameterComponent.VariableParameterMaintenance() { }
    }
}