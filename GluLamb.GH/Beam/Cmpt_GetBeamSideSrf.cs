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

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_GetBeamSideSrf : GH_Component
    {

        public Cmpt_GetBeamSideSrf()
          : base("Get Beam Surface", "BSrf",
              "Get Surface from a Beam side. ",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamSurface;
        public override Guid ComponentGuid => new Guid("1E1CD581-4830-4D62-B0B8-5773CD8CE39D");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Input Beam.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Side", "S", "Side of Beam to extract. 0 = Sides, 1 = Top / Bottom.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Offset", "O", "Offset distance from Glulam centreline (use negative values to go to opposite side).", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Width", "W", "Width of surface.", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("Extension", "E", "Amount to extend the Glulam centreline (to ensure surface overlaps).", GH_ParamAccess.item, 5.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Extracted surface.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get Beam
            Beam m_beam = null;
            DA.GetData<Beam>("Beam", ref m_beam);
            if (m_beam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid beam input.");
                return;
            }

            int side = 0;
            DA.GetData("Side", ref side);
            double offset = 0.0;
            DA.GetData("Offset", ref offset);
            double width = 0.0;
            DA.GetData("Width", ref width);
            double extension = 0.0;
            DA.GetData("Extension", ref extension);

            Brep b = BeamOps.GetSideSurface(m_beam, side, offset, width, extension);

            DA.SetData("Brep", b);
        }
    }
}