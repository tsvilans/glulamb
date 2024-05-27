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

namespace GluLamb.Raw.GH
{

    public class Cmpt_BasePlane : GH_Component
    {
        public Cmpt_BasePlane()
            : base("BasePlane", "BPlane",
                "Calculate a good baseplane for a Brep with straight edges.",
                "GluLamb", UiNames.RawSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.FindPlane;
        public override Guid ComponentGuid => new Guid("3dccbbf9-c5dc-4435-9be4-0121fabd7e0d");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep to analyse for base plane.", GH_ParamAccess.item);
            pManager.AddVectorParameter("XAxis", "X", "Optional vector to lock the primary direction (X-axis).", GH_ParamAccess.item, Vector3d.Unset);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Baseplane", "P", "Best baseplane for Brep.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            if (!DA.GetData("Brep", ref brep)) return;

            Vector3d xaxis = Vector3d.Unset;
            DA.GetData("XAxis", ref xaxis);

            var bplane = GluLamb.Utility.FindBestBasePlane(brep, xaxis);

            DA.SetData("Baseplane", bplane);
        }
    }
}
