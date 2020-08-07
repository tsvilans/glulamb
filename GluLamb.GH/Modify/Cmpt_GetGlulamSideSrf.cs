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
    public class Cmpt_GetGlulamSideSrf : GH_Component
    {

        public Cmpt_GetGlulamSideSrf()
          : base("Get Glulam Surface", "GSrf",
              "Get Surface from a Glulam side. ",
              "GluLamb", "Modify")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Input Glulam.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Side", "S", "Side of Glulam to extract. 0 = Sides, 1 = Top / Bottom.", GH_ParamAccess.item, 0);
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
            object raw_in = null;
            if (!DA.GetData("Glulam", ref raw_in))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No Glulam specified.");
                return;
            }
            GH_Glulam ghg = raw_in as GH.GH_Glulam;
            if (ghg == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not valid Glulam object.");
                return;
            }
            Glulam g = ghg.Value;

            int side = 0;
            DA.GetData("Side", ref side);
            double offset = 0.0;
            DA.GetData("Offset", ref offset);
            double width = 0.0;
            DA.GetData("Width", ref width);
            double extension = 0.0;
            DA.GetData("Extension", ref extension);

            Brep b = g.GetSideSurface(side, offset, width, extension);

            DA.SetData("Brep", b);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_GetGlulamSrf_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1E1CD581-4830-4D62-B0B8-5773CD8CE39D"); }
        }
    }
}