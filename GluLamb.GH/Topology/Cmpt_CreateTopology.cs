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
    public class Cmpt_CreateTopology : GH_Component
    {
        public Cmpt_CreateTopology()
          : base("Create Topology", "Topology",
              "Find links between individual curve elements.",
              "GluLamb", UiNames.TopologySection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.CreateTopology;
        public override Guid ComponentGuid => new Guid("3ae73fff-be6f-4957-bb62-c1ad2f2d23b8");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        double m_scale_to_doc = 1.0;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            m_scale_to_doc = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);

            pManager.AddCurveParameter("Curves", "C", "Curve elements as tree. Branch paths serve as element IDs.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Search distance", "SD", "Distance to consider joints between elements.", GH_ParamAccess.item, m_scale_to_doc * 0.1);
            pManager.AddNumberParameter("Overlap distance", "OD", "Distance to consider overlaps between element ends (splicing).", GH_ParamAccess.item, m_scale_to_doc * 0.05);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Connected pairs", "CP", "Indices of connected pairs as tree. Each path is the ID of the connection.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Connected parameters", "CT", "Parameters of connections as tree. Each path is the ID of the connection.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double search_distance = 100.0, overlap_distance = 50.0;

            GH_Structure<GH_Curve> curveTree;

            if (!DA.GetDataTree(0, out curveTree)) return;

            DA.GetData<double>("Search distance", ref search_distance);
            DA.GetData<double>("Overlap distance", ref overlap_distance);

            var curves = new Dictionary<int, Curve>();
            foreach (var path in curveTree.Paths)
            {
                var branch = curveTree[path];
                if (branch.Count < 1) continue;
                curves.Add(path.Indices[0], branch[0].Value);

                if (branch.Count > 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Got more curves than expected. Branches with more than one curve are discarded.");
                }
            }

            var topology = Topology.FindCurvePairs(curves, search_distance, overlap_distance);

            var pairTree = new DataTree<GH_Integer>();
            var paramTree = new DataTree<GH_Number>();

            var gh_path = new GH_Path(0);
            for (int i = 0; i < topology.Count; i++)
            {
                pairTree.Add(new GH_Integer(topology[i].A), gh_path);
                pairTree.Add(new GH_Integer(topology[i].B), gh_path);
                paramTree.Add(new GH_Number(topology[i].tA), gh_path);
                paramTree.Add(new GH_Number(topology[i].tB), gh_path);
                gh_path = gh_path.Increment(0);
            }

            DA.SetDataTree(0, pairTree);
            DA.SetDataTree(1, paramTree);
        }

    }
}