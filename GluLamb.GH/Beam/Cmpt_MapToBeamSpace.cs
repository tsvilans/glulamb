﻿/*
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
using Grasshopper.Kernel.Types;

using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_MapToBeamSpace : GH_Component
    {
        public Cmpt_MapToBeamSpace()
          : base("Map Geometry To Beam Space", "Map2Beam",
              "Maps geometry to free-form beam coordinate space.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamSoul;
        public override Guid ComponentGuid => new Guid("9A484B1E-9BCF-47F7-843C-29D27B6FE932");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Beam to map to.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Geometry", "Geo", "Geometry to map.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Geometry", "Geo", "Mapped geometry.", GH_ParamAccess.list);
        }

        protected void MapObjectToBeam(List<object> input, Beam beam, IGH_DataAccess DA)
        {
            if (input == null || input.Count < 1)
            {
                return;
            }
            if (input.Count == 1)
            {
                object single = input[0];
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, single.ToString());

                if (single is Point3d)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace((Point3d)single) });
                else if (single is GH_Point)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace((single as GH_Point).Value) });
                else if (single is Plane)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace((Plane)single) });
                else if (single is GH_Plane)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace((single as GH_Plane).Value) });
                if (single is GH_Mesh)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace((single as GH_Mesh).Value) });
                if (single is Mesh)
                    DA.SetDataList("Geometry", new object[] { beam.ToBeamSpace(single as Mesh) });

                return;
            }

            if (input.First() is GH_Plane)
                DA.SetDataList("Geometry", beam.ToBeamSpace(input.Select(x => (x as GH_Plane).Value).ToList()));
            else if (input.First() is Plane)
                DA.SetDataList("Geometry", beam.ToBeamSpace(input.Select(x => (Plane)x).ToList()));
            else if (input.First() is GH_Point)
                DA.SetDataList("Geometry", beam.ToBeamSpace(input.Select(x => (x as GH_Point).Value).ToList()));
            else if (input.First() is Point3d)
                DA.SetDataList("Geometry", beam.ToBeamSpace(input.Select(x => (Point3d)x).ToList()));
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;

            if (!DA.GetData<Beam>("Beam", ref beam))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No beam connected.");
                return;
            }

            List<object> m_objects = new List<object>();

            if (!DA.GetDataList("Geometry", m_objects))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid geometry specified.");
                return;
            }

            MapObjectToBeam(m_objects, beam, DA);
        }
    }
}