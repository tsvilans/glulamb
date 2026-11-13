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
using Rhino;

using GluLamb.Blanks;
using GluLamb.Constraints;

namespace GluLamb.GH.Components
{
    public class Cmpt_BlankOptions : GH_Component
    {
        public Cmpt_BlankOptions()
          : base("Blank Options", "BlankOpt",
              "Prototype. The blanks that a beam can be cut from: straight, single-curved, double-curved without twist, and the beam's own shape. " +
              "Each is the smallest of its kind that encloses the beam. Compare material (waste) against work (lamellae, twist).",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("e8b35d09-4f7a-4c12-a6d8-3c9e1b5f7a20");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Allowance", "A", "Extra material on each side of the section.", GH_ParamAccess.item, 0);
            pManager.AddGenericParameter("Rules", "R", "The rules that limit the lamella thickness: a Rule Set, or the name of a standard. Default: Eurocode.", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "M", "The facts for the rules, as key=value items, e.g. species=southern-pine.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Blanks", "Bl", "The blanks: Straight, SingleCurved, Untwisted (along the centreline, no twist), AsBeam (the beam's own shape).", GH_ParamAccess.list);
            pManager.AddTextParameter("Kind", "K", "The kind of each blank.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Volume", "V", "Volume of each blank.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Waste", "W", "Fraction of each blank that is not the beam.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Lamellae", "L", "Number of lamellae of each blank (across x up).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Twist", "T", "Largest twist of each blank (degrees per metre). Zero: developable lamellas.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Handle", "H", "A plane to orient each blank for the press: X along its chord, Y towards its bulge.", GH_ParamAccess.list);
            pManager.AddTextParameter("Summary", "S", "Each blank in one line.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData(0, ref beam) || beam == null) return;
            double allowance = 0;
            DA.GetData(1, ref allowance);

            RuleSet rules = null;
            object input = null;
            if (DA.GetData(2, ref input))
            {
                if (input is Grasshopper.Kernel.Types.IGH_Goo goo) input = goo.ScriptVariable();
                if (input is RuleSet set) rules = set;
                else if (input is string name && Enum.TryParse(name, true, out Standards.Standard standard)) rules = LaminationRules.For(standard);
                else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rules must be a Rule Set or the name of a standard (Eurocode, APA, None)."); return; }
            }
            var material = new List<string>();
            DA.GetDataList(3, material);

            var options = BlankOptions.Evaluate(beam, allowance, rules, Context.Parse(material));
            double perMetre = RhinoMath.ToDegrees(1) * Globals.FromMillimeters(1000);

            DA.SetDataList(0, options.Select(o => new GH_Glulam(o.Blank)));
            DA.SetDataList(1, options.Select(o => o.Kind.ToString()));
            DA.SetDataList(2, options.Select(o => o.Volume));
            DA.SetDataList(3, options.Select(o => o.Waste));
            DA.SetDataList(4, options.Select(o => o.LamellaeX * o.LamellaeY));
            DA.SetDataList(5, options.Select(o => o.Twist * perMetre));
            DA.SetDataList(6, options.Select(o => o.Handle));
            DA.SetDataList(7, options.Select(o => o.Summary));
        }
    }
}
