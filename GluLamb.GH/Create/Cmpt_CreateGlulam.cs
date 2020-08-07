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
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateGlulam : GH_Component
    {
        public Cmpt_CreateGlulam()
          : base("Glulam", "Glulam",
              "Create glulam.",
              "GluLamb", "Create")
        {
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Glulam centreline curve.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Orientation", "O", "Orientation of Glulam cross-section.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Data", "D", "Glulam data.", GH_ParamAccess.item);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam object.", GH_ParamAccess.item);
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
            Curve crv = null;

            if (!DA.GetData("Curve", ref crv))
                return;

            crv.Domain.MakeIncreasing();

            List<object> r_orientation = new List<object>();
            DA.GetDataList("Orientation", r_orientation);

            object r_data = null;
            DA.GetData("Data", ref r_data);

            CrossSectionOrientation orientation = ParseGlulamOrientation(r_orientation, crv);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, orientation.ToString());

            GlulamData data = ParseGlulamData(r_data);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, GlulamDataToString(data));

            data.Samples = (int)Math.Ceiling(crv.GetLength() / GlulamData.DefaultSampleDistance);

            Glulam glulam = Glulam.CreateGlulam(crv, orientation, data);


            DA.SetData("Glulam", new GH_Glulam(glulam));
        }

        protected string GlulamDataToString(GlulamData data)
        {
            return $"GlulamData [ lw {data.LamWidth} lh {data.LamHeight} nw {data.NumHeight} nh {data.NumHeight} s {data.Samples} sa {data.SectionAlignment} ]";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_FreeformGlulam_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("2567693B-04DC-4654-AE17-F4227615D732"); }
        }
    }
}