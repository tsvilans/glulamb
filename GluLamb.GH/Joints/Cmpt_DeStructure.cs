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
using Grasshopper.Kernel.Data;

using Rhino.Geometry;

using GluLamb.Joints;
using Grasshopper;

namespace GluLamb.GH.Components
{
    public class Cmpt_DeStructure : GH_Component
    {
        public Cmpt_DeStructure()
          : base("DeStructure", "DeStruct",
              "Get elements and joint geometry for each element from a Structure.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.FlipTopology;
        public override Guid ComponentGuid => new Guid("533a47b9-72da-4c72-94f5-67054c5fc22a");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Structure", "S", "Structure.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Element names", "EN", "Name of each element.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Element geometry", "EG", "Geometry of each element.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Joint geometry", "JG", "Geometry of joints for each element.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Joint type", "JT", "String description of joint.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            GH_Structure gh_structure = null;
            DA.GetData<GH_Structure>("Structure", ref gh_structure);

            if (gh_structure == null) return;

            var structure = gh_structure.Value;
            if (structure == null) return;

            var joint_data = new Grasshopper.Kernel.Data.GH_Structure<GH_Brep>();
            var element_data = new Grasshopper.Kernel.Data.GH_Structure<GH_Brep>();
            var name_data = new Grasshopper.Kernel.Data.GH_Structure<GH_String>();
            var joint_type_data = new Grasshopper.Kernel.Data.GH_Structure<GH_String>();

            foreach (var joint in structure.Joints)
            {
                if (joint == null) continue;
                for (int i = 0; i < joint.Parts.Length; ++i)
                {
                    joint.Parts[i].Geometry.Clear();
                }
                try
                {
                    joint.Construct();
                }
                catch (Exception ex)
                {
                    throw new Exception(String.Format("joint {0} involving element {1} failed to construct", joint, joint.Parts[0].Element.Name));
                }
            }


            var path = new GH_Path(0);
            foreach (var element in structure.Elements)
            {
                name_data.EnsurePath(path);
                element_data.EnsurePath(path);
                joint_data.EnsurePath(path);
                joint_type_data.EnsurePath(path);

                name_data.Append(new GH_String(element.Name), path);
                if (element is BeamElement)
                {
                    var be = element as BeamElement;
                    var glulam = (be.Beam as Glulam);

                    if (glulam == null) continue;
                    glulam = glulam.Duplicate();
                    //glulam.Extend(CurveEnd.Both, 80.0, CurveExtensionStyle.Smooth);
                    element_data.Append(new GH_Brep(glulam.ToBrep()), path);

                }
                else
                {
                    element_data.Append(new GH_Brep(element.Geometry as Brep), path);
                }

                foreach (var joint in element.Joints)
                {
                    var part = joint.Parts.Where(x => x.Element == element).FirstOrDefault();
                    if (part == null) continue;
                    joint_data.AppendRange(part.Geometry.Select(x => new GH_Brep(x)), path);
                    joint_type_data.Append(new GH_String(joint.ToString()));
                }

                path = path.Increment(0);
            }

            DA.SetDataTree(0, name_data);
            DA.SetDataTree(1, element_data);
            DA.SetDataTree(2, joint_data);
            DA.SetDataTree(3, joint_type_data);
        }
    }
}