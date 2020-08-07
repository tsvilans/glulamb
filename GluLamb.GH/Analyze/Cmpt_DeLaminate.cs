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

namespace GluLamb.GH.Components
{
    public class Cmpt_DeLaminate : GH_Component
    {
        public Cmpt_DeLaminate()
          : base("Delaminate", "DeLam",
              "Gets individual lamellas from Glulam.",
              "GluLamb", "Analyze")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Input glulam blank to deconstruct.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Type", "T", "Type of output: 0 = centreline curves, 1 = Mesh, 2 = Brep.", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Lamellae", "L", "Lamellae objects from Glulam.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Species", "S", "Lamellae species.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Reference", "R", "Lamellae IDs.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Glulam> inputs = new List<GH_Glulam>();

            if (!DA.GetDataList<GH_Glulam>("Glulam",  inputs))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No glulam blank connected.");
                return;
            }

            int type = 0;
            DA.GetData("Type", ref type);

            DataTree<object> output = new DataTree<object>();
            DataTree<string> species = new DataTree<string>();
            DataTree<Guid> ids = new DataTree<Guid>();

            for (int i = 0; i < inputs.Count; ++i)
            {

                Glulam g = inputs[i].Value;

                GH_Path path;
                int j = 0;
                switch (type)
                {
                    case (1):
                        var meshes = g.GetLamellaeMeshes();

                        if (meshes.Count < 1)
                            throw new NotImplementedException();

                        for (int x = 0; x < g.Data.NumWidth; ++x)
                        {
                            path = new GH_Path(i, x);
                            for (int y = 0; y < g.Data.NumHeight; ++y)
                            {
                                output.Add(meshes[j], path);
                                if (g.Data.Lamellae[x, y] != null)
                                {
                                    species.Add(g.Data.Lamellae[x, y].Species, path);
                                    ids.Add(g.Data.Lamellae[x, y].Reference, path);
                                }
                                j++;
                            }
                        }
                        break;
                    case (2):
                        var breps = g.GetLamellaeBreps();

                        for (int x = 0; x < g.Data.NumWidth; ++x)
                        {
                            path = new GH_Path(i, x);
                            for (int y = 0; y < g.Data.NumHeight; ++y)
                            {
                                output.Add(breps[j], path);
                                if (g.Data.Lamellae[x, y] != null)
                                {
                                    species.Add(g.Data.Lamellae[x, y].Species, path);
                                    ids.Add(g.Data.Lamellae[x, y].Reference, path);
                                }
                                j++;
                            }
                        }
                        break;
                    default:
                        var crvs = g.GetLamellaeCurves();

                        for (int x = 0; x < g.Data.NumWidth; ++x)
                        {
                            path = new GH_Path(i, x);
                            for (int y = 0; y < g.Data.NumHeight; ++y)
                            {
                                output.Add(crvs[j], path);
                                if (g.Data.Lamellae[x, y] != null)
                                {
                                    species.Add(g.Data.Lamellae[x, y].Species, path);
                                    ids.Add(g.Data.Lamellae[x, y].Reference, path);
                                }
                                j++;
                            }
                        }
                        break;
                }
            }

            DA.SetDataTree(0, output);
            DA.SetDataTree(1, species);
            DA.SetDataTree(2, ids);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_Delaminate_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("E4487BDA-9F44-491C-8EEB-5A0BF6D4330F"); }
        }
    }
}