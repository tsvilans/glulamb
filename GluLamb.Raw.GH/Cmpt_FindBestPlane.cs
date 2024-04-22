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

namespace GluLamb.Raw.GH.Components
{
    public class Cmpt_FindBestPlane : GH_Component
    {
        public Cmpt_FindBestPlane()
          : base("Find Plane", "Find",
              "Find the principal directions of a set of mesh vertices.",
              "GluLamb", UiNames.RawSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.FindPlane;
        public override Guid ComponentGuid => new Guid("c30d2e10-1dd7-4d55-aa49-b574e6bdeaea");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        private bool CentrePlane = true;

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Centre plane", ToggleCentrePlane, true, CentrePlane);

        }

        private void ToggleCentrePlane(object? sender, EventArgs e)
        {
            CentrePlane = !CentrePlane;
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh for which to find principal directions."+
                "A good mesh has an even distribution of vertices.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Plane aligned with the principal directions of the object.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Eigenvalues", "EV", "Eigenvalues of the mesh.", GH_ParamAccess.list);
            pManager.AddVectorParameter("Eigenvectors", "EV", "Eigenvectors of the mesh.", GH_ParamAccess.list);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData("Mesh", ref mesh))
            {
                return;
            }
            
            var pts = mesh.Vertices.ToPoint3dArray();
            int N = pts.Length;

            double[] X = new double[N], Y = new double[N], Z = new double[N];

            Point3d mean = Point3d.Origin;
            for (int i = 0; i < N; ++i)
            {
                X[i] = pts[i].X;
                Y[i] = pts[i].Y;
                Z[i] = pts[i].Z;

                mean += pts[i];
            }

            mean /= pts.Length;

            double[,] evec;
            double[] ev;

            GluLamb.Raw.Utility.GetEigenVectors(X, Y, Z, out ev, out evec);

            var vecs = new Vector3d[3];
            for (int i = 0; i < 3; ++i)
            {
                vecs[i] = new Vector3d(
                    evec[0, i] * ev[i],
                    evec[1, i] * ev[i],
                    evec[2, i] * ev[i]
                );
            }

            var plane = new Plane(mean, vecs[2], vecs[1]);

            if (!CentrePlane)
            {
                var bb = mesh.GetBoundingBox(plane);
                var min = plane.PointAt(bb.Min.X, bb.Min.Y, bb.Min.Z);

                plane.Origin = min;
            }

            DA.SetData("Plane", plane);
            DA.SetDataList("Eigenvalues", ev);
            DA.SetDataList("Eigenvectors", vecs);
        }
    }
}