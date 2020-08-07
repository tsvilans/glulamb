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

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Special;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateGlulamDataFromWidthHeight : GH_Component
    {
        public Cmpt_CreateGlulamDataFromWidthHeight()
          : base("GlulamData (WH)", "GlulamData",
              "Create glulam data from width and height.",
              "GluLamb", "Create")
        {
        }


        GH_ValueList valueList = null;
        IGH_Param parameter = null;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Width", "W", "Cross-section width.", GH_ParamAccess.item, 80.0);
            pManager.AddNumberParameter("Height", "H", "Cross-section height.", GH_ParamAccess.item, 80.0);
            pManager.AddIntegerParameter("Alignment", "A", "Cross-section alignment as an integer value between 0 and 8.", GH_ParamAccess.item, 4);
            pManager.AddIntegerParameter("Interpolation", "Int", "Interpolation method to use between glulam frames.", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("KWidth", "kX", "Maximum curvature to use for lamellae width calculation.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("KHeight", "kY", "Maximum curvature to use for lamellae height calculation.", GH_ParamAccess.item, 0.0);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;

            parameter = pManager[2];

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("GlulamData", "GD", "GlulamData object.", GH_ParamAccess.item);
        }

        protected override void BeforeSolveInstance()
        {
            if (valueList == null)
            {
                if (parameter.Sources.Count == 0)
                {
                    valueList = new GH_ValueList();
                }
                else
                {
                    foreach (var source in parameter.Sources)
                    {
                        if (source is GH_ValueList) valueList = source as GH_ValueList;
                        return;
                    }
                }

                valueList.CreateAttributes();
                valueList.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 1);
                valueList.ListItems.Clear();

                var alignmentNames = Enum.GetNames(typeof(GlulamData.CrossSectionPosition));
                var alignmentValues = Enum.GetValues(typeof(GlulamData.CrossSectionPosition));

                for (int i = 0; i < alignmentNames.Length; ++i)
                {
                    valueList.ListItems.Add(new GH_ValueListItem(alignmentNames[i], $"{i}"));
                }

                valueList.SelectItem(4);

                Instances.ActiveCanvas.Document.AddObject(valueList, false);
                parameter.AddSource(valueList);
                parameter.CollectData();
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double k_mult = 200.0;

            double m_width = 80.0, m_height = 80, m_kwidth = 0.0, m_kheight = 0.0;

            int m_alignment = 4;
            int m_interpolation = 2;

            DA.GetData("Width", ref m_width);
            DA.GetData("Height", ref m_height);

            DA.GetData("Alignment", ref m_alignment);
            DA.GetData("Interpolation", ref m_interpolation);
            DA.GetData("KWidth", ref m_kwidth);
            DA.GetData("KHeight", ref m_kheight);

            if (m_width < 0) m_width = -m_width;
            if (m_height < 0) m_height = -m_height;

            if (m_width == 0.0) m_width = 80.0;
            if (m_height == 0.0) m_height = 80.0;

            double l_width, l_height;
            if (m_kwidth > 0)
                l_width = 1 / (k_mult * m_kwidth);
            else
                l_width = m_width;

            if (m_kheight > 0)
                l_height = 1 / (k_mult * m_kheight);
            else
                l_height = m_height;

            int n_width = Math.Max((int)Math.Ceiling(m_width / l_width), 1);
            int n_height = Math.Max((int)Math.Ceiling(m_height / l_height), 1);

            l_width = m_width / n_width;
            l_height = m_height / n_height;

            GlulamData data = new GlulamData(n_width, n_height, l_width, l_height, GlulamData.DefaultCurvatureSamples);
            data.InterpolationType = (GlulamData.Interpolation)m_interpolation;
            data.SectionAlignment = (GlulamData.CrossSectionPosition)m_alignment;

            DA.SetData("GlulamData", new GH_GlulamData(data));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.glulamb_GlulamData_24x24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("3769C2D5-D4E3-4DDA-85E1-7C3C9865930D"); }
        }
    }
}