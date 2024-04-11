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

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.DocObjects;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using System.Linq;
using Rhino.Geometry;
using System.ComponentModel;
using System.Diagnostics;

namespace GluLamb.GH.Components
{
    public class Cmpt_UnbendGlulam : GH_Component
    {
        public Cmpt_UnbendGlulam()
          : base("Unbend Glulam", "Unbend",
              "Unroll a single-curved glulam blank. Results in flat lamellas with locator points.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Unbend;
        public override Guid ComponentGuid => new Guid("c706d356-3189-4846-8e03-e5bf34c4d3c0");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "A single-curved glulam blank.", GH_ParamAccess.item);
            pManager.AddNumberParameter("XSpacing", "X", "Locator spacing across the lamella width (X-direction).", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("YSpacing", "Y", "Locator spacing across the lamella length (Y-direction).", GH_ParamAccess.item, 200);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Lamellas", "L", "Flat lamellas as single surfaces.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Locators", "L", "Reference points on each lamella.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData("Glulam", ref beam)) return;
            var glulam = beam as Glulam;
            if (glulam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "This component requires a SingleCurvedGlulam.");
                return;
            }

            double xSpacing = 50, ySpacing = 200;
            DA.GetData("XSpacing", ref xSpacing);
            if (xSpacing <= 0) xSpacing = 50;

            DA.GetData("YSpacing", ref ySpacing);
            if (ySpacing <= 0) ySpacing = 200;

            var Nx = (int)Math.Ceiling(glulam.Data.LamWidth / xSpacing);
            var rx = glulam.Data.LamWidth - xSpacing * (Nx - 1);

            var xCoords = new double[Nx];
            for (int i = 0; i < Nx; ++i)
            {
                xCoords[i] = -glulam.Data.LamWidth * 0.5 + rx * 0.5 + xSpacing * i;
            }

            var Ny = (int)Math.Ceiling(glulam.Centreline.GetLength() / ySpacing);
            var ry = glulam.Centreline.GetLength() - ySpacing * (Ny - 1);

            var yCoords = new double[Ny];
            for (int i = 0; i < Ny; ++i)
            {
                yCoords[i] = ry * 0.5 + ySpacing * i;
            }

            var zCoords = new double[glulam.Data.NumHeight];
            var hHeight = glulam.Height * 0.5;
            for (int i = 0; i < glulam.Data.NumHeight; ++i)
            {
                zCoords[i] = -hHeight
                + glulam.Data.LamHeight * 0.5
                + glulam.Data.LamHeight * i;
            }

            var locators = new List<Line>();
            for (int i = 0; i < Ny; ++i)
            {
                glulam.Centreline.LengthParameter(yCoords[i], out double t);
                var plane = glulam.GetPlane(t);
                for (int j = 0; j < Nx; ++j)
                {
                    var lx = xCoords[j];
                    var line = new Line(
                        plane.PointAt(xCoords[j], glulam.Height),
                        plane.PointAt(xCoords[j], -glulam.Height)
                    );

                    locators.Add(line);
                }
            }

            var lamellas = new DataTree<Brep>();
            var lamellaPlanes = new DataTree<Plane>();
            var lamellaPoints = new DataTree<Point3d>();

            for (int i = 0; i < zCoords.Length; ++i)
            {
                var path = new GH_Path(i);
                var lamella = BeamOps.GetSideSurface(glulam, 1, zCoords[i], glulam.Data.LamWidth, 0);

                var xPoints = new List<Point3d>();
                foreach (var locator in locators)
                {
                    if (!Rhino.Geometry.Intersect.Intersection.CurveBrep(locator.ToNurbsCurve(),
                        lamella, 0.001,
                        out Curve[] overlapCurves, out Point3d[] intersectionPoints))
                    {
                    }
                    xPoints.AddRange(intersectionPoints);
                }

                var ur = new Unroller(lamella);

                ur.AddFollowingGeometry(xPoints);

                var unrolled = ur.PerformUnroll(out Curve[] unrolledCurves, out Point3d[] unrolledPoints, out TextDot[] unrolledDots);

                if (unrolled != null && unrolled.Length > 0)
                {
                    lamellas.AddRange(unrolled, path);
                    lamellaPlanes.Add(Plane.WorldXY, path);
                    lamellaPoints.AddRange(unrolledPoints, path);
                }
            }

            DA.SetDataTree(0, lamellas);
            DA.SetDataTree(1, lamellaPoints);
        }
    }
}