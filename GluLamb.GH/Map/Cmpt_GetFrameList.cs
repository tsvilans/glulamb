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

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_GetFrameList : GH_Component
    {
        public Cmpt_GetFrameList()
          : base("Get Frame List", "GetFrames",
              "Gets Glulam frames at multiple parameters along a Glulam object.",
              "GluLamb", "Map")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Input glulam blank to deconstruct.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Parameters", "t", "Parameters at which to extract a Glulam frame.", GH_ParamAccess.list);
            //pManager.AddIntegerParameter("Number", "N", "Number of equally-spaced frames to extract.", GH_ParamAccess.item, 10);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Planes", "P", "Extracted Glulam planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Glulam m_glulam = null;

            if (!DA.GetData<Glulam>("Glulam", ref m_glulam))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid glulam input.");
                return;
            }

            List<double> m_parameters = new List<double>();
            DA.GetDataList("Parameters", m_parameters);


            //double[] tt = g.Centreline.DivideByCount(N, true);

            Plane[] planes = m_parameters.Select(x => m_glulam.GetPlane(x)).ToArray();

            DA.SetDataList("Planes", planes);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_GlulamFrame_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("F5F14D9B-D168-4B35-86AC-D64F4634EC67"); }
        }
    }
}