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
    public class Cmpt_OffsetGlulam : GH_Component
    {
        public Cmpt_OffsetGlulam()
          : base("Offset Glulam", "OfGlulam",
              "Offset Glulam in its local space.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamOffset;
        public override Guid ComponentGuid => new Guid("774A63FE-35DC-4B30-812A-86420410C53B");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam to offset.", GH_ParamAccess.item);
            pManager.AddNumberParameter("OffsetX", "X", "Amount to offset in X direction.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("OffsetY", "Y", "Amount to offset in Y direction.", GH_ParamAccess.item, 0.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Offset Glulam.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            Glulam m_glulam = null;

            if (!DA.GetData<Glulam>("Glulam", ref m_glulam))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not get Glulam.");
                return;
            }

            double x = 0.0, y = 0.0;
            DA.GetData("OffsetX", ref x);
            DA.GetData("OffsetY", ref y);

            Curve crv = m_glulam.CreateOffsetCurve(x, y);


           // GlulamData data = GlulamData.FromCurveLimits(crv,g.Data.NumWidth * g.Data.LamWidth, g.Data.NumHeight * g.Data.LamHeight, g.GetAllPlanes());

            //data.Samples = g.Data.Samples;

            Glulam offset_glulam = Glulam.CreateGlulam(crv, m_glulam.Orientation.Duplicate(), m_glulam.Data.Duplicate());

            DA.SetData("Glulam", new GH_Glulam(offset_glulam));
        }
    }
}