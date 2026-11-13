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
              "A free-form beam from a mesh (IFC, OBJ, STL): a centreline through the section centres, with orientation, width and height.",
              "GluLamb", UiNames.ReconstructionSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Icons.Load("FindCentreline.png");
        public override Guid ComponentGuid => new Guid("c41e7b20-8f63-4d95-b2a7-0e5d93f1c8a6");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaults = new MeshBeamOptions();
            pManager.AddMeshParameter("Mesh", "M", "Mesh of one beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Spacing", "S", "Spacing of the fitted points. Minimum: 0.3 x the section depth.", GH_ParamAccess.item, defaults.SampleSpacing);
            pManager.AddIntegerParameter("Iterations", "I", "Iterations of the fit.", GH_ParamAccess.item, defaults.Iterations);
            pManager.AddIntegerParameter("Start", "St", "Start of the fit. 0: sections along the beam. 1: legacy arc (assumes constant curvature). 2: straight line.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Planar", "P", "Use a PlanarOrientation if all section frames lie in one plane.", GH_ParamAccess.item, defaults.SimplifyOrientation);
            pManager.AddAngleParameter("Sharp Angle", "A", "Smallest angle between face normals of a sharp edge.", GH_ParamAccess.item, defaults.SharpAngle);
            pManager.AddIntegerParameter("End", "E", "How the centreline continues to the mesh ends. 0: trend of the curvature. 1: legacy circle (assumes constant curvature). 2: straight. 3: Rhino's smooth extension.", GH_ParamAccess.item, (int)defaults.End);
            pManager.AddNumberParameter("End Length", "EL", "Length at each end that gives the curvature to continue. 0: 15% of the length.", GH_ParamAccess.item, defaults.EndFitLength);
            pManager.AddNumberParameter("Smoothness", "Sm", "Weight of the penalty on changes of curvature. 0: off. Use only for beams with even curvature. Lbfgs only.", GH_ParamAccess.item, defaults.Smoothness);
            pManager.AddNumberParameter("Typical Tolerance", "TT", "Allowed difference (fraction) between a section and the typical section. Sections that differ more (tapers, joints, end cuts) count less.", GH_ParamAccess.item, defaults.TypicalTolerance);
            pManager.AddIntegerParameter("Fit", "F", "Fit method. 0: L-BFGS. 1: least squares (faster).", GH_ParamAccess.item, (int)defaults.Fit);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Reconstructed beam.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Edges", "E", "Sharp edges that the fit uses.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Sections", "S", "Section outlines that give the frames, width and height.", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "P", "Points of the fitted centreline.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Error", "Er", "Fit error before and after.", GH_ParamAccess.list);
            pManager.AddTextParameter("Messages", "Msg", "What happened.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights", "W", "Weight of each fit sample. 1: typical section. 0: taper, joint or end cut.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Centreline", "C", "The beam's centreline.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "Wi", "The beam's width.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "The beam's height.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Principal Plane", "PP", "Principal axes of the mesh, at the box centre. X: along the beam. Y: the main bend direction.", GH_ParamAccess.item);
            pManager.AddBoxParameter("Principal Box", "PB", "Box around the mesh along its principal axes.", GH_ParamAccess.item);
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
            options.End = (EndExtension)Math.Clamp(end, 0, 3);
            DA.GetData(7, ref options.EndFitLength);
            DA.GetData(8, ref options.Smoothness);
            DA.GetData(9, ref options.TypicalTolerance);
            int fit = (int)options.Fit;
            DA.GetData(10, ref fit);
            options.Fit = (CentrelineFit)Math.Clamp(fit, 0, 1);

            if (options.SampleSpacing <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Spacing must be positive."); return; }

            var result = MeshBeam.Reconstruct(mesh, options);

            DA.SetDataList(1, result.Edges.Select(e => new PolylineCurve(e)));
            DA.SetDataList(2, result.Sections.Select(s => new PolylineCurve(s.Outline)));
            if (result.FittedPoints != null) DA.SetDataList(3, result.FittedPoints);
            DA.SetDataList(4, new[] { result.InitialError, result.Error });
            DA.SetDataList(5, result.Messages);
            if (result.Weights != null) DA.SetDataList(6, result.Weights);

            if (result.Bounds.SizeX > 0)
            {
                var box = result.Bounds.ToBox();
                var centre = result.Bounds.Plane;
                centre.Origin = box.Center;
                DA.SetData(10, centre);
                DA.SetData(11, box);
            }

            if (!result.Success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Reconstruction failed.");
                return;
            }

            Message = $"{result.Beam.Width:0.#} x {result.Beam.Height:0.#} ({options.Fit})";
            DA.SetData(0, new GH_Beam(result.Beam));
            DA.SetData(7, result.Beam.Centreline);
            DA.SetData(8, result.Beam.Width);
            DA.SetData(9, result.Beam.Height);
        }
    }
}
