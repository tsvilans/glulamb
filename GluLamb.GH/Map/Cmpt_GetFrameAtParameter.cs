﻿/*
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

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_GetFrameAtParameter : GH_Component
    {
        public Cmpt_GetFrameAtParameter()
          : base("Glulam Frame (t)", "GFrame(t)",
              "Get Glulam frame at parameter.",
              "GluLamb", "Map")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam to get plane from.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Parameter", "t", "Parameter at which to get plane.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Flip", "F", "Flip plane around Y-axis.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Output plane.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool m_flip = false;

            double m_parameter = 0;
            DA.GetData("Parameter", ref m_parameter);
            DA.GetData("Flip", ref m_flip);

            // Get Glulam
            Glulam m_glulam = null;
            DA.GetData<Glulam>("Glulam", ref m_glulam);
            if (m_glulam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid glulam input.");
                return;
            }

            Plane plane = m_glulam.GetPlane(m_parameter);

            if (m_flip)
                plane = plane.FlipAroundYAxis();

            DA.SetData("Plane", plane);
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
            get { return new Guid("6322C15A-DF23-49F6-94CD-AFA1996D0CDB"); }
        }
    }
}