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
    public class Cmpt_GridQuad01 : GH_Component
    {
        public Cmpt_GridQuad01()
          : base("Generate Grid (Quad)", "GenQGrid",
              "Generate a simple quad grid over a Brep.",
              "GluLamb", UiNames.TopologySection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GridQuad;
        public override Guid ComponentGuid => new Guid("e1d058be-479c-4385-a909-f4083a5a30d9");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        double m_scale_to_doc = 1.0;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            m_scale_to_doc = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);

            pManager.AddBrepParameter("Brep", "B", "Brep to project the grid upon.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Optional plane to control the orientation of the grid.", GH_ParamAccess.item, Plane.WorldXY);
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

            Plane plane = Plane.WorldXY;
            DA.GetData("Plane", ref plane);

            double dX = m_scale_to_doc * 2; // 2 meters
            double dY = m_scale_to_doc * 2; // 2 meters

            DA.GetData("SizeX", ref dX);
            DA.GetData("SizeY", ref dY);

            var beams = new DataTree<GH_Curve>();
            var topology = new DataTree<int>();
            var groups = new DataTree<int>();


            var stepX = dX;
            var stepY = dY;

            var bb = brep.GetBoundingBox(plane, out Box worldBox);
            var xform = Transform.PlaneToPlane(Plane.WorldXY, plane);

            var rangeX = bb.Max.X - bb.Min.X;
            var rangeY = bb.Max.Y - bb.Min.Y;

            var nX = (int)Math.Floor(rangeX / stepX);
            var nY = (int)Math.Floor(rangeY / stepY);

            var offsetX = (rangeX - (nX * stepX)) * 0.5;
            var offsetY = (rangeY - (nY * stepY)) * 0.5;

            var path = new GH_Path(0);
            for (int i = 0; i <= nX; ++i)
            {
                var p0 = new Point3d(bb.Min.X + offsetX + i * stepX,
                    bb.Min.Y, 0);
                var p1 = new Point3d(bb.Min.X + offsetX + i * stepX,
                    bb.Max.Y, 0);

                var crv = new Line(p0, p1).ToNurbsCurve();
                crv.Transform(xform);

                var proj = Curve.ProjectToBrep(crv, brep, -Vector3d.ZAxis, 0.01);

                foreach (var p in proj)
                {
                    beams.Add(new GH_Curve(p), path);
                    groups.Add(path.Indices[0], new GH_Path(0));
                    path = path.Increment(0);
                }
            }

            for (int i = 0; i <= nY; ++i)
            {
                var p0 = new Point3d(bb.Min.X,
                    bb.Min.Y + offsetY + i * stepY,
                    0);
                var p1 = new Point3d(bb.Max.X,
                    bb.Min.Y + offsetY + i * stepY,
                    0);

                var crv = new Line(p0, p1).ToNurbsCurve();
                crv.Transform(xform);

                var proj = Curve.ProjectToBrep(crv, brep, -Vector3d.ZAxis, 0.01);

                foreach (var p in proj)
                {
                    beams.Add(new GH_Curve(p), path);
                    groups.Add(path.Indices[0], new GH_Path(1));
                    path = path.Increment(0);
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