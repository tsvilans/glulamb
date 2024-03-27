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
    public class Cmpt_Distribute2 : GH_Component
    {
        public Cmpt_Distribute2()
          : base("Distribute2", "Dist2",
              "Distribute geometry compactly.",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Distribute2;
        public override Guid ComponentGuid => new Guid("c74409a1-e35b-4c8a-b33f-9ae93771fc05");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Origin", "O", "Distribution origin.", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddGeometryParameter("Geometry", "G", "Geometry to distribute.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Geometry planes", "P", "Planes for each piece of geometry.", GH_ParamAccess.list);
            pManager.AddNumberParameter("XSpacing", "X", "Spacing between elements in the X-direction.", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("YSpacing", "Y", "Spacing between elements in the Y-direction.", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("MaxX", "MX", "Maximum X-width before starting a new row.", GH_ParamAccess.item, 3000);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Distributed geometry.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Planes", "P", "Output distribution planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> inputGeometry = new List<GeometryBase>();
            List<Plane> inputPlanes = new List<Plane>();
            Point3d origin = Point3d.Origin;
            double spacingX = 50;
            double spacingY = 50;
            double maxX= 3000;

            DA.GetData("Origin", ref origin);
            DA.GetDataList("Geometry", inputGeometry);
            DA.GetDataList("Geometry planes", inputPlanes);
            DA.GetData("XSpacing", ref spacingX);
            DA.GetData("YSpacing", ref spacingY);
            DA.GetData("MaxX", ref maxX);

            double x = origin.X, y = origin.Y;

            double maxRowY = 0;
            double currentX = 0;

            var outputGeometry = new List<GeometryBase>();
            var outputPlanes = new List<Plane>();

            var debug = new List<object>();

            int N = Math.Min(inputGeometry.Count, inputPlanes.Count);

            for (int i = 0; i < N; ++i)
            {
                var geo = inputGeometry[i];
                if (geo == null) continue;

                var bb = geo.GetBoundingBox(inputPlanes[i]);
                debug.Add(bb);


                maxRowY = Math.Max(maxRowY, bb.Max.Y - bb.Min.Y);

                var plane = new Plane(new Point3d(x + currentX, y, 0), Vector3d.XAxis, Vector3d.YAxis);

                geo.Transform(Transform.PlaneToPlane(inputPlanes[i], plane));

                currentX += bb.Max.X - bb.Min.X + spacingX;

                if (currentX > maxX)
                {
                    currentX = 0;
                    y += maxRowY + spacingY;
                    maxRowY = 0;
                }

                outputGeometry.Add(geo);
                outputPlanes.Add(plane);
            }

            DA.SetDataList("Geometry", outputGeometry);
            DA.SetDataList("Planes", outputPlanes);
        }
    }
}