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

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using GluLamb.Blanks;
using GluLamb.Constraints;

namespace GluLamb.GH.Components
{
    /// <summary>
    /// Reads an object out of a generic input.
    /// </summary>
    internal static class Unwrap
    {
        public static bool Get<T>(IGH_DataAccess DA, int index, out T value) where T : class
        {
            value = null;
            object input = null;
            if (!DA.GetData(index, ref input) || input == null) return false;
            if (input is IGH_Goo goo) input = goo.ScriptVariable();
            value = input as T;
            return value != null;
        }
    }

    public class Cmpt_LaminationOptions : GH_Component
    {
        public Cmpt_LaminationOptions()
          : base("Lamination Options", "LamOpt",
              "Prototype. Options for Two-Step Blank: stacking axis, thicknesses, allowances and rules.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("a4d81f36-9b27-4e60-8c15-7f3a2d9e6b48");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var d = new TwoStepOptions();
            pManager.AddIntegerParameter("Stack Axis", "SA", "The beam's axis that the final layers are stacked along. 1: Y (height). 0: X (width).", GH_ParamAccess.item, d.StackAxis);
            pManager.AddNumberParameter("Layer Thickness", "LT", "Finished thickness of the final layers. 0: the largest that the rules allow.", GH_ParamAccess.item, d.LayerThickness);
            pManager.AddNumberParameter("Board Thickness", "BT", "Thickness of the boards of the single-curved glulam. 0: the largest that the rules allow.", GH_ParamAccess.item, d.BoardThickness);
            pManager.AddNumberParameter("Kerf", "K", "Saw kerf between the slices of G1.", GH_ParamAccess.item, d.Kerf);
            pManager.AddNumberParameter("Planing", "P", "Material that planing removes from each slice (both faces together).", GH_ParamAccess.item, d.Planing);
            pManager.AddNumberParameter("Edge Allowance", "EA", "Extra width on each edge of a layer.", GH_ParamAccess.item, d.EdgeAllowance);
            pManager.AddNumberParameter("End Allowance", "EnA", "Extra length at each end.", GH_ParamAccess.item, d.EndAllowance);
            pManager.AddGenericParameter("Rules", "R", "A Rule Set, or the name of a standard (Eurocode, APA, None). Default: Eurocode.", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "M", "The facts for the rules, as key=value items, e.g. species=southern-pine.", GH_ParamAccess.list);
            pManager[7].Optional = true;
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Options", "O", "Lamination options.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var options = new TwoStepOptions();
            DA.GetData(0, ref options.StackAxis);
            DA.GetData(1, ref options.LayerThickness);
            DA.GetData(2, ref options.BoardThickness);
            DA.GetData(3, ref options.Kerf);
            DA.GetData(4, ref options.Planing);
            DA.GetData(5, ref options.EdgeAllowance);
            DA.GetData(6, ref options.EndAllowance);

            object rules = null;
            if (DA.GetData(7, ref rules) && rules != null)
            {
                if (rules is IGH_Goo goo) rules = goo.ScriptVariable();
                if (rules is RuleSet set) options.Rules = set;
                else if (rules is string name && Enum.TryParse(name, true, out Standards.Standard standard)) options.Rules = LaminationRules.For(standard);
                else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rules must be a Rule Set or the name of a standard (Eurocode, APA, None)."); return; }
            }
            var material = new List<string>();
            DA.GetDataList(8, material);
            options.Material = Context.Parse(material);

            DA.SetData(0, new GH_ObjectWrapper(options));
            Message = options.Rules?.Name;
        }
    }

    public class Cmpt_TwoStepBlank : GH_Component
    {
        public Cmpt_TwoStepBlank()
          : base("Two-Step Blank", "TwoStep",
              "Prototype. The blanks of a two-step lamination of a free-form beam: boards bent into a single-curved glulam (G1), " +
              "G1 sliced into curved lamellas, the lamellas pressed into the free-form blank (G3). Analyze Glulam gives the details.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("3b8f2d61-7c4e-4a19-9e05-6d2a8c1f4b73");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Free-form beam.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Options", "O", "Lamination Options. Default: Eurocode, automatic thicknesses.", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Single-Curved", "G1", "The single-curved glulam that the lamellas are sliced from. It lies along the world X-axis.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Blank", "G3", "The final free-form blank. Its plane \"press\" orients it for the press.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Analysis", "A", "Layers, lamellas, true shape, strains. Open it with Analyze Glulam.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData(0, ref beam) || beam == null) return;
            Unwrap.Get(DA, 1, out TwoStepOptions options);

            var result = TwoStepLamination.Develop(beam, options);
            DA.SetData(2, new GH_ObjectWrapper(result));
            if (!result.Success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Failed.");
                return;
            }

            foreach (var m in result.Messages.Where(m => m.Contains("exceeds") || m.Contains("not developable") || m.Contains("sticks out")))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, m);

            DA.SetData(0, new GH_Glulam(result.SingleCurved));
            DA.SetData(1, new GH_Glulam(result.Blank));
            Message = $"{result.Layers.Count} x {result.LayerThickness:0.#}";
        }
    }

    public class Cmpt_AnalyzeGlulam : GH_Component
    {
        public Cmpt_AnalyzeGlulam()
          : base("Analyze Glulam", "Analyze",
              "Prototype. Analyses a glulam, or opens the analysis from Two-Step Blank. A glulam gives the strains of its lamellae. " +
              "A two-step analysis also gives layers, flat lamellas, true shape, curvatures and twist.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("7c2e9a54-0d6f-4b38-a1e7-5b8d3f6c2a91");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "A glulam, or the analysis from Two-Step Blank.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Rules", "R", "For a glulam: a Rule Set, or the name of a standard (Eurocode, APA, None). Default: Eurocode.", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Layers", "L", "Mid-surfaces of the layers in the final blank.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lamellas", "G2", "The flat outline that each layer needs, at the height of its slice in G1.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Slice", "Sl", "The flat outline of a slice of G1: each lamella as sawn.", GH_ParamAccess.item);
            pManager.AddMeshParameter("True Lamellas", "TL", "Each lamella in its true place in the blank. The sides are skewed and stepped.", GH_ParamAccess.list);
            pManager.AddBrepParameter("True Shape", "TS", "The outside of the blank through the lamella edges.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Boards", "G0", "Length of each board of G1.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Board thickness of G1, then layer thickness of the blank.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Strain", "S", "Actual then allowed strain, for: step 1 (boards of G1), step 3 (press), twist.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Twist", "Tw", "Twist (geodesic torsion) along each layer. Zero: developable.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Geodesic Curvature", "Kg", "In-plane curvature along each layer (step 1).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Normal Curvature", "Kn", "Out-of-plane curvature along each layer (step 3).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Edge Margin", "EM", "Smallest distance from a lamella edge to the beam's section. Negative: the beam sticks out.", GH_ParamAccess.item);
            pManager.AddTextParameter("Straight", "St", "Whether straight lamellas can make the beam, for stacking along Y and along X.", GH_ParamAccess.list);
            pManager.AddTextParameter("Messages", "Msg", "Sizes, strains and the rules that decided them.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Utilisation", "U", "Strain over allowed strain along each lamella (bending or twist, the larger). Above 1: too much. " +
                "One branch per lamella, in the order of Delaminate. Connect to Visualize Lamella.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object input = null;
            if (!DA.GetData(0, ref input) || input == null) return;
            if (input is IGH_Goo goo) input = goo.ScriptVariable();

            var a = input as GlulamAnalysis;
            if (a == null && input is Glulam glulam)
            {
                RuleSet rules = null;
                object r = null;
                if (DA.GetData(1, ref r) && r != null)
                {
                    if (r is IGH_Goo rg) r = rg.ScriptVariable();
                    if (r is RuleSet set) rules = set;
                    else if (r is string name && Enum.TryParse(name, true, out Standards.Standard standard)) rules = LaminationRules.For(standard);
                    else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rules must be a Rule Set or the name of a standard (Eurocode, APA, None)."); return; }
                }
                a = GlulamAnalysis.FromGlulam(glulam, rules);
            }
            if (a == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The input is not a glulam or a glulam analysis."); return; }

            DA.SetDataList(13, a.Messages);
            if (!a.Success) return;

            var utilisation = new DataTree<double>();
            foreach (var lamella in a.Lamellas)
                utilisation.AddRange(lamella.Utilisation, new GH_Path(lamella.X, lamella.Y));
            DA.SetDataTree(14, utilisation);
            DA.SetDataList(7, new[] { a.BendStrain, a.AllowedBendStrain, a.PressStrain, a.AllowedPressStrain, a.TwistStrain, a.AllowedTwistStrain });
            if (a.Lamellas.Any(l => l.Utilisation.Max() > 1.0001))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A lamella exceeds its allowed strain.");
            if (!a.IsTwoStep) return;

            var twist = new DataTree<double>();
            var kg = new DataTree<double>();
            var kn = new DataTree<double>();
            foreach (var layer in a.Layers)
            {
                var path = new GH_Path(layer.Index);
                twist.AddRange(layer.Twist, path);
                kg.AddRange(layer.GeodesicCurvature, path);
                kn.AddRange(layer.NormalCurvature, path);
            }

            DA.SetDataList(0, a.Layers.Select(l => l.Surface));
            DA.SetDataList(1, a.Layers.Select(l => new PolylineCurve(l.Outline)));
            DA.SetData(2, new PolylineCurve(a.SliceOutline));
            DA.SetDataList(3, a.Layers.Select(l => l.TrueMesh));
            DA.SetDataList(4, a.TrueShape);
            DA.SetDataList(5, a.BoardLengths);
            DA.SetDataList(6, new[] { a.BoardThickness, a.LayerThickness });
            DA.SetDataTree(8, twist);
            DA.SetDataTree(9, kg);
            DA.SetDataTree(10, kn);
            DA.SetData(11, a.EdgeMargin);
            DA.SetDataList(12, a.StraightLamellas.Select(c => c.ToString()));
        }
    }

    public class Cmpt_VisualizeLamella : GH_Component
    {
        public Cmpt_VisualizeLamella()
          : base("Visualize Lamella", "VisLam",
              "A coloured mesh of each lamella of a glulam, from values per lamella: one value, or values along its length.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("b93e6d17-2a48-4f05-9c7b-4e1d8a5f0c32");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Values", "V", "Values for the lamellae. One branch per lamella, in the order of Delaminate: one value, or values along its length. " +
                "A single list with one value per lamella also works.", GH_ParamAccess.tree);
            pManager.AddIntervalParameter("Domain", "D", "The values of the first and the last colour. Default: the smallest and largest value.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colours", "C", "The gradient. Default: blue, green, yellow, red.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "A coloured mesh of each lamella.", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Domain", "D", "The domain that the colours show.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Glulam glulam = null;
            if (!DA.GetData(0, ref glulam) || glulam == null) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Number> tree)) return;

            int lamellae = glulam.Data.NumWidth * glulam.Data.NumHeight;
            var values = new List<IList<double>>();
            if (tree.PathCount == 1 && tree.Branches[0].Count == lamellae && lamellae > 1)
                values.AddRange(tree.Branches[0].Select(v => (IList<double>)new[] { v.Value }));
            else
                values.AddRange(tree.Branches.Select(b => (IList<double>)b.Where(v => v != null).Select(v => v.Value).ToList()));

            if (values.Count != lamellae)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"The glulam has {lamellae} lamellae, but there are values for {values.Count}.");

            var all = values.SelectMany(v => v).ToList();
            var domain = all.Count > 0 ? new Interval(all.Min(), all.Max()) : new Interval(0, 1);
            DA.GetData(2, ref domain);

            var colours = new List<System.Drawing.Color>();
            DA.GetDataList(3, colours);

            DA.SetDataList(0, TwoStepLamination.LamellaMeshes(glulam, values, domain, colours.Count > 0 ? colours : null));
            DA.SetData(1, domain);
        }
    }
}
