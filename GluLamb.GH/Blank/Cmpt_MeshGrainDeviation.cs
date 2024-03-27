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
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_MeshGrainDeviation : GH_Component
    {
        public Cmpt_MeshGrainDeviation()
          : base("Mesh Grain Deviation", "GrDev",
              "Calculates the normal deviation from the longitudinal direction of a glulam blank per vertex or mesh face. Can use acos to get the actual angle.",
              "GluLamb", UiNames.BlankSection)
        {
        }


        protected override System.Drawing.Bitmap Icon => Properties.Resources.FibreCuttingAnalysis;
        public override Guid ComponentGuid => new Guid("5D559BF0-92A4-47E1-A8C8-FECD15D9D861");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam blank.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Mesh to check for grain deviation.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Faces", "F", "Use faces instead of vertices.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Deviations", "D", "Deviation between 0-1 for each mesh vertex or mesh face.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object m_obj = null;
            Mesh m_mesh = null;
            bool m_faces = false;

            DA.GetData("Mesh", ref m_mesh);
            DA.GetData("Glulam", ref m_obj);
            DA.GetData("Faces", ref m_faces);

            Curve m_curve = null;

            while (true)
            {
                GH_Glulam m_ghglulam = m_obj as GH_Glulam;
                if (m_ghglulam != null)
                {
                    m_curve = m_ghglulam.Value.Centreline;
                    break;
                }

                GH_Curve m_ghcurve = m_obj as GH_Curve;
                if (m_ghcurve != null)
                {
                    m_curve = m_ghcurve.Value;
                    break;
                }

                Glulam m_glulam = m_obj as Glulam;
                if (m_glulam != null)
                {
                    m_curve = m_glulam.Centreline;
                    break;
                }

                m_curve = m_obj as Curve;
                if (m_curve != null)
                {
                    break;
                }
                throw new Exception("Input must be either Glulam or Curve!");
            }

            List<double> deviations = m_mesh.CalculateTangentDeviation(m_curve, m_faces);

            DA.SetDataList("Deviations", deviations);
        }
    }
}