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
using Grasshopper;

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

        List<JointCondition> JointConditions = new List<JointCondition>();
        Dictionary<int, Point3d> JointOrigins = null;
        Dictionary<int, Line> JointLines = null;
        Dictionary<int, string> JointNames = null;
        Dictionary<int, string> JointTypes = null;

        readonly Dictionary<string, Color> JointColors = new Dictionary<string, Color>
          {
            {"E", Color.FromArgb(128, 255, 255)},
            {"T", Color.FromArgb(255, 255, 0)},
            {"X", Color.FromArgb(255, 200, 96)},
            {"L", Color.FromArgb(60, 255, 200)},
            {"3J", Color.FromArgb(0, 200, 255)}
          };

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Monochrome", ToggleMonochrome, true, monochrome);
            Menu_AppendItem(menu, "Full name", ToggleFullName, true, fullName);
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

        public string ClassifyJoint(int[] flags)
        {
            switch (flags.Length)
            {
                case (0):
                    throw new ArgumentException("No flags present!");
                case (1):
                    return "E";
                case (2):
                    int f0 = flags[0], f1 = flags[1];
                    if (((f0 & 1) == 0) && ((f1 & 1) == 0))
                        return "L";
                    if (((f0 & 1) == 1) && ((f1 & 1) == 1))
                        return "X";
                    return "T";
                default:
                    return $"{flags.Length}J";
            }
        }

        public int ClassifyJointPosition(Curve c, double t, double end_tolerance = 10)
        {
            var status = 0;
            // bit 1 = 0 is end of curve, 1 is middle
            // bit 2 = 0 is start end, 1 is end end

            if (t > c.Domain.Mid)
                status = status | 2;

            double length = 0;
            if ((status & 2) > 0)
                length = c.GetLength(new Interval(t, c.Domain.Max));
            else
                length = c.GetLength(new Interval(c.Domain.Min, t));

            if (length > end_tolerance)
            {
                status = status | 1;
            }
            return status;
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            foreach (var key in JointOrigins.Keys)
            {
                var pt = JointOrigins[key];

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
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Connected elements", "CI", "Indices of all connected elements for each joint. Each path is the ID of the joint.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Connected parameters", "CP", "Curve parameters of all connected elements for each joint at their connection point. Each path is the ID of the joint.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Connection type", "CT", "Type of each connection. Each path is the ID of the joint.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> curves)) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Integer> pairs)) return;
            if (!DA.GetDataTree(2, out GH_Structure<GH_Number> parameters)) return;

            double mergeDistance = 50;
            DA.GetData("Merge distance", ref mergeDistance);

            JointOrigins = new Dictionary<int, Point3d>();
            JointLines = new Dictionary<int, Line>();
            JointNames = new Dictionary<int, string>();
            JointTypes = new Dictionary<int, string>();

            var JointConditions = new List<JointCondition>();

            var jointPaths = pairs.Paths;
            foreach (var jp in jointPaths)
            {

                int id = jp.Indices[0];
                var indices = pairs[jp];
                if (indices.Count != 2) continue;

                var i0 = indices[0].Value;
                var i1 = indices[1].Value;

                var path0 = new GH_Path(i0);
                var path1 = new GH_Path(i1);

                var c0 = curves[i0][0].Value;
                var c1 = curves[i1][0].Value;

                var t0 = parameters[jp][0].Value;
                var t1 = parameters[jp][1].Value;

                var p0 = c0.PointAt(t0);
                var p1 = c1.PointAt(t1);

                var s0 = ClassifyJointPosition(c0, t0);
                var s1 = ClassifyJointPosition(c1, t1);

                var jc = new JointCondition((p0 + p1) * 0.5, new List<JointConditionPart>{
            new JointConditionPart(i0, s0, t0),
            new JointConditionPart(i1, s1, t1)
            });

                JointConditions.Add(jc);
            }

            JointConditions = JointCondition.MergeJointConditions(JointConditions, mergeDistance);

            for (int i = 0; i < JointConditions.Count; ++i)
            {
                var jc = JointConditions[i];
                JointOrigins.Add(i, jc.Position);

                var jointType = ClassifyJoint(jc.Parts.Select(x => x.Case).ToArray());

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

            for (int i = 0; i < JointConditions.Count; ++i)
            {
                var jc = JointConditions[i];

                var path = new GH_Path(i);
                foreach (var part in jc.Parts)
                {
                    jointTree.Add(part.Index, path);
                    parameterTree.Add(part.Parameter, path);
                }

                typeTree.Add(JointTypes[i], path);
            }

            DA.SetDataTree(0, jointTree);
            DA.SetDataTree(1, parameterTree);
            DA.SetDataTree(2, typeTree);
        }
    }
}