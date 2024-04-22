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
using Rhino;
using Grasshopper.Kernel.Data;
using Grasshopper;
using System.Collections;

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
            pManager.AddGenericParameter("Beams", "B", "Beams.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Joints", "J", "Joints to solve and construct.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Types", "T", "Type specification for joints.", GH_ParamAccess.tree);

            pManager.AddGenericParameter("Settings", "S", "Optional joint settings to specify types of joints.", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Joints", "J", "Joints.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Geometry", "G", "Cutting geometry for each beam.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var beams = new Dictionary<int, Beam>();
            var joints = new Dictionary<int, JointX>();
            var types = new Dictionary<int, string>();

            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> beamTree)) return;
            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> jointTree)) return;
            if (!DA.GetDataTree(2, out GH_Structure<GH_String> typesTree)) return;

            foreach (var path in beamTree.Paths)
            {
                var branch = beamTree[path];
                if (branch.Count < 1) continue;

                var beam = branch[0] as GH_Beam;
                if (beam == null) continue;

                beams.Add(path.Indices[0], beam.Value);
            }

            foreach (var path in jointTree.Paths)
            {
                var branch = jointTree[path];
                if (branch.Count < 1) continue;

                var joint = branch[0] as GH_Joint;
                if (joint == null) continue;

                joints.Add(path.Indices[0], joint.Value);
            }

            foreach (var path in typesTree.Paths)
            {
                var branch = typesTree[path];
                if (branch.Count < 1) continue;

                var type = branch[0] as GH_String;
                if (type == null) continue;

                types.Add(path.Indices[0], type.Value);
            }

            var keys = new List<int>(joints.Keys);
            foreach (var key in keys)
            {
                if (!joints.ContainsKey(key) || !types.ContainsKey(key)) continue;
                
                var joint = joints[key];
                var type = types[key];

                switch (type)
                {
                    case ("X"):
                        var crossJoint = new CrossJointX(joint);
                        crossJoint.Construct(beams);
                        joint = crossJoint;
                        break;
                    case ("T"):
                        var tenonJoint = new TJointX(joint);
                        tenonJoint.BlindOffset = 30;

                        tenonJoint.Construct(beams);
                        joint = tenonJoint;
                        break;
                    case ("S"):
                        var spliceJoint = new SpliceJointX(joint);
                        spliceJoint.Added = 10;
                        spliceJoint.SpliceAngle = RhinoMath.ToRadians(10.0);
                        spliceJoint.SpliceLength = 300;

                        spliceJoint.Construct(beams);
                        joint = spliceJoint;
                        break;
                    case ("L"):
                        var cornerJoint = new CornerJointX(joint);
                        cornerJoint.BlindOffset = 0;
                        cornerJoint.Added = 20;
                        cornerJoint.Construct(beams);
                        joint = cornerJoint;
                        break;
                    default:
                        break;
                }

                joints[key] = joint;
            }

            var jointsOutput = new DataTree<GH_Joint>();
            var jointGeometries = new DataTree<GH_Brep>();

            foreach (var key in keys)
            {
                foreach (var part in joints[key].Parts)
                {
                    var path = new GH_Path(part.ElementIndex);

                    jointGeometries.AddRange(part.Geometry.Select(x => new GH_Brep(x)), path);
                }

                jointsOutput.Add(new GH_Joint(joints[key]), new GH_Path(key));
            }

            DA.SetDataTree(0, jointsOutput);
            DA.SetDataTree(1, jointGeometries);

            /*
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
            */
        }
    }
}