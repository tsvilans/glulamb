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

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.DocObjects;
using Grasshopper.Kernel.Types;

namespace GluLamb.GH.Components
{
    public class Cmpt_DeLaminate : GH_Component
    {
        public Cmpt_DeLaminate()
          : base("Delaminate", "DeLam",
              "Gets individual lamellas from Glulam, and its material data (as set by Glulam Material).",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GlulamDelaminate;
        public override Guid ComponentGuid => new Guid("E4487BDA-9F44-491C-8EEB-5A0BF6D4330F");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        int GlulamInput = 0, TypeInput = 1;
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            GlulamInput = pManager.AddGenericParameter("Glulam", "G", "Input glulam blank to deconstruct.", GH_ParamAccess.tree);
            TypeInput = pManager.AddIntegerParameter("Type", "T", "Type of output: 0 = centreline curves, 1 = Mesh, 2 = Brep.", GH_ParamAccess.item, 0);

            pManager[TypeInput].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Lamellae", "L", "Lamellae objects from Glulam.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Species", "S", "Lamellae species.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Reference", "R", "Lamellae IDs.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Grade", "Gr", "Lamellae grades.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Id", "Id", "Glulam id, per glulam.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Strength Class", "SC", "Strength class, per glulam.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Layup", "Lu", "Layup, per glulam.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Allowance", "A", "Machining allowances (inside, outside, sides, start, end), per glulam.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Glulam> inputs = new List<GH_Glulam>();

            Glulam glulam = null;

            //DataTree<GH_Glulam> glulams = new DataTree<GH_Glulam>();
            GH_Structure<IGH_Goo> glulams;

            if (!DA.GetDataTree<IGH_Goo>(GlulamInput, out glulams))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No glulam blank connected.");
                return;
            }

            int type = 0;
            DA.GetData("Type", ref type);

            DataTree<object> output = new DataTree<object>();
            DataTree<string> species = new DataTree<string>();
            DataTree<Guid> ids = new DataTree<Guid>();
            DataTree<string> grades = new DataTree<string>();
            DataTree<string> glulamIds = new DataTree<string>();
            DataTree<string> strengthClasses = new DataTree<string>();
            DataTree<string> layups = new DataTree<string>();
            DataTree<double> allowances = new DataTree<double>();

            GH_Path element_path;

            foreach (var path in glulams.Paths)
            {
                var branch = glulams[path];
                for (int i = 0; i < branch.Count; ++i)
                {
                    IGH_Goo goo = branch[i];
                    var gh_glulam = goo as GH_Glulam;
                    if (gh_glulam == null) continue;

                    glulam = gh_glulam.Value;
                    if (glulam == null) continue;

                    // One glulam per branch keeps the branch's path; several get an index each
                    GH_Path new_path = branch.Count > 1 ? path.AppendElement(i) : new GH_Path(path);

                    glulamIds.Add(glulam.Id, new_path);
                    strengthClasses.Add(glulam.Data.StrengthClass, new_path);
                    layups.Add(glulam.Data.Layup?.ToString() ?? "", new_path);
                    allowances.AddRange(glulam.Data.Allowance.ToValues(), new_path);

                    int j = 0;

                    var objects = new List<object>();
                    switch (type)
                    {
                        case (1):
                            objects.AddRange(glulam.GetLamellaeMeshes());
                            break;
                        case (2):
                            objects.AddRange(glulam.GetLamellaeBreps());
                            break;
                        default:
                            objects.AddRange(glulam.GetLamellaeCurves());
                            break;
                    }

                    if (objects.Count < 1) throw new NotImplementedException();

                    for (int x = 0; x < glulam.Data.NumWidth; ++x)
                    {
                        for (int y = 0; y < glulam.Data.NumHeight; ++y)
                        {
                            //new_path = new GH_Path(x, y);
                            element_path = new GH_Path(new_path);
                            element_path.AppendElement(x);
                            element_path.AppendElement(y);

                            output.Add(objects[j], element_path);
                            if (glulam.Data.Lamellae[x, y] != null)
                            {
                                species.Add(glulam.Data.Lamellae[x, y].Species, element_path);
                                ids.Add(glulam.Data.Lamellae[x, y].Reference, element_path);
                                grades.Add(glulam.Data.Lamellae[x, y].Grade, element_path);
                            }
                            j++;
                        }
                    }
                }
            }

            //for (int i = 0; i < inputs.Count; ++i)
            //{

            //    Glulam g = inputs[i].Value;


            DA.SetDataTree(0, output);
            DA.SetDataTree(1, species);
            DA.SetDataTree(2, ids);
            DA.SetDataTree(3, grades);
            DA.SetDataTree(4, glulamIds);
            DA.SetDataTree(5, strengthClasses);
            DA.SetDataTree(6, layups);
            DA.SetDataTree(7, allowances);
        }
    }
}