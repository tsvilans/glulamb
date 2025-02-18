using GluLamb.Cix.Operations;

using Rhino.Geometry;
using Rhino.Display;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace GluLamb.Cix
{
    public class Visualizer
    {
        public Visualizer(string filepath)
        {
            CixPath = filepath;

            var sides = new Dictionary<string, Dictionary<string, double>>()
            {
                {"E1", new Dictionary<string, double>()},
                {"E2", new Dictionary<string, double>()},
                {"IN", new Dictionary<string, double>()},
                {"OUT", new Dictionary<string, double>()},
                {"TOP", new Dictionary<string, double>()},
            };

            var shapeData = new Dictionary<string, double>();
            var blankData = new Dictionary<string, double>();

            var parameters = new Dictionary<string, double>()
            {
                {"ORIGO_X", 0},
                {"ORIGO_Y", 0},
                {"BL_L", 0},
                {"BL_W", 0},
                {"V_START", 0},
            };

            var lines = System.IO.File.ReadAllLines(CixPath);
            Operations = new List<Operation>();
            Bounds = BoundingBox.Empty;

            bool publicVars = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (line.StartsWith("(") && line.EndsWith(")"))
                    continue;

                if (!publicVars && trimmed == "BEGIN PUBLICVARS")
                    publicVars = true;
                else if (publicVars && trimmed == "END PUBLICVARS")
                    publicVars = false;

                if (!publicVars) continue;

                var keyValue = line.Split('=', StringSplitOptions.TrimEntries);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0];
                var value = keyValue[1];

                if (parameters.ContainsKey(key))
                {
                    parameters[key] = double.Parse(value);
                    continue;
                }

                var tok = key.Split('_', StringSplitOptions.TrimEntries);
                switch (tok[0])
                {
                    case ("IN"):
                        sides["IN"][string.Join('_', tok[1..])] = double.Parse(value);
                        break;
                    case ("OUT"):
                        sides["OUT"][string.Join('_', tok[1..])] = double.Parse(value);
                        break;
                    case ("E"):
                        if (tok[1] == "1")
                        {
                            sides["E1"][string.Join('_', tok[2..])] = double.Parse(value);
                            break;
                        }
                        else
                            sides["E2"][string.Join('_', tok[2..])] = double.Parse(value);
                        break;
                    case ("BL"):
                        blankData[key] = double.Parse(value);
                        break;
                    case ("TOP"):
                        if (tok[1] == "IN" || tok[1] == "OUT")
                        {
                            shapeData[key] = double.Parse(value);
                        }
                        else
                        {
                            sides["TOP"][string.Join('_', tok[1..])] = double.Parse(value);
                        }
                        break;
                    case ("BOTTOM"):
                        if (tok[1] == "IN" || tok[1] == "OUT")
                        {
                            shapeData[key] = double.Parse(value);
                        }
                        else
                        {
                            sides["TOP"][string.Join('_', tok[1..])] = double.Parse(value);
                        }
                        break;
                    case ("SEC"):
                        break;
                    default:
                        Console.WriteLine($"Found unknown parameter '{tok[0]}'.");
                        break;
                }
            }

            foreach (var side in sides)
            {
                FindShape(shapeData);
                FindBlank(blankData);
                FindHaks(side.Value, side.Key);
                FindEndCuts(side.Value, side.Key);
                FindSlotCuts(side.Value, side.Key);
                FindSlotMachinings(side.Value, side.Key);
                FindDrillings(side.Value, side.Key);
                FindTaps(side.Value, side.Key);
            }

            FindCleanCuts(sides["E1"], "E1");
            FindCleanCuts(sides["E2"], "E2");

            foreach (var kvp in sides["E1"])
            {
                // Console.WriteLine(kvp.Key);
            }


            foreach (var operation in Operations)
            {
                var objects = operation.GetObjects();
                foreach (var obj in objects)
                {
                    switch (obj)
                    {
                        case GeometryBase geo:
                            Bounds.Union(geo.GetBoundingBox(true));
                            break;
                        case Line line:
                            Bounds.Union(line.From);
                            Bounds.Union(line.To);
                            break;
                        case Plane plane:
                            Bounds.Union(plane.Origin);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public string CixPath = string.Empty;
        public List<Operation> Operations = null;
        public BoundingBox Bounds = BoundingBox.Empty;
        public Curve[] Splines = new Curve[4];
        public Curve[] BlankCurves = new Curve[2];
        private Rhino.Display.DisplayPen Pen = new Rhino.Display.DisplayPen() { Color = Color.Red };


        public readonly int numSplinePoints = 25;
        public readonly string[] splineNames = new string[]
        {
        "TOP_IN_SPL",
        "TOP_OUT_SPL",
        "BOTTOM_IN_SPL",
        "BOTTOM_OUT_SPL"
        };
        public readonly int numBlankPoints = 45;
        public readonly string[] blankCurveNames = new string[]
        {
        "BL_IN_CURVE",
        "BL_OUT_CURVE"
        };

        public void FindShape(Dictionary<string, double> shapeData)
        {
            for (int i = 0; i < splineNames.Length; ++i)
            {
                try
                {
                    var splinePoints = new Point3d[numSplinePoints];
                    for (int j = 0; j < splinePoints.Length; ++j)
                    {
                        splinePoints[j] = new Point3d(
                            shapeData[$"{splineNames[i]}_P_{j + 1}_X"],
                            shapeData[$"{splineNames[i]}_P_{j + 1}_Y"],
                            -shapeData[$"{splineNames[i]}_P_{j + 1}_Z"]
                        );
                    }

                    Splines[i] = Curve.CreateInterpolatedCurve(splinePoints, 3);
                    Bounds.Union(Splines[i].GetBoundingBox(true));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Splines: Failed to parse points for {splineNames[i]}");
                }
            }
        }

        public void FindCleanCuts(Dictionary<string, double> cix, string prefix = "")
        {
            // Should only be one clean cut per side/end...
            try
            {
                var cleancut = CleanCut.FromCix(cix, "");
                if (cleancut != null)
                {
                    cleancut.Name = $"{prefix}_{cleancut.Name}";
                    Operations.Add(cleancut);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
            }
        }

        public void FindBlank(Dictionary<string, double> blankData)
        {
            for (int i = 0; i < blankCurveNames.Length; ++i)
            {
                try
                {
                    var blankPoints = new Point3d[numBlankPoints];
                    for (int j = 0; j < blankPoints.Length; ++j)
                    {
                        blankPoints[j] = new Point3d(
                            blankData[$"{blankCurveNames[i]}_P_{j + 1}_X"],
                            blankData[$"{blankCurveNames[i]}_P_{j + 1}_Y"],
                            0
                        );
                    }

                    BlankCurves[i] = Curve.CreateInterpolatedCurve(blankPoints, 3);

                    Bounds.Union(BlankCurves[i].GetBoundingBox(true));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"BlankCurves: Failed to parse points for {blankCurveNames[i]}");
                }
            }
        }

        public void FindHaks(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"HAK_{i}"))
                {
                    try
                    {
                        var cutout = CrossJointCutout.FromCix(cix, "", $"{i}");
                        if (cutout != null)
                        {
                            cutout.Name = $"{prefix}_{cutout.Name}";
                            Operations.Add(cutout);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR: {e.Message}");
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void FindEndCuts(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"CUT_{i}"))
                {
                    try
                    {
                        var endcut = EndCut.FromCix(cix, "", $"{i}");
                        if (endcut != null)
                        {
                            endcut.Name = $"{prefix}_{endcut.Name}";
                            Operations.Add(endcut);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR: {e.Message}");
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void FindSlotCuts(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"SLOT_CUT_{i}"))
                {
                    try
                    {
                        var slotCut = SlotCut.FromCix(cix, "", $"{i}");
                        if (slotCut != null)
                        {
                            slotCut.Name = $"{prefix}_{slotCut.Name}";
                            Operations.Add(slotCut);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR: {e.Message}");
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void FindSlotMachinings(Dictionary<string, double> cix, string prefix = "")
        {
            var slotNames = new string[] { "SLIDS", "SLIDS_LODRET", "TAPHUL" };
            foreach (var slotName in slotNames)
            {
                // Handle case where there is no operation index
                if (cix.ContainsKey($"{slotName}"))
                {
                    try
                    {
                        var slotCut = SlotMachining.FromCix(cix, string.Empty, string.Empty, slotName);
                        if (slotCut != null)
                        {
                            slotCut.Name = $"{prefix}_{slotCut.Name}";
                            Operations.Add(slotCut);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR (FindSlotMachinings): {e.Message}");
                    }
                }

                // Handle indexed operations
                for (int i = 1; i < 10; ++i)
                {
                    if (cix.ContainsKey($"{slotName}_{i}"))
                    {
                        try
                        {
                            var slotCut = SlotMachining.FromCix(cix, "", $"{i}", slotName);
                            if (slotCut != null)
                            {
                                slotCut.Name = $"{prefix}_{slotCut.Name}";
                                Operations.Add(slotCut);

                                if (cix.ContainsKey($"{slotName}_{i}_EXTRA"))
                                {
                                    var slotCutExtra = SlotMachiningExtraFromCix(cix, slotCut.Plane, $"{slotName}_{i}_EXTRA");
                                    slotCutExtra.Name = $"{prefix}_{slotCutExtra.Name}";

                                    Operations.Add(slotCutExtra);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR (FindSlotMachinings): {e.Message}");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public SlotMachining SlotMachiningExtraFromCix(Dictionary<string, double> cix, Plane plane, string name = "")
        {
            var extra = new SlotMachining(name);
            extra.Plane = plane;

            extra.XLine = new Line(plane.Origin, plane.XAxis, 100);

            extra.Outline = new Polyline();
            for (int i = 1; i <= 9; ++i)
            {
                extra.Outline.Add(
                    new Point3d(
                        cix[$"{name}_PKT_{i}_X"],
                        cix[$"{name}_PKT_{i}_Y"],
                        0
                ));
            }

            extra.Depth = cix[$"{name}_DYBDE"];
            extra.Outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, extra.Plane));

            return extra;
        }

        public void FindDrillings(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"HUL_{i}"))
                {
                    try
                    {
                        var drillgrp = DrillGroup2.FromCix(cix, "", $"{i}");
                        if (drillgrp != null)
                        {
                            drillgrp.Name = $"{prefix}_{drillgrp.Name}";
                            Operations.Add(drillgrp);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR (FindDrillings): {e.Message} Prefix {prefix}");
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void FindTaps(Dictionary<string, double> cix, string prefix = "")
        {
            // Handle case where there is no operation index
            if (cix.ContainsKey($"TAP"))
            {
                try
                {
                    var tap = TapFromCix(cix, string.Empty, string.Empty);
                    if (tap != null)
                    {
                        tap.Name = $"{prefix}_{tap.Name}";
                        Operations.Add(tap);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR (FindTaps): {e.Message}");
                }
            }
        }

        public Tenon TapFromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = string.IsNullOrEmpty(id) ? $"{prefix}TAP" : $"{prefix}TAP_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var tenon = new Tenon(name);

            var p1 = new Point3d(
                cix[$"{name}_PL_PKT_1_X"],
                cix[$"{name}_PL_PKT_1_Y"],
                0
            );

            var p2 = new Point3d(
                cix[$"{name}_PL_PKT_2_X"],
                cix[$"{name}_PL_PKT_2_Y"],
                0
            );

            tenon.PlaneLine = new Line(
                cix[$"{name}_PL_PKT_1_X"],
                cix[$"{name}_PL_PKT_1_Y"],
                0,
                cix[$"{name}_PL_PKT_2_X"],
                cix[$"{name}_PL_PKT_2_Y"],
                0
            );

            tenon.LocalSawLine = new Line(
                cix[$"{name}_SAV_1_PKT_1_X"],
                cix[$"{name}_SAV_1_PKT_1_Y"],
                0,
                cix[$"{name}_SAV_1_PKT_2_X"],
                cix[$"{name}_SAV_1_PKT_2_Y"],
                0
            );

            tenon.SawLine = new Line(
                cix[$"{name}_SAV_2_PKT_1_X"],
                cix[$"{name}_SAV_2_PKT_1_Y"],
                0,
                cix[$"{name}_SAV_2_PKT_2_X"],
                cix[$"{name}_SAV_2_PKT_2_Y"],
                0
            );

            tenon.T = cix[$"{name}_T"];
            tenon.TO = cix[$"{name}_T_O"];
            tenon.TU = cix[$"{name}_T_U"];
            tenon.OutlineRadius = cix[$"{name}_R"];
            tenon.Depth = cix[$"{name}_DYBDE"];
            tenon.DoOutline = cix[$"{name}_OMKRINGFRAES"] > 0;
            tenon.DoSideCuts = cix[$"{name}_HAK_1"] > 0;

            tenon.Outline = new Polyline();
            for (int i = 1; i <= 9; ++i)
            {
                tenon.Outline.Add(
                    cix[$"{name}_PKT_{i}_X"],
                    cix[$"{name}_PKT_{i}_Y"],
                    0
                );
            }

            return tenon;
        }
        public void DrawViewportWires(DisplayPipeline display)
        {

            display.Draw2dText(CixPath, System.Drawing.Color.White, new Point2d(20, 30), false, 15);

            Pen.SetPattern(new float[] { 20, 6, 3, 6 });
            Pen.Color = Color.Orange;
            Pen.Thickness = 2;
            Pen.PatternLengthInWorldUnits = false;
            Pen.ThicknessSpace = Rhino.DocObjects.CoordinateSystem.Screen;

            for (int i = 0; i < Splines.Length; ++i)
            {
                if (Splines[i] == null) continue;
                display.DrawPoint(Splines[i].PointAtStart,
                    Rhino.Display.PointStyle.X, 4, Color.Orange);
                display.DrawCurve(Splines[i], Pen);
                display.DrawArrowHead(Splines[i].PointAtEnd, Splines[i].TangentAtEnd, Color.Orange, 0, 30);
                display.Draw2dText(splineNames[i], Color.Orange,
                    Splines[i].PointAtEnd + Vector3d.ZAxis * 10, false);
            }

            Pen.SetPattern(new float[] { 10, 10 });
            Pen.Color = Color.White;
            Pen.Thickness = 3;

            for (int i = 0; i < BlankCurves.Length; ++i)
            {
                if (BlankCurves[i] == null) continue;
                display.DrawPoint(BlankCurves[i].PointAtStart,
                    Rhino.Display.PointStyle.X, 4, Color.White);
                display.DrawCurve(BlankCurves[i], Pen);
                display.DrawArrowHead(BlankCurves[i].PointAtEnd, BlankCurves[i].TangentAtEnd, Color.White, 0, 30);
                display.Draw2dText(blankCurveNames[i], Color.White,
                    BlankCurves[i].PointAtEnd + Vector3d.ZAxis * 10, false);
            }

            Pen.SetPattern(new float[] { 0 });

            foreach (var operation in Operations)
            {
                switch (operation)
                {
                    case EndCut endcut:
                        display.Draw2dText(endcut.Name, Color.Yellow, endcut.Plane.Origin, false);
                        display.DrawArrow(
                            new Line(
                                endcut.Plane.Origin,
                                endcut.Plane.ZAxis,
                                100), Color.Yellow);
                        display.DrawLine(endcut.CutLine, Color.Yellow);
                        display.DrawCircle(
                            new Circle(
                                endcut.Plane, 200
                            ), Color.Yellow
                        );
                        break;

                    case CrossJointCutout cutout:
                        display.DrawPoint(cutout.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, Color.Blue);
                        display.Draw2dText(cutout.Name, Color.Blue,
                            cutout.Plane.Origin + Vector3d.ZAxis * 10, false);
                        display.DrawPolyline(cutout.Outline, Color.Blue);
                        display.DrawLines(cutout.SideLines, Color.Blue);
                        display.DrawLine(cutout.MaxSpan, Color.Blue);
                        display.DrawArrow(
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
                        display.DrawCurve(depthOutline.ToNurbsCurve(), Pen);

                        display.DrawPoint(slot.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, slotColor);
                        display.Draw2dText(slot.Name, slotColor,
                            slot.Plane.Origin + Vector3d.ZAxis * textOffset, false);
                        display.DrawPolyline(slot.Outline, slotColor);
                        display.DrawArrow(
                            new Line(slot.Plane.Origin, slot.Plane.ZAxis, slot.Depth), slotColor
                        );
                        break;

                    case CleanCut cleancut:
                        display.DrawPoint(cleancut.CutLine.From,
                            Rhino.Display.PointStyle.X, 4, Color.Magenta);
                        display.Draw2dText(cleancut.Name, Color.Magenta, cleancut.CutLine.From + Vector3d.ZAxis * 10, false);
                        display.DrawLine(cleancut.CutLine, Color.Magenta);

                        break;

                    case DrillGroup2 drillgrp:
                        display.DrawPoint(drillgrp.Plane.Origin,
                            Rhino.Display.PointStyle.X, 4, Color.LimeGreen);
                        display.Draw2dText(drillgrp.Name, Color.LimeGreen, drillgrp.Plane.Origin, false);

                        foreach (var drill in drillgrp.Drillings)
                        {
                            display.DrawArrow(
                                new Line(
                                    drillgrp.Plane.PointAt(drill.Position.X, drill.Position.Y),
                                    drillgrp.Plane.ZAxis,
                                    drill.Depth), Color.LimeGreen);

                            display.DrawCircle(
                                new Circle(
                                    new Plane(
                                        drillgrp.Plane.PointAt(drill.Position.X, drill.Position.Y),
                                        drillgrp.Plane.ZAxis),
                                    drill.Diameter * 0.5), Color.LimeGreen
                            );
                        }
                        break;

                    case SlotCut slotcut:
                        display.DrawPoint(slotcut.Path.From,
                            Rhino.Display.PointStyle.X, 4, Color.LightBlue);
                        display.Draw2dText(slotcut.Name, Color.LightBlue,
                            slotcut.Path.From + Vector3d.ZAxis * 10, false);
                        display.DrawLine(slotcut.Path, Color.LightBlue);
                        break;

                    case Tenon tenon:
                        var tenonPlane = new Plane(tenon.PlaneLine.From, tenon.PlaneLine.Direction, -Vector3d.ZAxis);

                        display.DrawPoint(tenon.PlaneLine.From,
                            Rhino.Display.PointStyle.X, 4, Color.Chartreuse);
                        display.Draw2dText(tenon.Name, Color.Chartreuse,
                            tenon.PlaneLine.From + Vector3d.ZAxis * 10, false);

                        display.DrawLine(tenon.PlaneLine, Color.Chartreuse);
                        display.DrawDottedLine(tenon.SawLine, Color.Chartreuse);

                        var localSawline = tenon.LocalSawLine;
                        localSawline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, tenonPlane));
                        display.DrawDottedLine(localSawline, Color.Chartreuse);

                        var outline = tenon.Outline.Duplicate();
                        outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, tenonPlane));

                        display.DrawPolyline(outline, Color.Chartreuse);

                        outline.Transform(Rhino.Geometry.Transform.Translation(tenonPlane.ZAxis * tenon.Depth));
                        display.DrawDottedPolyline(outline, Color.Chartreuse, true);

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
