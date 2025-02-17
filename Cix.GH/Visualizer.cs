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
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

using GluLamb.Cix;
using GluLamb.Cix.Operations;

namespace GluLamb.GH.Components
{
    public class Cmpt_LoadCix : GH_Component
    {
        public Cmpt_LoadCix()
          : base("Load CIX", "CIX",
              "Load and visualize CIX variables for timber fabrication with Winther AS.",
              "GluLamb", "7. Cix")
        {
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("52AF0824-BAF9-4A0B-BF8E-1C6B722BFFB5");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override bool IsPreviewCapable => true;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Filepath", "F", "Path to .cix file.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string cixPath = string.Empty;
            DA.GetData("Filepath", ref cixPath);
                
            if (!System.IO.Path.Exists(cixPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"File '{cixPath}' does not exist.");
                return;
            }

            Message = System.IO.Path.GetFileNameWithoutExtension(cixPath);

            Cix = new Visualizer(cixPath);
        }

        private Visualizer Cix = null;
        private Rhino.Display.DisplayPen Pen = new Rhino.Display.DisplayPen() { Color = Color.Red };

        public override BoundingBox ClippingBox => Cix == null? BoundingBox.Empty : Cix.Bounds;


        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (Cix == null) return;

            args.Display.Draw2dText(Cix.CixPath, System.Drawing.Color.White, new Point2d(20, 30), false, 15);

            Pen.SetPattern(new float[] { 20, 6, 3, 6 });
            Pen.Color = Color.Orange;
            Pen.Thickness = 2;
            Pen.PatternLengthInWorldUnits = false;
            Pen.ThicknessSpace = Rhino.DocObjects.CoordinateSystem.Screen;

            for (int i = 0; i < Cix.Splines.Length; ++i)
            {
                if (Cix.Splines[i] == null) continue;
                args.Display.DrawPoint(Cix.Splines[i].PointAtStart,
                    Rhino.Display.PointStyle.X, 4, Color.Orange);
                args.Display.DrawCurve(Cix.Splines[i], Pen);
                args.Display.DrawArrowHead(Cix.Splines[i].PointAtEnd, Cix.Splines[i].TangentAtEnd, Color.Orange, 0, 30);
                args.Display.Draw2dText(Cix.splineNames[i], Color.Orange,
                    Cix.Splines[i].PointAtEnd + Vector3d.ZAxis * 10, false);
            }

            Pen.SetPattern(new float[] { 10, 10 });
            Pen.Color = Color.White;
            Pen.Thickness = 3;

            for (int i = 0; i < Cix.BlankCurves.Length; ++i)
            {
                if (Cix.BlankCurves[i] == null) continue;
                args.Display.DrawPoint(Cix.BlankCurves[i].PointAtStart,
                    Rhino.Display.PointStyle.X, 4, Color.White);
                args.Display.DrawCurve(Cix.BlankCurves[i], Pen);
                args.Display.DrawArrowHead(Cix.BlankCurves[i].PointAtEnd, Cix.BlankCurves[i].TangentAtEnd, Color.White, 0, 30);
                args.Display.Draw2dText(Cix.blankCurveNames[i], Color.White,
                    Cix.BlankCurves[i].PointAtEnd + Vector3d.ZAxis * 10, false);
            }

            Pen.SetPattern(new float[] { 0 });

            foreach (var operation in Cix.Operations)
            {
                switch (operation)
                {
                    case EndCut endcut:
                        args.Display.Draw2dText(endcut.Name, Color.Yellow, endcut.Plane.Origin, false);
                        args.Display.DrawArrow(
                            new Line(
                                endcut.Plane.Origin,
                                endcut.Plane.ZAxis,
                                100), Color.Yellow);
                        args.Display.DrawLine(endcut.CutLine, Color.Yellow);
                        args.Display.DrawCircle(
                            new Circle(
                                endcut.Plane, 200
                            ), Color.Yellow
                        );
                        break;

                    case CrossJointCutout cutout:
                        args.Display.DrawPoint(cutout.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, Color.Blue);
                        args.Display.Draw2dText(cutout.Name, Color.Blue,
                            cutout.Plane.Origin + Vector3d.ZAxis * 10, false);
                        args.Display.DrawPolyline(cutout.Outline, Color.Blue);
                        args.Display.DrawLines(cutout.SideLines, Color.Blue);
                        args.Display.DrawLine(cutout.MaxSpan, Color.Blue);
                        args.Display.DrawArrow(
                            new Line(cutout.Plane.Origin, cutout.Plane.ZAxis, 100), Color.Blue
                        );

                        break;

                    case SlotMachining slot:
                        double textOffset = 10;
                        Pen.SetPattern(new float[] { 2, 2 });
                        Pen.Color = Color.Cyan;
                        Pen.Thickness = 1;
                        Color slotColor = Color.Cyan;

                        if (slot.Name.EndsWith("EXTRA"))
                        {
                            textOffset = 20;
                            Pen.Color = Color.DodgerBlue;
                            slotColor = Color.DodgerBlue;
                        }

                        var depthOutline = slot.Outline.Duplicate();
                        depthOutline.Transform(Transform.Translation(slot.Plane.ZAxis * slot.Depth));
                        args.Display.DrawCurve(depthOutline.ToNurbsCurve(), Pen);

                        args.Display.DrawPoint(slot.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, slotColor);
                        args.Display.Draw2dText(slot.Name, slotColor,
                            slot.Plane.Origin + Vector3d.ZAxis * textOffset, false);
                        args.Display.DrawPolyline(slot.Outline, slotColor);
                        args.Display.DrawArrow(
                            new Line(slot.Plane.Origin, slot.Plane.ZAxis, slot.Depth), slotColor
                        );
                        break;

                    case CleanCut cleancut:
                        args.Display.DrawPoint(cleancut.CutLine.From,
                            Rhino.Display.PointStyle.X, 4, Color.Magenta);
                        args.Display.Draw2dText(cleancut.Name, Color.Magenta, cleancut.CutLine.From + Vector3d.ZAxis * 10, false);
                        args.Display.DrawLine(cleancut.CutLine, Color.Magenta);

                        break;

                    case DrillGroup2 drillgrp:
                        args.Display.DrawPoint(drillgrp.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, Color.LimeGreen);
                        args.Display.Draw2dText(drillgrp.Name, Color.LimeGreen, drillgrp.Plane.Origin, false);

                        foreach (var drill in drillgrp.Drillings)
                        {
                            args.Display.DrawArrow(
                                new Line(
                                    drillgrp.Plane.PointAt(drill.Position.X, drill.Position.Y),
                                    drillgrp.Plane.ZAxis,
                                    drill.Depth), Color.LimeGreen);

                            args.Display.DrawCircle(
                                new Circle(
                                    new Plane(
                                        drillgrp.Plane.PointAt(drill.Position.X, drill.Position.Y),
                                        drillgrp.Plane.ZAxis),
                                    drill.Diameter * 0.5), Color.LimeGreen
                            );
                        }
                        break;

                    case SlotCut slotcut:
                        args.Display.DrawPoint(slotcut.Path.From,
                            Rhino.Display.PointStyle.X, 4, Color.LightBlue);
                        args.Display.Draw2dText(slotcut.Name, Color.LightBlue,
                            slotcut.Path.From + Vector3d.ZAxis * 10, false);
                        args.Display.DrawLine(slotcut.Path, Color.LightBlue);
                        break;

                    case Tenon tenon:
                        var tenonPlane = new Plane(tenon.PlaneLine.From, tenon.PlaneLine.Direction, -Vector3d.ZAxis);

                        args.Display.DrawPoint(tenon.PlaneLine.From,
                            Rhino.Display.PointStyle.X, 4, Color.Chartreuse);
                        args.Display.Draw2dText(tenon.Name, Color.Chartreuse,
                            tenon.PlaneLine.From + Vector3d.ZAxis * 10, false);

                        args.Display.DrawLine(tenon.PlaneLine, Color.Chartreuse);
                        args.Display.DrawDottedLine(tenon.SawLine, Color.Chartreuse);

                        var localSawline = tenon.LocalSawLine;
                        localSawline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, tenonPlane));
                        args.Display.DrawDottedLine(localSawline, Color.Chartreuse);

                        var outline = tenon.Outline.Duplicate();
                        outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, tenonPlane));

                        args.Display.DrawPolyline(outline, Color.Chartreuse);

                        outline.Transform(Rhino.Geometry.Transform.Translation(tenonPlane.ZAxis * tenon.Depth));
                        args.Display.DrawDottedPolyline(outline, Color.Chartreuse, true);

                        // for (int i = 0; i < 2; ++i)
                        // {
                        //     args.Display.DrawLine(new Line(
                        //         tenonPlane.PointAt(tenon.SideCuts[i][0].X, tenon.SideCuts[i][0].Y),
                        //         tenonPlane.PointAt(tenon.SideCuts[i][1].X, tenon.SideCuts[i][1].Y)
                        //     ), Color.Chartreuse);
                        // }
                        break;

                    // case TenonOutline tenonOutline:
                    //     args.Display.DrawPolyline(tenonOutline.Outline, Color.YellowGreen);
                    //     break;
                    default:
                        break;
                }
            }
        }
    }
}