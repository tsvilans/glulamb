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
using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Grasshopper;

namespace GluLamb.GH.Components
{
    public class Cmpt_GridIsoQuad01 : GH_Component
    {
        public Cmpt_GridIsoQuad01()
          : base("Generate Iso Grid (Quad)", "GenIQGrid",
              "Generate a simple quad grid in the parameter space of a Brep.",
              "GluLamb", UiNames.TopologySection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GridIsoQuad;
        public override Guid ComponentGuid => new Guid("bb13143d-1d28-43bd-832b-e77db7bbbb8b");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        double m_scale_to_doc = 1.0;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            m_scale_to_doc = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);

            pManager.AddBrepParameter("Brep", "B", "Brep to project the grid upon.", GH_ParamAccess.item);
            pManager.AddNumberParameter("SizeX", "X", "Size of the grid in the X-direction.", GH_ParamAccess.item, m_scale_to_doc * 2);
            pManager.AddNumberParameter("SizeY", "Y", "Size of the grid in the Y-direction.", GH_ParamAccess.item, m_scale_to_doc * 2);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Centrelines", "C", "Beam centrelines as a tree. Each curve has a unique path, used as an identifier.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Topology", "T", "Connectivity of elements as tree. Each tree branch has the identifiers of connecting elements.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Groups", "G", "Element groups as a tree. Branch 0 contains identifiers for elements in the X-direction, " +
                "branch 1 for elements in the Y-direction, and branch 2 for the boundary edges of the Brep.", GH_ParamAccess.tree);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            if (!DA.GetData("Brep", ref brep)) return;

            double dX = m_scale_to_doc * 2; // 2 meters
            double dY = m_scale_to_doc * 2; // 2 meters

            DA.GetData("SizeX", ref dX);
            DA.GetData("SizeY", ref dY);

            var beams = new DataTree<GH_Curve>();
            var topology = new DataTree<int>();
            var groups = new DataTree<int>();

            var stepX = dX;
            var stepY = dY;

            var stepsI = new double[] { dX, dY };

            var path = new GH_Path(0);
            foreach (BrepFace face in brep.Faces)
            {
                for (int i = 0; i < 2; ++i)
                {
                    var domain = face.Domain(1 - i);

                    var baseCurve = face.IsoCurve(i, domain.Min);

                    var nI = (int)Math.Ceiling(baseCurve.GetLength() / stepsI[i]);

                    for (int j = 1; j < nI; ++j)
                    {
                        baseCurve.LengthParameter(stepsI[i] * j, out double t);
                        var isoCurve = face.IsoCurve(1 - i, t);
                        var isoCurves = face.TrimAwareIsoCurve(1 - i, t);

                        for(int k = 0; k < isoCurves.Length; ++k)
                        {
                            beams.Add(new GH_Curve(isoCurves[k]), path);
                            groups.Add(path.Indices[0], new GH_Path(i));
                            path = path.Increment(0);
                        }
                    }
                }
            }

            for (int i = 0; i < brep.Loops.Count; ++i)
            {
                var loop = brep.Loops[i];

                for (int j = 0; j < loop.Trims.Count; ++j)
                {
                    var trim = loop.Trims[j];

                    if (trim.Edge != null)
                    {
                        var edge = trim.Edge;

                        beams.Add(new GH_Curve(edge.ToNurbsCurve()), path);
                        groups.Add(path.Indices[0], new GH_Path(2));
                        path = path.Increment(0);
                    }
                }
                
            }

            DA.SetDataTree(0, beams);
            DA.SetDataTree(1, topology);
            DA.SetDataTree(2, groups);
        }
    }
}