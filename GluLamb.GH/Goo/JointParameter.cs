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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GluLamb.GH
{

    public class JointParameter : GH_PersistentParam<GH_Joint>
    {
        public JointParameter() : this("Joint", "Joint", "This is a joint.", "GluLamb", UiNames.UtilitiesSection) { }
        public JointParameter(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory) { }
        public JointParameter(GH_InstanceDescription tag) : base(tag) { }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public override System.Guid ComponentGuid => new Guid("b3998813-4a46-453c-bf5b-13368d593f35");
        protected override Bitmap Icon => Properties.Resources.JointParameter;

        protected override GH_GetterResult Prompt_Singular(ref GH_Joint value)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc is null) return GH_GetterResult.cancel;

            //value = new GH_Beam();
            return GH_GetterResult.cancel;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<GH_Joint> values)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc is null) return GH_GetterResult.cancel;

            //values = new List<GH_Beam>();
            return GH_GetterResult.cancel;
        }



    }
}