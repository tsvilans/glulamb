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
using System.Threading.Tasks;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_InterpolatePlanes : GH_Component
    {
        public Cmpt_InterpolatePlanes()
          : base("Interpolate Planes", "IntPl",
              "Interpolate between two planes using quaternion interpolation.",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.InterpolatePlanes;
        public override Guid ComponentGuid => new Guid("0453d9a7-a62f-458c-9b91-f329565d926f");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("PlaneA", "A", "First plane.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("PlaneB", "B", "Second plane.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Parameter", "t", "Parameter for interpolation.", GH_ParamAccess.item, 0.5);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Interpolated plane.", GH_ParamAccess.item);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Plane planeA = Plane.Unset;
            Plane planeB = Plane.Unset;

            double t = 0.5;

            DA.GetData("PlaneA", ref planeA);
            DA.GetData("PlaneB", ref planeB);

            if (planeA == Plane.Unset || planeB == Plane.Unset)
            {
                return;
            }

            DA.GetData("Parameter", ref t);

            var planeC = Interpolation.InterpolatePlanes2(planeA, planeB, t);

            DA.SetData("Plane", planeC);
        }
    }
}