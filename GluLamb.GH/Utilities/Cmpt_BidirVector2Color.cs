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
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Display;

using System.Windows.Forms;

namespace GluLamb.GH.Components
{
    public class Cmpt_BidirVector2Color : GH_Component
    {
        public Cmpt_BidirVector2Color()
          : base("Bidir Vector 2 Color", "BV2C",
              "Creates a color mapping for a bidirectional vector (same color for vector and its reverse).",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.VectorColor;
        public override Guid ComponentGuid => new Guid("4F4B5311-DD7C-4216-86FA-19ACFF6202C3");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        int method = 1;

        private void Menu_Method1Clicked(object sender, EventArgs e)
        {
            RecordUndoEvent("ChangeV2CMethod");
            method = 1;
            ExpireSolution(true);
        }

        private void Menu_Method2Clicked(object sender, EventArgs e)
        {
            RecordUndoEvent("ChangeV2CMethod");
            method = 2;
            ExpireSolution(true);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            ToolStripMenuItem method1_item = Menu_AppendItem(menu, "Method 1", Menu_Method1Clicked, true, method == 1);
            method1_item.ToolTipText = "Basic method for converting vector to color, using (0.5, 0.5, 0.5) as the vector origin.";

            ToolStripMenuItem method2_item = Menu_AppendItem(menu, "Method 2", Menu_Method2Clicked, true, method == 2);
            method2_item.ToolTipText = "Alternate method for converting vector to color.";

            base.AppendAdditionalComponentMenuItems(menu);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Vector", "V", "Bidirectional vector.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddColourParameter("Color", "C", "Colour vector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Vector3d m_vec = Vector3d.Unset;

            DA.GetData("Vector", ref m_vec);
            m_vec.Unitize();

            switch (method)
            {
                case (2):
                    DA.SetData("Color", Mapping.VectorToColor2(m_vec).AsSystemColor());
                    break;
                default:
                    DA.SetData("Color", Mapping.VectorToColor1(m_vec).AsSystemColor());
                    break;
            }
        }
    }
}