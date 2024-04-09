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
    public class Cmpt_GetBeamFace : GH_Component
    {

        public Cmpt_GetBeamFace()
          : base("Get Beam Faces", "BFace",
              "Get a specific face or multiple faces of the Beam. ",
              "GluLamb", UiNames.BeamSection)
        {
        }
        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamFace;
        public override Guid ComponentGuid => new Guid("66C2719B-5992-41D2-96A6-0EA88539472C");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Input Beam.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Side", "S", "Side of Beam to extract. Use bitmask flags to get multiple sides.", GH_ParamAccess.item, 0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Faces", "F", "Beam faces.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData<Beam>("Beam", ref beam))
            {
                return;
            }

            int side = 0;
            DA.GetData("Side", ref side);

            Brep[] breps = BeamOps.GetFaces(beam, side);

            DA.SetDataList("Faces", breps);
        }
    }
}