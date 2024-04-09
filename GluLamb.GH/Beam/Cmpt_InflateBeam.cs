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

namespace GluLamb.GH.Components
{
    public class Cmpt_InflateBeam : GH_Component
    {

        public Cmpt_InflateBeam()
          : base("Inflate Beam", "InfBeam",
              "Expand Beam in all directions.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamInflate;
        public override Guid ComponentGuid => new Guid("3372B2F2-9875-47D5-B175-14EF2BFEC927");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Input Beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Amount", "A", "Amount to inflate all sides.", GH_ParamAccess.item, 10.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The inflated Beam.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData<Beam>("Beam", ref beam))
            {
                return;
            }

            double offset = 0.0;
            DA.GetData("Amount", ref offset);

            var new_beam = beam.Duplicate();
            new_beam.Width += offset;
            new_beam.Height += offset;

            DA.SetData("Beam", new GH_Beam(new_beam));
        }
    }
}