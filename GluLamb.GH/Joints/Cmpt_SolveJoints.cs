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

using GluLamb.Joints;

namespace GluLamb.GH.Components
{
    public class Cmpt_SolveJoints : GH_Component
    {
        public Cmpt_SolveJoints()
          : base("Solve joints", "Solve",
              "Solve joint conditions and construct joints.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Joint;
        public override Guid ComponentGuid => new Guid("b43a7437-972c-49d3-8f90-cadeb725381a");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Structure", "S", "Structure.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Joint solver", "JS", "Joint solver to use for solving joints.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Search radius", "SR", "Radius to search for joints.", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("End threshold", "ET", "Length from end to consider overlap.", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("Merge distance", "MD", "Distance within which to merge joint conditions.", GH_ParamAccess.item, 50.0);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Structure", "S", "Timber structure.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            GH_Structure structure = null;
            DA.GetData<GH_Structure>("Structure", ref structure);

            object joint_solver_obj = null;

            DA.GetData("Joint solver", ref joint_solver_obj);

            var solver = (joint_solver_obj as GH_ObjectWrapper).Value as JointSolver;
            if (solver == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Wrong input for solver.");
                return;
            }

            double search_radius = 100, end_threshold = 50, merge_distance = 30;
            DA.GetData("Search radius", ref search_radius);
            DA.GetData("End threshold", ref end_threshold);
            DA.GetData("Merge distance", ref merge_distance);

            var jcs = GluLamb.Factory.JointCondition.FindJointConditions(structure.Value.Elements, search_radius, end_threshold, merge_distance);
            var joints = solver.Solve(structure.Value.Elements, jcs);
            structure.Value.Joints = joints;

            foreach (var element in structure.Value.Elements)
                element.Joints = new List<Joint>();

            foreach (var joint in joints)
            {
                if (joint == null)
                {
                    Rhino.RhinoApp.WriteLine("Joint {0} is null.", joint);
                    continue;
                }
                foreach(var part in joint.Parts)
                {
                    part.Element.Joints.Add(joint);
                }
            }

            DA.SetData("Structure", structure);
        }
    }
}