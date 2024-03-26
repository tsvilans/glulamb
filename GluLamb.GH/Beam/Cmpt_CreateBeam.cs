/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2020 Tom Svilans
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
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Types.Transforms;
using Rhino;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateBeam : GH_Component, IGH_VariableParameterComponent
    {
        public Cmpt_CreateBeam()
          : base("Create Beam", "Beam",
              "Create glulam.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_FreeformGlulam_24x24;
        public override Guid ComponentGuid => new Guid("2567693B-04DC-4654-AE17-F4227615D732");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        private double m_scale = 1.0;

        readonly IGH_Param[] parameters = new IGH_Param[4]
        {
            new Param_Number() { Name = "Width", NickName = "W", Description = "Cross-section width (X-axis).", Optional = true },
            new Param_Number() { Name = "Height", NickName = "H", Description = "Cross-section height (Y-axis).", Optional = true },
            new Param_Integer() { Name = "Samples", NickName = "S", Description = "Samples along length.", Optional = true },
            new Param_Integer() { Name = "Alignment", NickName = "A", Description = "Cross-section alignment as integer value between 0 and 8.", Optional = true },
        };

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Dimensions", AddWidthHeight, true, Params.Input.Any(x => x.Name == "Width") && Params.Input.Any(x => x.Name == "Height"));
            Menu_AppendItem(menu, "Samples", AddSamples, true, Params.Input.Any(x => x.Name == "Samples"));
            Menu_AppendItem(menu, "Alignment", AddAlignment, true, Params.Input.Any(x => x.Name == "Alignment"));
        }

        private void AddWidthHeight(object sender, EventArgs e)
        {
            AddParam(0);
            AddParam(1);
        }

        private void AddSamples(object sender, EventArgs e) => AddParam(2);
        private void AddAlignment(object sender, EventArgs e) => AddParam(3);

        private void AddParam(int index)
        {
            IGH_Param parameter = parameters[index];

            if (Params.Input.Any(x => x.Name == parameter.Name))
            {
                Params.UnregisterInputParameter(Params.Input.First(x => x.Name == parameter.Name), true);
            }
            else
            {
                int insertIndex = Params.Input.Count;
                for (int i = 0; i < Params.Input.Count; i++)
                {
                    int otherIndex = Array.FindIndex(parameters, x => x.Name == Params.Input[i].Name);
                    if (otherIndex > index)
                    {
                        insertIndex = i;
                        break;
                    }
                }
                Params.RegisterInputParam(parameter, insertIndex);
            }
            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Glulam centreline curve.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Orientation", "O", "Orientation of Glulam cross-section.", GH_ParamAccess.list);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam object.", GH_ParamAccess.item);
        }

        protected CrossSectionOrientation ParseGlulamOrientation(List<object> input, Curve curve)
        {
            if (input == null || input.Count < 1)
            {
                if (curve.IsPlanar(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                {
                    curve.TryGetPlane(out Plane plane, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    return new PlanarOrientation(plane);
                }
                return new RmfOrientation();
            }
            if (input.Count == 1)
            {
                object single = input[0];
                //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, single.ToString());

                if (single is Vector3d)
                    return new VectorOrientation((Vector3d)single);
                if (single is GH_Vector)
                    return new VectorOrientation((single as GH_Vector).Value);
                if (single is Plane)
                    return new VectorOrientation(((Plane)single).YAxis);
                if (single is GH_Plane)
                    return new VectorOrientation((single as GH_Plane).Value.YAxis);
                if (single is GH_Line)
                    return new VectorOrientation((single as GH_Line).Value.Direction);
                if (single is Surface)
                    return new SurfaceOrientation((single as Surface).ToBrep());
                if (single is GH_Surface)
                    return new SurfaceOrientation((single as GH_Surface).Value);
                if (single is Brep)
                    return new SurfaceOrientation(single as Brep);
                if (single is GH_Brep)
                    return new SurfaceOrientation((single as GH_Brep).Value);
                if (single is GH_Curve)
                {
                    Curve crv = (single as GH_Curve).Value;
                    if (crv.IsLinear())
                        return new VectorOrientation(crv.TangentAtStart);

                    return new RailCurveOrientation((single as GH_Curve).Value);
                }
                if (single == null)
                    return new RmfOrientation();
            }

            if (input.First() is GH_Plane)
            {

                List<double> parameters = new List<double>();
                List<Vector3d> vectors = new List<Vector3d>();

                for (int i = 0; i < input.Count; ++i)
                {
                    GH_Plane p = input[i] as GH_Plane;
                    if (p == null) continue;
                    double t;
                    curve.ClosestPoint(p.Value.Origin, out t);

                    parameters.Add(t);
                    vectors.Add(p.Value.YAxis);
                }

                return new VectorListOrientation(curve, parameters, vectors);
            }

            if (input.First() is GH_Line)
            {

                List<double> parameters = new List<double>();
                List<Vector3d> vectors = new List<Vector3d>();

                for (int i = 0; i < input.Count; ++i)
                {
                    GH_Line line = input[i] as GH_Line;
                    if (line == null) continue;

                    double t;
                    curve.ClosestPoint(line.Value.From, out t);
                    parameters.Add(t);
                    vectors.Add(line.Value.Direction);
                }

                return new VectorListOrientation(curve, parameters, vectors);
            }

            if (input.First() is GH_Curve)
            {
                List<double> parameters = new List<double>();
                List<Vector3d> vectors = new List<Vector3d>();

                for (int i = 0; i < input.Count; ++i)
                {
                    GH_Curve crv = input[i] as GH_Curve;
                    if (crv == null) continue;

                    double t;
                    curve.ClosestPoint(crv.Value.PointAtStart, out t);
                    parameters.Add(t);
                    vectors.Add(crv.Value.TangentAtStart);
                }

                return new VectorListOrientation(curve, parameters, vectors);
            }

            if (curve.IsPlanar())
            {
                curve.TryGetPlane(out Plane plane);
                return new PlanarOrientation(plane);
            }
            return new RmfOrientation();
        }

        protected GlulamData ParseGlulamData(object input)
        {
            if (input == null)
            {
                return GlulamData.Default;
            }
            if (input is GlulamData)
                return (input as GlulamData).Duplicate();
            else if (input is GH_GlulamData)
                return (input as GH_GlulamData).Value.Duplicate();
            else
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get GlulamData.");
            return GlulamData.Default;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            m_scale = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            Curve crv = null;

            if (!DA.GetData("Curve", ref crv)) return;
            crv.Domain.MakeIncreasing();

            List<object> r_orientation = new List<object>();
            DA.GetDataList("Orientation", r_orientation);

            double width = 0.1 * m_scale, height = 0.2 * m_scale;
            int samples = (int)Math.Ceiling(crv.GetLength() / (0.05 * m_scale)) + 1;
            int alignment = 0;

            bool hasDimensions = Params.Input.Any(x => x.Name == "Width");
            bool hasSamples = Params.Input.Any(x => x.Name == "Samples");
            bool hasAlignment = Params.Input.Any(x => x.Name == "Alignment");

            if (hasDimensions)
            {
                DA.GetData("Width", ref width);
                DA.GetData("Height", ref height);
            }

            if (hasSamples)
            {
                DA.GetData("Samples", ref samples);
            }

            if (hasAlignment)
            {
                DA.GetData("Alignment", ref alignment);
            }



            CrossSectionOrientation orientation = ParseGlulamOrientation(r_orientation, crv);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, orientation.ToString());

            var beam = new Beam() { Centreline = crv, Orientation = orientation, Width = width, Height = height };

            DA.SetData("Beam", new GH_Beam(beam));
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index) => false;
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index) => false;
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index) => null;
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index) => false;
        void IGH_VariableParameterComponent.VariableParameterMaintenance() { }
    }
}