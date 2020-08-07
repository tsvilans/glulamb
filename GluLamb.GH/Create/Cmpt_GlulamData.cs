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
    public class Cmpt_GlulamData : GH_Component
    {
        public Cmpt_GlulamData()
          : base("GlulamData", "GlulamData",
              "Create glulam data.",
              "GluLamb", "Create")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("LamW", "LW", "Lamella width.", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("LamH", "LH", "Lamella height.", GH_ParamAccess.item, 20.0);
            pManager.AddIntegerParameter("NumW", "NW", "Number of lamellas in X-direction.", GH_ParamAccess.item, 4);
            pManager.AddIntegerParameter("NumH", "NH", "Number of lamellas in Y-direction.", GH_ParamAccess.item, 4);
            pManager.AddIntegerParameter("Interpolation", "Int", "Interpolation method to use between glulam frames.", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("Samples", "S", "Number of samples along glulam length for generating cross-sections.", GH_ParamAccess.item, 100);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("GlulamData", "GD", "GlulamData object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double lw = 0, lh = 0;
            int nw = 0, nh = 0;

            int samples = 0;
            int interpolation = 2;

            DA.GetData("LamW", ref lw);
            DA.GetData("LamH", ref lh);
            DA.GetData("NumW", ref nw);
            DA.GetData("NumH", ref nh);
            DA.GetData("Samples", ref samples);
            DA.GetData("Interpolation", ref interpolation);

            GlulamData data = new GlulamData(Math.Max(nw, 1), Math.Max(nh, 1), lw, lh, samples);
            data.InterpolationType = (GlulamData.Interpolation)interpolation;

            DA.SetData("GlulamData", new GH_GlulamData(data));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_GlulamData_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("22DF602E-4E4E-4141-94BC-4378BCA839ED"); }
        }
    }
}