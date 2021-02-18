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
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateGlulamBeamElement : GH_Component
    {
        public Cmpt_CreateGlulamBeamElement()
          : base("Glulam Beam", "GBeam",
              "Create glulam beam element.",
              "GluLamb", "Create")
        {
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name of glulam beam.", GH_ParamAccess.item, "GlulamBeamElement");
            pManager.AddGenericParameter("Glulam", "G", "Glulam blank.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Handle for element (baseplane).", GH_ParamAccess.item);

            pManager[0].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Element", "E", "Glulam beam element.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Glulam glulam = null;

            if (!DA.GetData("Glulam", ref glulam))
                return;

            string name = "GlulamBeamElement";
            DA.GetData("Name", ref name);

            Plane handle = Plane.Unset;
            DA.GetData("Plane", ref handle);

            BeamElement beam_element;
            if (handle == Plane.Unset)
                beam_element = new BeamElement(glulam, name);
            else
                beam_element = new BeamElement(glulam, handle, name);

            DA.SetData("Element", new GH_Element(beam_element));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_FreeformGlulam_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("950B8230-422B-45D0-A5EA-01FE2E446E8A"); }
        }
    }
}