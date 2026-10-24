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
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_DivideForLength : GH_Component
    {
        public Cmpt_DivideForLength()
          : base("Divide For Length", "DivLen",
              "Divide curve elements so that no piece, with its extensions, is longer than a maximum length. " +
              "Cuts keep clear of existing joints and of the curve ends, and the pieces are made as equal as possible. " +
              "Run the result through Create Topology again so the cuts become splice joints.",
              "GluLamb", UiNames.TopologySection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamSplit;
        public override Guid ComponentGuid => new Guid("c1a7f3d2-5b8e-4e6a-9f21-7d3b0a6e4c58");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            double scale = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Millimeters);

            pManager.AddCurveParameter("Curves", "C", "Curve elements as tree. Branch paths serve as element IDs.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Connected elements", "CI", "Optional element indices per joint, from Create Topology (CP) or Classify Joints (CI). Cuts keep clear of these joints.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Connected parameters", "CT", "Curve parameters matching the connected elements, from Create Topology (CT) or Classify Joints (CP).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Max length", "L", "Maximum length of a piece, extensions included.", GH_ParamAccess.item, scale * 12.0);
            pManager.AddNumberParameter("Clearance", "CL", "Minimum distance from a cut to a joint or to a curve end.", GH_ParamAccess.item, scale * 0.5);
            pManager.AddNumberParameter("Splice allowance", "SA", "Extra length each piece needs at a cut, e.g. the splice's overlap. Leave at 0 to account for it in the max length instead.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Extensions", "E", "Optional extensions {start, end} per curve path, e.g. from a first Construct Joints. The end pieces count them in their length.", GH_ParamAccess.tree);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Divided curve elements, one per branch, renumbered from 0. Plug into Create Topology.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Source", "S", "Index of the input element each piece came from.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Lengths", "L", "Length of each piece, extensions and splice allowances included.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Cuts", "X", "Cut positions.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> curveTree)) return;
            DA.GetDataTree(1, out GH_Structure<GH_Integer> pairs);
            DA.GetDataTree(2, out GH_Structure<GH_Number> parameters);

            double maxLength = 0, clearance = 0, allowance = 0;
            DA.GetData("Max length", ref maxLength);
            DA.GetData("Clearance", ref clearance);
            DA.GetData("Splice allowance", ref allowance);
            DA.GetDataTree(6, out GH_Structure<GH_Number> extensionTree);

            if (maxLength <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Max length must be greater than 0.");
                return;
            }

            // Joint parameters per element
            var joints = new Dictionary<int, List<double>>();
            if (pairs != null && parameters != null)
                foreach (var path in pairs.Paths)
                {
                    var indices = pairs[path];
                    if (!parameters.PathExists(path)) continue;
                    var ts = parameters[path];

                    for (int i = 0; i < Math.Min(indices.Count, ts.Count); ++i)
                    {
                        if (indices[i] == null || ts[i] == null) continue;
                        if (!joints.TryGetValue(indices[i].Value, out var list))
                            joints[indices[i].Value] = list = new List<double>();
                        list.Add(ts[i].Value);
                    }
                }

            var curvesOut = new DataTree<GH_Curve>();
            var sourceOut = new DataTree<GH_Integer>();
            var lengthsOut = new DataTree<GH_Number>();
            var cutsOut = new List<GH_Point>();

            int k = 0;
            foreach (var path in curveTree.Paths)
            {
                var branch = curveTree[path];
                if (branch.Count < 1 || branch[0]?.Value == null) continue;
                if (branch.Count > 1)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Got more curves than expected. Branches with more than one curve are discarded.");

                var index = path.Indices[0];
                var curve = branch[0].Value;

                double e0 = 0, e1 = 0;
                if (extensionTree != null && extensionTree.PathExists(path))
                {
                    var ext = extensionTree[path];
                    if (ext.Count > 0 && ext[0] != null) e0 = ext[0].Value;
                    if (ext.Count > 1 && ext[1] != null) e1 = ext[1].Value;
                }

                joints.TryGetValue(index, out var jointParameters);
                var cuts = LengthDivision.FindCuts(curve, jointParameters ?? new List<double>(), maxLength, clearance, allowance, e0, e1);

                if (cuts == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Element {index}: no division fits the max length and clearances; it is left whole.");
                    cuts = new double[0];
                }

                var pieces = LengthDivision.Divide(curve, cuts);
                var lengths = LengthDivision.PieceLengths(curve.GetLength(), cuts, allowance, e0, e1);

                for (int i = 0; i < pieces.Length; ++i)
                {
                    var newPath = new GH_Path(k++);
                    curvesOut.Add(new GH_Curve(pieces[i]), newPath);
                    sourceOut.Add(new GH_Integer(index), newPath);
                    if (pieces.Length == lengths.Length)
                        lengthsOut.Add(new GH_Number(lengths[i]), newPath);
                }

                foreach (var s in cuts)
                    if (curve.LengthParameter(s, out double t))
                        cutsOut.Add(new GH_Point(curve.PointAt(t)));
            }

            DA.SetDataTree(0, curvesOut);
            DA.SetDataTree(1, sourceOut);
            DA.SetDataTree(2, lengthsOut);
            DA.SetDataList(3, cutsOut);
        }
    }
}
