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
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_Distribute : GH_Component
    {
        public Cmpt_Distribute()
          : base("Distribute", "Dist",
              "Distribute geometry on an XY grid.",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Origin", "O", "Distribution origin.", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddGeometryParameter("Geometry", "G", "Geometry to distribute.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Geometry planes", "P", "Planes for each piece of geometry.", GH_ParamAccess.list);
            pManager.AddNumberParameter("XSpacing", "X", "Spacing in the X-direction.", GH_ParamAccess.item, 2000.0);
            pManager.AddNumberParameter("YSpacing", "Y", "Spacing in the Y-direction.", GH_ParamAccess.item, 1000.0);
            pManager.AddIntegerParameter("NumColumns", "N", "Number of columns in the distribution.", GH_ParamAccess.item, 4);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Distributed geometry.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Planes", "P", "Output distribution planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> geometry_input = new List<GeometryBase>();
            List<Plane> planes_input = new List<Plane>();
            Point3d origin = Point3d.Origin;
            double xspacing = 2000.0;
            double yspacing = 1000.0;
            int numCols = 4;

            DA.GetData("Origin", ref origin);
            DA.GetDataList("Geometry", geometry_input);
            DA.GetDataList("Geometry planes", planes_input);
            DA.GetData("XSpacing", ref xspacing);
            DA.GetData("YSpacing", ref yspacing);
            DA.GetData("NumColumns", ref numCols);

            int N = Math.Min(geometry_input.Count, planes_input.Count);
            int nCol = numCols;
            int nRow = (int)Math.Ceiling((double)N / nCol);

            var planes = new List<Plane>();
            var geometry = new List<GeometryBase>();

            int counter = 0;
            for (int i = 0; i < nRow; ++i)
            {
                for (int j = 0; j < nCol && counter < N; ++j, counter++)
                {
                    var plane = new Plane(origin + Vector3d.XAxis * xspacing * j + Vector3d.YAxis * yspacing * i,
                      Vector3d.XAxis, Vector3d.YAxis);

                    var geo = geometry_input[counter];
                    if (geo == null) continue;
                    geo.Transform(Transform.PlaneToPlane(planes_input[counter], plane));
                    geometry.Add(geo);

                    planes.Add(plane);
                }
            }
            DA.SetDataList("Geometry", geometry);
            DA.SetDataList("Planes", planes);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_GlulamFrame_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("daf77ad9-94cb-4280-9310-4f74b5d4e754"); }
        }
    }
}