/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
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
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_NamedFrame : GH_Component
    {
        public Cmpt_NamedFrame()
          : base("Named Frame", "NFrame",
              "Gets a named plane of a beam or blank, e.g. \"press\": the plane that orients a blank for the press.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("2f6b0c91-8e34-4d7a-b5c2-9a1e7d4f3b60");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam or blank.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Name of the plane.", GH_ParamAccess.item, "press");
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "The plane.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "N", "Names of all the beam's planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData(0, ref beam) || beam == null) return;
            string name = "press";
            DA.GetData(1, ref name);

            var frames = beam.Frames;
            if (frames != null) DA.SetDataList(1, frames.Keys.ToList());
            if (frames != null && frames.TryGetValue(name, out Plane plane)) DA.SetData(0, plane);
            else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"The beam has no plane named \"{name}\".");
        }
    }
}
