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
    public class Cmpt_GridTri01 : GH_Component
    {
        public Cmpt_GridTri01()
          : base("Generate Grid (Tri)", "GenTGrid",
              "Generate a simple tri grid over a Brep.",
              "GluLamb", UiNames.TopologySection)
        {
            
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.GridTri;
        public override Guid ComponentGuid => new Guid("3bb2ed21-56e4-4f36-80eb-947f6f661fb2");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        double m_scale_to_doc = 1.0;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            m_scale_to_doc = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            pManager.AddBrepParameter("Brep", "B", "Brep to project the grid upon.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Optional plane to control the orientation of the grid.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("SizeU", "U", "Size of the grid in the U-direction.", GH_ParamAccess.item, m_scale_to_doc * 2);
            pManager.AddNumberParameter("SizeV", "V", "Size of the grid in the V-direction.", GH_ParamAccess.item, m_scale_to_doc * 2);
            pManager.AddNumberParameter("Offset", "O", "Offset of the V-axis.", GH_ParamAccess.item, 0);
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

            double dU = m_scale_to_doc * 2; // 2 meters
            double dV = m_scale_to_doc * 2; // 2 meters
            double offsetV = 0;

            DA.GetData("SizeU", ref dU);
            DA.GetData("SizeV", ref dV);
            DA.GetData("Offset", ref offsetV);

            var beams = new DataTree<GH_Curve>();
            var topology = new DataTree<int>();
            var groups = new DataTree<int>();

            var bb = brep.GetBoundingBox(plane, out Box worldBox);
            var xform = Transform.PlaneToPlane(Plane.WorldXY, plane);

            var rangeX = bb.Max.X - bb.Min.X;
            var rangeY = bb.Max.Y - bb.Min.Y;

            double tan60 = Math.Tan(RhinoMath.ToRadians(60));
            double cos60 = Math.Cos(RhinoMath.ToRadians(60));

            var stepU = dU;
            stepU = stepU / cos60;
            var stepV = dV;
            var stepX = 2 * dU / tan60;


            var startU = tan60 * rangeX;
            var nStartU = (int)Math.Floor(startU / stepU);

            startU = stepU * nStartU;

            var nU = (int)Math.Floor(rangeY / stepU) + nStartU;
            var nV = (int)Math.Floor(rangeY / stepV);
            var nX = (int)Math.Floor(rangeX / stepX);


            var offsetX = (rangeX - (nX * stepX)) * 0.5;
            var shiftX = offsetX * tan60;

            var offsetY = (rangeY - (nV * stepV)) * 0.5;


            var path = new GH_Path(0);

            for (int i = 0; i <= nU; ++i)
            {

                var minY = new double[]{
            bb.Min.Y - startU + i * stepU,
            bb.Min.Y - startU *0 + i * stepU
            };

                var maxY = new double[]{
            bb.Min.Y - startU + rangeX * tan60 + i * stepU,
            bb.Min.Y - startU*0 - rangeX * tan60 + i * stepU
            };

                var shift = new double[] { -shiftX, shiftX };

                for (int j = 0; j < 2; ++j)
                {
                    var p0 = new Point3d(
                        bb.Min.X,
                        minY[j] + offsetY + shift[j],
                        0);
                    var p1 = new Point3d(
                        bb.Max.X,
                        maxY[j] + offsetY + shift[j],
                        0);

                    var crv = new Line(p0, p1).ToNurbsCurve();
                    crv.Transform(xform);

                    var proj = Curve.ProjectToBrep(crv, brep, -Vector3d.ZAxis, 0.01);

                    foreach (var p in proj)
                    {
                        beams.Add(new GH_Curve(p), path);
                        groups.Add(path.Indices[0], new GH_Path(j));
                        path = path.Increment(0);
                    }
                }
            }

            for (int i = 0; i <= nV; ++i)
            {
                var p0 = new Point3d(bb.Min.X,
                    bb.Min.Y + offsetY + offsetV + i * stepV,
                    0);
                var p1 = new Point3d(bb.Max.X,
                    bb.Min.Y + offsetY + offsetV + i * stepV,
                    0);

                var crv = new Line(p0, p1).ToNurbsCurve();
                crv.Transform(xform);

                var proj = Curve.ProjectToBrep(crv, brep, -Vector3d.ZAxis, 0.01);

                foreach (var p in proj)
                {
                    beams.Add(new GH_Curve(p), path);
                    groups.Add(path.Indices[0], new GH_Path(2));
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
                        groups.Add(path.Indices[0], new GH_Path(3));
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