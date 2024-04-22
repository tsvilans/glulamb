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
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Types.Transforms;
using Rhino;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_DeBeam : GH_Component, IGH_VariableParameterComponent
    {
        public Cmpt_DeBeam()
          : base("Beam Info", "BInfo",
              "Get beam parameters.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamInfo;
        public override Guid ComponentGuid => new Guid("5e74cb63-e332-4d3e-a1a1-0504a388b20e");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        private double m_scale = 1.0;

        readonly IGH_Param[] parameters = new IGH_Param[4]
        {
            new Param_GenericObject() {Name = "Orientation", NickName = "O", Description = "Orientation object for the beam.", Optional = true },
            new Param_Number() { Name = "OffsetX", NickName = "OX", Description = "Cross-section width offset (X-axis).", Optional = true },
            new Param_Number() { Name = "OffsetY", NickName = "OY", Description = "Cross-section height offset (Y-axis).", Optional = true },
            new Param_Integer() { Name = "Samples", NickName = "S", Description = "Samples along length.", Optional = true },
        };

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Orientation", AddOrientation, true, Params.Output.Any(x => x.Name == "Orientation"));
            Menu_AppendItem(menu, "Offsets", AddOffsets, true, Params.Output.Any(x => x.Name == "OffsetX") && Params.Output.Any(x => x.Name == "OffsetY"));
            Menu_AppendItem(menu, "Samples", AddSamples, true, Params.Output.Any(x => x.Name == "Samples"));
        }

        private void AddOrientation(object sender, EventArgs e)
        {
            AddParam(0);
        }

        private void AddOffsets(object sender, EventArgs e)
        {
            AddParams(new int[] { 1, 2 });
        }
        private void AddSamples(object sender, EventArgs e) => AddParam(3);

        private void AddParams(int[] indices)
        {
            foreach (var index in indices)
            {
                IGH_Param parameter = parameters[index];

                if (Params.Output.Any(x => x.Name == parameter.Name))
                {
                    Params.UnregisterOutputParameter(Params.Output.First(x => x.Name == parameter.Name), true);
                }
                else
                {
                    int insertIndex = Params.Output.Count;
                    for (int i = 0; i < Params.Output.Count; i++)
                    {
                        int otherIndex = Array.FindIndex(parameters, x => x.Name == Params.Output[i].Name);
                        if (otherIndex > index)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                    Params.RegisterOutputParam(parameter, insertIndex);
                }
            }
            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        private void AddParam(int index)
        {
            IGH_Param parameter = parameters[index];

            if (Params.Output.Any(x => x.Name == parameter.Name))
            {
                Params.UnregisterOutputParameter(Params.Output.First(x => x.Name == parameter.Name), true);
            }
            else
            {
                int insertIndex = Params.Output.Count;
                for (int i = 0; i < Params.Output.Count; i++)
                {
                    int otherIndex = Array.FindIndex(parameters, x => x.Name == Params.Output[i].Name);
                    if (otherIndex > index)
                    {
                        insertIndex = i;
                        break;
                    }
                }
                Params.RegisterOutputParam(parameter, insertIndex);
            }
            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam to deconstruct.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Centreline", "C", "Centreline of beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "W", "Width of beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "Height of beam.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            m_scale = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            Beam beam = null;

            bool hasOrientation = Params.Output.Any(x => x.Name == "Orientation");
            bool hasSamples = Params.Output.Any(x => x.Name == "Samples");
            bool hasAlignment = Params.Output.Any(x => x.Name == "Alignment");
            bool hasOffsets = Params.Output.Any(x => x.Name == "OffsetX");

            DA.GetData("Beam", ref beam);

            if (beam == null) return;

            DA.SetData("Centreline", beam.Centreline);
            DA.SetData("Width", beam.Width);
            DA.SetData("Height", beam.Height);

            if (hasOffsets)
            {
                DA.SetData("OffsetX", beam.OffsetX);
                DA.SetData("OffsetY", beam.OffsetY);
            }

            if (hasSamples)
            {
                DA.SetData("Samples", 50);
            }

            if (hasOrientation)
            {
                DA.SetData("Orientation", beam.Orientation.GetDriver());
            }


        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index) => false;
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index) => false;
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index) => null;
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index) => false;
        void IGH_VariableParameterComponent.VariableParameterMaintenance() { }
    }
}