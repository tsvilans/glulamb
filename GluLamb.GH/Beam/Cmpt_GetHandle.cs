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
          : base("Get Handle", "Handle",
              "Gets a plane to orient a beam or blank. With a name: the beam's plane of that name (e.g. \"press\"). " +
              "Without: the beam's first named plane, or a plane along the beam's chord that bulges to +Y.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamHandle;
        public override Guid ComponentGuid => new Guid("4ce71d9b-9dda-4f9f-a636-c818d68e72d5");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam or blank.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Flip", "F", "Flip the plane around its Y-axis.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Bounds", "B", "Put the plane at the corner of the beam's bounding box in the plane. Slow.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Name", "N", "Name of the plane to get.", GH_ParamAccess.item);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "The plane.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "N", "Names of the beam's planes.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool m_flip = false, m_bounds = false;
            DA.GetData("Flip", ref m_flip);
            DA.GetData("Bounds", ref m_bounds);

            Beam m_beam = null;
            DA.GetData<Beam>("Beam", ref m_beam);
            if (m_beam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid beam input.");
                return;
            }

            var frames = m_beam.Frames ?? new Dictionary<string, Plane>();
            DA.SetDataList("Names", frames.Keys);

            // The named plane, else the first plane, else a plane along the chord
            string name = null;
            DA.GetData("Name", ref name);
            Plane plane;
            if (!string.IsNullOrEmpty(name))
            {
                if (!frames.TryGetValue(name, out plane))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"The beam has no plane named \"{name}\". Using a plane along its chord.");
                    plane = Chord(m_beam);
                }
            }
            else if (frames.Count > 0)
            {
                var first = System.Linq.Enumerable.First(frames);
                plane = first.Value;
                Message = first.Key;
            }
            else
                plane = Chord(m_beam);

            if (m_flip)
                plane = plane.FlipAroundYAxis();

            if (m_bounds)
            {
                var brep = m_beam.ToBrep();
                var bb = brep.GetBoundingBox(plane);
                plane.Origin = plane.PointAt(bb.Min.X, bb.Min.Y, bb.Min.Z);
            }

            DA.SetData("Plane", plane);
        }

        /// <summary>
        /// A plane at the beam's start: X along its chord, Y towards its bulge.
        /// </summary>
        static Plane Chord(Beam beam) =>
            GluLamb.Blanks.PressAlignment.Handle(beam.Centreline, beam.GetPlane(beam.Centreline.Domain.Min).YAxis);
    }
}
