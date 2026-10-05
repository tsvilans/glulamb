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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Grasshopper;

using GluLamb.Joints;

namespace GluLamb.GH.Components
{
    /// <summary>
    /// Construct joints with the IJoint system: joint types come from JointRegistry,
    /// output is features per beam.
    /// </summary>
    public class Cmpt_ConstructJoints : GH_Component
    {
        public Cmpt_ConstructJoints()
          : base("Construct joints", "ConJ",
              "Construct joints from joint conditions using the joint registry. " +
              "Beams are matched to joint parts by beam id, or by the first path index of the Beams tree.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.Joint;
        public override Guid ComponentGuid => new Guid("6f0b8d1e-3c55-4b8a-9a5e-2f7e4d1c9b31");
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beams, one per branch. The first path index is the beam index used by the joint conditions.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Joints", "J", "Joint conditions (e.g. from Classify Joints), one per branch.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Types", "T", "Optional joint type id per joint branch. If empty, the best-scoring registered type is used.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "t", "Modelling tolerance.", GH_ParamAccess.item, 1e-3);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Joints", "J", "Constructed joints.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Features", "F", "Features per beam.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Cutters", "C", "Cutting geometry per beam.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Messages", "M", "Status and messages per joint.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Registry", "R", "Registered joint types, and any registry errors.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> beamTree)) return;
            if (!DA.GetDataTree(1, out GH_Structure<IGH_Goo> jointTree)) return;
            DA.GetDataTree(2, out GH_Structure<GH_String> typeTree);
            double tolerance = 1e-3;
            DA.GetData(3, ref tolerance);

            var registry = JointRegistry.Default;

            // Beams by first path index, so ElementIndex in the conditions resolves correctly
            var beamsByIndex = new SortedDictionary<int, Beam>();
            foreach (var path in beamTree.Paths)
            {
                var branch = beamTree[path];
                if (branch.Count < 1 || !(branch[0] is GH_Beam ghBeam) || ghBeam.Value == null) continue;
                beamsByIndex[path.Indices[0]] = ghBeam.Value;
            }

            var beamList = new List<Beam>();
            if (beamsByIndex.Count > 0)
            {
                beamList.AddRange(new Beam[beamsByIndex.Keys.Max() + 1]);
                foreach (var kvp in beamsByIndex)
                    beamList[kvp.Key] = kvp.Value;
            }

            BeamCollection context;
            try
            {
                context = new BeamCollection(beamList, tolerance);
            }
            catch (ArgumentException e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                return;
            }

            var indexById = beamsByIndex.ToDictionary(x => x.Value.Id, x => x.Key);

            var jointsOut = new DataTree<GH_ObjectWrapper>();
            var featuresOut = new DataTree<GH_ObjectWrapper>();
            var cuttersOut = new DataTree<GH_Brep>();
            var messagesOut = new DataTree<string>();

            foreach (var path in jointTree.Paths)
            {
                var branch = jointTree[path];
                if (branch.Count < 1 || !(branch[0] is GH_Joint ghJoint) || ghJoint.Value == null) continue;

                var condition = ghJoint.Value;

                string typeId = null;
                if (typeTree != null && typeTree.PathExists(path) && typeTree[path].Count > 0)
                    typeId = typeTree[path][0]?.Value;

                IJoint joint;
                try
                {
                    joint = string.IsNullOrWhiteSpace(typeId)
                        ? registry.Allocate(condition, context)
                        : registry.Create(typeId, condition);
                }
                catch (Exception e)
                {
                    messagesOut.Add($"Failed: {e.Message}", path);
                    continue;
                }

                if (joint == null)
                {
                    messagesOut.Add($"Skipped: no registered joint type handles this {JointRegistry.Classify(condition, registry.PerpendicularThreshold)} condition.", path);
                    continue;
                }

                var result = joint.Construct(context);

                jointsOut.Add(new GH_ObjectWrapper(joint), path);
                messagesOut.Add($"{result.Status}: {joint}", path);
                foreach (var message in result.Messages)
                    messagesOut.Add(message, path);

                foreach (var kvp in result.Features)
                {
                    if (!indexById.TryGetValue(kvp.Key, out int beamIndex)) continue;
                    var beam = context.GetBeam(kvp.Key);
                    var beamPath = new GH_Path(beamIndex);

                    foreach (var feature in kvp.Value)
                    {
                        featuresOut.Add(new GH_ObjectWrapper(feature), beamPath);
                        foreach (var cutter in feature.GetCutters(beam, tolerance))
                            cuttersOut.Add(new GH_Brep(cutter), beamPath);
                    }
                }
            }

            var registryInfo = registry.Types.Select(x => $"{x.Id}: {x.Name} ({x.Attribute.Topology}, {x.Attribute.Arity} parts)")
                .Concat(registry.Errors.Select(x => $"Error: {x}"))
                .ToList();

            DA.SetDataTree(0, jointsOut);
            DA.SetDataTree(1, featuresOut);
            DA.SetDataTree(2, cuttersOut);
            DA.SetDataTree(3, messagesOut);
            DA.SetDataList(4, registryInfo);
        }
    }
}
