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
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

using GluLamb.Joints;
using GH_IO.Serialization;

namespace GluLamb.GH.Components
{
    public class Cmpt_ApplyJoints : GH_Component
    {
        public Cmpt_ApplyJoints()
          : base("Apply joints", "ApplyJ",
              "Pick what type of joints to apply to structure.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Joint;
        public override Guid ComponentGuid => new Guid("77abcac2-f2c7-42f4-a084-b9acbeaf3d2b");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        List<Guid> LastJointId;
        List<IGH_Param> JointParams;
        List<List<Type>> JointTypes;

        int[] JointIndices = new int[7];

        List<Type> BaseJointTypes = new List<Type> {
            typeof(TenonJoint),
            typeof(CrossJoint),
            typeof(SpliceJoint),
            typeof(BranchJoint),
            typeof(FourWayJoint),
            typeof(VBeamJoint),
            typeof(CornerJoint)};

        List<string> BaseJointShortnames = new List<string> { 
            "TJ",
            "CJ", 
            "SJ", 
            "BJ", 
            "FJ", 
            "VJ", 
            "CJ"};

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            LastJointId = new List<Guid>();
            JointParams = new List<IGH_Param>();
            JointTypes = new List<List<Type>>();

            for (int i = 0; i < BaseJointTypes.Count; ++i)
            {
                int res = pManager.AddIntegerParameter(BaseJointTypes[i].Name, BaseJointShortnames[i], $"Type of {BaseJointTypes[i].Name} to apply.", GH_ParamAccess.item);
                JointParams.Add(pManager[res]);
                LastJointId.Add(Guid.Empty);
                JointTypes.Add(new List<Type>());
                pManager[res].Optional = true;
            }

        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Joint solver", "JS", "Joint solver.", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            for (int i = 0; i < BaseJointTypes.Count; ++i)
            {
                if (JointParams[i] != null && JointParams[i].SourceCount > 0)
                {
                    var last = JointParams[i].Sources.Last();
                    if (last.InstanceGuid != LastJointId[i] && last is GH_ValueList)
                    {
                        var valueList = last as GH_ValueList;
                        valueList.ListItems.Clear();

                        JointTypes[i] = new List<Type>();

                        var baseType = BaseJointTypes[i];
                        var assembly = baseType.Assembly;
                        var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType)).ToList();

                        JointTypes[i].Add(baseType);
                        valueList.ListItems.Add(new GH_ValueListItem("Default" + baseType.Name, $"{0}"));

                        if (types.Count > 0)
                        for (int j = 0; j < types.Count; ++j)
                        {
                            JointTypes[i].Add(types[j]);
                            valueList.ListItems.Add(new GH_ValueListItem(types[j].Name, $"{j + 1}"));
                        }

                        
                        valueList.SelectItem(JointIndices[i]);
                        LastJointId[i] = last.InstanceGuid;
                    }

                    JointParams[i].CollectData();
                }
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int[] indices = new int[BaseJointTypes.Count];
            var jointSolver = new GluLamb.Joints.JointSolver();

            for (int i = 0; i < BaseJointTypes.Count; ++i)
            {
                DA.GetData(BaseJointTypes[i].Name, ref indices[i]);
                if (indices[i] < 0) indices[i] = 0;
            }

            int index = 0;

            jointSolver.TenonJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(TenonJoint); index++;
            jointSolver.CrossJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(CrossJoint); index++;
            jointSolver.SpliceJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(SpliceJoint); index++;
            jointSolver.BranchJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(BranchJoint); index++;
            jointSolver.FourWayJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(FourWayJoint); index++;
            jointSolver.VBeamJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(VBeamJoint); index++;
            jointSolver.CornerJoint = indices[index] < JointTypes[index].Count ? JointTypes[index][indices[index]] : typeof(CornerJoint);

            DA.SetData("Joint solver", new GH_ObjectWrapper(jointSolver));
        }

        public override bool Write(GH_IWriter writer)
        {
            for (int i = 0; i < JointIndices.Length; ++i)
                writer.SetInt32("joint_index", i, JointIndices[i]);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("joint_index"))
            {
                for (int i = 0; i < JointIndices.Length; ++i)
                {
                    try
                    {
                        JointIndices[i] = reader.GetInt32("joint_index", i);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            return base.Read(reader);
        }
    }
}