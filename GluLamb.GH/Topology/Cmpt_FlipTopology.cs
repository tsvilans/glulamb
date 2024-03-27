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
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using GluLamb;
using Rhino;

namespace GluLamb.GH.Components
{
    public class Cmpt_FlipTopology : GH_Component
    {
        public Cmpt_FlipTopology()
          : base("Flip Topology", "FlipTop",
              "Invert a tree of indices to create a new tree where every path is the index of the original element. " + 
                "I.e., remap a tree of connections that holds indices to elements to a tree of elements that holds indices to connections.",
              "GluLamb", UiNames.TopologySection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.FlipTopology;
        public override Guid ComponentGuid => new Guid("66ae9557-b770-4aa6-baf5-4251382df3f9");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Topology", "T", "Indices of elements as tree. Each path is the ID of the connection.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Values", "V", "Optional tree to invert in parallel. Must have same paths as the topology.", GH_ParamAccess.tree);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Topology", "T", "Indices of connections as tree. Each path is the ID of the element.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Index", "I", "Indices of connection parts as tree. Each path is the ID of the element.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Values", "V", "Optional tree to invert in parallel. Must have same paths as the topology.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            GH_Structure<GH_Integer> topology;
            GH_Structure<IGH_Goo> parallel;

            if (!DA.GetDataTree(0, out topology)) return;
            DA.GetDataTree(1, out parallel);

            var etree = new DataTree<GH_Integer>();
            var itree = new DataTree<GH_Integer>();
            var atree = new DataTree<IGH_Goo>();

            foreach (var path in topology.Paths)
            {
                var branch = topology[path];
                for (int i = 0; i < branch.Count; ++i)
                {
                    var newPath = new GH_Path(branch[i].Value);
                    etree.Add(new GH_Integer(path.Indices[0]), newPath);
                    itree.Add(new GH_Integer(i), newPath);

                    if (parallel != null && parallel.PathExists(path))
                    {
                        atree.Add(parallel[path][i], newPath);
                    }
                }
            }

            DA.SetDataTree(0, etree);
            DA.SetDataTree(1, itree);
            DA.SetDataTree(2, atree);
        }

    }
}