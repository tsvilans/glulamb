/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
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

using GluLamb.Constraints;

namespace GluLamb.GH.Components
{
    public class Cmpt_RuleSet : GH_Component
    {
        public Cmpt_RuleSet()
          : base("Rule Set", "Rules",
              "Prototype. A set of rules that limit quantities (radius over thickness, thickness, twist strain): a standard, or your own from JSON. " +
              "Your own rules come first; the standard answers where they are silent.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("5e1a9c47-3d2b-4c86-b7f0-8a4d6e2c9b15");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Standard", "S", "Eurocode, APA or None.", GH_ParamAccess.item, "Eurocode");
            pManager.AddTextParameter("JSON", "J", "Your own rules: JSON text, or the path of a JSON file. Use the JSON output as a template.", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Rule Set", "R", "The rule set.", GH_ParamAccess.item);
            pManager.AddTextParameter("Rules", "L", "The rules, as text.", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON", "J", "The rule set as JSON.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "Eurocode", json = null;
            DA.GetData(0, ref name);
            if (!Enum.TryParse(name, true, out Standards.Standard standard))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Standard must be Eurocode, APA or None.");
                return;
            }
            var set = LaminationRules.For(standard);

            if (DA.GetData(1, ref json) && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    if (!json.TrimStart().StartsWith("{") && System.IO.File.Exists(json)) json = System.IO.File.ReadAllText(json);
                    var own = RuleSet.FromJson(json);
                    own.Parent = set;
                    set = own;
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The JSON is not a rule set: " + ex.Message);
                    return;
                }
            }

            var lines = new List<string>();
            for (var s = set; s != null; s = s.Parent)
                lines.AddRange(s.Rules.Select(r => $"{s.Name}: {r}"));

            DA.SetData(0, new GH_ObjectWrapper(set));
            DA.SetDataList(1, lines);
            DA.SetData(2, set.ToJson());
            Message = set.Parent != null ? $"{set.Name} + {set.Parent.Name}" : set.Name;
        }
    }

    public class Cmpt_RuleQuery : GH_Component
    {
        public Cmpt_RuleQuery()
          : base("Rule Query", "Query",
              "Prototype. Ask a rule set for the limit on a quantity, and test a value against it. The answer names the rule that decided it.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("c27f4b83-6a1d-4e59-9d30-1b7e5a8f2c64");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Rule Set", "R", "The rule set.", GH_ParamAccess.item);
            pManager.AddTextParameter("Quantity", "Q", $"The quantity, e.g. {Quantities.RadiusRatio}, {Quantities.MaxThickness}, {Quantities.TwistStrain}.", GH_ParamAccess.item, Quantities.RadiusRatio);
            pManager.AddTextParameter("Context", "C", "The facts, as key=value items, e.g. thickness=19 (millimetres), species=southern-pine, step=press.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Actual", "A", "A value to test against the limit.", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Limit", "L", "The limit.", GH_ParamAccess.item);
            pManager.AddTextParameter("Rule", "R", "The rule that gives it.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Passed", "P", "Whether the actual value is within the limit.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Utilisation", "U", "How much of the limit the actual value uses (at most 1 passes).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object input = null;
            if (!DA.GetData(0, ref input)) return;
            if (input is IGH_Goo goo) input = goo.ScriptVariable();
            if (!(input is RuleSet set)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rule Set is not a rule set."); return; }

            string quantity = Quantities.RadiusRatio;
            DA.GetData(1, ref quantity);
            var items = new List<string>();
            DA.GetDataList(2, items);
            var context = Context.Parse(items);

            var decision = set.Find(quantity, context);
            DA.SetData(1, decision.ToString());
            if (!decision.Found)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, decision.ToString());
                return;
            }
            DA.SetData(0, decision.Value);

            double actual = 0;
            if (DA.GetData(3, ref actual))
            {
                var check = set.Check(quantity, context, actual);
                DA.SetData(2, check.Passed);
                DA.SetData(3, check.Utilisation);
                if (!check.Passed) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, check.ToString());
            }
        }
    }
}
