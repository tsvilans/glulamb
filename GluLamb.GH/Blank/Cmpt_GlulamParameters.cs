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
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;

namespace GluLamb.GH.Components
{
    public class Cmpt_GlulamParameters : GH_Component
    {
        public Cmpt_GlulamParameters()
          : base("Get Glulam parameters", "GParam",
              "Extracts parameters from Glulam object.",
              "GluLamb", UiNames.BlankSection)
        {
        }


        protected override System.Drawing.Bitmap Icon => Properties.Resources.GlulamDelaminate;
        public override Guid ComponentGuid => new Guid("EE4E6303-23F3-445D-80DC-279CC43C579B");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        GH_ValueList valueList = null;
        IGH_Param parameter = null;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Input glulam blank to deconstruct.", GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Key value of parameters to extract.", GH_ParamAccess.list);
            parameter = pManager[1];
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Params", "P", "Extracted parameters", GH_ParamAccess.tree);
        }

        protected override void BeforeSolveInstance()
        {
            if (valueList == null)
            {
                if (parameter.Sources.Count == 0)
                {
                    valueList = new GH_ValueList();
                }
                else
                {
                    foreach (var source in parameter.Sources)
                    {
                        if (source is GH_ValueList) valueList = source as GH_ValueList;
                        return;
                    }
                }

                valueList.CreateAttributes();
                valueList.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 1);
                valueList.ListItems.Clear();

                var glulamParameters = Glulam.ListProperties();

                foreach (string param in glulamParameters)
                {
                    valueList.ListItems.Add(new GH_ValueListItem(param, $"\"{param}\""));
                }

                Instances.ActiveCanvas.Document.AddObject(valueList, false);
                parameter.AddSource(valueList);
                parameter.CollectData();
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Glulam g = null;

            if (!DA.GetData<Glulam>("Glulam", ref g))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No glulam blank connected.");
                return;
            }

            List<string> keys = new List<string>();

            if (!DA.GetDataList("Key", keys))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No keys found.");
                return;
            }

            /*
            Glulam g;

            if (obj is GH_Glulam)
                g = (obj as GH_Glulam).Value;
            else
                g = obj as Glulam;

            if (g == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid glulam input.");
                return;
            }
            */

            DataTree<object> output = new DataTree<object>();
            Dictionary<string, object> props = g.GetProperties();
            
            for (int i = 0; i < keys.Count; ++i)
            {
                if (props.ContainsKey(keys[i]))
                {
                    if (props[keys[i]].GetType().IsArray)
                    {
                        Type t = props[keys[i]].GetType().GetElementType();
                        if (t == typeof(Rhino.Geometry.Plane))
                        {
                            var ar = props[keys[i]] as Rhino.Geometry.Plane[];
                            GH_Path path = new GH_Path(i);
                            for (int j = 0; j < ar.Length; ++j)
                            {
                                output.Add(ar[j], path);
                            }
                        }
                    }
                    else
                        output.Add(props[keys[i]], new GH_Path(i));
                }
            }
            
            DA.SetDataTree(0, output);
        }
    }
}