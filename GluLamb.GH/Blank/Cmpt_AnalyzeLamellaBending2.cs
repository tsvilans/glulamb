using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_AnalyzeLamellaBending2 : GH_Component
    {
        public Cmpt_AnalyzeLamellaBending2()
          : base("Analyze Lamella Bending", "LamK",
              "Compares the ratio between the glulam curvature and lamella size to a specified tolerance (i.e. Eurocode 5 1/200 ratio between "
                + "lamella thickness and radius of curvature).",
              "GluLamb", UiNames.BlankSection)
        {
        }


        protected override System.Drawing.Bitmap Icon => Properties.Resources.glulamb_CurvatureAnalysis_24x24;
        public override Guid ComponentGuid => new Guid("62BBB1D6-F714-4462-A21C-5305114BE561");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Glulam", "G", "Glulam blank.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Ratio", "R", "Override for the ratio (1 / input value). Default is 200 according to " +
                "Eurocode 5 specifications (lamella thickness:radius of curvature = 1:200).", GH_ParamAccess.item, 200.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Glulam mesh.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Factor", "F", "Resultant factor at each Glulam mesh vertex. Values over 1.0 exceed the allowed ratio between lamella thickness "+
                "and curvature.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Glulam m_glulam = null;

            if (!DA.GetData("Glulam", ref m_glulam))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No glulam input.");
                return;
            }

            Mesh m = m_glulam.ToMesh();

            double m_ratio = 200.0;
            DA.GetData("Ratio", ref m_ratio);

            if (m_ratio <= 0.0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Ratio values must be greater than 0.");
                return;
            }

            double min_rx = m_glulam.Data.LamWidth * m_ratio;
            double min_ry = m_glulam.Data.LamHeight * m_ratio;

            double max_kx = 1 / min_rx;
            double max_ky = 1 / min_ry;

            List<double> f_values = new List<double>();
            double t;

            for (int i = 0; i < m.Vertices.Count; ++i)
            {
                m_glulam.Centreline.ClosestPoint(m.Vertices[i], out t);
                Plane frame = m_glulam.GetPlane(t);

                Vector3d offset = m.Vertices[i] - frame.Origin;

                double offset_x = offset * frame.XAxis;
                double offset_y = offset * frame.YAxis;

                Vector3d kv = m_glulam.Centreline.CurvatureAt(t);
                double k = kv.Length;

                kv.Unitize();

                double r = (1 / k) - offset * kv;

                k = 1 / r;

                double kx = k * (kv * frame.XAxis);
                double ky = k * (kv * frame.YAxis);

                f_values.Add(Math.Max(Math.Abs(kx / max_kx), Math.Abs(ky / max_ky)));
            }

            DA.SetData("Mesh", m);
            DA.SetDataList("Factor", f_values);
        }
    }
}