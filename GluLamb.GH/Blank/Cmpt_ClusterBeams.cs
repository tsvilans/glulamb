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

using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

using GluLamb.Blanks;
using GluLamb.Joints;

namespace GluLamb.GH.Components
{
    public class Cmpt_ClusterBeams : GH_Component
    {
        public Cmpt_ClusterBeams()
          : base("Cluster Beams", "Cluster",
              "Group near-identical beams so they can share one blank. Beams are compared by rigid alignment " +
              "(moved, turned and reversed, never mirrored), including the roll of their sections. " +
              "Every two beams in a group are within the tolerance of each other.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.SegmentedBlank;
        public override Guid ComponentGuid => new Guid("3d8e6a41-9b27-4c5f-a0e3-6f1b2c7d9e54");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            double scale = RhinoMath.UnitScale(UnitSystem.Millimeters, RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Millimeters);

            pManager.AddGenericParameter("Beams", "B", "Beams to group, as a tree.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "T", "Largest deviation between the two most different beams of a group, after aligning them. Sections must also be within it.", GH_ParamAccess.item, scale * 5.0);
            pManager.AddNumberParameter("Length Tolerance", "LT", "How much shorter than its blank a beam may be. Within a group, the longest and shortest beams differ by at most this, " +
                "and the shorter ones are cut from the blank (their shape still within Tolerance). 0: only beams of the same length share a blank.", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Samples", "S", "Number of stations compared along each beam.", GH_ParamAccess.item, 24);
            pManager.AddNumberParameter("Extensions", "E", "Optional extensions {start, end} per beam path, e.g. from Construct Joints. The blank covers them.", GH_ParamAccess.tree);

            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Blanks", "B", "One blank per group, in the position of its reference beam (the longest member): " +
                "the reference's centreline extended to cover every member, with a constant section big enough for every member.", GH_ParamAccess.list);
            pManager.AddTextParameter("Members", "M", "The beam paths in each group, one branch per blank, reference first.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Blank Index", "I", "Index of the blank each beam is made from, in the structure of Beams.", GH_ParamAccess.tree);
            pManager.AddTransformParameter("Transforms", "X", "Transform from the blank to each beam, in the structure of Beams.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Deviation", "D", "Largest deviation of each beam from its blank's reference, in the structure of Beams.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Partial", "P", "Whether each beam is cut from a longer blank, in the structure of Beams.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Placed Blanks", "PB", "Each beam's blank, moved onto the beam, in the structure of Beams. " +
                "Beams that share a blank get copies with the same id.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> beamTree)) return;

            double tolerance = 5.0;
            double lengthTolerance = 0;
            int samples = 24;
            DA.GetData(1, ref tolerance);
            DA.GetData(2, ref lengthTolerance);
            DA.GetData(3, ref samples);
            DA.GetDataTree(4, out GH_Structure<GH_Number> extensionTree);

            // Flatten, remembering where each beam came from
            var beams = new List<Beam>();
            var extensions = new List<BeamExtension>();
            var names = new List<string>();
            // Every input item, with its index in beams (-1 if it isn't a beam)
            var slots = new List<(GH_Path Path, int Beam)>();

            foreach (var path in beamTree.Paths)
            {
                var branch = beamTree[path];
                var extension = extensionTree != null && extensionTree.PathExists(path)
                    ? extensionTree[path].Select(x => x?.Value ?? 0).ToList() : null;

                for (int i = 0; i < branch.Count; ++i)
                {
                    var beam = branch[i] is GH_Beam ghBeam ? ghBeam.Value : branch[i]?.ScriptVariable() as Beam;
                    slots.Add((path, beam == null ? -1 : beams.Count));
                    if (beam == null) continue;

                    beams.Add(beam);
                    extensions.Add(BeamClustering.ExtensionFrom(extension, i, branch.Count));
                    names.Add((branch.Count > 1 ? path.AppendElement(i) : path).ToString(false));
                }
            }

            BeamClusterResult result;
            try
            {
                result = BeamClustering.Cluster(beams, tolerance, lengthTolerance, samples, extensions);
            }
            catch (ArgumentException e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                return;
            }

            var membersOut = new DataTree<string>();
            for (int c = 0; c < result.Clusters.Count; ++c)
                membersOut.AddRange(result.Clusters[c].Members.Select(m => names[m]), new GH_Path(c));

            var indexOut = new DataTree<int>();
            var transformsOut = new DataTree<GH_Transform>();
            var deviationOut = new DataTree<double>();
            var partialOut = new DataTree<bool>();
            var placedOut = new DataTree<GH_Glulam>();

            // In the structure of Beams; items that aren't beams get -1 / null
            foreach (var (path, b) in slots)
            {
                indexOut.Add(b < 0 ? -1 : result.BlankIndex[b], path);
                transformsOut.Add(b < 0 ? null : new GH_Transform(result.Transforms[b]), path);
                deviationOut.Add(b < 0 ? 0 : result.Deviation[b], path);
                partialOut.Add(b >= 0 && result.Partial[b], path);
                placedOut.Add(b < 0 || result.PlacedBlanks[b] == null ? null : new GH_Glulam(result.PlacedBlanks[b]), path);
            }

            foreach (var warning in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            if (result.Skipped > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{result.Skipped} beams couldn't be sampled and have no blank (index -1).");
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{result.Alignments} alignments in {result.Elapsed.TotalMilliseconds:0} ms.");

            Message = result.ToString();

            DA.SetDataList(0, result.Clusters.Select(x => new GH_Glulam(x.Blank)));
            DA.SetDataTree(1, membersOut);
            DA.SetDataTree(2, indexOut);
            DA.SetDataTree(3, transformsOut);
            DA.SetDataTree(4, deviationOut);
            DA.SetDataTree(5, partialOut);
            DA.SetDataTree(6, placedOut);
        }
    }
}
