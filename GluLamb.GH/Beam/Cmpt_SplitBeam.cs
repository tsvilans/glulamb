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

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace GluLamb.GH.Components
{
    public class Cmpt_SplitBeam : GH_Component
    {
        public Cmpt_SplitBeam()
          : base("Split Beam", "SplitB",
              "Splits Beam with optional overlap.",
              "GluLamb", UiNames.BeamSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.BeamSplit;
        public override Guid ComponentGuid => new Guid("5B41EAAE-0E01-466C-9E6B-B5F73EDB1EF9");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "Input beam to split.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Parameter", "T", "Point on beam at which to split.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Overlap", "O", "Amount of overlap at split point.", GH_ParamAccess.item, 0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beam pieces.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            // Get Glulam
            Beam m_beam = null;
            DA.GetData<Beam>("Beam", ref m_beam);

            if (m_beam == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid beam input.");
                return;
            }

            List<double> m_params = new List<double>();
            DA.GetDataList("Parameter", m_params);
            if (m_params.Count < 1) return;

            double m_overlap = 0;

            DA.GetData("Overlap", ref m_overlap);

            m_params.Sort();

            List<Beam> m_beams = new List<Beam>();

            //m_glulams = g.Split(m_params.ToArray(), m_overlap);

            List<Interval> domains = new List<Interval>();
            double dmin = m_beam.Centreline.Domain.Min;

            domains.Add(new Interval(dmin, m_params.First()));
            for (int i = 0; i < m_params.Count - 1; ++i)
            {
                domains.Add(new Interval(m_params[i], m_params[i + 1]));
            }
            domains.Add(new Interval(m_params.Last(), m_beam.Centreline.Domain.Max));

            //domains = domains.Where(x => m_glulam.Centreline.GetLength(x) > m_overlap).ToList();

            for (int i = 0; i < domains.Count; ++i)
            {
                Beam temp = m_beam.Trim(domains[i], m_overlap);
                if (temp == null)
                    continue;

                m_beams.Add(temp);
            }

            DA.SetDataList("Beams", m_beams.Select(x => new GH_Beam(x)));
        }
    }
}