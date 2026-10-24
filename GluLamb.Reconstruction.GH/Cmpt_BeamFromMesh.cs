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
using Rhino.Geometry;

using GluLamb.GH;

namespace GluLamb.Reconstruction.GH
{
    public class Cmpt_BeamFromMesh : GH_Component
    {
        public Cmpt_BeamFromMesh()
          : base("Beam From Mesh", "MeshBeam",
              "Reconstruct a free-form beam from a mesh of it (e.g. from IFC, OBJ or STL): a centreline fitted through the centres " +
              "of its sections, the section frames as its orientation, and the largest section width and height.",
              "GluLamb", UiNames.ReconstructionSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("c41e7b20-8f63-4d95-b2a7-0e5d93f1c8a6");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaults = new MeshBeamOptions();
            pManager.AddMeshParameter("Mesh", "M", "Mesh of one beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Spacing", "S", "Spacing of the points the centreline is fitted through. It isn't taken below 0.3 x the section's depth, where closer points add freedom but no information.", GH_ParamAccess.item, defaults.SampleSpacing);
            pManager.AddIntegerParameter("Iterations", "I", "Iterations of the fit.", GH_ParamAccess.item, defaults.Iterations);
            pManager.AddIntegerParameter("Start", "St", "What the fit starts from: 0 sections walked along the beam, 1 an arc through three sections, 2 a straight line.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Planar", "P", "Use a PlanarOrientation if the section frames lie in one plane.", GH_ParamAccess.item, defaults.SimplifyOrientation);
            pManager.AddAngleParameter("Sharp Angle", "A", "Smallest angle between face normals for an edge to count as a sharp edge of the beam.", GH_ParamAccess.item, defaults.SharpAngle);
            pManager.AddIntegerParameter("End", "E", "How the centreline is run out past its first and last typical sections to the ends of the mesh: " +
                "0 continue the curvature of the last stretch (an arc), 1 straight, 2 Rhino's smooth extension.", GH_ParamAccess.item, (int)defaults.End);
            pManager.AddNumberParameter("End Length", "EL", "Length at each end of the fitted centreline that the curvature to continue is taken from. 0: 15% of its length.", GH_ParamAccess.item, defaults.EndFitLength);
            pManager.AddNumberParameter("Smoothness", "Sm", "Weight of the penalty on changes of curvature. Higher keeps the ends closer to the shape of the rest of the beam; 0 turns it off.", GH_ParamAccess.item, defaults.Smoothness);
            pManager.AddNumberParameter("Typical Tolerance", "TT", "How much a section's size may differ from the beam's typical section (as a fraction) and still count fully in the fit. " +
                "Sections that differ more (tapers, joints, end cuts) count less.", GH_ParamAccess.item, defaults.TypicalTolerance);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Reconstructed beam.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Edges", "E", "Sharp edges of the mesh that the fit uses.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Sections", "S", "Section outlines that give the frames, width and height.", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "P", "Points the fitted centreline passes through.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Error", "Er", "Fit error before and after (weighted distances of the samples from their section centres, plus the curvature penalty).", GH_ParamAccess.list);
            pManager.AddTextParameter("Messages", "Msg", "What happened.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights", "W", "Weight of each fit sample in the last round: 1 a typical section, 0 a taper, joint or end cut.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh)) return;

            var options = new MeshBeamOptions();
            DA.GetData(1, ref options.SampleSpacing);
            DA.GetData(2, ref options.Iterations);
            int start = 0;
            DA.GetData(3, ref start);
            options.Start = (InitialCentreline)Math.Clamp(start, 0, 2);
            DA.GetData(4, ref options.SimplifyOrientation);
            DA.GetData(5, ref options.SharpAngle);
            if (Params.Input[5] is Grasshopper.Kernel.Parameters.Param_Number p && p.UseDegrees)
                options.SharpAngle = Rhino.RhinoMath.ToRadians(options.SharpAngle);

            int end = (int)options.End;
            DA.GetData(6, ref end);
            options.End = (EndExtension)Math.Clamp(end, 0, 2);
            DA.GetData(7, ref options.EndFitLength);
            DA.GetData(8, ref options.Smoothness);
            DA.GetData(9, ref options.TypicalTolerance);

            if (options.SampleSpacing <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Spacing must be positive."); return; }

            var result = MeshBeam.Reconstruct(mesh, options);

            DA.SetDataList(1, result.Edges.Select(e => new PolylineCurve(e)));
            DA.SetDataList(2, result.Sections.Select(s => new PolylineCurve(s.Outline)));
            if (result.FittedPoints != null) DA.SetDataList(3, result.FittedPoints);
            DA.SetDataList(4, new[] { result.InitialError, result.Error });
            DA.SetDataList(5, result.Messages);
            if (result.Weights != null) DA.SetDataList(6, result.Weights);

            if (!result.Success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Reconstruction failed.");
                return;
            }

            Message = $"{result.Beam.Width:0.#} x {result.Beam.Height:0.#}";
            DA.SetData(0, new GH_Beam(result.Beam));
        }
    }
}
