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

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_GetEdgePoints : GH_Component
    {
        public Cmpt_GetEdgePoints()
          : base("Get Edge Points", "GetEdges",
              "Gets list of points for each beam section edge.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamEdges;
        public override Guid ComponentGuid => new Guid("A406032A-6EB6-4ABA-A5FA-4EF5DC72483E");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Input beam to deconstruct.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Samples", "S", "Optional number of samples to use. " + 
                "Must be >= 2.", GH_ParamAccess.item, -1);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("TopRight", "TR", "Top right points (+Y, +X).", GH_ParamAccess.list);
            pManager.AddPointParameter("TopLeft", "TL", "Top left points (+Y, -X).", GH_ParamAccess.list);
            pManager.AddPointParameter("BottomRight", "BR", "Bottom right points (-Y, +X).", GH_ParamAccess.list);
            pManager.AddPointParameter("BottomLeft", "BL", "Bottom left points (-Y, -X).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get Beam
            Beam m_beam = null;
            DA.GetData<Beam>("Beam", ref m_beam);
            if (m_beam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid beam input.");
                return;
            }

            int N = -1;

            DA.GetData("Samples", ref N);

            if (N < 2) N = Math.Max(40, 6);

            //double[] tt = g.Centreline.DivideByCount(N, true);

            //Plane[] planes = tt.Select(x => g.GetPlane(x)).ToArray();

           BeamOps.GenerateCrossSectionPlanes(m_beam, N, out Plane[] planes, out double[] parameters, GlulamData.Interpolation.LINEAR);


            double w = m_beam.Width;
            double h = m_beam.Height;
            double hw = w / 2;
            double hh = h / 2;

            Point3d[] corners = BeamOps.GenerateCorners(m_beam);

            Point3d[] ptsTR = planes.Select(x => x.PointAt(corners[0].X, corners[0].Y)).ToArray();
            Point3d[] ptsTL = planes.Select(x => x.PointAt(corners[1].X, corners[1].Y)).ToArray();
            Point3d[] ptsBR = planes.Select(x => x.PointAt(corners[2].X, corners[2].Y)).ToArray();
            Point3d[] ptsBL = planes.Select(x => x.PointAt(corners[3].X, corners[3].Y)).ToArray();

            DA.SetDataList("TopRight", ptsTR);
            DA.SetDataList("TopLeft", ptsTL);
            DA.SetDataList("BottomRight", ptsBR);
            DA.SetDataList("BottomLeft", ptsBL);
        }
    }
}