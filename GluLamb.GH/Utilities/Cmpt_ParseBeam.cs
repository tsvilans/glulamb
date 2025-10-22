/*
 * RawLamb
 * Copyright 2024 Tom Svilans
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

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GluLamb.GH
{

    public class Cmpt_ParseBeam : GH_Component
    {
        public Cmpt_ParseBeam()
            : base("GetBeam", "BGet",
                "Creates a straight GluLamb beam from geometry.",
                "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamNew;
        public override Guid ComponentGuid => new Guid("29B85827-30BE-4114-8261-4E48B0F56C83");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep to analyse.", GH_ParamAccess.item);
            pManager.AddVectorParameter("XAxis", "X", "Optional vector to lock the primary direction (X-axis).", GH_ParamAccess.item, Vector3d.Unset);
            pManager.AddVectorParameter("Up", "U", "Optional vector to lock the up-axis of the cross-section.", GH_ParamAccess.item, Vector3d.Unset);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            if (!DA.GetData("Brep", ref brep)) return;

            if (brep == null) return;

            Vector3d xaxis = Vector3d.Unset, up = Vector3d.Unset;
            DA.GetData("XAxis", ref xaxis);
            DA.GetData("Up", ref up);

            var beam = Beam.StraightFromGeometry(brep, xaxis, up);

            DA.SetData("Beam", new GH_Beam(beam));
        }
    }
}
