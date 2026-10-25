/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
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

using Grasshopper.Kernel;
using GH_IO.Serialization;
using Rhino.Geometry;

using GluLamb.Numerics;

namespace GluLamb.Reconstruction.GH
{
    /// <summary>
    /// Principal axes and the oriented bounding box along them. From GluLamb.Raw.GH; same GUID, so
    /// existing definitions keep it.
    /// </summary>
    public class Cmpt_FindPlane : GH_Component
    {
        public Cmpt_FindPlane()
          : base("Find Plane", "Find",
              "Find the principal directions of a mesh (or anything that converts to one, e.g. a Brep), and the oriented " +
              "bounding box along them.",
              "GluLamb", UiNames.ReconstructionSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Icons.Load("FindPlane.png");
        public override Guid ComponentGuid => new Guid("c30d2e10-1dd7-4d55-aa49-b574e6bdeaea");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        bool m_centrePlane = true;
        bool m_weighted = true;

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Centre plane", (s, e) => { m_centrePlane = !m_centrePlane; ExpireSolution(true); }, true, m_centrePlane);
            Menu_AppendItem(menu, "Weight by area", (s, e) => { m_weighted = !m_weighted; ExpireSolution(true); }, true, m_weighted);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("CentrePlane", m_centrePlane);
            writer.SetBoolean("WeightByArea", m_weighted);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("CentrePlane")) m_centrePlane = reader.GetBoolean("CentrePlane");
            if (reader.ItemExists("WeightByArea")) m_weighted = reader.GetBoolean("WeightByArea");
            return base.Read(reader);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh for which to find principal directions. With Weight by area (on by default) " +
                "each vertex counts by its share of the surface, so uneven meshes work too.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Plane along the principal directions: X the largest spread, Y the next. " +
                "At the centre of the box, or at its minimum corner with Centre plane off.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Eigenvalues", "EV", "Variance along X, Y and Z.", GH_ParamAccess.list);
            pManager.AddVectorParameter("Eigenvectors", "EV", "Unit principal directions X, Y and Z.", GH_ParamAccess.list);
            pManager.AddBoxParameter("Box", "B", "Oriented bounding box along the principal directions.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh) || mesh == null || mesh.Vertices.Count < 3) return;

            var bounds = m_weighted
                ? Pca.WeightedMesh(mesh, bendAxis: false)
                : Pca.Points(mesh.Vertices.ToPoint3dArray(), bendAxis: false);

            var box = bounds.ToBox();
            var plane = bounds.Plane;
            if (m_centrePlane) plane.Origin = box.Center;

            DA.SetData(0, plane);
            DA.SetDataList(1, new[] { bounds.VarianceX, bounds.VarianceY, bounds.VarianceZ });
            DA.SetDataList(2, new[] { plane.XAxis, plane.YAxis, plane.ZAxis });
            DA.SetData(3, box);
        }
    }
}
