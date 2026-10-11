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

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

using GluLamb.Btlx;

namespace GluLamb.GH.Components
{
    /// <summary>
    /// Writes beams to a BTLx file as parts of a project. Parts only for now: size, position and
    /// reference side; processings from joint features come later.
    /// </summary>
    public class Cmpt_ExportBtlx : GH_Component
    {
        public Cmpt_ExportBtlx()
          : base("Export BTLx", "BTLx",
              "Write beams to a BTLx file as the parts of a project (parts only for now, no processings).",
              "GluLamb", UiNames.UtilitiesSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.JointInfo;
        public override Guid ComponentGuid => new Guid("c0e4f0a8-6a39-4b1f-9d0c-2f7d4b8e5a13");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beams, one per branch, as for Construct Joints (e.g. its extended beams). Each becomes a part named by its path; " +
                "the first path index is its single member number.", GH_ParamAccess.tree);
            pManager.AddTextParameter("File", "F", "BTLx file to write.", GH_ParamAccess.item);
            pManager.AddTextParameter("Project", "P", "Project name.", GH_ParamAccess.item, "GluLamb project");
            pManager.AddTextParameter("Version", "V", "BTLx version: 2.2.0 or 2.3.0.", GH_ParamAccess.item, BtlxWriter.DefaultVersion);
            pManager.AddBooleanParameter("Write", "W", "Write the file.", GH_ParamAccess.item, false);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Xml", "X", "The BTLx document.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Part frames", "F", "Each part's coordinate system (reference corner, X along the part, Y along its width), by beam path.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Messages", "M", "Anything the file can't represent exactly.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> beamTree)) return;
            string file = null, name = "GluLamb project", version = BtlxWriter.DefaultVersion;
            bool write = false;
            DA.GetData(1, ref file);
            DA.GetData(2, ref name);
            DA.GetData(3, ref version);
            DA.GetData(4, ref write);

            if (version != "2.2.0" && version != "2.3.0")
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"BTLx version {version} isn't one this was written for (2.2.0, 2.3.0).");

            var parts = new List<BtlxPart>();
            var frames = new Grasshopper.DataTree<Plane>();
            foreach (var path in beamTree.Paths)
            {
                var branch = beamTree[path];
                if (branch.Count < 1 || !(branch[0] is GH_Beam ghBeam) || ghBeam.Value == null) continue;

                parts.Add(new BtlxPart
                {
                    Beam = ghBeam.Value,
                    SingleMemberNumber = path.Indices[0],
                    Designation = path.ToString(false),
                });
                frames.Add(BtlxWriter.PartFrame(ghBeam.Value, out _, out _, out _), path);
            }

            var messages = new List<string>();
            var document = BtlxWriter.Write(new BtlxProject { Name = name }, parts, messages, version);

            if (write)
            {
                if (string.IsNullOrWhiteSpace(file))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No file given.");
                else
                {
                    try
                    {
                        document.Save(file);
                        messages.Insert(0, $"Wrote {parts.Count} parts to {file}.");
                    }
                    catch (Exception e)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Couldn't write {file}: {e.Message}");
                    }
                }
            }

            DA.SetData(0, document.ToString());
            DA.SetDataTree(1, frames);
            DA.SetDataList(2, messages);
        }
    }
}
