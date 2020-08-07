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
    public class Cmpt_MeshGrainDirection : GH_Component
    {
        public Cmpt_MeshGrainDirection()
          : base("Mesh Grain Direction", "MGDir",
              "Calculates the normal deviation from the longitudinal direction of a glulam blank per vertex or mesh face. Can use acos to get the actual angle.",
              "GluLamb", "Analyze")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam blank.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Mesh to check for grain direction.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Faces", "F", "Use faces instead of vertices.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("Vectors", "V", "Direction vector for each mesh vertex or mesh face.", GH_ParamAccess.list);
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

            List<Vector3d> deviations = m_mesh.CalculateTangentVector(m_curve, m_faces);

            DA.SetDataList("Vectors", deviations);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_FibreDirection_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("B304CC44-D967-4CFF-904F-0EFD80B3800A"); }
        }
    }
}