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
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

using GluLamb.Cix;
using GluLamb.Cix.Operations;
using Cix.GH.Properties;

namespace Cix.GH.Components
{
    public class Cmpt_LoadCix : GH_Component
    {
        public Cmpt_LoadCix()
          : base("Load CIX", "CIX",
              "Load and visualize CIX variables for timber fabrication with Winther AS.",
              "GluLamb", "7. Cix")
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Cix;
        public override Guid ComponentGuid => new Guid("52AF0824-BAF9-4A0B-BF8E-1C6B722BFFB5");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override bool IsPreviewCapable => true;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Filepath", "F", "Path to .cix file.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string cixPath = string.Empty;
            DA.GetData("Filepath", ref cixPath);

            Cix = null;
                
            if (!System.IO.Path.Exists(cixPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"File '{cixPath}' does not exist.");
                return;
            }

            Message = System.IO.Path.GetFileNameWithoutExtension(cixPath);

            Cix = new Visualizer(cixPath);
        }

        private Visualizer Cix = null;

        public override BoundingBox ClippingBox => Cix == null? BoundingBox.Empty : Cix.Bounds;

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (Cix != null)
                Cix.DrawViewportWires(args.Display);
        }
    }
}