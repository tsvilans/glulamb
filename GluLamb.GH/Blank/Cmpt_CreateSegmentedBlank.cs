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

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.DocObjects;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GluLamb.GH.Components
{
    public class Cmpt_CreateSegmentedBlank : GH_Component
    {
        public Cmpt_CreateSegmentedBlank()
          : base("Create Segmented Blank", "Blank",
              "Create a segmented glulam blank for a planar beam.",
              "GluLamb", UiNames.BlankSection)
        {
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.SegmentedBlank;
        public override Guid ComponentGuid => new Guid("17b59c1f-095e-4f53-af34-a7a54160b885");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "A planar or straight beam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness", "T", "Layer thickness", GH_ParamAccess.item, 20);
            pManager.AddNumberParameter("MinLength", "Min", "Minimum segment length.", GH_ParamAccess.item, 200);
            pManager.AddNumberParameter("MaxLength", "Max", "Maximum segment length.", GH_ParamAccess.item, 400);
            pManager.AddNumberParameter("Offset", "O", "Offset from edge of segments for locator pins.", GH_ParamAccess.item, 10);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Segments", "S", "Individual segment geometry.", GH_ParamAccess.tree);
            pManager.AddPlaneParameter("Planes", "P", "Individual segment planes.", GH_ParamAccess.tree);
            pManager.AddTextParameter("IDs", "ID", "Indivudal segment IDs.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Locators", "L", "Locator pins for each segment.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Beam beam = null;
            if (!DA.GetData("Beam", ref beam)) return;

            if (!beam.Centreline.IsPlanar())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Beam is not planar!");
                return;
            }

            double minLength = 300;
            double maxLength = 400;
            double thickness = 20;
            double pinOffset = 15;

            DA.GetData("Thickness", ref thickness);
            if (thickness <= 0) thickness = 20;

            DA.GetData("MinLength", ref minLength);
            DA.GetData("MaxLength", ref maxLength);
            DA.GetData("Offset", ref pinOffset);

            beam.Centreline.TryGetPlane(out Plane plane);
            var width = beam.Height * 0.5;

            var offsetCurves = new Curve[2];
            offsetCurves[0] = beam.Centreline.Offset(plane, width, 0.1, CurveOffsetCornerStyle.None)[0];
            offsetCurves[1] = beam.Centreline.Offset(plane, -width, 0.1, CurveOffsetCornerStyle.None)[0];


            var segBlank = new GluLamb.Blanks.SegmentedBlankX(
                beam.Centreline,
                offsetCurves,
                plane,
                width,
                width,
                beam.Width,
                20
            );

            var divisions = segBlank.SegmentCentreline2b(minLength, maxLength, true);


            // Create division planes
            segBlank.CreateDivisionPlanes(divisions);
            segBlank.TrimOffsetsToDivisionPlanes();

            // Create pin locations
            var innerPinOffset = segBlank.InnerOffset.Offset(segBlank.Plane, pinOffset, 0.01, CurveOffsetCornerStyle.None)[0];
            var outerPinOffset = segBlank.OuterOffset.Offset(segBlank.Plane, -pinOffset, 0.01, CurveOffsetCornerStyle.None)[0];

            var pinPoints = new List<Point3d>();

            foreach (var divPlane in segBlank.DivisionPlanes)
            {
                var front = new Plane(divPlane.Origin + divPlane.ZAxis * pinOffset, divPlane.XAxis, divPlane.YAxis);
                var back = new Plane(divPlane.Origin - divPlane.ZAxis * pinOffset, divPlane.XAxis, divPlane.YAxis);

                var xInnerFrontPin = Rhino.Geometry.Intersect.Intersection.CurvePlane(innerPinOffset, front, 0.001);
                var xOuterFrontPin = Rhino.Geometry.Intersect.Intersection.CurvePlane(outerPinOffset, front, 0.001);

                var xInnerBackPin = Rhino.Geometry.Intersect.Intersection.CurvePlane(innerPinOffset, back, 0.001);
                var xOuterBackPin = Rhino.Geometry.Intersect.Intersection.CurvePlane(outerPinOffset, back, 0.001);

                if (xInnerFrontPin != null && xInnerFrontPin.Count > 0)
                    pinPoints.Add(xInnerFrontPin[0].PointA);
                if (xOuterFrontPin != null && xOuterFrontPin.Count > 0)
                    pinPoints.Add(xOuterFrontPin[0].PointA);
                if (xInnerBackPin != null && xInnerBackPin.Count > 0)
                    pinPoints.Add(xInnerBackPin[0].PointA);
                if (xOuterBackPin != null && xOuterBackPin.Count > 0)
                    pinPoints.Add(xOuterBackPin[0].PointA);
            }

            var segs = segBlank.CreateSegments();

            var segments = new DataTree<Brep>();
            var planes = new DataTree<Plane>();
            var ids = new DataTree<string>();
            var pins = new DataTree<Point3d>();

            var path = new GH_Path(new int[] {0,0});
            int layerId = 0, segmentId = 0;
            foreach (var layer in segs)
            {
                foreach (var seg in layer)
                {
                    segments.Add(seg.Geometry, path);
                    planes.Add(seg.Handle, path);
                    ids.Add($"{layerId}-{segmentId}", path);

                    var outline = seg.GetOutline();
                    foreach (var pt in pinPoints)
                    {
                        var pc = outline.Contains(pt, segBlank.Plane, 0.001);
                        if (pc == PointContainment.Inside)
                        {
                            pins.Add(seg.Handle.ClosestPoint(pt), path);
                        }
                    }

                    path = path.Increment(1);
                }

                path = path.Increment(0);
            }

            DA.SetDataTree(0, segments);
            DA.SetDataTree(1, planes);
            DA.SetDataTree(2, ids);
            DA.SetDataTree(3, pins);
        }
    }
}