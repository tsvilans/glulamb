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
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Types.Transforms;
using Rhino;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_DeJoint : GH_Component
    {
        public Cmpt_DeJoint()
          : base("Joint Info", "JInfo",
              "Get joint parameters.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.JointInfo;
        public override Guid ComponentGuid => new Guid("4d5548e8-d3e8-4ce0-9de6-5d19c9c92a7d");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Joint", "J", "Joint to deconstruct.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Position", "P", "Location of joint.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Element IDs", "E", "Indices of the connected beams.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Cases", "C", "Numbers representing the case of each joint part.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Parameters", "P", "Parameters for each beam at which the joint occurs.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree<IGH_Goo>(0, out GH_Structure<IGH_Goo> joints)) return;

            var elementIndexTree = new DataTree<int>();
            var caseTree = new DataTree<int>();
            var parameterTree = new DataTree<double>();
            var positionTree = new DataTree<Point3d>();

            for (int i = 0; i < joints.Paths.Count; ++i)
            {
                var path = joints.Paths[i];
                var branch = joints[path];
                if (branch.Count < 1) continue;

                path.AppendElement(0);

                for (int j = 0; j < branch.Count; ++j)
                {
                    var ghJoint = branch[j] as GH_Joint;
                    if (ghJoint == null) continue;

                    var joint = ghJoint.Value;
                    if (joint == null) continue;

                    foreach (var part in joint.Parts)
                    {
                        elementIndexTree.Add(part.ElementIndex, path);
                        caseTree.Add(part.Case, path);
                        parameterTree.Add(part.Parameter, path);
                    }
                    positionTree.Add(joint.Position, path);

                    //path = path.Increment(path.Indices.Length - 1);
                    path = path.Increment(0);
                }

            }

            DA.SetDataTree(0, positionTree);
            DA.SetDataTree(1, elementIndexTree);
            DA.SetDataTree(2, caseTree);
            DA.SetDataTree(3, parameterTree);
        }
    }
}