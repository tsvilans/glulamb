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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

using GluLamb.Joints;

namespace GluLamb.GH.Components
{
    /// <summary>
    /// Single-beam joint conditions at arbitrary points, for joints placed along a beam (a
    /// drilling, a plate pocket) rather than found from the network. Each point goes to the
    /// nearest beam; the point itself is kept as the joint position, so a joint can use it to
    /// pick a face or a spot on a face.
    /// </summary>
    public class Cmpt_PointJoints : GH_Component
    {
        public Cmpt_PointJoints()
          : base("Point joints", "PointJ",
              "Single-beam joint conditions at points along beams, for Construct Joints.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.ClassifyJoints;
        public override Guid ComponentGuid => new Guid("5b0f6f3e-2f7c-4c1d-9a51-7d3f0b8e2c41");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beams, one per branch, as for Construct Joints. The first path index is the beam index.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Points", "P", "Points where joints go. Each goes to the nearest beam.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Distance", "D", "Largest distance from a point to a beam's centreline. 0 = any distance.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("End tolerance", "ET", "Distance within which a point counts as at the end of a beam.", GH_ParamAccess.item, 10);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Joints", "J", "Single-beam joint conditions, one per point (null where no beam was found).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Beam index", "I", "Index of the beam each point went to (-1 if none).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Parameter", "t", "Parameter on the beam's centreline.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> beamTree)) return;
            var points = new List<Point3d>();
            if (!DA.GetDataList(1, points)) return;
            double maxDistance = 0, endTolerance = 10;
            DA.GetData(2, ref maxDistance);
            DA.GetData(3, ref endTolerance);

            var beams = new List<(int Index, Beam Beam)>();
            foreach (var path in beamTree.Paths)
            {
                var branch = beamTree[path];
                if (branch.Count < 1 || !(branch[0] is GH_Beam ghBeam) || ghBeam.Value == null) continue;
                beams.Add((path.Indices[0], ghBeam.Value));
            }

            var joints = new List<GH_Joint>();
            var indices = new List<int>();
            var parameters = new List<double>();

            for (int i = 0; i < points.Count; ++i)
            {
                var point = points[i];
                int best = -1;
                double bestDistance = double.MaxValue, bestT = 0;
                foreach (var (index, beam) in beams)
                {
                    if (!beam.Centreline.ClosestPoint(point, out double t)) continue;
                    var d = beam.Centreline.PointAt(t).DistanceTo(point);
                    if (d < bestDistance) { best = index; bestDistance = d; bestT = t; }
                }

                if (best < 0 || (maxDistance > 0 && bestDistance > maxDistance))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Point {i}: no beam within reach.");
                    joints.Add(null);
                    indices.Add(-1);
                    parameters.Add(double.NaN);
                    continue;
                }

                var curve = beams.First(x => x.Index == best).Beam.Centreline;
                JointUtil.ClassifyJointPosition(curve, bestT, out int jointCase, out Vector3d direction, endTolerance);

                var jc = new JointX(new List<JointPartX>
                {
                    new JointPartX() { Case = jointCase, ElementIndex = best, JointIndex = i, Parameter = bestT, Direction = direction }
                }, point);

                joints.Add(new GH_Joint(jc));
                indices.Add(best);
                parameters.Add(bestT);
            }

            DA.SetDataList(0, joints);
            DA.SetDataList(1, indices);
            DA.SetDataList(2, parameters);
        }
    }
}
