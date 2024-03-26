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
    public class Cmpt_AssembleStructure : GH_Component
    {
        public Cmpt_AssembleStructure()
          : base("Assemble structure", "Assemble",
              "Assemble structure.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_FreeformGlulam_24x24;
        public override Guid ComponentGuid => new Guid("62EF7D36-3052-48D6-BC82-3DB4B8606217");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name of structure.", GH_ParamAccess.item, "New structure");
            pManager.AddGenericParameter("Elements", "E", "Timber elements to assemble.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Search distance", "SD", "Distance to consider joints between elements.", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("Overlap distance", "OD", "Distance to consider overlaps between element ends (splicing).", GH_ParamAccess.item, 50.0);

            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Structure", "S", "Timber structure.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double search_distance = 100.0, overlap_distance = 50.0;

            DA.GetData<double>("Search distance", ref search_distance);
            DA.GetData<double>("Overlap distance", ref overlap_distance);

            string name = "New structure";
            DA.GetData<string>(0, ref name);

            var elements = new List<GH_Element>();
            //var elements = new List<GH_ObjectWrapper>();
            //var elements = new List<object>();
            DA.GetDataList(1, elements);

            List<BeamElement> beams = new List<BeamElement>();

            foreach (var element in elements)
            {
                if (element == null) continue;

                if (element.Value is BeamElement)
                {
                    var beam = element.Value as BeamElement;
                    beams.Add(beam);
                }

            }

            //Rhino.RhinoApp.WriteLine("Num beams: {0}", beams.Count);

            if (beams.Count < 1) throw new Exception("No beams to assemble.");

            Structure structure = Structure.FromBeamElements(beams, search_distance, overlap_distance);
            structure.Name = name;

            DA.SetData("Structure", new GH_Structure(structure));
        }
    }
}