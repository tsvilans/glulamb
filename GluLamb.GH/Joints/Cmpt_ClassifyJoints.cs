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
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

using GH_IO.Serialization;
using Grasshopper.Kernel.Data;
using static Rhino.FileIO.FileObjWriteOptions;
using System.Drawing;
using GluLamb.Factory;
using GluLamb.Joints;
using Grasshopper;
using Rhino;

namespace GluLamb.GH.Components
{
    public class Cmpt_ClassifyJoints : GH_Component
    {
        public Cmpt_ClassifyJoints()
          : base("Classify joints", "ClassifyJ",
              "Classify joints across a topology.",
              "GluLamb", UiNames.JointsSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.ClassifyJoints;
        public override Guid ComponentGuid => new Guid("92295fa4-1566-41fc-821b-734186a8c661");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        bool monochrome = false;
        bool fullName = false;
        bool freeEnds = false;
        bool splitDoubleT = true;

        List<JointCondition> JointConditions = new List<JointCondition>();
        Dictionary<int, Plane> JointOrigins = null;
        Dictionary<int, Line> JointLines = null;
        Dictionary<int, string> JointNames = null;
        Dictionary<int, string> JointTypes = null;

        readonly Dictionary<string, Color> JointColors = new Dictionary<string, Color>
          {
            {"E", Color.FromArgb(128, 255, 255)},
            {"T", Color.FromArgb(255, 255, 0)},
            {"X", Color.FromArgb(255, 200, 96)},
            {"L", Color.FromArgb(60, 255, 200)},
            {"S", Color.FromArgb(200, 160, 255)},
            {"K", Color.FromArgb(0, 200, 255)},
            {"TT", Color.FromArgb(255, 120, 200)},
            {"3J", Color.FromArgb(0, 200, 255)}
          };

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Monochrome", ToggleMonochrome, true, monochrome);
            Menu_AppendItem(menu, "Full name", ToggleFullName, true, fullName);
            Menu_AppendItem(menu, "Free ends", ToggleFreeEnds, true, freeEnds)
                .ToolTipText = "Also make single-beam (E) conditions at curve ends that aren't in any joint.";
            Menu_AppendItem(menu, "Split double T", ToggleSplitDoubleT, true, splitDoubleT)
                .ToolTipText = "Where two beams end on the side of a third from opposite sides (TT), make two T conditions instead of one three-beam condition.";
        }

        private void ToggleFreeEnds(object sender, EventArgs e)
        {
            RecordUndoEvent("Free ends");
            freeEnds = !freeEnds;
            UpdateMessage();
            ExpireSolution(true);
        }

        private void ToggleSplitDoubleT(object sender, EventArgs e)
        {
            RecordUndoEvent("Split double T");
            splitDoubleT = !splitDoubleT;
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            var flags = new List<string>();
            if (freeEnds) flags.Add("Free ends");
            if (!splitDoubleT) flags.Add("Whole TT");
            Message = flags.Count > 0 ? string.Join(", ", flags) : null;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("FreeEnds", freeEnds);
            writer.SetBoolean("SplitDoubleT", splitDoubleT);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("FreeEnds"))
                freeEnds = reader.GetBoolean("FreeEnds");
            if (reader.ItemExists("SplitDoubleT"))
                splitDoubleT = reader.GetBoolean("SplitDoubleT");
            UpdateMessage();
            return base.Read(reader);
        }

        private void ToggleMonochrome(object sender, EventArgs e)
        {
            monochrome = !monochrome;
            ExpirePreview(true);
        }

        private void ToggleFullName(object sender, EventArgs e)
        {
            fullName = !fullName;
            ExpireSolution(true);
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (JointOrigins != null)
            foreach (var key in JointOrigins.Keys)
            {
                var pt = JointOrigins[key].Origin;

                args.Display.DrawPoint(pt, Rhino.Display.PointStyle.RoundActivePoint, 5, Color.White);

                var spt = args.Display.Viewport.WorldToClient(pt);

                var color = Color.White;
                if (!monochrome)
                    JointColors.TryGetValue(JointTypes[key], out color);

                args.Display.Draw2dText(JointNames[key], color, new Point2d(spt.X, spt.Y - 16), true, 16);

                if (JointLines.ContainsKey(key))
                {
                    args.Display.DrawLine(JointLines[key], Color.Cyan);
                }
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Input centrelines to use for classifying joint conditions.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Connected pairs", "CP", "Indices of connected pairs as tree. Each path is the ID of the connection.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Connected parameters", "CT", "Parameters of connections as tree. Each path is the ID of the connection.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Merge distance", "M", "Distance within which to merge joint conditions.", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("End tolerance", "ET", "Distance within which to consider a joint at the end of an element.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Perp threshold", "PT", "Angle threshold at which to consider a joint a splice, corner, or graft.", GH_ParamAccess.item, JointX.PerpendicularThreshold);
            pManager.AddVectorParameter("Post direction", "PD", "Where three or more beams end at one joint, the one most along this direction (within 45°) is put first, as the post. A zero vector keeps the input order.", GH_ParamAccess.item, Vector3d.ZAxis);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Connected elements", "CI", "Indices of all connected elements for each joint. Each path is the ID of the joint.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Connected parameters", "CP", "Curve parameters of all connected elements for each joint at their connection point. Each path is the ID of the joint.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Connection type", "CT", "Type of each connection. Each path is the ID of the joint.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Joints", "J", "Joints created from the input joint conditions.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> curves)) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Integer> pairs)) return;
            if (!DA.GetDataTree(2, out GH_Structure<GH_Number> parameters)) return;

            double mergeDistance = 50;
            DA.GetData("Merge distance", ref mergeDistance);

            double endTolerance = 10;
            DA.GetData("End tolerance", ref endTolerance);

            double csThreshold = JointX.PerpendicularThreshold;
            DA.GetData("Perp threshold", ref csThreshold);

            var postDirection = Vector3d.ZAxis;
            DA.GetData("Post direction", ref postDirection);

            JointOrigins = new Dictionary<int, Plane>();
            JointLines = new Dictionary<int, Line>();
            JointNames = new Dictionary<int, string>();
            JointTypes = new Dictionary<int, string>();

            var curveMap = new Dictionary<int, Curve>();
            foreach (var path in curves.Paths)
                if (curves[path].Count > 0 && curves[path][0]?.Value != null)
                    curveMap[path.Indices[0]] = curves[path][0].Value;

            var pairList = new List<Pair>();
            foreach (var jp in pairs.Paths)
            {
                var indices = pairs[jp];
                if (indices.Count != 2 || !parameters.PathExists(jp) || parameters[jp].Count < 2) continue;
                pairList.Add(new Pair { A = indices[0].Value, B = indices[1].Value, tA = parameters[jp][0].Value, tB = parameters[jp][1].Value });
            }

            var Joints = JointClassifier.Classify(curveMap, pairList, mergeDistance, endTolerance, csThreshold, postDirection, splitDoubleT, freeEnds);

            for (int i = 0; i < Joints.Count; ++i)
            {
                var jc = Joints[i];
                JointOrigins.Add(i, jc.Position);

                var jointType = JointX.ClassifyJoint(jc, csThreshold);


                if (fullName)
                {
                    JointNames.Add(i, $"Joint {i} ({jointType})");
                }
                else
                {
                    JointNames.Add(i, $"{jointType}");
                }

                JointTypes.Add(i, jointType);
            }

            var jointTree = new DataTree<int>();
            var parameterTree = new DataTree<double>();
            var typeTree = new DataTree<string>();

            for (int i = 0; i < Joints.Count; ++i)
            {
                var jc = Joints[i];

                var path = new GH_Path(i);
                foreach (var part in jc.Parts)
                {
                    jointTree.Add(part.ElementIndex, path);
                    parameterTree.Add(part.Parameter, path);
                }

                typeTree.Add(JointTypes[i], path);
            }

            DA.SetDataTree(0, jointTree);
            DA.SetDataTree(1, parameterTree);
            DA.SetDataTree(2, typeTree);
            DA.SetDataList(3, Joints.Select(x => new GH_Joint(x)));
        }
    }
}
