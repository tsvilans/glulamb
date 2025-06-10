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
    public class Cmpt_Overbend : GH_Component
    {
        public Cmpt_Overbend()
          : base("Overbend", "Over",
              "Over- or under-bend a curve by a factor.",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamOffset;
        public override Guid ComponentGuid => new Guid("387841CA-4116-429B-9951-9B8797F1BF4C");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Curve to modify.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Factor", "F", "Factor to over- or under-bend by.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Samples", "S", "Resolution of discretization.", GH_ParamAccess.item, 200);
            pManager.AddBooleanParameter("Centre", "C", "Centre the resulting curve on the original curve.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Over- or under-bent curve.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            Curve curve = null;
            double factor = 1.0;
            int samples = 200;
            bool middle = false;

            if (!DA.GetData<Curve>("Curve", ref curve))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid Curve input.");
                return;
            }

            DA.GetData("Factor", ref factor);
            DA.GetData("Samples", ref samples);
            DA.GetData("Centre", ref middle);

            var new_curve = curve.Overbend(factor, samples, middle);

            DA.SetData("Curve", new GH_Curve(new_curve));
        }
    }
}