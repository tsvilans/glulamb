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
              "Reconstruct the blank of a detailed beam Brep (with joints, cuts, drillings): a rectangular section swept along a " +
              "centreline that follows the Brep's longest run of parallel edges, big enough to enclose the Brep.",
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
            pManager.AddNumberParameter("Cluster Threshold", "T", "How parallel edges must be to be grouped: mean angle (radians) plus " +
                "the spread of their distances divided by 100 mm.", GH_ParamAccess.item, defaults.ClusterThreshold);
            pManager.AddBooleanParameter("Planar", "P", "Use a PlanarOrientation if the section frames lie in one plane.", GH_ParamAccess.item, defaults.SimplifyOrientation);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Simplified beam: centred on its section, enclosing the Brep.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Blank", "Bl", "Glulam blank made from the beam (its section rounded up to whole lamellae).", GH_ParamAccess.item);
            pManager.AddGenericParameter("Placement", "Pl", "Where the beam sits in the blank.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Edges", "E", "The parallel edges the centreline follows.", GH_ParamAccess.list);
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
            DA.GetData(3, ref options.ClusterThreshold);
            DA.GetData(4, ref options.SimplifyOrientation);

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
