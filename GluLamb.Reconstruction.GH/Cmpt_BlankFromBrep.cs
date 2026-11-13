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

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using GluLamb.GH;

namespace GluLamb.Reconstruction.GH
{
    public class Cmpt_BlankFromBrep : GH_Component
    {
        public Cmpt_BlankFromBrep()
          : base("Blank From Brep", "BrepBlank",
              "The blank of a detailed beam Brep (joints, cuts, drillings): a rectangular section along a centreline that encloses the Brep.",
              "GluLamb", UiNames.ReconstructionSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("7a9d3e15-2b6c-4f08-8e41-d5c2a0b9f736");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaults = new BrepBlankOptions();
            pManager.AddBrepParameter("Brep", "B", "Detailed Brep of one beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Allowance", "A", "Extra material on each side of the section.", GH_ParamAccess.item, defaults.Allowance);
            pManager.AddNumberParameter("End Allowance", "EA", "Extra length at each end.", GH_ParamAccess.item, defaults.EndAllowance);
            pManager.AddNumberParameter("Cluster Threshold", "T", "Largest distance between edges in one group: mean angle (radians) plus spread of their distances / 100 mm.", GH_ParamAccess.item, defaults.Clustering.ClusterThreshold);
            pManager.AddBooleanParameter("Planar", "P", "Use a PlanarOrientation if all section frames lie in one plane.", GH_ParamAccess.item, defaults.SimplifyOrientation);
            pManager.AddCurveParameter("Guides", "G", "Edges that the blank lies along: two or more, e.g. the long edges of one face. They keep their places in the blank's section. " +
                "On a beam that tapers, the taper goes to the other side. Without guides the blank is centred on the sections.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Sections", "S", "Number of sections of the Brep that give the orientation.", GH_ParamAccess.item, defaults.SectionSamples);
            pManager.AddIntegerParameter("Edge Samples", "ES", "Number of points on each edge and guide.", GH_ParamAccess.item, defaults.Clustering.EdgeSamples);
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Simplified beam: centred on its section, encloses the Brep.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Blank", "Bl", "Glulam blank from the beam. Its section is rounded up to whole lamellae.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Placement", "Pl", "Placement of the beam in the blank.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Edges", "E", "Parallel edges that the centreline follows, or the guides.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Sections", "S", "Sections of the Brep that give the orientation.", GH_ParamAccess.list);
            pManager.AddTextParameter("Messages", "Msg", "What happened.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            if (!DA.GetData(0, ref brep)) return;

            var options = new BrepBlankOptions();
            DA.GetData(1, ref options.Allowance);
            DA.GetData(2, ref options.EndAllowance);
            DA.GetData(3, ref options.Clustering.ClusterThreshold);
            DA.GetData(4, ref options.SimplifyOrientation);
            var guides = new List<Curve>();
            if (DA.GetDataList(5, guides) && guides.Count > 0)
            {
                if (guides.Count < 2) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Guides needs two or more edges. Ignored.");
                else options.Guides = guides;
            }
            DA.GetData(6, ref options.SectionSamples);
            DA.GetData(7, ref options.Clustering.EdgeSamples);
            options.SectionSamples = Math.Max(3, options.SectionSamples);
            options.Clustering.EdgeSamples = Math.Max(10, options.Clustering.EdgeSamples);

            var result = BrepBlank.Reconstruct(brep, options);

            DA.SetDataList(3, result.Cluster.Select(e => new PolylineCurve(e)));
            DA.SetDataList(4, result.Sections.Select(s => new PolylineCurve(s.Outline)));
            DA.SetDataList(5, result.Messages);

            if (!result.Success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Reconstruction failed.");
                return;
            }

            Message = $"{result.Beam.Width:0.#} x {result.Beam.Height:0.#}";
            DA.SetData(0, new GH_Beam(result.Beam));
            if (result.Blank != null) DA.SetData(1, new GH_Glulam(result.Blank));
            if (result.Placement != null) DA.SetData(2, new GH_ObjectWrapper(result.Placement));
        }
    }
}
