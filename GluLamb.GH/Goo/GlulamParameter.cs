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

    public class GlulamParameter : GH_PersistentParam<GH_Glulam>
    {
        public GlulamParameter() : this("Glulam parameter", "Glulam", "This is a glulam.", "GluLamb", "Parameters") { }
        public GlulamParameter(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory) { }
        public GlulamParameter(GH_InstanceDescription tag) : base(tag) { }

        //public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override System.Guid ComponentGuid => new Guid("A43600E5-70B5-4B63-85DE-A6D40DC20DCB");
        protected override GH_GetterResult Prompt_Singular(ref GH_Glulam value)
        {
            value = new GH_Glulam();
            return GH_GetterResult.success;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<GH_Glulam> values)
        {
            values = new List<GH_Glulam>();
            return GH_GetterResult.success;
        }

        protected override Bitmap Icon => Properties.Resources.glulamb_FreeformGlulam_24x24;

    }

    public class GlulamDataParameter : GH_PersistentParam<GH_GlulamData>
    {
        public GlulamDataParameter() : this("GlulamData parameter", "GlulamData", "This is a glulam.", "GluLamb", "Parameters") { }
        public GlulamDataParameter(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory) { }
        public GlulamDataParameter(GH_InstanceDescription tag) : base(tag) { }

        //public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_GlulamData_24x24;

        public override System.Guid ComponentGuid => new Guid("83A42E0E-ACB8-4B25-B455-3448A391CEB2");
        protected override GH_GetterResult Prompt_Singular(ref GH_GlulamData value)
        {
            value = new GH_GlulamData();
            return GH_GetterResult.success;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<GH_GlulamData> values)
        {
            values = new List<GH_GlulamData>();
            return GH_GetterResult.success;
        }
    }

    /*
    public class GlulamAssemblyParameter : GH_PersistentParam<GH_Assembly>
    {
        public GlulamAssemblyParameter() : base("Assembly parameter", "Assembly", "This is a glulam assembly.", "GluLamb", "Parameters") { }
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_Assembly_24x24;
        public override System.Guid ComponentGuid => new Guid("5E678C6C-F4A3-48DA-ABD2-383226FDA67C");
        protected override GH_GetterResult Prompt_Singular(ref GH_Assembly value)
        {
            value = new GH_Assembly();
            return GH_GetterResult.success;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<GH_Assembly> values)
        {
            values = new List<GH_Assembly>();
            return GH_GetterResult.success;
        }

    }

    public class GlulamWorkpieceParameter : GH_PersistentParam<GH_GlulamWorkpiece>
    {
        public GlulamWorkpieceParameter() : base("Workpiece parameter", "Workpiece", "This is a glulam workpiece.", "GluLamb", "Parameters") { }
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_Workpiece_24x24;
        public override System.Guid ComponentGuid => new Guid("EC5527DB-6C1B-4B61-9655-86CC269C5B96");
        protected override GH_GetterResult Prompt_Singular(ref GH_GlulamWorkpiece value)
        {
            value = new GH_GlulamWorkpiece();
            return GH_GetterResult.success;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<GH_GlulamWorkpiece> values)
        {
            values = new List<GH_GlulamWorkpiece>();
            return GH_GetterResult.success;
        }
    }
    */
}