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
using System.Linq;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

using GluLamb.Blanks;
using GluLamb.Constraints;

namespace GluLamb.GH.Components
{
    public class Cmpt_TwoStepBlank : GH_Component
    {
        public Cmpt_TwoStepBlank()
          : base("Two-Step Blank", "TwoStep",
              "Prototype. The geometry of a two-step lamination of a free-form beam: flat boards (G0) bent into a single-curved " +
              "glulam (G1), G1 sliced into curved lamellas (G2), the lamellas pressed out of plane into the free-form blank (G3). " +
              "Each layer of G3 is developed from the beam's frame: its in-plane curvature comes from step 1, its out-of-plane " +
              "curvature and twist from step 3.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("3b8f2d61-7c4e-4a19-9e05-6d2a8c1f4b73");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var d = new TwoStepOptions();
            pManager.AddGenericParameter("Beam", "B", "Free-form beam.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Stack Axis", "SA", "The beam's axis the final layers are stacked along: 1 its Y-axis (height), 0 its X-axis (width).", GH_ParamAccess.item, d.StackAxis);
            pManager.AddNumberParameter("Layer Thickness", "LT", "Finished thickness of the final layers. 0: the largest the radius ratio allows.", GH_ParamAccess.item, d.LayerThickness);
            pManager.AddNumberParameter("Board Thickness", "BT", "Thickness of the boards of the single-curved glulam. 0: the largest the radius ratio allows.", GH_ParamAccess.item, d.BoardThickness);
            pManager.AddNumberParameter("Kerf", "K", "Saw kerf between the slices of G1.", GH_ParamAccess.item, d.Kerf);
            pManager.AddNumberParameter("Planing", "P", "What planing takes off each slice (both faces together).", GH_ParamAccess.item, d.Planing);
            pManager.AddNumberParameter("Edge Allowance", "EA", "Extra width on each edge of a layer.", GH_ParamAccess.item, d.EdgeAllowance);
            pManager.AddNumberParameter("End Allowance", "EnA", "Extra length at each end.", GH_ParamAccess.item, d.EndAllowance);
            pManager.AddGenericParameter("Rules", "R", "The rules that limit radius over thickness, thickness and twist strain: a Rule Set, or the name of a standard (Eurocode, APA, None). Default: Eurocode.", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "M", "What the rules are asked about, as key=value items, e.g. species=southern-pine, species-group=softwood.", GH_ParamAccess.list);
            pManager[8].Optional = true;
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Blank", "G3", "The final free-form blank (a glulam with the layers).", GH_ParamAccess.item);
            pManager.AddBrepParameter("Layers", "L", "The layers in the final blank (their mid-surfaces).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lamellas", "G2", "The outline each layer needs of its lamella, flat, at the height of its slice in G1 (all inside the slice outline).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Single-Curved", "G1", "The single-curved glulam in the XY plane that the lamellas are sliced from.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Boards", "G0", "Length of each board of G1.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Board Thickness", "BT", "Thickness of the boards of G1.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Layer Thickness", "LT", "Thickness of the final layers.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Strain", "S", "Allowed strain of step 3, then the largest strains of step 1 (boards of G1), step 3 (pressing the layers) and twist, then the allowed strains of step 1 and of twist.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Twist", "T", "Geodesic torsion (twist per unit length) along each layer: zero for a developable layer.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Geodesic Curvature", "Kg", "In-plane curvature along each layer (made in step 1).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Normal Curvature", "Kn", "Out-of-plane curvature along each layer (made in step 3).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Messages", "Msg", "What happened.", GH_ParamAccess.list);
            pManager.AddMeshParameter("True Lamellas", "TL", "Each lamella as it really sits in the blank: the same flat slice of G1 pressed onto its own layer. " +
                "Neighbours' edges don't line up, so the blank's sides are skewed and stepped.", GH_ParamAccess.list);
            pManager.AddBrepParameter("True Shape", "TS", "The outside of the blank through the lamellas' edges: skewed sides (steps smoothed over), bottom and top.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Slice", "Sl", "The flat outline of a slice of G1: every lamella as it is sawn.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Handle", "H", "A plane to orient the blank for the press: X along its chord, Y towards its bulge. The flat outputs (G1, G2, Slice) already lie along the world X-axis.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Edge Margin", "EM", "The least any lamella's edges stand out past the beam's section (negative: the beam sticks out).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData(0, ref beam) || beam == null) return;

            var options = new TwoStepOptions();
            DA.GetData(1, ref options.StackAxis);
            DA.GetData(2, ref options.LayerThickness);
            DA.GetData(3, ref options.BoardThickness);
            DA.GetData(4, ref options.Kerf);
            DA.GetData(5, ref options.Planing);
            DA.GetData(6, ref options.EdgeAllowance);
            DA.GetData(7, ref options.EndAllowance);
            object rules = null;
            if (DA.GetData(8, ref rules))
            {
                if (rules is Grasshopper.Kernel.Types.IGH_Goo goo) rules = goo.ScriptVariable();
                if (rules is RuleSet set) options.Rules = set;
                else if (rules is string name && Enum.TryParse(name, true, out Standards.Standard standard)) options.Rules = LaminationRules.For(standard);
                else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rules must be a Rule Set or the name of a standard (Eurocode, APA, None)."); return; }
            }
            var material = new System.Collections.Generic.List<string>();
            DA.GetDataList(9, material);
            options.Material = Context.Parse(material);

            var result = TwoStepLamination.Develop(beam, options);
            DA.SetDataList(11, result.Messages);
            if (!result.Success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Failed.");
                return;
            }

            foreach (var m in result.Messages.Where(m => m.Contains("exceeds") || m.Contains("not developable")))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, m);

            var twist = new DataTree<double>();
            var kg = new DataTree<double>();
            var kn = new DataTree<double>();
            foreach (var layer in result.Layers)
            {
                var path = new GH_Path(layer.Index);
                twist.AddRange(layer.Twist, path);
                kg.AddRange(layer.GeodesicCurvature, path);
                kn.AddRange(layer.NormalCurvature, path);
            }

            DA.SetData(0, new GH_Glulam(result.Blank));
            DA.SetDataList(1, result.Layers.Select(l => l.Surface));
            DA.SetDataList(2, result.Layers.Select(l => new PolylineCurve(l.Outline)));
            DA.SetData(3, new GH_Glulam(result.SingleCurved));
            DA.SetDataList(4, result.BoardLengths);
            DA.SetData(5, result.BoardThickness);
            DA.SetData(6, result.LayerThickness);
            DA.SetDataList(7, new[] { result.AllowedStrain, result.BendStrain, result.PressStrain, result.TwistStrain, result.AllowedBendStrain, result.AllowedTwistStrain });
            DA.SetDataTree(8, twist);
            DA.SetDataTree(9, kg);
            DA.SetDataTree(10, kn);
            DA.SetDataList(12, result.Layers.Select(l => l.TrueMesh));
            DA.SetDataList(13, result.TrueShape);
            DA.SetData(14, new PolylineCurve(result.SliceOutline));
            DA.SetData(15, result.BlankHandle);
            DA.SetData(16, result.EdgeMargin);
            Message = $"{result.Layers.Count} x {result.LayerThickness:0.#}";
        }
    }
}
