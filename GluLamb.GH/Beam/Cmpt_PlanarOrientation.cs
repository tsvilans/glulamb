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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;

namespace GluLamb.GH.Components
{
    public class Cmpt_PlanarOrientation : GH_Component
    {
        public Cmpt_PlanarOrientation()
          : base("Planar Orientation", "Planar",
              "If a beam's orientation vectors all lie in one plane (e.g. a VectorListOrientation from sections of a single-curved beam), " +
              "give it the PlanarOrientation that gives the same vectors. Other beams pass through unchanged.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamOrientation;
        public override Guid ComponentGuid => new Guid("8b0f6c2e-5d4a-4e7b-9a13-2c6e1f7d0b48");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam with a VectorListOrientation.", GH_ParamAccess.item);
            pManager.AddAngleParameter("Angle Tolerance", "A", "Largest angle between an orientation vector and the plane.", GH_ParamAccess.item, RhinoMath.ToRadians(1.0));
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam, with a PlanarOrientation if its vectors lie in one plane.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Planar", "P", "Whether the orientation was made planar.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "Pl", "The PlanarOrientation's plane.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo goo = null;
            if (!DA.GetData(0, ref goo)) return;
            var beam = goo is GH_Beam ghBeam ? ghBeam.Value : goo?.ScriptVariable() as Beam;
            if (beam == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a beam."); return; }

            double angle = RhinoMath.ToRadians(1.0);
            DA.GetData(1, ref angle);
            if (Params.Input[1] is Grasshopper.Kernel.Parameters.Param_Number p && p.UseDegrees)
                angle = RhinoMath.ToRadians(angle);

            if (beam.Orientation is VectorListOrientation vlo && vlo.TryGetPlanarOrientation(beam.Centreline, angle, out PlanarOrientation planar))
            {
                var copy = beam.Duplicate();
                copy.Orientation = planar;
                DA.SetData(0, new GH_Beam(copy));
                DA.SetData(1, true);
                DA.SetData(2, planar.Plane);
                return;
            }

            if (beam.Orientation is PlanarOrientation already)
                DA.SetData(2, already.Plane);
            else if (!(beam.Orientation is VectorListOrientation))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"The beam has a {beam.Orientation}; only a VectorListOrientation is converted.");

            DA.SetData(0, new GH_Beam(beam));
            DA.SetData(1, beam.Orientation is PlanarOrientation);
        }
    }
}
