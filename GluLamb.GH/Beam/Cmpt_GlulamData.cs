#if OBSOLETE

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
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;

using Rhino.Geometry;
using GH_IO.Serialization;

namespace GluLamb.GH.Components
{
    public enum GlulamDataMethod
    {
        Section,
        Lamella
    }

    public class Cmpt_GlulamData : GH_Component, IGH_VariableParameterComponent
    {
        public Cmpt_GlulamData()
          : base("GlulamData", "GlulamData",
              "Create glulam data.",
              "GluLamb", "Create")
        {
            DataMethod = GlulamDataMethod.Section;

            foreach (var parameter in parameters)
            {
                if (Params.Input.Any(x => x.Name == parameter.Name))
                    Params.UnregisterInputParameter(Params.Input.First(x => x.Name == parameter.Name), true);
            }

            AddParam(4);
            AddParam(5);
            AddParam(6);
            AddParam(7);

            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        //GH_ValueList valueList = null;
        Guid LastValueList = Guid.Empty;
        IGH_Param alignment_parameter = null;

        public GlulamDataMethod DataMethod = GlulamDataMethod.Section;
        readonly IGH_Param[] parameters = new IGH_Param[8]
        {
            new Param_Number() {Name = "LamellaX", NickName = "Lx", Description = "Lamella dimension in the X-axis.", Optional = true, Access = GH_ParamAccess.item},
            new Param_Number(){Name = "LamellaY", NickName = "Ly", Description = "Lamella dimension in the Y-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Integer(){Name = "NumX", NickName = "Nx", Description = "Number of lamellae in X-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Integer(){Name = "NumY", NickName = "Ny", Description = "Number of lamellae in Y-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Number() {Name = "Width (X)", NickName = "X", Description = "Section dimension in the X-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Number(){Name = "Height (Y)", NickName = "Y", Description = "Section dimension in the Y-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Number() {Name = "Curvature (X)", NickName = "Kx", Description = "Maximum curvature in the X-axis.", Optional = true, Access = GH_ParamAccess.item },
            new Param_Number(){Name = "Curvature (Y)", NickName = "Ky", Description = "Maximum curvature in the Y-axis.", Optional = true, Access = GH_ParamAccess.item },
        };

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index) => false;
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index) => false;
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index) => null;
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index) => false;
        void IGH_VariableParameterComponent.VariableParameterMaintenance() { }

        private void AddParam(int index)
        {
            int insertIndex = Params.Input.Count;
            for (int i = 0; i < Params.Input.Count; i++)
            {
                int otherIndex = Array.FindIndex(parameters, x => x.Name == Params.Input[i].Name);
                if (otherIndex > index)
                {
                    insertIndex = i;
                    break;
                }
            }
            Params.RegisterInputParam(parameters[index], insertIndex);
        }

        private void SetMethodLamella(object sender, EventArgs e)
        {
            DataMethod = GlulamDataMethod.Lamella;

            foreach (var parameter in parameters)
            {
                if (Params.Input.Any(x => x.Name == parameter.Name))
                    Params.UnregisterInputParameter(Params.Input.First(x => x.Name == parameter.Name), true);
            }

            AddParam(0);
            AddParam(1);
            AddParam(2);
            AddParam(3);

            Params.OnParametersChanged();
            ExpireSolution(true);
        }
        private void SetMethodSection(object sender, EventArgs e)
        {
            DataMethod = GlulamDataMethod.Section;

            foreach (var parameter in parameters)
            {
                if (Params.Input.Any(x => x.Name == parameter.Name))
                    Params.UnregisterInputParameter(Params.Input.First(x => x.Name == parameter.Name), true);
            }

            AddParam(4);
            AddParam(5);
            AddParam(6);
            AddParam(7);

            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Lamella", SetMethodLamella, true, DataMethod == GlulamDataMethod.Lamella);
            Menu_AppendItem(menu, "Section", SetMethodSection, true, DataMethod == GlulamDataMethod.Section);
            base.AppendAdditionalComponentMenuItems(menu);
        }

        protected override void BeforeSolveInstance()
        {
            if (alignment_parameter.SourceCount > 0)
            {
                var last = alignment_parameter.Sources.Last();
                if (last.InstanceGuid != LastValueList && last is GH_ValueList)
                {
                    var valueList = last as GH_ValueList;
                    valueList.ListItems.Clear();

                    var alignmentNames = Enum.GetNames(typeof(GlulamData.CrossSectionPosition));
                    //var alignmentValues = Enum.GetValues(typeof(GlulamData.CrossSectionPosition));

                    for (int i = 0; i < alignmentNames.Length; ++i)
                    {
                        valueList.ListItems.Add(new GH_ValueListItem(alignmentNames[i], $"{i}"));
                    }

                    valueList.SelectItem(4);
                    LastValueList = last.InstanceGuid;
                }

                alignment_parameter.CollectData();
            }

            /*

        }
        foreach (var source in alignment_parameter.Sources)
        {
            if (source is GH_ValueList) valueList = source as GH_ValueList;
            valueList.ListItems.Clear();

            for (int i = 0; i < alignmentNames.Length; ++i)
            {
                valueList.ListItems.Add(new GH_ValueListItem(alignmentNames[i], $"{i}"));
            }

            valueList.SelectItem(4);
            //return;
        }
    }
    if (valueList == null)
    {
        if (alignment_parameter.Sources.Count == 0)
        {
            valueList = new GH_ValueList();

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
            alignment_parameter.AddSource(valueList);

        }
        else
        {
            foreach (var source in alignment_parameter.Sources)
            {
                if (source is GH_ValueList) valueList = source as GH_ValueList;
                //return;
            }
        }

        alignment_parameter.CollectData();
    }*/
        }

        private void AlignmentParameter_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            if (sender is GH_ValueList)
            {
                var valueList = sender as GH_ValueList;
                valueList.ListItems.Clear();

                var alignmentNames = Enum.GetNames(typeof(GlulamData.CrossSectionPosition));

                for (int i = 0; i < alignmentNames.Length; ++i)
                {
                    valueList.ListItems.Add(new GH_ValueListItem(alignmentNames[i], $"{i}"));
                }

                valueList.SelectItem(4);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //pManager.AddIntegerParameter("Interpolation", "Int", "Interpolation method to use between glulam frames.", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("Samples", "S", "Resolution of length subdivision.", GH_ParamAccess.item, 100);
            pManager.AddIntegerParameter("Alignment", "A", "Cross-section alignment as an integer value between 0 and 8.", GH_ParamAccess.item, 4);

            alignment_parameter = pManager[1];

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("GlulamData", "GD", "GlulamData object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int samples = 0, m_alignment = 4;
            //int interpolation = 2;

            DA.GetData("Samples", ref samples);
            DA.GetData("Alignment", ref m_alignment);

            //DA.GetData("Interpolation", ref interpolation);
            GlulamData data = null;

            if (DataMethod == GlulamDataMethod.Lamella)
            {
                double lamella_x = 20, lamella_y = 20;
                int num_x = 4, num_y = 4;

                DA.GetData("LamellaX", ref lamella_x);
                DA.GetData("LamellaY", ref lamella_y);
                DA.GetData("NumX", ref num_x);
                DA.GetData("NumY", ref num_y);

                data = new GlulamData(Math.Max(num_x, 1), Math.Max(num_y, 1), lamella_x, lamella_y, samples);
                data.SectionAlignment = (GlulamData.CrossSectionPosition)m_alignment;

                //data.InterpolationType = (GlulamData.Interpolation)interpolation;
            }
            else if (DataMethod == GlulamDataMethod.Section)
            {
                double m_width = 80.0, m_height = 80.0;
                double kx = 0, ky = 0;
                DA.GetData("Width (X)", ref m_width);
                DA.GetData("Height (Y)", ref m_height);

                DA.GetData("Curvature (X)", ref kx);
                DA.GetData("Curvature (Y)", ref ky);

                if (m_width < 0) m_width = -m_width;
                if (m_height < 0) m_height = -m_height;

                if (m_width == 0.0) m_width = 80.0;
                if (m_height == 0.0) m_height = 80.0;

                double l_width, l_height;
                if (kx > 0)
                {
                    l_width = GluLamb.Standards.NoStandard.Instance.CalculateLaminationThickness(kx);
                    // l_width = 1 / (Glulam.RadiusMultiplier * kx);
                }
                else
                    l_width = m_width;

                if (ky > 0)
                {
                    l_height = GluLamb.Standards.NoStandard.Instance.CalculateLaminationThickness(ky);
                    // l_height = 1 / (Glulam.RadiusMultiplier * ky);
                }
                else
                    l_height = m_height;

                int n_width = Math.Max((int)Math.Ceiling(m_width / l_width), 1);
                int n_height = Math.Max((int)Math.Ceiling(m_height / l_height), 1);

                l_width = m_width / n_width;
                l_height = m_height / n_height;

                data = new GlulamData(n_width, n_height, l_width, l_height, GlulamData.DefaultCurvatureSamples);
                data.SectionAlignment = (GlulamData.CrossSectionPosition)m_alignment;
                //data.InterpolationType = (GlulamData.Interpolation)interpolation;
            }

            DA.SetData("GlulamData", new GH_GlulamData(data));
        }

        public override bool Read(GH_IReader reader)
        {
            int data_method = 0;
            reader.TryGetInt32("DataMethod", ref data_method);
            DataMethod = (GlulamDataMethod)data_method;

            return base.Read(reader);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("DataMethod", (int)DataMethod);
            return base.Write(writer);
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
            get { return new Guid("22DF602E-4E4E-4141-94BC-4378BCA839ED"); }
        }
    }
}

#endif