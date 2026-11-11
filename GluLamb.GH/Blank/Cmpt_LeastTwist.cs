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

using Grasshopper.Kernel;
using Rhino;

using GluLamb.Blanks;

namespace GluLamb.GH.Components
{
    public class Cmpt_LeastTwist : GH_Component
    {
        public Cmpt_LeastTwist()
          : base("Least Twist Orientation", "LeastTwist",
              "Prototype. Suggest an orientation for a free-form beam whose two-step lamination layers twist as little as possible, " +
              "and show what it costs: how far it turns the section from the design. A layer that follows the curve's " +
              "rotation-minimizing frame doesn't twist, so it is developable.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("9d4c7a28-1e6b-4f53-8a90-2b5e7c3d1f84");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Free-form beam (the design).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Mode", "M", "0 no twist (the rotation-minimizing frame at the one angle closest to the design; every layer developable), " +
                "1 even twist between the design's end orientations, 2 the orientation closest to the design whose twist stays within the allowed strain.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Stack Axis", "SA", "The beam's axis the layers are stacked along: 1 its Y-axis (height), 0 its X-axis (width).", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Layer Thickness", "LT", "Thickness of the layers, for the twist limit (shear of their faces). 0: leave it out.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Radius Ratio", "RR", "Smallest radius over lamella thickness (Eurocode: 200). The allowed strain is 1 / (2 x this).", GH_ParamAccess.item, Glulam.RadiusMultiplier);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam with the suggested orientation.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Deviation", "D", "How far the suggestion turns the section from the design, along the beam (degrees).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Twist", "T", "The suggestion's twist along the beam (degrees per metre).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Design Twist", "DT", "The design's twist along the beam (degrees per metre).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Allowed Twist", "AT", "Largest twist the allowed strain lets a layer of this width and thickness take (degrees per metre).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max Deviation", "MD", "Largest turn from the design (degrees).", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "Msg", "What happened.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData(0, ref beam) || beam == null) return;
            int mode = 0, stack = 1;
            double thickness = 0, ratio = Glulam.RadiusMultiplier;
            DA.GetData(1, ref mode);
            DA.GetData(2, ref stack);
            DA.GetData(3, ref thickness);
            DA.GetData(4, ref ratio);

            var result = LeastTwist.Suggest(beam, (TwistMode)Math.Clamp(mode, 0, 2), stack, 0, thickness, ratio);
            DA.SetDataList(6, result.Messages);
            if (result.Beam == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, result.Messages.LastOrDefault() ?? "Failed."); return; }

            // Per metre in model units
            double perMetre = RhinoMath.ToDegrees(1) * Globals.FromMillimeters(1000);
            var design = new double[result.S.Length];
            for (int i = 0; i < design.Length; ++i)
            {
                int i0 = Math.Max(0, i - 1), i1 = Math.Min(design.Length - 1, i + 1);
                design[i] = (result.DesignAngle[i1] - result.DesignAngle[i0]) / Math.Max(result.S[i1] - result.S[i0], 1e-12) * perMetre;
            }

            DA.SetData(0, new GH_Beam(result.Beam));
            DA.SetDataList(1, result.Angle.Select((a, i) => RhinoMath.ToDegrees(a - result.DesignAngle[i])));
            DA.SetDataList(2, result.Twist.Select(t => t * perMetre));
            DA.SetDataList(3, design);
            DA.SetData(4, result.AllowedTwist * perMetre);
            DA.SetData(5, RhinoMath.ToDegrees(result.MaxDeviation));
            Message = $"{(TwistMode)Math.Clamp(mode, 0, 2)}: {RhinoMath.ToDegrees(result.MaxDeviation):0.#} deg";
        }
    }
}
