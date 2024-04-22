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
using System.Threading.Tasks;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using System.Linq;

using GluLamb.Raw;
using System.Runtime.Versioning;

namespace GluLamb.Raw.GH.Components
{
    /// <summary>
    /// Uses the dual contouring method implemented in Blender, originally
    /// by Tao Ju.
    /// Ju, T., Losasso, F., Schaefer, S., Warre, J. - 2002 - Dual Contouring of Hermite Data
    /// https://www.cse.wustl.edu/~taoju/research/dualContour.pdf
    /// </summary>
    public class Cmpt_DualCon : GH_Component
    {
        public Cmpt_DualCon()
          : base("Remesh", "Rmsh",
              "Remesh a mesh using the dual-contouring method by Tao Ju.",
              "GluLamb", UiNames.RawSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamMesh;
        public override Guid ComponentGuid => new Guid("66dd5889-f66e-4a8c-b886-fcb9680f8e0c");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Target mesh to remesh.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Depth", "D", "Levels to remesh.", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Threshold", "T", "Threshold.", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Remeshed mesh.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData("Mesh", ref mesh))
            {
                return;
            }

            mesh.Faces.ConvertQuadsToTriangles();
            var verts = mesh.Vertices.ToFloatArray();
            var tris = mesh.Faces.ToIntArray(true);

            int depth = 2;
            DA.GetData("Depth", ref depth);

            double threshold = 1.0;
            DA.GetData("Threshold", ref threshold);

            int Flags = 0, Mode = 2, Depth = 2;
            float Threshold = 1.0f, HermiteNumber = 1.0f, Scale = 0.9f;

            var dc = new DualCon();
            dc.Depth = Math.Max(1, depth);
            dc.Threshold = (float)threshold;

            dc.Remesh(verts, tris);

            var remeshed = new Mesh();
            for (int i = 0; i < dc.Output.Vertices.Length; i++)
            {
                if (dc.Output.Vertices[i] != null)
                    remeshed.Vertices.Add(dc.Output.Vertices[i][0], dc.Output.Vertices[i][1], dc.Output.Vertices[i][2]);
            }

            for (int i = 0; i < dc.Output.Quads.Length; ++i)
            {
                if (dc.Output.Quads[i] != null)
                    remeshed.Faces.AddFace(dc.Output.Quads[i][0], dc.Output.Quads[i][1], dc.Output.Quads[i][2], dc.Output.Quads[i][3]);
            }

            if (remeshed.IsValid)
            {
                DA.SetData("Mesh", remeshed);
            }

        }
    }
}