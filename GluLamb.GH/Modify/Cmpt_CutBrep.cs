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
    public class Cmpt_CutBrep : GH_Component
    {
        public Cmpt_CutBrep()
          : base("Cut Brep", "Cut",
              "A combination of solid boolean subtraction and BooleanSplit on a Brep.",
              "GluLamb", "Modify")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep to cut.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Cutters", "C", "Breps to cut with.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Result", "R", "Result of cutting operation.", GH_ParamAccess.item);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Brep> breps = null;
            GH_Structure<GH_Brep> cutters = null;

            //Brep brep = null;
            //List<Brep> cutters = new List<Brep>();

            if (!DA.GetDataTree("Brep", out breps))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid Brep input.");
                return;
            }
            if (!DA.GetDataTree("Cutters", out cutters))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid cutter input.");
                return;
            }

            GH_Structure<GH_Brep> resTree = new GH_Structure<GH_Brep>();

            foreach (var path in breps.Paths)
            {
                resTree.EnsurePath(path);
            }

            Parallel.For(0, breps.Paths.Count, index =>
            {

                GH_Path path = breps.Paths[index];
                if (cutters.PathExists(path) && breps[path].Count > 0)
                {
                    resTree[path].Add(new GH_Brep(breps[path][0].Value.Cut(
                        cutters[path].Select(x => x.Value))));
                }
                else if (cutters.PathCount == 1 && breps[path].Count > 0) // Handle a single list of cutters
                {
                    resTree[path].Add(new GH_Brep(breps[path][0].Value.Cut(
                        cutters[cutters.Paths[0]].Select(x => x.Value))));
                }
            });

            DA.SetDataTree(0, resTree);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_Bisector_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("6c3e67cd-b172-48d2-bab9-497b3ec3047d"); }
        }
    }
}