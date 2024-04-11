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
    public class Cmpt_GetHandle : GH_Component
    {
        public Cmpt_GetHandle()
          : base("Beam Handle", "BHandle",
              "Get plane handle that can be used for orienting the beam.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamHandle;
        public override Guid ComponentGuid => new Guid("4ce71d9b-9dda-4f9f-a636-c818d68e72d5");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam to get plane from.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Flip", "F", "Flip plane around Y-axis.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Bounds", "B", "Locate the plane at the corner of the beam bounding box. " +
                "Otherwise its origin will line at the start of the centreline. Currently this is done very inefficiently.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Output plane.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool m_flip = false, m_bounds = false;

            DA.GetData("Flip", ref m_flip);
            DA.GetData("Bounds", ref m_bounds);

            // Get Beam
            Beam m_beam = null;
            DA.GetData<Beam>("Beam", ref m_beam);
            if (m_beam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid beam input.");
                return;
            }

            var xaxis = m_beam.Centreline.PointAtEnd - m_beam.Centreline.PointAtStart;
            var yaxis = m_beam.Centreline.PointAt(m_beam.Centreline.Domain.Mid) - m_beam.Centreline.PointAtStart;
            var origin = m_beam.Centreline.PointAtStart;

            Plane plane = new Plane(origin, xaxis, yaxis);

            if (m_flip)
                plane = plane.FlipAroundYAxis();

            if (m_bounds)
            {
                var brep = m_beam.ToBrep();
                var bb = brep.GetBoundingBox(plane);

                origin = plane.PointAt(bb.Min.X, bb.Min.Y, bb.Min.Z);

                plane.Origin = origin;
            }

            DA.SetData("Plane", plane);
        }
    }
}