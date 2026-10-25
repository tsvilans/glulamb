#if WIP
using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GluLamb;
using GluLamb.Blanks;
using GluLamb.Projects.HHDAC22;
using Rhino.Collections;

using System.Diagnostics;
using System.Linq;
using System.IO;

using GluLamb.Cix;
using GluLamb.Cix.Operations;

using Workpiece = GluLamb.Cix.CixWorkpiece;

namespace GluLamb.Projects
{

    public class CixFactory
    {
        /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
        /// <param name="text">String to print.</param>
        private void Print(string text) { /* Implementation hidden. */ }
        /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
        /// <param name="format">String format.</param>
        /// <param name="args">Formatting parameters.</param>
        private void Print(string format, params object[] args) { /* Implementation hidden. */ }
        /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj) { /* Implementation hidden. */ }
        /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }

        /// <summary>Gets the current Rhino document.</summary>
        private readonly RhinoDoc RhinoDocument;
        /// <summary>Gets the Grasshopper document that owns this script.</summary>
        private readonly GH_Document GrasshopperDocument;
        /// <summary>Gets the Grasshopper script component that owns this script.</summary>
        private readonly IGH_Component Component;
        /// <summary>
        /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
        /// Any subsequent call within the same solution will increment the Iteration count.
        /// </summary>
        private readonly int Iteration;

        string[] featureNames = new string[]{
                            "E_1_CUT_1",
                            "E_1_CUT_2",
                            "E_2_CUT_1",
                            "E_2_CUT_2",
                            "E_1_HUL_1",
                            //"E_1_HUL_2",
                            "E_2_HUL_1",
                            //"E_2_HUL_2",
                            "TOP_HUL_1",
                            "TOP_HUL_2",
                            "TOP_HUL_3",
                            "TOP_HUL_4",
                            "IN_HUL_1",
                            "IN_HUL_2",
                            "IN_HUL_3",
                            //"IN_HUL_4",
                            //"IN_HUL_5",
                            //"IN_HUL_6",
                            "OUT_HUL_1",
                            "OUT_HUL_2",
                            "OUT_HUL_3",
                            //"OUT_HUL_4",
                            //"OUT_HUL_5",
                            //"OUT_HUL_6",
                            "IN_HAK_1",
                            //"IN_HAK_2",
                            "OUT_HAK_1",
                            //"OUT_HAK_2",
                            "TOP_DYVELHULE_E_1",
                            "TOP_DYVELHULE_E_2",

                            "E_1_SLIDS_LODRET_1",
                            "E_1_SLIDS_LODRET_1_GROV",
                            "E_2_SLIDS_LODRET_1",
                            "E_2_SLIDS_LODRET_1_GROV",

                            "E_1_SKRAA_1",
                            "E_2_SKRAA_1",
                            "E_1_SKRAA_2",
                            "E_2_SKRAA_2",
                            //"E_1_SLIDS",
                            //"E_2_SLIDS",
                            "E_1_TAPHUL",
                            "E_2_TAPHUL",
                            "TOP_TAPHUL_1",
                            "E_1_TAP",
                            "E_2_TAP",
                            "E_1_TAP_OMKRINGFRAES",
                            "E_2_TAP_OMKRINGFRAES",
                            "E1_TAP_OUTLINE",
                            "E2_TAP_OUTLINE",
                            "E1_TAP_OUTLINE_1",
                            "E1_TAP_OUTLINE_2",
                            "E2_TAP_OUTLINE_1",
                            "E2_TAP_OUTLINE_2",
                            "E_1_TAP_HAK_1",
                            "E_1_TAP_HAK_2",
                            "E_2_TAP_HAK_1",
                            "E_2_TAP_HAK_2",
                            //"E1_RENSKAER",
                            //"E2_RENSKAER",
                            };

        private void RunScript(object S, DataTree<object> BL, List<Plane> PP, bool Ex, List<string> F, List<string> CN, List<Vector3d> CV, List<string> EE, out object DN, out object DG, out object P, out object BP, out object N)
        {

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            Log = new List<string>();

            this.Component.Message = "";

            Structure structure = S as Structure;
            if (structure == null) throw new ArgumentNullException("Structure is null.");

            double spacingY = 2000;
            debug = new List<BakeFeature>();

            var planes = new List<Plane>();
            var blank_planes = new List<Plane>();
            var names = new List<string>();

            // Date and export variables
            var dt = System.DateTime.Now;
            var datestring = string.Format("{0:0000}-{1:00}-{2:00}", dt.Year, dt.Month, dt.Day);
            var timestring = string.Format("{0:00}:{1:00}:{2:00}", dt.Hour, dt.Minute, dt.Second);

            string directory = Environment.ExpandEnvironmentVariables("%onedrivecommercial%/03_Projects/2022_DAC_HH/D - Production files/Details");
            string subdir = System.IO.Path.Combine(directory, datestring);
            string subdir_flipped = System.IO.Path.Combine(directory, datestring + "_back");

            if (!System.IO.Directory.Exists(subdir) && Ex)
                System.IO.Directory.CreateDirectory(subdir);
            if (!System.IO.Directory.Exists(subdir_flipped) && Ex)
                System.IO.Directory.CreateDirectory(subdir_flipped);

            string test_dir = System.IO.Path.Combine(directory, "TESTING");
            if (!System.IO.Directory.Exists(test_dir) && Ex)
                System.IO.Directory.CreateDirectory(test_dir);

            FlippedList = new Dictionary<string, bool>();

            Print("{0:00.00}s : processing elements... ", (double)timer.ElapsedMilliseconds / 1000.0);
            int counter = 0;

            var filter = new string[]{
              "F",
              "B",
              "J",
              "P",
              "S",
              "V"
              };

            for (int i = 0; i < structure.Elements.Count; ++i)
            {
                var ele = structure.Elements[i];

                if (EE.Count > 0)
                    if (EE.IndexOf(ele.Name) < 0) continue;
                    else if (Array.IndexOf(filter, ele.Name.Substring(0, 1)) < 0) continue;

                var beam = (ele as BeamElement).Beam;
                var blank = BL.Branch(i)[0] as SegmentedBlankX;

                Print("");
                Print("{0}", ele.Name);

                var plane = new Plane(
                  new Point3d(0, spacingY * counter, 0), Vector3d.XAxis, Vector3d.YAxis);

                plane = PP[i];

                double beamWidth = beam.Width;
                if (ele.Name.StartsWith("F"))
                    beamWidth = 28;

                var BoundRec = new Rectangle3d(plane, -(blank.Bounds.Max.X - blank.Bounds.Min.X), -(blank.Bounds.Max.Y - blank.Bounds.Min.Y));
                var boundsFeature = new BakeFeature(string.Format("{0}_Bounds", ele.Name), new List<object>());
                boundsFeature.Objects.Add(BoundRec);

                debug.Add(boundsFeature);

                Plane TargetPlane = new Plane(new Point3d(0, 0, -beamWidth * 0.5), Vector3d.XAxis, Vector3d.YAxis);

                Flipped = F.IndexOf(ele.Name) >= 0;
                //Flipped = false;

                FlippedList[ele.Name] = Flipped;

                if (Flipped)
                {
                    var flipPoint = new Point3d(blank.Bounds.Min.X, blank.Bounds.Max.Y, -beamWidth * 0.5);
                    TargetPlane = new Plane(flipPoint, -Vector3d.XAxis, Vector3d.YAxis);
                }

                // Create transforms
                Correction = Transform.Identity;
                int ci = CN.IndexOf(ele.Name);
                if (ci >= 0)
                {
                    Correction = Transform.Translation(CV[ci]);
                    Print("CORRECTING ELEMENT {0}", ele.Name);
                }

                /*
                      if (ele.Name.StartsWith("J") && false)
                      {
                        var joistPlane = new Plane(blank.Plane.Origin, blank.Plane.XAxis, blank.Plane.XAxis);

                        World2Local = Transform.PlaneToPlane(joistPlane, TargetPlane);

                      }
                      else*/
                World2Local = Transform.PlaneToPlane(blank.Plane, TargetPlane);
                World2Local = Transform.Multiply(Correction, World2Local);

                Local2Plane = Transform.PlaneToPlane(Plane.WorldXY, plane);

                MidPoint = (blank.Bounds.Max + blank.Bounds.Min) / 2;

                planes.Add(plane);
                names.Add(ele.Name);

                // Create CIX list
                //var cix = new List<string>();



                // Add CIX header
                var eleName = Flipped ? string.Format("{0} - flipped", ele.Name) : ele.Name;
                //cix.Add(string.Format("({0})", eleName));
                //cix.Add(string.Format("({0} {1})", datestring, timestring));

                // Testing new CIX making
                using (var cixStream = new StringWriter())
                {

                    cixStream.WriteLine(string.Format("({0})", eleName));
                    cixStream.WriteLine(string.Format("({0} {1})", datestring, timestring));
                    cixStream.WriteLine("BEGIN PUBLICVARS");

                    var testFeature = new BakeFeature(string.Format("{0}_{1}", ele.Name, "Testing"), new List<object>());
                    CreateCixElement(cixStream, ele, blank, ci >= 0);
                    //cix2.AddRange(CreateCixElement(ele, blank, true));

                    if (ele.UserDictionary.ContainsKey("edge_curves"))
                    {
                        ParseEdgeCurves(ele.UserDictionary["edge_curves"], cixStream, testFeature.Objects, beam);
                    }

                    var featureFlags = new bool[featureNames.Length];

                    cixStream.WriteLine("(Begin operations)");

                    var wp = ConstructOperations(ele);

                    HarvestData(wp);
                    var wpLines = new List<string>();

                    wp.ToCix(wpLines, "\t");

                    foreach (var line in wpLines)
                        cixStream.WriteLine(line);

                    // Turn off all features
                    /*
                    cixStream.WriteLine("(Turn off unused features)");
                                            
                    foreach (string line in cixStream.)
                    {
                        string trimmed = line.Trim();
                        var tok = trimmed.Split('=');
                        if (tok.Length < 2) continue;

                        var index = Array.IndexOf(featureNames, tok[0]);

                        if (index >= 0)
                            featureFlags[index] = true;
                    }

                    for (int j = 0; j < featureFlags.Length; ++j)
                        if (!featureFlags[j])
                            cixStream.WriteLine(string.Format("\t{0}=0", featureNames[j]));
                    */

                    cixStream.WriteLine("END PUBLICVARS");

                    if (Ex)
                    {
                        File.WriteAllText(System.IO.Path.Combine(subdir, $"{ele.Name}-A.cix"), cixStream.ToString());
                    }
                }

                if (ele.Geometry != null)
                {
                    var geo = ele.Geometry.Duplicate();
                    geo.Transform(World2Local);

                    if (Ex && true)
                    {

                        string elementdir = System.IO.Path.Combine(directory, "Elements");

                        var rhinoFile = new Rhino.FileIO.File3dm();
                        var attr = new Rhino.DocObjects.ObjectAttributes();
                        attr.Name = ele.Name;
                        attr.WireDensity = 0;
                        rhinoFile.Objects.AddBrep(geo as Brep, attr);
                        rhinoFile.Write(
                            System.IO.Path.Combine(elementdir, string.Format("{0}.3dm", ele.Name)), 7);
                    }

                    geo.Transform(Local2Plane);

                    var bakeFeature = new BakeFeature(string.Format("{0}_{1}", ele.Name, "Geometry"), new List<object>());
                    bakeFeature.Objects.Add(geo);
                    debug.Add(bakeFeature);

                }

                Print("{0:00.00}s : element {1}... ", (double)timer.ElapsedMilliseconds / 1000.0, ele.Name);

                // ***********************
                // Create flipped CIX file
                // ***********************

                if (false)
                {

                    Flipped = !Flipped;
                    if (Flipped)
                        TargetPlane = new Plane(new Point3d(blank.Bounds.Min.X, blank.Bounds.Max.Y, -beamWidth * 0.5), -Vector3d.XAxis, Vector3d.YAxis);
                    else
                        TargetPlane = new Plane(new Point3d(0, 0, -beamWidth * 0.5), Vector3d.XAxis, Vector3d.YAxis);

                    // Recreate transformation with flip

                    World2Local = Transform.PlaneToPlane(blank.Plane, TargetPlane);
                    World2Local = Transform.Multiply(Correction, World2Local);

                    eleName = string.Format("{0} B", ele.Name);

                    if (Ex)
                    {
                        using (var cixFlippedStream = new StreamWriter(System.IO.Path.Combine(subdir_flipped, $"{ele.Name}-B.cix")))
                        {

                            cixFlippedStream.WriteLine(string.Format("({0} - back)", eleName));
                            cixFlippedStream.WriteLine(string.Format("({0} {1})", datestring, timestring));
                            cixFlippedStream.WriteLine("BEGIN PUBLICVARS");

                            CreateCixElement(cixFlippedStream, ele, blank, ci >= 0);

                            if (ele.UserDictionary.ContainsKey("edge_curves"))
                            {
                                cixFlippedStream.WriteLine(string.Format("({0})", "edge_curves"));
                                ParseEdgeCurves(ele.UserDictionary["edge_curves"], cixFlippedStream, null, beam);
                            }
                            cixFlippedStream.WriteLine("END PUBLICVARS");
                        }
                    }


                    // ***********************
                    // End flipped CIX file
                    // ***********************

                    blank_planes.Add(ele.UserDictionary.GetPlane("blank_plane", Plane.WorldXY));
                    counter++;
                }
            }

            if (Ex)
                ExportFlippedList(System.IO.Path.Combine(subdir, string.Format("{0}_DAC_HH_FlippedList.csv", datestring)));

            var debugNames = new DataTree<string>();
            var debugGeo = new DataTree<object>();
            var debugPath = new GH_Path(0);
            foreach (var feat in debug)
            {
                debugGeo.EnsurePath(debugPath);
                debugGeo.AddRange(feat.Objects, debugPath);
                debugNames.Add(feat.Name, debugPath);
                debugPath = debugPath.Increment(0);
            }


            DN = debugNames;
            DG = debugGeo;
            P = planes;
            BP = blank_planes;
            N = names;

            System.IO.File.WriteAllLines(System.IO.Path.Combine(directory, "log.txt"), Log);

        }

        // <Custom additional code> 

        Transform World2Local;
        Transform Local2Plane;
        Transform Correction;
        Transform Local2World;

        List<string> Log;

        //Plane SafetyPlane = new Plane(new Point3d(0, 0, 10), Vector3d.XAxis, Vector3d.YAxis);
        Plane SafetyPlane = Plane.WorldXY;
        Point3d MidPoint;
        double MaxDepth = 118;

        Dictionary<string, List<string>> JointKeys;
        string[] JointNames = new string[] { "DowelGroupT", "DowelGroupM", "Locators", "EdgeCurves", "EndCut", "D2" };

        bool Flipped = false;
        List<BakeFeature> debug = new List<BakeFeature>();
        Dictionary<string, bool> FlippedList;

        // Count end cuts
        List<Plane>[] EndCuts;
        Plane[] EndPlanes;

        // Track current variables
        Curve InnerOffset = null;
        Curve OuterOffset = null;
        string CurrentName = "";

        CixHelper CixHelp = new CixHelper();
        //string ReplacementPath = "%onedrivecommercial%/03_Projects/2022_DAC_HH/D - Production files/Blanks/2022-02-27";
        string ReplacementPath = "%onedrivecommercial%/03_Projects/2022_DAC_HH/D - Production files/Blank replacements";

        struct BakeFeature
        {
            public string Name;
            public List<object> Objects;

            public BakeFeature(string name, List<object> objects)
            {
                Name = name;
                Objects = objects;
            }
        }

        public bool AlignedPlane(Point3d origin, Vector3d v, out Plane plane, out double angle)
        {
            Vector3d xaxis, yaxis;

            // Handle case where the vector is pointing straight up or down
            double dot = Vector3d.ZAxis * v;
            if (dot == 1)
                xaxis = Vector3d.XAxis;
            else if (dot == -1)
                xaxis = -Vector3d.XAxis;
            else
                xaxis = Vector3d.CrossProduct(Vector3d.ZAxis, v);

            yaxis = Vector3d.CrossProduct(xaxis, v);

            plane = new Plane(origin, xaxis, yaxis);

            var sign = v * Vector3d.ZAxis > 0 ? 1 : -1;
            angle = Vector3d.VectorAngle(yaxis, -Vector3d.ZAxis) * sign;

            return true;
        }

        public Workpiece ConstructOperations(Element ele)
        {
            Log.Add(string.Format("CONSTRUCTING ELEMENT {0}", ele.Name));
            var wp = new Workpiece(ele.Name);
            var beam = (ele as BeamElement).Beam.Duplicate();

            beam.Transform(World2Local);

            var dict = ele.UserDictionary;

            var jKeys = new Dictionary<string, List<string>>();

            foreach (var key in dict.Keys)
            {
                if (key.StartsWith("DowelGroupT")) // Thru-holes for dowels from the ends
                {
                    ConstructDrillGroup(dict[key], key, wp, beam, true);
                }
                else if (key.StartsWith("DowelGroupM")) // Thru-holes for dowels from the top
                {
                    ConstructDrillGroup(dict[key], key, wp, beam, false);
                }
                else if (key.StartsWith("EndCut"))
                {
                    ConstructEndCut(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("edge"))
                {
                }
                else if (key.StartsWith("Locators"))
                {
                }
                else if (key.StartsWith("D2")) // Cross joint
                {
                    ConstructCrossJointCut(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("PlateSlot")) // Slots for plates at the ends of beams
                {
                    ConstructRoughSlot(dict[key], key + "Rough", wp, beam);
                    ConstructFinishSlot(dict[key], key + "Finish", wp, beam);
                    //ConstructPlateSlot(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("PlateDowel")) // Thru-hole for dowels from the side
                {
                    ConstructPlateDowel(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("FourWay"))
                {
                    ConstructSplitCut2(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("TenonSlot")) // Tap hole, slot for plate into the side of an element
                {
                    ConstructTenonSlot2(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("SillTenonSlot")) // Tap hole for sill
                {
                    ConstructSillTenonSlot(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("Tap")) // Tap hole for sill
                {
                    Print("Got {0}", key);

                    ConstructTenon(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("TOutline"))
                {
                    ConstructTenonOutline(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("EndTenon")) // For portal beams
                {
                    Print("Got {0}", key);
                    ConstructPortalTenon(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("EndMortise")) // For portal beams
                {
                    Print("Got {0}", key);
                    ConstructPortalMortise(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("Fals"))
                {
                    Print("Got {0}", key);

                    ConstructFals(dict[key], key, wp, beam);
                }
                else if (key.StartsWith("SillJoistSlot"))
                {
                    ConstructSillJoistSlot(dict[key], key, wp, beam);
                }
            }

            // Clean cuts need to happen after all end cuts
            ConstructCleanCut(wp, beam);

            // Organize operations
            OrganizeOperations(wp);

            return wp;
        }

        void OrganizeOperations(Workpiece wp)
        {
            foreach (var side in wp.Sides)
            {
                int endCutId = 1;
                int drillGroupId = 1;
                int drillGroup2Id = 1;
                int cleanCutId = 1;
                int plateDowelId = 1;
                int slotId = 1;
                int slotRoughId = 1;
                int lineMachId = 1;
                int slotCutId = 1;
                int sawingId = 1;
                int tenonId = 1;
                int sideClearId = 1;
                int tenonOutlineId = 1;
                int falsId = 1;
                int sillTapHoleId = 1;
                int drillGroupTopId = 1;

                //if (side == wp.Top)
                //  drillGroup2Id = 3;

                for (int i = 0; i < side.Operations.Count; ++i)
                {
                    var op = side.Operations[i];

                    if (op is EndCut)
                    {
                        op.Id = endCutId; endCutId++;
                    }
                    else if (op is DrillGroup)
                    {
                        op.Id = drillGroupId; drillGroupId++;
                    }
                    else if (op is DrillGroup2)
                    {
                        op.Id = drillGroup2Id; drillGroup2Id++;
                    }
                    else if (op is CleanCut)
                    {
                        op.Id = cleanCutId; cleanCutId++;
                    }
                    else if (op is SlotMachining)
                    {
                        if ((op as SlotMachining).Rough)
                        {
                            op.Id = slotRoughId; slotRoughId++;
                        }
                        else
                        {
                            op.Id = slotId; slotId++;
                        }
                    }
                    else if (op is SideDrillGroup)
                    {
                        op.Id = plateDowelId; plateDowelId++;
                    }
                    else if (op is LineMachining)
                    {
                        op.Id = lineMachId; lineMachId++;
                    }
                    else if (op is SlotCut)
                    {
                        op.Id = slotCutId; slotCutId++;
                    }
                    else if (op is Sawing)
                    {
                        op.Id = sawingId; sawingId++;
                    }
                    else if (op is Tenon)
                    {
                        op.Id = tenonId; tenonId++;
                    }
                    else if (op is SideClearing)
                    {
                        op.Id = sideClearId; sideClearId++;
                    }
                    else if (op is TenonOutline)
                    {
                        op.Id = tenonOutlineId; tenonOutlineId++;
                    }
                    else if (op is Fals)
                    {
                        op.Id = falsId; falsId++;
                    }
                    else if (op is SillSlotMachining)
                    {
                        op.Id = sillTapHoleId; sillTapHoleId++;
                    }
                    else if (op is DrillGroupTop)
                    {
                        op.Id = drillGroupTopId; drillGroupTopId++;
                    }
                }
            }
        }

        public void ConstructFals(object obj, string name, Workpiece wp, Beam beam)
        {
            var falsAd = obj as ArchivableDictionary;
            double depth = falsAd.GetDouble("Depth");
            double width = falsAd.GetDouble("Width");
            var p0 = falsAd.GetPoint3d("Start");
            var p1 = falsAd.GetPoint3d("End");

            p0.Transform(World2Local);
            p1.Transform(World2Local);

            var falsOp = new Fals(name);

            falsOp.Path = new Line(p0, p1);
            falsOp.Depth = depth;
            falsOp.Width = width;

            wp.Outside.Operations.Add(falsOp);
        }

        void HarvestData(Workpiece wp)
        {
            var ops = wp.GetAllOperations();
            foreach (var op in ops)
            {
                var objects = new List<object>();
                foreach (var obj in op.GetObjects())
                {
                    if (obj is Plane)
                    {
                        var p = (Plane)obj;
                        p.Transform(Local2Plane);

                        objects.Add(p);
                        objects.Add(new Line(p.Origin, p.XAxis * 75));
                        objects.Add(new Line(p.Origin, p.YAxis * 150));
                    }
                    else if (obj is Line)
                    {
                        var l = (Line)obj;
                        l.Transform(Local2Plane);
                        objects.Add(l);
                    }
                    else if (obj is Polyline)
                    {
                        var pl = new Polyline((Polyline)obj);
                        pl.Transform(Local2Plane);
                        objects.Add(pl);
                    }

                }
                var bf = new BakeFeature(string.Format("{0}_{1}", op.Name, op.Id), objects);
                debug.Add(bf);
            }
        }

        Polyline MakeSlotPolyline(IList<Point3d> poly, double radius, bool nine = true, int side = 0)
        {
            Vector3d v;

            var pts = new List<Point3d>();

            int offset = side;
            var indices = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < 4; ++i)
            {
                indices[i] = (indices[i] + side).Modulus(4);
            }

            if (nine)
            {
                pts.Add((poly[indices[0]] + poly[indices[3]]) * 0.5);

                v = poly[indices[3]] - poly[indices[0]]; v.Unitize();

                pts.Add(poly[indices[0]] + v * radius);

                v = poly[indices[1]] - poly[indices[0]]; v.Unitize();

                pts.Add(poly[indices[0]] + v * radius);
                pts.Add(poly[indices[1]] - v * radius);

                v = poly[indices[2]] - poly[indices[1]]; v.Unitize();

                pts.Add(poly[indices[1]] + v * radius);
                pts.Add(poly[indices[2]] - v * radius);

                v = poly[indices[3]] - poly[indices[2]]; v.Unitize();

                pts.Add(poly[indices[2]] + v * radius);
                pts.Add(poly[indices[3]] - v * radius);

                v = poly[indices[0]] - poly[indices[3]]; v.Unitize();
                pts.Add(poly[indices[3]] + v * radius);

                return new Polyline(pts);
            }

            pts.Add((poly[indices[0]] + poly[indices[3]]) * 0.5);
            pts.Add(poly[indices[0]]);
            pts.Add(poly[indices[1]]);
            pts.Add(poly[indices[2]]);
            pts.Add(poly[indices[3]]);

            return new Polyline(pts);
        }

        void ConstructSillTenonSlot2(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            //var sidePlane = (Plane) slotDict["SidePlane"];
            //var outsidePlane = (Plane) slotDict["OutsidePlane"];
            var slotPlane = (Plane)slotDict["SlotPlane"];

            Plane bottomPlane = Plane.Unset;
            if (!slotDict.TryGetPlane("EndPlane", out bottomPlane))
                bottomPlane = Plane.Unset;

            var platePlanes = new Plane[2];
            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");

            var platePlane = new Plane((platePlanes[0].Origin + platePlanes[1].Origin) / 2, platePlanes[0].XAxis, platePlanes[0].YAxis);

            var endPlane0 = (Plane)slotDict["TenonSide0"];
            var endPlane1 = (Plane)slotDict["TenonSide1"];

            var plateThickness = 20.85;
            if (!slotDict.TryGetDouble("PlateThickness", out plateThickness))
                plateThickness = 20.85;

            var depth = slotDict.GetDouble("Depth");

            bottomPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane0.Transform(World2Local);
            endPlane1.Transform(World2Local);

            //slotPlane.Transform(Transform.Translation(slotPlane.ZAxis * 30));

            if (endPlane0.ZAxis * (MidPoint - endPlane0.Origin) > 0)
            {
                endPlane0 = new Plane(endPlane0.Origin, -endPlane0.XAxis, endPlane0.YAxis);
            }
            if (bottomPlane == Plane.Unset)
                bottomPlane = new Plane(slotPlane.Origin - slotPlane.ZAxis * depth, slotPlane.XAxis, slotPlane.YAxis);

            //var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane0.ZAxis;

            Point3d origin_top, origin_btm;
            Point3d end0, end1;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0, out end0);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1, out end1);

            slotPlane = new Plane(end0, platePlane.ZAxis, end1 - end0);

            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, slot_length = 0, slot_angle = 0;
            Point3d[] outline;


            slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
            slot_length = end0.DistanceTo(end1);

            double hThick = plateThickness / 2;
            outline = new Point3d[]{
      slotPlane.PointAt(hThick, 0),
      slotPlane.PointAt(-hThick, 0),
      slotPlane.PointAt(-hThick, slot_length),
      slotPlane.PointAt(hThick, slot_length)};


            // (Check if it works on the bottom)
            //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

            depth = bottomPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);
            depthRough = depth;
            depthFinish = depth;


            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            // Determine endpoints
            var PointE1 = (InnerOffset.PointAtStart + OuterOffset.PointAtStart) / 2;
            var PointE2 = (InnerOffset.PointAtEnd + OuterOffset.PointAtEnd) / 2;

            int wpSide = 0;
            if (slotPlane.ZAxis * Vector3d.YAxis > 0)
                wpSide = 1; // Out
            else
                wpSide = 2; // In

            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            var slotOp = new SillSlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;
            if (wpSide == 4 || wpSide == 3)
                slotOp.OperationName = "SLIDS_LODRET";
            else
                slotOp.OperationName = "TAPHUL";

            switch (wpSide)
            {
                case (1): // Out
                    wp.Outside.Operations.Add(slotOp);
                    break;
                case (2): // In
                    wp.Inside.Operations.Add(slotOp);
                    break;
                default: // Top
                    wp.Top.Operations.Add(slotOp);
                    break;
            }
        }

        void ConstructTenonSlot2(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            //var sidePlane = (Plane) slotDict["SidePlane"];
            //var outsidePlane = (Plane) slotDict["OutsidePlane"];
            var slotPlane = (Plane)slotDict["SlotPlane"];

            Plane bottomPlane = Plane.Unset;
            if (!slotDict.TryGetPlane("EndPlane", out bottomPlane))
                bottomPlane = Plane.Unset;

            var platePlanes = new Plane[2];
            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");

            var platePlane = new Plane((platePlanes[0].Origin + platePlanes[1].Origin) / 2, platePlanes[0].XAxis, platePlanes[0].YAxis);

            var endPlane0 = (Plane)slotDict["TenonSide0"];
            var endPlane1 = (Plane)slotDict["TenonSide1"];

            var plateThickness = 20.85;
            if (!slotDict.TryGetDouble("PlateThickness", out plateThickness))
                plateThickness = 20.85;

            var depth = slotDict.GetDouble("Depth");

            bottomPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane0.Transform(World2Local);
            endPlane1.Transform(World2Local);

            var slotZSign = slotPlane.ZAxis * Vector3d.ZAxis > 0 ? 1 : -1;
            slotPlane.Transform(Transform.Translation(slotPlane.ZAxis * -(slotPlane.Origin.Z - 2) * slotZSign));

            if (endPlane0.ZAxis * (MidPoint - endPlane0.Origin) > 0)
            {
                endPlane0 = new Plane(endPlane0.Origin, -endPlane0.XAxis, endPlane0.YAxis);
            }
            if (bottomPlane == Plane.Unset)
                bottomPlane = new Plane(slotPlane.Origin - slotPlane.ZAxis * depth, slotPlane.XAxis, slotPlane.YAxis);

            //var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane0.ZAxis;

            Point3d origin_top, origin_btm;
            Point3d end0, end1;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0, out end0);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1, out end1);

            slotPlane = new Plane(end0, platePlane.ZAxis, end1 - end0);

            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, slot_length = 0, slot_angle = 0;
            Point3d[] outline;


            slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
            slot_length = end0.DistanceTo(end1);

            double hThick = plateThickness / 2;
            outline = new Point3d[]{
      slotPlane.PointAt(hThick, 0),
      slotPlane.PointAt(-hThick, 0),
      slotPlane.PointAt(-hThick, slot_length),
      slotPlane.PointAt(hThick, slot_length)};


            // (Check if it works on the bottom)
            //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

            depth = bottomPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);
            depth = Math.Min(MaxDepth, depth);
            depthRough = depth;
            depthFinish = depth;



            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            // Determine endpoints
            var PointE1 = (InnerOffset.PointAtStart + OuterOffset.PointAtStart) / 2;
            var PointE2 = (InnerOffset.PointAtEnd + OuterOffset.PointAtEnd) / 2;

            int wpSide = 0;
            if (Math.Abs(Vector3d.VectorAngle(-Vector3d.ZAxis, slotPlane.ZAxis)) < RhinoMath.ToRadians(50))
            {
                if (slotPlane.Origin.DistanceTo(PointE1) < slotPlane.Origin.DistanceTo(MidPoint))
                {
                    wpSide = 3; // E1
                }
                else if (slotPlane.Origin.DistanceTo(PointE2) < slotPlane.Origin.DistanceTo(MidPoint))
                {
                    wpSide = 4;//E2
                }
                else
                {
                    wpSide = 0; // Top
                }
            }
            else if (slotPlane.ZAxis * Vector3d.YAxis > 0)
                wpSide = 1; // Out
            else
                wpSide = 2; // In

            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            var slotOp = new SlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;
            if (wpSide == 4 || wpSide == 3)
                slotOp.OperationName = "SLIDS_LODRET";
            else
                slotOp.OperationName = "TAPHUL";

            switch (wpSide)
            {
                case (1): // Out
                    wp.Outside.Operations.Add(slotOp);
                    break;
                case (2): // In
                    wp.Inside.Operations.Add(slotOp);
                    break;
                case (3): // E1
                    wp.E1.Operations.Add(slotOp);
                    break;
                case (4): // E2
                    wp.E2.Operations.Add(slotOp);
                    break;
                default: // Top
                    wp.Top.Operations.Add(slotOp);
                    break;
            }
        }

        void ConstructTenon(object obj, string name, Workpiece wp, Beam beam)
        {
            var adTenon = obj as ArchivableDictionary;
            var doSideCuts = adTenon.GetBool("DoSideCuts", false);
            var doOutline = adTenon.GetBool("DoOutline", false);

            var basePlane = adTenon.GetPlane("BasePlane");
            var topPlane = adTenon.GetPlane("TopPlane");

            var widthPlanes = new Plane[2];


            var heightPlanes = new Plane[2];
            heightPlanes[0] = adTenon.GetPlane("HeightPlane0");
            heightPlanes[1] = adTenon.GetPlane("HeightPlane1");

            // Transform everything
            basePlane.Transform(World2Local);
            topPlane.Transform(World2Local);

            heightPlanes[0].Transform(World2Local);
            heightPlanes[1].Transform(World2Local);


            Point3d pl0, pl1;

            var END = topPlane.Origin.X > MidPoint.X ? 1 : 2;
            // Set left and right offsets based on the end we are at
            Curve[] blankOffsets = END == 1 ?
              new Curve[] { OuterOffset, InnerOffset } :
              new Curve[] { InnerOffset, OuterOffset };

            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[0], topPlane, 0.01);
            pl0 = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[1], topPlane, 0.01);
            pl1 = res[0].PointA;

            pl0 = Plane.WorldXY.ClosestPoint(pl0);
            pl1 = Plane.WorldXY.ClosestPoint(pl1);

            var YDirection = END == 1 ?
              Vector3d.YAxis :
              -Vector3d.YAxis;

            var planeLine = new Line(pl0, pl1);

            double TO = Math.Abs(Math.Max(
              heightPlanes[0].Origin.Z,
              heightPlanes[1].Origin.Z));

            double T = heightPlanes[0].ClosestPoint(heightPlanes[1].Origin).DistanceTo(heightPlanes[1].Origin);

            double TU = beam.Width - T - TO;
            // Use top plane as new plane
            topPlane = new Plane(
              pl0,
              planeLine.Direction,
              -Vector3d.ZAxis);

            var sawLineLocal = planeLine;
            sawLineLocal.Transform(Transform.Translation(0, 0, -TO));

            Point3d s0, s1;
            topPlane.RemapToPlaneSpace(sawLineLocal.From, out s0);
            topPlane.RemapToPlaneSpace(sawLineLocal.To, out s1);

            sawLineLocal = new Line(s0, s1);
            double depth = topPlane.ClosestPoint(basePlane.Origin).DistanceTo(basePlane.Origin);
            depth = Math.Min(118, depth);

            // Top sawing
            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[0], basePlane, 0.01);
            pl0 = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[1], basePlane, 0.01);
            pl1 = res[0].PointA;
            var sawLine = new Line(pl0, pl1);

            var tenOp = new Tenon(name);
            tenOp.PlaneLine = planeLine;
            tenOp.LocalSawLine = sawLineLocal;
            tenOp.SawLine = sawLine;
            tenOp.DoOutline = doOutline;
            tenOp.DoSideCuts = doSideCuts;
            tenOp.Depth = depth;
            tenOp.T = T;
            tenOp.TO = TO;
            tenOp.TU = TU;
            tenOp.OutlineRadius = 8.0;

            // Only get width planes if necessary
            if (doOutline || doSideCuts)
            {
                widthPlanes[0] = adTenon.GetPlane("WidthPlane0");
                widthPlanes[1] = adTenon.GetPlane("WidthPlane1");
                widthPlanes[0].Transform(World2Local);
                widthPlanes[1].Transform(World2Local);

                widthPlanes = (widthPlanes[0].Origin - widthPlanes[1].Origin) * YDirection < 0 ?
                  new Plane[] { widthPlanes[0], widthPlanes[1] } :
                  new Plane[] { widthPlanes[1], widthPlanes[0] };
            }

            if (doOutline)
            {
                var pts = new Point3d[4];
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[0], heightPlanes[0], out pts[0]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[1], heightPlanes[0], out pts[1]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[1], heightPlanes[1], out pts[2]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[0], heightPlanes[1], out pts[3]);

                Plane outlinePlane = new Plane(pts[1], pts[0], pts[2]);
                if (outlinePlane.ZAxis * topPlane.ZAxis > 0)
                    Array.Reverse(pts);

                //if (END == 2)
                //  Array.Reverse(pts);

                for (int i = 0; i < pts.Length; ++i)
                    topPlane.RemapToPlaneSpace(pts[i], out pts[i]);

                var outlinePoly = MakeSlotPolyline(pts, tenOp.OutlineRadius, true, 1);


                tenOp.Outline = outlinePoly;
            }

            if (doSideCuts)
            {
                for (int i = 0; i < 2; ++i)
                {
                    var pts = new Point3d[3];
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[i], Plane.WorldXY, out pts[0]);
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(basePlane, widthPlanes[i], Plane.WorldXY, out pts[1]);
                    res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[i], basePlane, 0.01);
                    pts[2] = res[0].PointA;

                    tenOp.SideCuts[i] = new Polyline(pts);
                }
            }

            if (END == 1)
            {
                wp.E1.Operations.Add(tenOp);
            }
            else
            {
                wp.E2.Operations.Add(tenOp);
            }
        }

        void ConstructTenonOutline(object obj, string name, Workpiece wp, Beam beam)
        {
            var adTenon = obj as ArchivableDictionary;

            var basePlane = adTenon.GetPlane("BasePlane");
            var topPlane = adTenon.GetPlane("TopPlane");

            var widthPlanes = new Plane[2];


            var heightPlanes = new Plane[2];
            heightPlanes[0] = adTenon.GetPlane("HeightPlane0");
            heightPlanes[1] = adTenon.GetPlane("HeightPlane1");

            // Transform everything
            basePlane.Transform(World2Local);
            topPlane.Transform(World2Local);

            heightPlanes[0].Transform(World2Local);
            heightPlanes[1].Transform(World2Local);

            Point3d pl0, pl1;

            var END = topPlane.Origin.X > MidPoint.X ? 1 : 2;
            // Set left and right offsets based on the end we are at
            Curve[] blankOffsets = END == 1 ?
              new Curve[] { OuterOffset, InnerOffset } :
              new Curve[] { InnerOffset, OuterOffset };

            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[0], topPlane, 0.01);
            pl0 = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(blankOffsets[1], topPlane, 0.01);
            pl1 = res[0].PointA;

            pl0 = Plane.WorldXY.ClosestPoint(pl0);
            pl1 = Plane.WorldXY.ClosestPoint(pl1);

            var YDirection = END == 1 ?
              Vector3d.YAxis :
              -Vector3d.YAxis;

            var planeLine = new Line(pl0, pl1);

            // Use top plane as new plane
            topPlane = new Plane(
              pl0,
              planeLine.Direction,
              -Vector3d.ZAxis);

            double depth = topPlane.ClosestPoint(basePlane.Origin).DistanceTo(basePlane.Origin);
            depth = Math.Min(depth, 118);


            var tenOp = new TenonOutline(name);
            tenOp.PlaneLine = planeLine;
            tenOp.Depth = depth;
            tenOp.OutlineRadius = 8.0;

            widthPlanes[0] = adTenon.GetPlane("WidthPlane0");
            widthPlanes[1] = adTenon.GetPlane("WidthPlane1");
            widthPlanes[0].Transform(World2Local);
            widthPlanes[1].Transform(World2Local);

            widthPlanes = (widthPlanes[0].Origin - widthPlanes[1].Origin) * YDirection < 0 ?
              new Plane[] { widthPlanes[0], widthPlanes[1] } :
              new Plane[] { widthPlanes[1], widthPlanes[0] };

            var pts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[0], heightPlanes[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[1], heightPlanes[0], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[1], heightPlanes[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, widthPlanes[0], heightPlanes[1], out pts[3]);

            Plane outlinePlane = new Plane(pts[1], pts[0], pts[2]);
            if (outlinePlane.ZAxis * topPlane.ZAxis > 0)
                Array.Reverse(pts);

            for (int i = 0; i < pts.Length; ++i)
                topPlane.RemapToPlaneSpace(pts[i], out pts[i]);

            var outlinePoly = MakeSlotPolyline(pts, tenOp.OutlineRadius, true, 1);

            tenOp.Outline = outlinePoly;

            if (END == 1)
            {
                wp.E1.Operations.Add(tenOp);
            }
            else
            {
                wp.E2.Operations.Add(tenOp);
            }
        }

        void ConstructSillTenonSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            //var sidePlane = (Plane) slotDict["SidePlane"];
            //var outsidePlane = (Plane) slotDict["OutsidePlane"];
            var slotPlane = (Plane)slotDict["SlotPlane"];

            Plane bottomPlane = Plane.Unset;
            if (!slotDict.TryGetPlane("EndPlane", out bottomPlane))
                bottomPlane = Plane.Unset;

            var platePlanes = new Plane[2];
            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");

            var platePlane = new Plane((platePlanes[0].Origin + platePlanes[1].Origin) / 2, platePlanes[0].XAxis, platePlanes[0].YAxis);

            var endPlane0 = (Plane)slotDict["TenonSide0"];
            var endPlane1 = (Plane)slotDict["TenonSide1"];

            var plateThickness = 20.85;
            if (!slotDict.TryGetDouble("PlateThickness", out plateThickness))
                plateThickness = 20.85;

            var depth = slotDict.GetDouble("Depth");

            bottomPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane0.Transform(World2Local);
            endPlane1.Transform(World2Local);

            var YDirection = slotPlane.Origin.X > MidPoint.X ? Vector3d.YAxis : -Vector3d.YAxis;
            var XDirection = slotPlane.Origin.X > MidPoint.X ? -Vector3d.XAxis : Vector3d.XAxis;

            int wpSide = slotPlane.Origin.X > MidPoint.X ? 0 : 1;

            if (endPlane0.ZAxis * YDirection > 0)
            {
                endPlane0 = new Plane(endPlane0.Origin, -endPlane0.XAxis, endPlane0.YAxis);
            }

            if (bottomPlane == Plane.Unset)
                bottomPlane = new Plane(slotPlane.Origin - slotPlane.ZAxis * depth, slotPlane.XAxis, slotPlane.YAxis);

            //var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane0.ZAxis;

            Point3d origin_top, origin_btm;
            Point3d end0, end1;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0, out end0);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1, out end1);

            slotPlane = new Plane(end0, platePlane.ZAxis, end1 - end0);

            if (slotPlane.ZAxis * XDirection < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, slot_length = 0, slot_angle = 0;
            Point3d[] outline;


            slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
            slot_length = end0.DistanceTo(end1);

            double hThick = plateThickness / 2;
            outline = new Point3d[]{
      slotPlane.PointAt(hThick, 0),
      slotPlane.PointAt(-hThick, 0),
      slotPlane.PointAt(-hThick, slot_length),
      slotPlane.PointAt(hThick, slot_length)};


            // (Check if it works on the bottom)
            //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

            depth = bottomPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);
            depthRough = depth;
            depthFinish = depth;


            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * XDirection < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            xaxis = slotPlane.XAxis * YDirection < 0 ? -slotPlane.XAxis : slotPlane.XAxis;

            var slotOp = new EndSlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + xaxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;
            slotOp.OperationName = "TAPHUL";

            switch (wpSide)
            {
                case (1): // End2
                    wp.E2.Operations.Add(slotOp);
                    break;
                default: // End1
                    wp.E1.Operations.Add(slotOp);
                    break;
            }
        }

        void ConstructSillJoistSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            //var sidePlane = (Plane) slotDict["SidePlane"];
            //var outsidePlane = (Plane) slotDict["OutsidePlane"];
            var slotPlane = (Plane)slotDict["SlotPlane"];

            Plane bottomPlane = Plane.Unset;
            if (!slotDict.TryGetPlane("EndPlane", out bottomPlane))
                bottomPlane = Plane.Unset;

            Plane shoulderPlane = bottomPlane;
            if (!slotDict.TryGetPlane("ShoulderPlane", out shoulderPlane))
                shoulderPlane = bottomPlane;

            var platePlanes = new Plane[2];
            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");

            var platePlane = new Plane((platePlanes[0].Origin + platePlanes[1].Origin) / 2, platePlanes[0].XAxis, platePlanes[0].YAxis);

            var endPlane0 = (Plane)slotDict["TenonSide0"];
            var endPlane1 = (Plane)slotDict["TenonSide1"];

            Plane endPlane0Extra = Plane.Unset;
            Plane endPlane1Extra = Plane.Unset;
            foreach (var key in slotDict.Keys)
            {
                Print("KEY {0}", key);
                if (key == "Tango0")
                {
                    Print("Found Tango0");
                    endPlane0Extra = (Plane)slotDict[key];
                }
                else if (key == "Tango1")
                {
                    Print("Found Tango1");
                    endPlane1Extra = (Plane)slotDict[key];
                }
            }


            //var endPlane0Extra = (Plane) slotDict["Tango0"];
            //var endPlane1Extra = (Plane) slotDict["Tango1"];


            var plateThickness = 20.85;
            if (!slotDict.TryGetDouble("PlateThickness", out plateThickness))
                plateThickness = 20.85;

            //double depth = 30;
            var depth = slotDict.GetDouble("Depth");

            bottomPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane0.Transform(World2Local);
            endPlane1.Transform(World2Local);
            endPlane0Extra.Transform(World2Local);
            endPlane1Extra.Transform(World2Local);
            shoulderPlane.Transform(World2Local);

            var YDirection = slotPlane.Origin.X > MidPoint.X ? Vector3d.YAxis : -Vector3d.YAxis;
            var XDirection = slotPlane.Origin.X > MidPoint.X ? -Vector3d.XAxis : Vector3d.XAxis;

            int wpSide = slotPlane.ZAxis * Vector3d.YAxis < 0 ? 0 : 1;
            wpSide = 0;

            slotPlane.Transform(Transform.Translation(slotPlane.ZAxis * 20));

            if (endPlane0.ZAxis * YDirection > 0)
            {
                endPlane0 = new Plane(endPlane0.Origin, -endPlane0.XAxis, endPlane0.YAxis);
            }

            if (bottomPlane == Plane.Unset)
                bottomPlane = new Plane(slotPlane.Origin - slotPlane.ZAxis * depth, slotPlane.XAxis, slotPlane.YAxis);

            //var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane0.ZAxis;

            Point3d origin_top, origin_btm;
            Point3d end0, end1;
            Point3d end0Extra, end1Extra;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0, out end0);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1, out end1);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0Extra, out end0Extra);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1Extra, out end1Extra);

            slotPlane = new Plane(end0, platePlane.ZAxis, end1 - end0);

            if (slotPlane.ZAxis * XDirection < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, slot_length = 0, slot_angle = 0;
            Point3d[] outline;
            Point3d[] outlineExtra;
            Line breakOutLine;


            slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
            slot_length = end0.DistanceTo(end1);
            double slot_lengthExtra = end0Extra.DistanceTo(end1Extra);

            double hThick = plateThickness / 2;
            outline = new Point3d[]{
              slotPlane.PointAt(hThick, 0),
              slotPlane.PointAt(-hThick, 0),
              slotPlane.PointAt(-hThick, slot_length),
              slotPlane.PointAt(hThick, slot_length)};

                    outlineExtra = new Point3d[]{
              slotPlane.PointAt(hThick, 0),
              slotPlane.PointAt(-hThick, 0),
              slotPlane.PointAt(-hThick, slot_lengthExtra),
              slotPlane.PointAt(hThick, slot_lengthExtra)
              };
            breakOutLine = new Line(
              slotPlane.PointAt(-hThick, slot_lengthExtra),
              slotPlane.PointAt(-hThick, slot_length - 20)
              );


            // (Check if it works on the bottom)
            //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

            depth = bottomPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);
            var depthExtra = shoulderPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);

            depthRough = depth;
            depthFinish = depth;


            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * XDirection < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);
            var outlineExtraPoly = MakeSlotPolyline(outlineExtra.ToList(), 8.0, true, 1);

            xaxis = slotPlane.XAxis * Vector3d.XAxis > 0 ? -slotPlane.XAxis : slotPlane.XAxis;

            var slotOp = new SillSlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + xaxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.ExtraOutline = outlineExtraPoly;
            slotOp.DoExtra = true;
            slotOp.ExtraDepth = depthExtra;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;
            slotOp.OperationName = "TAPHUL";
            slotOp.ExtraBreakOut = breakOutLine;

            switch (wpSide)
            {
                case (1): // End2
                    wp.Outside.Operations.Add(slotOp);
                    break;
                default: // End1
                    wp.Inside.Operations.Add(slotOp);
                    break;
            }
        }

        void ConstructSillTenon(object obj, string name, Workpiece wp, Beam beam)
        {
            Print("Entering ConstructSillTenon()");
            var slotDict = obj as ArchivableDictionary;
            var slotPlane = (Plane)slotDict["SlotPlane"];

            Plane bottomPlane = Plane.Unset;
            if (!slotDict.TryGetPlane("EndPlane", out bottomPlane))
                bottomPlane = Plane.Unset;

            var platePlanes = new Plane[2];
            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");

            var platePlane = new Plane((platePlanes[0].Origin + platePlanes[1].Origin) / 2, platePlanes[0].XAxis, platePlanes[0].YAxis);

            platePlanes[0].Transform(World2Local);
            platePlanes[1].Transform(World2Local);

            var endPlane0 = (Plane)slotDict["TenonSide0"];
            var endPlane1 = (Plane)slotDict["TenonSide1"];

            var plateThickness = 20.85;
            if (!slotDict.TryGetDouble("PlateThickness", out plateThickness))
                plateThickness = 20.85;

            var depth = slotDict.GetDouble("Depth");

            bottomPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane0.Transform(World2Local);
            endPlane1.Transform(World2Local);

            var YDirection = slotPlane.Origin.X > MidPoint.X ? Vector3d.YAxis : -Vector3d.YAxis;
            var XDirection = slotPlane.Origin.X > MidPoint.X ? -Vector3d.XAxis : Vector3d.XAxis;

            int wpSide = slotPlane.Origin.X > MidPoint.X ? 0 : 1;

            if (bottomPlane == Plane.Unset)
                bottomPlane = new Plane(slotPlane.Origin - slotPlane.ZAxis * depth, slotPlane.XAxis, slotPlane.YAxis);

            // ******************
            // SAWING
            // ******************

            Point3d saw0, saw1;
            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, bottomPlane, 0.01);
            if (res == null || res.Count < 1) throw new Exception("Sawing couldn't intersect InnerOffset.");

            saw0 = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, bottomPlane, 0.01);
            if (res == null || res.Count < 1) throw new Exception("Sawing couldn't intersect InnerOffset.");

            saw1 = res[0].PointA;

            Line sawLine;
            if ((saw1 - saw0) * YDirection < 0)
                sawLine = new Line(saw0, saw1);
            else
                sawLine = new Line(saw1, saw0);

            var sawOpTop = new Sawing(string.Format("TenonSawingTop - {0}", name));
            sawOpTop.Normal = bottomPlane.ZAxis * XDirection < 0 ? bottomPlane.ZAxis : -bottomPlane.ZAxis;
            sawOpTop.Path = sawLine;

            var sawOpEnd = new Sawing(string.Format("TenonSawingEnd - {0}", name));
            sawOpEnd.Normal = Vector3d.ZAxis;
            sawOpEnd.Path = sawLine;

            // ***************
            // SIDE CLEARING
            // ***************
            var sideOp0 = new SideClearing(string.Format("SideClearing1 - {0}", name));

            var pts = new Point3d[3];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, platePlanes[0], Plane.WorldXY, out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[0], Plane.WorldXY, out pts[1]);
            pts[0] = pts[1] + platePlanes[0].ZAxis * 100;
            //pts[2] = pts[1] + bottomPlane.ZAxis * 100;

            sideOp0.Path = new Polyline(pts);

            var sideOp1 = new SideClearing(string.Format("SideClearing2 - {0}", name));

            pts = new Point3d[3];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, platePlanes[1], Plane.WorldXY, out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[1], Plane.WorldXY, out pts[1]);
            pts[0] = pts[1] - platePlanes[1].ZAxis * 100;
            //pts[2] = pts[1] + bottomPlane.ZAxis * 100;

            sideOp1.Path = new Polyline(pts);

            // ************
            // TENON
            // ************

            var tenonOp = new TenonMachining(string.Format("TenonMachining - {0}", name));

            if (endPlane0.ZAxis * YDirection > 0)
            {
                endPlane0 = new Plane(endPlane0.Origin, -endPlane0.XAxis, endPlane0.YAxis);
            }

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane0.ZAxis;

            Point3d origin_top, origin_btm;
            Point3d end0, end1;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane0, out end0);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, slotPlane, endPlane1, out end1);

            slotPlane = new Plane(end0, platePlane.ZAxis, end1 - end0);

            if (slotPlane.ZAxis * XDirection < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, tenon_width = 0, slot_angle = 0;
            Point3d[] outline;


            slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
            tenon_width = end0.DistanceTo(end1);

            double hThick = plateThickness / 2;
            outline = new Point3d[]{
      slotPlane.PointAt(hThick, 0),
      slotPlane.PointAt(-hThick, 0),
      slotPlane.PointAt(-hThick, tenon_width),
      slotPlane.PointAt(hThick, tenon_width)};


            // (Check if it works on the bottom)
            //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

            depth = bottomPlane.ClosestPoint(slotPlane.Origin).DistanceTo(slotPlane.Origin);
            depthRough = depth;
            depthFinish = depth;


            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * XDirection < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            xaxis = slotPlane.XAxis * XDirection < 0 ? -slotPlane.XAxis : slotPlane.XAxis;

            tenonOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            tenonOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + xaxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            tenonOp.Angle = angle;
            tenonOp.Plane = slotPlane;
            tenonOp.Outline = outlinePoly;
            tenonOp.Radius = 8;
            tenonOp.Depth = depthFinish;
            tenonOp.Depth0 = depthRough;
            tenonOp.OperationName = "TAP";

            BeamSide beam_side = wpSide > 0 ? wp.E2 : wp.E1;

            beam_side.Operations.Add(sawOpTop);
            beam_side.Operations.Add(sawOpEnd);
            beam_side.Operations.Add(tenonOp);
            beam_side.Operations.Add(sideOp0);
            beam_side.Operations.Add(sideOp1);
        }

        void ConstructTenonSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            var platePlanes = new Plane[2];
            var sidePlanes = new Plane[2];
            var endPlane = slotDict.GetPlane("EndPlane");
            var slotPlane = slotDict.GetPlane("SlotPlane");
            double depth = slotDict.GetDouble("Depth", 0);

            platePlanes[0] = slotDict.GetPlane("PlateFace0");
            platePlanes[1] = slotDict.GetPlane("PlateFace1");
            sidePlanes[0] = slotDict.GetPlane("TenonSide0");
            sidePlanes[1] = slotDict.GetPlane("TenonSide1");

            endPlane.Transform(World2Local);
            slotPlane.Transform(World2Local);
            platePlanes[0].Transform(World2Local);
            platePlanes[1].Transform(World2Local);
            sidePlanes[0].Transform(World2Local);
            sidePlanes[1].Transform(World2Local);

            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            var outlinePts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[0], sidePlanes[1], out outlinePts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[1], sidePlanes[1], out outlinePts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[1], sidePlanes[0], out outlinePts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, platePlanes[0], sidePlanes[0], out outlinePts[3]);


            var worldFinishProj = Plane.WorldXY.ProjectAlongVector(endPlane.ZAxis);
            var finishProj = slotPlane.ProjectAlongVector(slotPlane.ZAxis);

            var outlinePoly = new Polyline(outlinePts);

            outlinePoly.Transform(finishProj);

            double minZ = double.MaxValue;
            int minIndex = 0;
            for (int i = 0; i < outlinePoly.Count; ++i)
            {
                if (outlinePoly[i].Z < minZ)
                {
                    minIndex = i;
                    minZ = outlinePoly[i].Z;
                }
            }
            //endPlane.Origin = endPlane.Origin - endPlane.ZAxis * depth;

            var origin = outlinePoly[minIndex];
            origin.Transform(worldFinishProj);

            //slotPlane = endPlane;
            slotPlane.Origin = origin;


            Plane outlinePlane;
            outlinePoly.ToNurbsCurve().TryGetPlane(out outlinePlane);
            if (outlinePlane.ZAxis * slotPlane.ZAxis < 0) outlinePoly.Reverse();

            outlinePoly = MakeSlotPolyline(outlinePoly, 8.0, true);

            depth = origin.DistanceTo(endPlane.ClosestPoint(origin));

            double angle;
            if (endPlane.ZAxis * Vector3d.ZAxis > 0)
                GluLamb.Utility.AlignedPlane(origin, slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(origin, -slotPlane.ZAxis, out slotPlane, out angle);


            var slotOp = new SlotMachining(name);
            slotOp.OverridePlane = true;
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);
            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depth;
            slotOp.Depth0 = depth - 2;

            wp.Top.Operations.Add(slotOp);
            /*

            if (endPlane.Origin.X < MidPoint.X)
              wp.E2.Operations.Add(slotOp);
            else
              wp.E1.Operations.Add(slotOp);
            */
        }

        void ConstructSplitCut2(object obj, string name, Workpiece wp, Beam beam)
        {
            var splitDict = obj as ArchivableDictionary;
            var inner0 = splitDict.GetPlane("Inner0");
            var inner1 = splitDict.GetPlane("Outer0");
            var outer0 = splitDict.GetPlane("Inner1");
            var outer1 = splitDict.GetPlane("Outer1");
            var platePlane = splitDict.GetPlane("PlatePlane");

            inner0.Transform(World2Local);
            inner1.Transform(World2Local);
            outer0.Transform(World2Local);
            outer1.Transform(World2Local);
            platePlane.Transform(World2Local);

            var inner = Math.Abs(inner0.ZAxis * Vector3d.ZAxis) > Math.Abs(inner1.ZAxis * Vector3d.ZAxis) ?
              new Plane[] { inner0, inner1 } : new Plane[] { inner1, inner0 };
            var outer = Math.Abs(outer0.ZAxis * Vector3d.ZAxis) > Math.Abs(outer1.ZAxis * Vector3d.ZAxis) ?
              new Plane[] { outer0, outer1 } : new Plane[] { outer1, outer0 };

            ConstructEndCut(inner[0], name + "Inner", wp, beam, false);
            ConstructEndCut(outer[0], name + "Outer", wp, beam, false);

            Print("Creating split cut...");
            int EndIndex = inner[0].Origin.X < MidPoint.X ? 1 : 0;

            var XDirection = EndIndex > 0 ? -Vector3d.XAxis : Vector3d.XAxis;
            var YDirection = EndIndex > 0 ? -Vector3d.YAxis : Vector3d.YAxis;

            // *****************************
            // Find end plane for undercuts
            // *****************************
            Plane p0 = inner[0], p1 = outer[0];
            var BottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            double dot0 = p0.ZAxis * -Vector3d.ZAxis, dot1 = p1.ZAxis * -Vector3d.ZAxis;

            if (dot0 < 0 && dot1 < 0)
            {
                if (dot0 < dot1)
                    p0 = BottomPlane;
                else
                    p1 = BottomPlane;
            }
            else if (dot0 > 0 && dot1 > 0)
            {
                if (dot0 > dot1)
                    p0 = Plane.WorldXY;
                else
                    p1 = Plane.WorldXY;
            }
            Line xLine;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(p0, p1, out xLine);
            EndPlanes[EndIndex] = new Plane(xLine.From, xLine.Direction, Vector3d.ZAxis);


            var EndPlane = EndPlanes[EndIndex];
            EndPlane.Origin = EndPlane.Origin + XDirection * 40;

            var planes0 = new Plane[] { inner[1], outer[1] };
            var planes1 = new Plane[] { outer[0], inner[0] };

            // Handle the splits
            Point3d temp;
            for (int i = 0; i < 2; ++i)
            {
                var plane = planes0[i];
                //var xaxis = Vector3d.CrossProduct(platePlane.ZAxis, Vector3d.CrossProduct(platePlane.ZAxis, plane.XAxis));

                Vector3d xaxis;
                if (false)
                {
                    xaxis = plane.Project(platePlane.ZAxis);
                    xaxis.Unitize();
                }
                else
                    xaxis = plane.XAxis;

                var inV = platePlane.ClosestPoint(plane.Origin) - plane.Origin; inV.Unitize();

                if (xaxis * YDirection < 0)
                    xaxis.Reverse();

                var yaxis = Vector3d.CrossProduct(plane.ZAxis, xaxis);
                yaxis.Unitize();
                if (yaxis * Vector3d.ZAxis > 0) yaxis.Reverse();

                plane = new Plane(plane.Origin, xaxis, yaxis);

                var outPlane = new Plane(platePlane.Origin - inV * 100, platePlane.XAxis, platePlane.YAxis);

                var pts = new Point3d[4];

                var isUnder = plane.Origin.Z < -30;

                var localPlatePlane = new Plane(platePlane.Origin - inV * 8, platePlane.XAxis, platePlane.YAxis);

                var srfPlane = plane.Origin.Z > -30 ? Plane.WorldXY : BottomPlane;
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outPlane, planes1[i], plane, out pts[0]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(outPlane, srfPlane, plane, out pts[1]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(localPlatePlane, srfPlane, plane, out pts[2]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(localPlatePlane, planes1[i], plane, out pts[3]);

                var locPts = new Point3d[4];
                double maxX = double.MinValue, minX = double.MaxValue,
                  maxY = double.MinValue, minY = double.MaxValue;
                for (int j = 0; j < 4; ++j)
                {
                    plane.RemapToPlaneSpace(pts[j], out locPts[j]);
                    maxX = Math.Max(maxX, locPts[j].X);
                    minX = Math.Min(minX, locPts[j].X);
                    maxY = Math.Max(maxY, locPts[j].Y);
                    minY = Math.Min(minY, locPts[j].Y);
                }

                pts[0] = plane.PointAt(minX, maxY);
                pts[1] = plane.PointAt(minX, minY);
                pts[2] = plane.PointAt(maxX, minY);
                pts[3] = plane.PointAt(maxX, maxY);


                //pts[0].Transform(Local2Plane);
                //pts[1].Transform(Local2Plane);
                //pts[2].Transform(Local2Plane);
                //pts[3].Transform(Local2Plane);
                //debug.Add(new BakeFeature("SPLIT", new List<object>{pts[1], pts[2]}));


                Line line;
                line = new Line(pts[1], pts[2]);

                if (line.Direction * YDirection < 0)
                    line.Flip();

                double depth = maxY - minY;

                if (plane.ZAxis * Vector3d.ZAxis > Math.Cos(RhinoMath.ToRadians(45)) && false)
                {
                    Log.Add(string.Format("WARNING :: {0} SplitCut ({1}) is from the side.", CurrentName, name));
                    line = new Line(pts[0], pts[1]);
                    depth = maxX - minX;

                    Vector3d tv;
                    tv = xaxis;
                    xaxis = yaxis;
                    yaxis = tv;
                }

                var tiltVec = Vector3d.CrossProduct(line.Direction, Vector3d.CrossProduct(line.Direction, Vector3d.ZAxis));


                //int sign = yaxis * tiltVec > 0 ? 1 : -1;
                int sign = yaxis * XDirection > 0 ? 1 : -1;
                var zaxis = Vector3d.CrossProduct(xaxis, Vector3d.CrossProduct(xaxis, Vector3d.ZAxis));
                //var zaxis = Vector3d.CrossProduct(xaxis, Vector3d.ZAxis);


                var lmtest = new LineMachining(string.Format("{0}_LM{1}", name, i + 1));
                //lmtext.PlaneX = new Line(plane.Origin, Plane.WorldXY.Project(xaxis) * 100);
                lmtest.PlaneX = line;
                lmtest.Path = line;
                lmtest.Tilt = Vector3d.VectorAngle(yaxis, zaxis) * sign;
                lmtest.Depth = depth;
                lmtest.CheckPlane = plane;

                if (plane.Origin.X < MidPoint.X)
                {
                    wp.E2.Operations.Add(lmtest);
                }
                else
                {
                    wp.E1.Operations.Add(lmtest);
                }

                continue;

                /*
                      var projPlate = platePlane.ProjectAlongVector(xaxis);

                      Point3d end = plane.Origin, start = plane.Origin;
                      end.Transform(projPlate);

                      Transform projXY;
                      if (plane.ZAxis * Vector3d.ZAxis > 0)
                        projXY = Plane.WorldXY.ProjectAlongVector(yaxis);
                      else
                        projXY = BottomPlane.ProjectAlongVector(yaxis);

                      var end2 = end;
                      end2.Transform(projXY);
                      if (end2.Z > start.Z)
                      {
                        start = start + (end2 - end);
                        end = end2;
                      }

                      Transform projPlane;
                      projPlane = planes1[i].ProjectAlongVector(yaxis);

                      Point3d startP = start, endP = end;
                      startP.Transform(projPlane);
                      endP.Transform(projPlane);
                      double dStart = start.DistanceTo(startP);
                      double dEnd = end.DistanceTo(endP);

                      double depth = Math.Max(dStart, dEnd);

                      // *************

                      var lineV = end - start; lineV.Unitize();
                      Line line = new Line(start - lineV * 30, end);

                      if (lineV * YDirection < 0)
                      {
                        line.Flip();
                        yaxis = Vector3d.CrossProduct(plane.ZAxis, -xaxis);
                        yaxis.Unitize();
                        if (yaxis * Vector3d.ZAxis > 0) yaxis.Reverse();
                      }

                      int sign = yaxis * XDirection > 0 ? 1 : -1;

                      //if (startP.Z > start.Z)

                      if (Vector3d.CrossProduct(xaxis, yaxis) * Vector3d.ZAxis > 0)
                      {
                        Vector3d translate;
                        if (dStart < dEnd)
                          translate = startP - start;
                        else
                          translate = endP - end;

                        line.Transform(Transform.Translation(translate));
                        //yaxis.Reverse();
                        //sign = -sign;
                      }




                      var zaxis = Vector3d.CrossProduct(xaxis, Vector3d.CrossProduct(xaxis, Vector3d.ZAxis));

                */
            }
        }

        void ConstructSplitCut(object obj, string name, Workpiece wp, Beam beam)
        {
            var splitDict = obj as ArchivableDictionary;
            var inner0 = splitDict.GetPlane("Inner0");
            var inner1 = splitDict.GetPlane("Outer0");
            var outer0 = splitDict.GetPlane("Inner1");
            var outer1 = splitDict.GetPlane("Outer1");
            var platePlane = splitDict.GetPlane("PlatePlane");

            inner0.Transform(World2Local);
            inner1.Transform(World2Local);
            outer0.Transform(World2Local);
            outer1.Transform(World2Local);
            platePlane.Transform(World2Local);

            var inner = Math.Abs(inner0.ZAxis * Vector3d.ZAxis) > Math.Abs(inner1.ZAxis * Vector3d.ZAxis) ?
              new Plane[] { inner0, inner1 } : new Plane[] { inner1, inner0 };
            var outer = Math.Abs(outer0.ZAxis * Vector3d.ZAxis) > Math.Abs(outer1.ZAxis * Vector3d.ZAxis) ?
              new Plane[] { outer0, outer1 } : new Plane[] { outer1, outer0 };

            ConstructEndCut(inner[0], name + "Inner", wp, beam, false);
            ConstructEndCut(outer[0], name + "Outer", wp, beam, false);

            Print("Creating split cut...");
            int EndIndex = inner[0].Origin.X < MidPoint.X ? 1 : 0;

            var XDirection = EndIndex > 0 ? -Vector3d.XAxis : Vector3d.XAxis;
            var YDirection = EndIndex > 0 ? -Vector3d.YAxis : Vector3d.YAxis;

            // *****************************
            // Find end plane for undercuts
            // *****************************
            Plane p0 = inner[0], p1 = outer[0];
            var BottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            double dot0 = p0.ZAxis * -Vector3d.ZAxis, dot1 = p1.ZAxis * -Vector3d.ZAxis;

            if (dot0 < 0 && dot1 < 0)
            {
                if (dot0 < dot1)
                    p0 = BottomPlane;
                else
                    p1 = BottomPlane;
            }
            else if (dot0 > 0 && dot1 > 0)
            {
                if (dot0 > dot1)
                    p0 = Plane.WorldXY;
                else
                    p1 = Plane.WorldXY;
            }
            Line xLine;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(p0, p1, out xLine);
            EndPlanes[EndIndex] = new Plane(xLine.From, xLine.Direction, Vector3d.ZAxis);


            var EndPlane = EndPlanes[EndIndex];
            EndPlane.Origin = EndPlane.Origin + XDirection * 40;

            var planes0 = new Plane[] { inner[1], outer[1] };
            var planes1 = new Plane[] { outer[0], inner[0] };

            Point3d temp;
            for (int i = 0; i < 2; ++i)
            {
                var plane = planes0[i];
                //var xaxis = Vector3d.CrossProduct(platePlane.ZAxis, Vector3d.CrossProduct(platePlane.ZAxis, plane.XAxis));
                var xaxis = plane.Project(platePlane.ZAxis);
                xaxis.Unitize();
                if (xaxis * YDirection < 0)
                {
                    xaxis.Reverse();
                }

                var yaxis = Vector3d.CrossProduct(plane.ZAxis, xaxis);
                yaxis.Unitize();
                if (yaxis * Vector3d.ZAxis > 0) yaxis.Reverse();

                var projPlate = platePlane.ProjectAlongVector(xaxis);

                Point3d end = plane.Origin, start = plane.Origin;
                end.Transform(projPlate);

                Transform projXY;
                if (plane.ZAxis * Vector3d.ZAxis > 0)
                    projXY = Plane.WorldXY.ProjectAlongVector(yaxis);
                else
                    projXY = BottomPlane.ProjectAlongVector(yaxis);

                var end2 = end;
                end2.Transform(projXY);
                if (end2.Z > start.Z)
                {
                    start = start + (end2 - end);
                    end = end2;
                }

                Transform projPlane;
                projPlane = planes1[i].ProjectAlongVector(yaxis);

                Point3d startP = start, endP = end;
                startP.Transform(projPlane);
                endP.Transform(projPlane);
                double dStart = start.DistanceTo(startP);
                double dEnd = end.DistanceTo(endP);

                double depth = Math.Max(dStart, dEnd);

                // *************

                var lineV = end - start; lineV.Unitize();
                Line line = new Line(start - lineV * 30, end);

                if (lineV * YDirection < 0)
                {
                    line.Flip();
                    yaxis = Vector3d.CrossProduct(plane.ZAxis, -xaxis);
                    yaxis.Unitize();
                    if (yaxis * Vector3d.ZAxis > 0) yaxis.Reverse();
                }

                int sign = 1;
                sign = yaxis * XDirection > 0 ? 1 : -1;

                //if (startP.Z > start.Z)

                if (Vector3d.CrossProduct(xaxis, yaxis) * Vector3d.ZAxis > 0)
                {
                    Vector3d translate;
                    if (dStart < dEnd)
                        translate = startP - start;
                    else
                        translate = endP - end;

                    line.Transform(Transform.Translation(translate));
                    //yaxis.Reverse();
                    //sign = -sign;
                }




                var zaxis = Vector3d.CrossProduct(xaxis, Vector3d.CrossProduct(xaxis, Vector3d.ZAxis));

                var lmtest = new LineMachining(string.Format("{0}_LM{1}", name, i + 1));
                lmtest.Path = line;
                lmtest.Tilt = Vector3d.VectorAngle(yaxis, zaxis) * sign;
                lmtest.Depth = depth;
                lmtest.CheckPlane = plane;

                if (plane.Origin.X < MidPoint.X)
                {
                    wp.E2.Operations.Add(lmtest);
                }
                else
                {
                    wp.E1.Operations.Add(lmtest);
                }
                continue;


                //var binormal = Vector3d.CrossProduct(platePlane.ZAxis, plane.ZAxis);
                //plane = new Plane(plane.Origin, binormal, plane.XAxis);

                if (plane.XAxis * YDirection < 0)
                    plane.XAxis = -plane.XAxis;
                if (plane.YAxis * XDirection > 0)
                    plane.YAxis = -plane.YAxis;

                if (plane.ZAxis * Vector3d.ZAxis < 0)
                    projPlane = planes1[i].ProjectAlongVector(plane.YAxis);
                else
                    projPlane = Plane.WorldXY.ProjectAlongVector(plane.YAxis);

                temp = plane.Origin;
                temp.Transform(projPlane);
                plane.Origin = temp;



                /*
                var projBtm = BottomPlane.ProjectAlongVector(plane.YAxis);
                var projTop = Plane.WorldXY.ProjectAlongVector(plane.YAxis);

                if (plane.YAxis * YDirection < 0)
                  projTop = EndPlane.ProjectAlongVector(plane.YAxis);

                temp = plane.Origin;
                temp.Transform(projTop);
                //plane.Origin = temp;

                btm = plane.Origin;
                btm.Transform(projBtm);
          */
                end = plane.Origin;
                //end = platePlane.ClosestPoint(end);
                end.Transform(projPlate);

                //temp = end;
                //temp.Transform(projTop);

                Line XLine = new Line(plane.Origin, end);
                var lineDir = XLine.Direction; lineDir.Unitize();
                // *******************************************************
                // Tweak the dimensions of the cut to cover all geometry
                // *******************************************************

                XLine = new Line(XLine.From - lineDir * 3, XLine.To - lineDir * 10);

                if (XLine.Direction * YDirection < 0) XLine.Flip();

                Vector3d refVec = Vector3d.CrossProduct(Vector3d.ZAxis, -plane.XAxis);



                //if (plane.YAxis * midVec < 0) plane.YAxis = -plane.YAxis;


                //double tilt;
                //AlignedPlane(plane.Origin, plane.ZAxis, out plane, out tilt);




                var lm = new LineMachining(string.Format("{0}_LM{1}", name, i + 1));
                lm.Path = XLine;
                lm.Tilt = Vector3d.VectorAngle(plane.YAxis, -Vector3d.ZAxis) * sign;// *sign2;
                                                                                    //lm.Tilt = tilt;
                                                                                    //lm.Depth = btm.DistanceTo(plane.Origin);
                lm.Depth = 50;
                lm.CheckPlane = plane;

                if (plane.Origin.X < MidPoint.X)
                {
                    wp.E2.Operations.Add(lm);
                }
                else
                {
                    wp.E1.Operations.Add(lm);
                }

                refVec.Transform(Local2Plane);
                plane.Transform(Local2Plane);
                debug.Add(new BakeFeature("MissyMiss", new List<object> { new Line(plane.Origin, refVec * 300) }));

            }

        }

        void deprec_MakePerpendicularSlot(Plane platePlane, Plane endPlane, double plateThickness, Beam beam, out Polyline outline, out Plane slotPlane)
        {
            var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);

            double h = plateThickness * 0.5;

            var plateFaces = new Plane[]{
      new Plane(platePlane.Origin + platePlane.ZAxis * h, platePlane.XAxis, platePlane.YAxis),
      new Plane(platePlane.Origin - platePlane.ZAxis * h, platePlane.XAxis, platePlane.YAxis)};

            var pts = new Point3d[4];

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(Plane.WorldXY, endPlane, plateFaces[0], out pts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(Plane.WorldXY, endPlane, plateFaces[1], out pts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, endPlane, plateFaces[1], out pts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, endPlane, plateFaces[0], out pts[3]);

            slotPlane = new Plane(pts[0], pts[1] - pts[0], pts[3] - pts[0]);

            outline = new Polyline(pts);
            outline.ToNurbsCurve().TryGetPlane(out slotPlane);
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                outline.Reverse();
        }

        void ConstructRoughSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            var sidePlane = (Plane)slotDict["SidePlane"];
            var outsidePlane = (Plane)slotDict["OutsidePlane"];
            var platePlane = (Plane)slotDict["PlatePlane"];
            var endPlane = (Plane)slotDict["EndPlane"];
            var plateThickness = slotDict.GetDouble("PlateThickness");

            sidePlane.Transform(World2Local);
            outsidePlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane.Transform(World2Local);

            if (endPlane.ZAxis * (MidPoint - endPlane.Origin) > 0)
            {
                endPlane = new Plane(endPlane.Origin, -endPlane.XAxis, endPlane.YAxis);
            }

            var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = Plane.WorldXY.Project(endPlane.ZAxis);
            var yaxis = Vector3d.CrossProduct(platePlane.ZAxis, xaxis);
            Point3d origin_top, origin_btm;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, Plane.WorldXY, endPlane, out origin_top);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, bottomPlane, endPlane, out origin_btm);

            Plane slotPlane = new Plane(origin_top, platePlane.ZAxis, xaxis);
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            Point3d local_btm;
            slotPlane.RemapToPlaneSpace(origin_btm, out local_btm);

            if (local_btm.Y > 0)
                slotPlane.Origin = slotPlane.ClosestPoint(origin_btm);

            double hThick = plateThickness / 2;
            var outline = new Point3d[]{
      slotPlane.PointAt(hThick, 8),
      slotPlane.PointAt(-hThick, 8),
      slotPlane.PointAt(-hThick, 240),
      slotPlane.PointAt(hThick, 240)};

            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, false, 0);

            // (Check if it works on the bottom)
            // outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));


            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(projZAxis, Vector3d.ZAxis);
            alignedXAxis.Unitize();

            // Angle between the slot plane normal and the Z-axis
            var angle2 = Math.Acos(Math.Abs(slotPlane.ZAxis * Vector3d.ZAxis));

            double angle;
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);



            var slotOpRough = new SlotMachining(name);
            slotOpRough.OverridePlane = true;
            //slotOpRough.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOpRough.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);
            //slotOpRough.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOpRough.Angle = angle;
            slotOpRough.Plane = slotPlane;
            slotOpRough.Outline = outlinePoly;
            slotOpRough.Rough = true;
            slotOpRough.Radius = 8;
            slotOpRough.Depth = beam.Width / Math.Abs(Vector3d.ZAxis * slotPlane.ZAxis) + (Math.Tan(angle2) * 8 + 1);
            slotOpRough.Depth0 = beam.Width / Math.Abs(Vector3d.ZAxis * slotPlane.ZAxis) - 3;

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotOpRough);
            else
                wp.E1.Operations.Add(slotOpRough);
        }

        void ConstructFinishSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            //var sidePlane = (Plane) slotDict["SidePlane"];
            //var outsidePlane = (Plane) slotDict["OutsidePlane"];
            var platePlane = (Plane)slotDict["PlatePlane"];
            var endPlane = (Plane)slotDict["EndPlane"];
            var plateThickness = slotDict.GetDouble("PlateThickness");

            //sidePlane.Transform(World2Local);
            //outsidePlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane.Transform(World2Local);

            if (endPlane.ZAxis * (MidPoint - endPlane.Origin) > 0)
            {
                endPlane = new Plane(endPlane.Origin, -endPlane.XAxis, endPlane.YAxis);
            }

            var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            //var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);
            var yaxis = endPlane.ZAxis;

            Point3d origin_top, origin_btm;

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, Plane.WorldXY, endPlane, out origin_top);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(platePlane, bottomPlane, endPlane, out origin_btm);

            Plane slotPlane = new Plane(origin_top, xaxis, yaxis);

            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                slotPlane.XAxis = -slotPlane.XAxis;

            // ***********
            // VARIABLES
            // ***********

            double depthRough = 0, depthFinish = 0, slot_length = 0, slot_angle = 0;
            Point3d[] outline;

            // If slot is too steep
            if (Math.Abs(endPlane.ZAxis * Vector3d.ZAxis) > Math.Cos(RhinoMath.ToRadians(45)))
            {

                slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);

                depthRough = Math.Tan(slot_angle) * beam.Width * Math.Cos(slot_angle) - 4;
                depthFinish = Math.Tan(slot_angle) * beam.Width * Math.Cos(slot_angle) - 4;

                depthRough = Math.Sin(slot_angle) * beam.Width - 4;
                depthFinish = Math.Sin(slot_angle) * beam.Width;


                slotPlane = new Plane(slotPlane.Origin + slotPlane.YAxis * depthFinish, slotPlane.XAxis, -slotPlane.ZAxis);
                slot_length = origin_top.DistanceTo(origin_btm);
                double hThick = plateThickness / 2;

                outline = new Point3d[]{
        slotPlane.PointAt(hThick, -16),
        slotPlane.PointAt(-hThick, -16),
        slotPlane.PointAt(-hThick, slot_length + 16),
        slotPlane.PointAt(hThick, slot_length + 16)};
            }
            else
            {

                slot_angle = Vector3d.VectorAngle(Vector3d.ZAxis, slotPlane.ZAxis);
                slot_length = Math.Max(40, Math.Tan(slot_angle) * beam.Width * Math.Cos(slot_angle));

                double hThick = plateThickness / 2;
                outline = new Point3d[]{
        slotPlane.PointAt(hThick, 0),
        slotPlane.PointAt(-hThick, 0),
        slotPlane.PointAt(-hThick, slot_length),
        slotPlane.PointAt(hThick, slot_length)};


                // (Check if it works on the bottom)
                //outlinePoly.Transform(Transform.Translation(slotPlane.ZAxis * -60));

                // Calculate depth
                var dot = Math.Abs(slotPlane.ZAxis * Vector3d.ZAxis);
                var extraDepth = Math.Sin(Math.Acos(dot)) * plateThickness * 0.5;
                depthRough = 60 / dot - 3;
                depthFinish = 60 / dot + extraDepth * 2 + 1;

            }
            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            var slotOp = new SlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);
            //slotOp.LongSlot = true;

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotOp);
            else
                wp.E1.Operations.Add(slotOp);
        }

        void ConstructPortalMortise(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;

            var platePlane = (Plane)slotDict["PlatePlane"];
            var endPlane = (Plane)slotDict["EndPlane"];
            var plateThickness = slotDict.GetDouble("PlateThickness");
            var depth = slotDict.GetDouble("Depth", 100);

            platePlane.Transform(World2Local);
            endPlane.Transform(World2Local);

            var XDirection = (MidPoint - endPlane.Origin) * Vector3d.XAxis > 0 ? Vector3d.XAxis : -Vector3d.XAxis;
            var YDirection = MidPoint.X > endPlane.Origin.X ? -Vector3d.YAxis : Vector3d.YAxis;

            if (endPlane.ZAxis * XDirection > 0)
            {
                endPlane = new Plane(endPlane.Origin, -endPlane.XAxis, endPlane.YAxis);
            }

            var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);
            // Vertical slot plane

            var xaxis = platePlane.ZAxis;
            var yaxis = Vector3d.CrossProduct(endPlane.ZAxis, xaxis);


            //depth = 0;
            Plane slotPlane = new Plane(endPlane.Origin + endPlane.ZAxis * depth, xaxis, yaxis);
            if (slotPlane.ZAxis * XDirection > 0)
                slotPlane = new Plane(slotPlane.Origin, -slotPlane.XAxis, slotPlane.YAxis);

            Point3d inner_pt, outer_pt;
            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, endPlane, 0.01);
            if (res.Count < 1) throw new Exception("ConstructEndSlot() failed: no intersection with InnerOffset.");

            inner_pt = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, endPlane, 0.01);
            if (res.Count < 1) throw new Exception("ConstructEndSlot() failed: no intersection with OuterOffset.");

            outer_pt = res[0].PointA;

            Point3d local_inner_pt, local_outer_pt;
            slotPlane.RemapToPlaneSpace(inner_pt, out local_inner_pt);
            slotPlane.RemapToPlaneSpace(outer_pt, out local_outer_pt);

            double slot_length = inner_pt.DistanceTo(outer_pt);

            double hThick = plateThickness / 2;
            double extra = 20;
            var outline = new Point3d[]{
      slotPlane.PointAt(hThick, -slot_length / 2 - extra),
      slotPlane.PointAt(-hThick, -slot_length / 2 - extra),
      slotPlane.PointAt(-hThick, slot_length / 2 + extra),
      slotPlane.PointAt(hThick, slot_length / 2 + extra)};

            var outlinePlane = new Plane(outline[1], outline[2], outline[0]);
            //if (outlinePlane.ZAxis * YDirection > 0)
            //  Array.Reverse(outline);


            var depthRough = depth - 3;
            var depthFinish = depth;

            // Now construct the aligned plane
            var projZAxis = Plane.WorldXY.Project(slotPlane.ZAxis);
            projZAxis.Unitize();

            var alignedXAxis = Vector3d.CrossProduct(Vector3d.ZAxis, projZAxis);
            alignedXAxis.Unitize();

            double angle;
            if (slotPlane.ZAxis * XDirection > 0)
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(slotPlane.Origin, slotPlane.ZAxis, out slotPlane, out angle);


            var outlinePoly = MakeSlotPolyline(outline.ToList(), 8.0, true, 1);

            var slotOp = new EndSlotMachining(name);
            slotOp.OverridePlane = true;
            //slotOp.XLine = new Line(origin_top, origin_top + alignedXAxis * 100);
            slotOp.XLine = new Line(slotPlane.Origin, slotPlane.Origin + slotPlane.XAxis * 100);

            //slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Angle = angle;
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish;
            slotOp.Depth0 = depthRough;
            //slotOp.OperationName = "TAPHUL";

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotOp);
            else
                wp.E1.Operations.Add(slotOp);
        }

        void ConstructPortalTenon(object obj, string name, Workpiece wp, Beam beam)
        {
            var ad = obj as ArchivableDictionary;
            var endPlane = ad.GetPlane("EndPlane");
            var thickness = ad.GetDouble("Thickness");

            endPlane.Transform(World2Local);

            var XDirection = (MidPoint - endPlane.Origin) * Vector3d.XAxis > 0 ? Vector3d.XAxis : -Vector3d.XAxis;
            var YDirection = (MidPoint - endPlane.Origin) * Vector3d.XAxis > 0 ? Vector3d.YAxis : -Vector3d.YAxis;

            Point3d ptInner, ptOuter;

            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, endPlane, 0.01);
            if (res.Count < 1) throw new Exception("ConstructPortalTenon() failed: Couldn't intersect InnerOffset.");

            ptInner = res[0].PointA;

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, endPlane, 0.01);
            if (res.Count < 1) throw new Exception("ConstructPortalTenon() failed: Couldn't intersect OuterOffset.");

            ptOuter = res[0].PointA;

            ptInner = Plane.WorldXY.ClosestPoint(ptInner);
            ptOuter = Plane.WorldXY.ClosestPoint(ptOuter);

            if ((ptInner - ptOuter) * YDirection < 0)
            {
                var temp = ptInner;
                ptInner = ptOuter;
                ptOuter = temp;
            }

            double extra_thickness = 1.0;

            var slotCutTop = new SlotCut(name);
            slotCutTop.Path = new Line(ptInner + Vector3d.ZAxis * extra_thickness, ptOuter + Vector3d.ZAxis * extra_thickness);
            slotCutTop.Depth = thickness + extra_thickness;

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotCutTop);
            else
                wp.E1.Operations.Add(slotCutTop);

            var bottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);

            ptInner = bottomPlane.ClosestPoint(ptInner);
            ptOuter = bottomPlane.ClosestPoint(ptOuter);

            var slotCutBtm = new SlotCut(name);
            slotCutBtm.Path = new Line(ptInner + Vector3d.ZAxis * thickness, ptOuter + Vector3d.ZAxis * thickness);
            slotCutBtm.Depth = thickness + extra_thickness;

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotCutBtm);
            else
                wp.E1.Operations.Add(slotCutBtm);

        }

        void ConstructPlateSlot(object obj, string name, Workpiece wp, Beam beam)
        {
            var slotDict = obj as ArchivableDictionary;
            var sidePlane = (Plane)slotDict["SidePlane"];
            var outsidePlane = (Plane)slotDict["OutsidePlane"];
            var platePlane = (Plane)slotDict["PlatePlane"];
            var endPlane = (Plane)slotDict["EndPlane"];
            var plateThickness = slotDict.GetDouble("PlateThickness");

            var topOutline = (slotDict["TopLoop"] as Curve).DuplicateCurve();
            var btmOutline = (slotDict["BottomLoop"] as Curve).DuplicateCurve();

            topOutline.Transform(World2Local);
            btmOutline.Transform(World2Local);

            sidePlane.Transform(World2Local);
            outsidePlane.Transform(World2Local);
            platePlane.Transform(World2Local);
            endPlane.Transform(World2Local);

            if (endPlane.ZAxis * (MidPoint - endPlane.Origin) < 0)
            {
                endPlane = new Plane(endPlane.Origin, -endPlane.XAxis, endPlane.YAxis);
            }

            var topPlane = sidePlane.Origin.Z > outsidePlane.Origin.Z ? sidePlane : outsidePlane;
            var bottomPlane = sidePlane.Origin.Z < outsidePlane.Origin.Z ? sidePlane : outsidePlane;

            //var plane = new Plane(topPlane.Origin, Vector3d.CrossProduct(platePlane.ZAxis, sidePlane.ZAxis));

            //var slotEnd = topPlane.Project(endPlane.ZAxis); slotEnd.Unitize();
            var slotEnd = Vector3d.CrossProduct(platePlane.ZAxis, sidePlane.ZAxis); slotEnd.Unitize();

            var slotEndSign = slotEnd * (MidPoint - endPlane.Origin) > 0 ? -1 : 1;

            Point3d originTop, originBtm;
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, endPlane, platePlane, out originTop);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, endPlane, platePlane, out originBtm);

            originTop = topPlane.ClosestPoint(originTop);
            originBtm = topPlane.ClosestPoint(originBtm);

            Point3d origin = MidPoint.DistanceTo(originTop) > MidPoint.DistanceTo(originBtm) ? originTop : originBtm;

            var startPlane = new Plane(origin + slotEnd * 150 * slotEndSign, endPlane.XAxis, endPlane.YAxis);

            var topOutlinePts = new Point3d[4];
            var btmOutlinePts = new Point3d[4];

            var platePlaneSign = Vector3d.CrossProduct(slotEnd, Vector3d.ZAxis) * platePlane.ZAxis < 0 ? -1 : 1;

            Plane platePlaneTop = new Plane(platePlane.Origin + platePlane.ZAxis * plateThickness * 0.5 * platePlaneSign, platePlane.XAxis, platePlane.YAxis);
            Plane platePlaneBtm = new Plane(platePlane.Origin - platePlane.ZAxis * plateThickness * 0.5 * platePlaneSign, platePlane.XAxis, platePlane.YAxis);

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, endPlane, platePlaneTop, out topOutlinePts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, endPlane, platePlaneBtm, out topOutlinePts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, startPlane, platePlaneBtm, out topOutlinePts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, startPlane, platePlaneTop, out topOutlinePts[3]);

            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, endPlane, platePlaneTop, out btmOutlinePts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, endPlane, platePlaneBtm, out btmOutlinePts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, startPlane, platePlaneBtm, out btmOutlinePts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(bottomPlane, startPlane, platePlaneTop, out btmOutlinePts[3]);

            // ******************************
            // Figure out vertical end plane
            // ******************************
            var vertEndPlane = new Plane(origin, platePlane.ZAxis, Vector3d.CrossProduct(slotEnd * slotEndSign, platePlane.ZAxis));
            Point3d p0, p1;
            vertEndPlane.RemapToPlaneSpace(topOutlinePts[0], out p0);
            vertEndPlane.RemapToPlaneSpace(btmOutlinePts[0], out p1);

            if (p0.Z > p1.Z)
                vertEndPlane.Origin = vertEndPlane.Origin + vertEndPlane.ZAxis * (p0.Z + 8.0);
            else
                vertEndPlane.Origin = vertEndPlane.Origin + vertEndPlane.ZAxis * (p1.Z + 8.0);

            //startPlane.Origin = vertEndPlane.Origin + vertEndPlane.ZAxis * 150;
            startPlane = new Plane(vertEndPlane.Origin + vertEndPlane.ZAxis * 200, vertEndPlane.XAxis, vertEndPlane.YAxis);

            var roughOutlinePts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, vertEndPlane, platePlaneTop, out roughOutlinePts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, vertEndPlane, platePlaneBtm, out roughOutlinePts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, startPlane, platePlaneBtm, out roughOutlinePts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(topPlane, startPlane, platePlaneTop, out roughOutlinePts[3]);

            Array.Reverse(topOutlinePts);
            var outlinePoly = new Polyline(topOutlinePts);

            double angle;
            Point3d temp;

            Vector3d slotNormal = platePlane.ZAxis;
            var slotRoughPlane = new Plane((topOutlinePts[0] + topOutlinePts[1]) * 0.5, slotEnd, platePlane.ZAxis);
            var slotPlane = new Plane(slotRoughPlane.Origin, endPlane.ZAxis * slotEndSign, platePlane.ZAxis);

            var endPlaneOffset = new Plane(endPlane.Origin - endPlane.ZAxis * 70, endPlane.XAxis, endPlane.YAxis);

            var outlinePts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, endPlane, platePlaneTop, out outlinePts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, endPlane, platePlaneBtm, out outlinePts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, endPlaneOffset, platePlaneBtm, out outlinePts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(slotPlane, endPlaneOffset, platePlaneTop, out outlinePts[3]);



            Vector3d slotDirection = Vector3d.CrossProduct(endPlane.ZAxis, platePlane.ZAxis);
            if (slotDirection * Vector3d.ZAxis > 0) slotDirection.Reverse();

            var slotXAxis = Vector3d.CrossProduct(slotNormal, sidePlane.ZAxis);


            if (sidePlane.Origin.Z < -10)
            {
                sidePlane.Origin = Plane.WorldXY.ClosestPoint(sidePlane.Origin);
            }

            // ******************
            // Calculate depth
            // ******************
            var dot = Math.Abs(slotRoughPlane.ZAxis * Vector3d.ZAxis);
            var extraDepth = Math.Sin(Math.Acos(dot)) * plateThickness * 0.5;
            var depthRough = 60 / dot - 3;
            var depthFinish = 60 / dot + extraDepth * 2 + 1;

            var originRough = origin + slotRoughPlane.ZAxis * extraDepth;

            var dot2 = Math.Abs(slotDirection * Vector3d.ZAxis);
            var extraDepth2 = Math.Sin(Math.Acos(dot2)) * plateThickness * 0.5;
            var depthRough2 = 60 / dot2 - 3;
            var depthFinish2 = 60 / dot2 + extraDepth2 * 2 + 1;

            var originFinish = origin - slotDirection * extraDepth2;


            // Make aligned planes

            if (slotPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(originFinish, -slotPlane.ZAxis, out slotPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(originFinish, slotPlane.ZAxis, out slotPlane, out angle);

            if (slotRoughPlane.ZAxis * Vector3d.ZAxis < 0)
                GluLamb.Utility.AlignedPlane(originRough, -slotRoughPlane.ZAxis, out slotRoughPlane, out angle);
            else
                GluLamb.Utility.AlignedPlane(originRough, slotRoughPlane.ZAxis, out slotRoughPlane, out angle);

            // *********************************************
            // Messy way of making sure our slot outline
            // and plane are positioned above the material.
            // *********************************************

            var worldRoughProj = Plane.WorldXY.ProjectAlongVector(slotRoughPlane.ZAxis);
            var roughProj = slotRoughPlane.ProjectAlongVector(slotRoughPlane.ZAxis);

            Array.Reverse(roughOutlinePts);
            var outlineRoughPoly = new Polyline(roughOutlinePts);
            outlineRoughPoly.Transform(roughProj);

            double minZ; int minIndex;
            minZ = double.MaxValue;
            minIndex = 0;
            for (int i = 0; i < outlineRoughPoly.Count; ++i)
            {
                if (outlineRoughPoly[i].Z < minZ)
                {
                    minIndex = i;
                    minZ = outlineRoughPoly[i].Z;
                }
            }

            originRough = outlineRoughPoly[minIndex];
            originRough.Transform(worldRoughProj);

            slotRoughPlane.Origin = originRough;

            // *********************************************

            var worldFinishProj = Plane.WorldXY.ProjectAlongVector(slotPlane.ZAxis);
            var finishProj = slotPlane.ProjectAlongVector(slotPlane.ZAxis);

            Array.Reverse(topOutlinePts);
            outlinePoly = new Polyline(outlinePts);
            outlinePoly.Transform(finishProj);

            minZ = double.MaxValue;
            minIndex = 0;
            for (int i = 0; i < outlinePoly.Count; ++i)
            {
                if (outlinePoly[i].Z < minZ)
                {
                    minIndex = i;
                    minZ = outlinePoly[i].Z;
                }
            }

            originFinish = outlinePoly[minIndex];
            originFinish.Transform(worldFinishProj);

            slotPlane.Origin = originFinish;

            Plane outlineRoughPlane, outlinePlane;
            outlineRoughPoly.ToNurbsCurve().TryGetPlane(out outlineRoughPlane);
            if (outlineRoughPlane.ZAxis * slotRoughPlane.ZAxis < 0) outlineRoughPoly.Reverse();

            outlinePoly.ToNurbsCurve().TryGetPlane(out outlinePlane);
            if (outlinePlane.ZAxis * slotPlane.ZAxis < 0) outlinePoly.Reverse();


            outlineRoughPoly = MakeSlotPolyline(outlineRoughPoly, 8.0, false);
            outlinePoly = MakeSlotPolyline(outlinePoly, 8.0, true);


            if (Math.Abs(endPlane.ZAxis * Vector3d.ZAxis) > 1e-6 || true)
            {
                var slotOpRough = new SlotMachining(name);
                slotOpRough.OverridePlane = true;
                slotOpRough.XLine = new Line(originRough, originRough + slotRoughPlane.XAxis * 100);
                slotOpRough.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotRoughPlane.XAxis, -Vector3d.ZAxis), slotRoughPlane.ZAxis);
                slotOpRough.Plane = slotRoughPlane;
                slotOpRough.Outline = outlineRoughPoly;
                slotOpRough.Rough = true;
                slotOpRough.Radius = 8;
                slotOpRough.Depth = depthFinish;
                slotOpRough.Depth0 = depthRough;

                if (endPlane.Origin.X < MidPoint.X)
                    wp.E2.Operations.Add(slotOpRough);
                else
                    wp.E1.Operations.Add(slotOpRough);
            }



            var slotOp = new SlotMachining(name);
            slotOp.OverridePlane = true;
            slotOp.XLine = new Line(originFinish, originFinish + slotPlane.XAxis * 100);
            slotOp.Angle = Vector3d.VectorAngle(Vector3d.CrossProduct(slotPlane.XAxis, -Vector3d.ZAxis), slotPlane.ZAxis);
            slotOp.Plane = slotPlane;
            slotOp.Outline = outlinePoly;
            slotOp.Rough = false;
            slotOp.Radius = 8;
            slotOp.Depth = depthFinish2;
            slotOp.Depth0 = depthRough2;

            if (false)
            {
                double maxDepth = 132 - Math.Abs(Math.Tan(Vector3d.VectorAngle(slotPlane.ZAxis, Vector3d.ZAxis)) * 31.5);

                slotOp.Depth = Math.Min(maxDepth, slotOp.Depth);
                slotOp.Depth0 = Math.Min(slotOp.Depth0, maxDepth - 3);
            }

            if (endPlane.Origin.X < MidPoint.X)
                wp.E2.Operations.Add(slotOp);
            else
                wp.E1.Operations.Add(slotOp);

            /*
            originRough.Transform(Local2Plane);
            originFinish.Transform(Local2Plane);
            origin.Transform(Local2Plane);
            var polyDebug = new Polyline(outlineRoughPoly);
            polyDebug.Transform(Local2Plane);

            debug.Add(new BakeFeature("SlottyMcSlotters", new List<object>(){originRough, originFinish, origin, polyDebug}));
            */
        }

        void ConstructPlateDowel(object obj, string name, Workpiece wp, Beam beam)
        {
            var axis = (Line)obj;
            double toolHolderDiameter = 65;
            double thr = toolHolderDiameter * 0.5;
            double toolLength = 120;

            axis.Transform(World2Local);
            Plane planeInner, planeOuter;
            double angle;
            double max_depth = 118;
            Curve blank_edge;

            var normal = axis.Direction;
            normal.Unitize();

            Point3d originIn, originOut;
            Point3d axisIn, axisOut;
            Vector3d normalIn, normalOut;

            if (normal * Vector3d.YAxis > 0)
            {
                axisIn = axis.To;
                axisOut = axis.From;
                normalIn = normal;
                normalOut = -normal;
            }
            else
            {
                axisIn = axis.From;
                axisOut = axis.To;
                normalIn = -normal;
                normalOut = normal;
            }

            originIn = Plane.WorldXY.ClosestPoint(axisIn);
            originOut = Plane.WorldXY.ClosestPoint(axisOut);
            Vector3d kIn, kOut;
            double extraIn, extraOut;
            double rIn, rOut;
            double dotIn, dotOut;

            var bx = Rhino.Geometry.Intersect.Intersection.CurveLine(InnerOffset, new Line(originIn, normalIn), 0.01, 0.01);
            if (bx == null || bx.Count < 1) Print("ERROR: Couldn't find inner blank edge for placing dowel hole.");

            originIn = bx[0].PointB;
            kIn = InnerOffset.CurvatureAt(bx[0].ParameterA);
            if (kIn.Length > 0)
                rIn = 1 / kIn.Length;
            else
                rIn = 0;
            kIn.Unitize();

            extraIn = 0;

            dotIn = normalIn * kIn;
            extraIn = Math.Sin(Math.Acos(Math.Abs(dotIn))) * toolHolderDiameter;

            if (dotIn > 0)
            {
                extraIn -= rIn - Math.Sqrt(rIn * rIn + thr * thr);
            }
            if (extraIn > 10)
                extraIn -= 10;

            originIn = originIn + normalIn * extraIn;

            bx = Rhino.Geometry.Intersect.Intersection.CurveLine(OuterOffset, new Line(originOut, normalOut), 0.01, 0.01);
            if (bx == null || bx.Count < 1) Print("ERROR: Couldn't find outer blank edge for placing dowel hole.");

            originOut = bx[0].PointB;
            kOut = OuterOffset.CurvatureAt(bx[0].ParameterA);
            if (kOut.Length > 0)
                rOut = 1 / kOut.Length;
            else
                rOut = 0;
            kOut.Unitize();

            extraIn = 0;

            dotOut = normalOut * kOut;
            extraOut = Math.Sin(Math.Acos(Math.Abs(dotOut))) * toolHolderDiameter;

            if (dotIn > 0)
            {
                extraOut -= rOut - Math.Sqrt(rOut * rOut + thr * thr);
            }
            if (extraOut > 10)
                extraOut -= 10;

            originOut = originOut + normalOut * extraOut;


            AlignedPlane(originIn, normalIn, out planeInner, out angle);
            AlignedPlane(originOut, normalOut, out planeOuter, out angle);

            axisIn = planeInner.ClosestPoint(axisIn);
            axisOut = planeOuter.ClosestPoint(axisOut);

            var axisNew = new Line(axisIn, axisOut);

            var sideDrillOpIn = new SideDrillGroup(name);
            sideDrillOpIn.Drillings.Add(new Drill2d(axisIn, 16.0, Math.Min(max_depth, axisNew.Length * 0.5 + 1)));
            sideDrillOpIn.Plane = planeInner;

            wp.Inside.Operations.Add(sideDrillOpIn);


            var sideDrillOpOut = new SideDrillGroup(name + "_reverse");
            sideDrillOpOut.Drillings.Add(new Drill2d(axisOut, 16.0, Math.Min(max_depth, axisNew.Length * 0.5 + 1)));
            sideDrillOpOut.Plane = planeOuter;

            wp.Outside.Operations.Add(sideDrillOpOut);

        }

        void ConstructEndCut(object obj, string name, Workpiece wp, Beam beam, bool transform = true)
        {
            var endCut = (Plane)obj;
            if (transform)
                endCut.Transform(World2Local);

            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(beam.Centreline, endCut, 0.01);
            double d = double.MaxValue;
            int index = 0;
            for (int j = 0; j < res.Count; ++j)
            {
                double temp = res[j].PointA.DistanceTo(endCut.Origin);
                if (temp < d)
                {
                    d = temp;
                    index = j;
                }
            }

            // Put end cut on closest curve intersection point
            endCut.Origin = res[index].PointA;

            // Make sure end cut is pointing away from the midpoint of the blank
            var midPt = beam.Centreline.PointAt(beam.Centreline.Domain.Mid);
            var endVec = MidPoint - endCut.Origin;
            endVec.Unitize();

            // Check which way the end cut is pointing...
            var dot = endVec * endCut.ZAxis;

            // Set the normal to the correct direction
            var normal = dot < 0 ? -endCut.ZAxis : endCut.ZAxis;

            // Make new correct end plane
            double angle;
            AlignedPlane(endCut.Origin, normal, out endCut, out angle);
            endCut.XAxis = -endCut.XAxis;
            endCut.YAxis = -endCut.YAxis;

            // Make default cutline
            Line cpLine = new Line(endCut.Origin, endCut.XAxis * 100);

            // Join blank offsets into a closed loop in case we have an oblique end plane
            var joined = Curve.JoinCurves(new Curve[]{
      InnerOffset, OuterOffset,
      new Line(InnerOffset.PointAtStart, OuterOffset.PointAtStart).ToNurbsCurve(),
      new Line(InnerOffset.PointAtEnd, OuterOffset.PointAtEnd).ToNurbsCurve()}, 0.01)[0];

            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(joined, endCut, 0.01);
            if (res != null && res.Count > 1)
                cpLine = new Line(res[0].PointA, res[1].PointA);
            else
            {
                Print("End cut {0} ({1}) failed.", CurrentName, name); //throw new Exception("Couldn't intersect joined! " + CurrentName);
                this.Component.Message = "INCOMPLETE";

                var planes = new Plane[6];
                planes[0] = new Plane(OuterOffset.PointAtStart, OuterOffset.TangentAtStart, Vector3d.ZAxis);
                planes[1] = new Plane(InnerOffset.PointAtStart, InnerOffset.TangentAtStart, Vector3d.ZAxis);
                planes[2] = new Plane(OuterOffset.PointAtStart, InnerOffset.PointAtStart - OuterOffset.PointAtStart, Vector3d.ZAxis);

                planes[3] = new Plane(OuterOffset.PointAtEnd, OuterOffset.TangentAtEnd, Vector3d.ZAxis);
                planes[4] = new Plane(InnerOffset.PointAtEnd, InnerOffset.TangentAtEnd, Vector3d.ZAxis);
                planes[5] = new Plane(OuterOffset.PointAtEnd, InnerOffset.PointAtEnd - OuterOffset.PointAtEnd, Vector3d.ZAxis);

                Point3d xp0, xp1;
                int startIndex = 0;
                if (endCut.Origin.X < MidPoint.X)
                    startIndex = 3;

                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endCut, planes[startIndex], Plane.WorldXY, out xp0);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endCut, planes[1 + startIndex], Plane.WorldXY, out xp1);

                cpLine = new Line(xp0, xp1);

                //endCut = Plane.Unset;
            }

            if (cpLine.Direction * endCut.XAxis < 0) cpLine.Flip();

            endCut.Origin = cpLine.From;

            // ************************************
            var endcutOp = new EndCut(name);
            endcutOp.Plane = endCut;
            endcutOp.CutLine = cpLine;

            // *******************************
            // Add extra depth if necessary
            // *******************************
            if (endCut.ZAxis * Vector3d.ZAxis > Math.Cos(RhinoMath.ToRadians(45)))
            {
                var planes = new Plane[6];
                planes[0] = new Plane(OuterOffset.PointAtStart, OuterOffset.TangentAtStart, Vector3d.ZAxis);
                planes[1] = new Plane(InnerOffset.PointAtStart, InnerOffset.TangentAtStart, Vector3d.ZAxis);
                planes[2] = new Plane(OuterOffset.PointAtStart, InnerOffset.PointAtStart - OuterOffset.PointAtStart, Vector3d.ZAxis);

                planes[3] = new Plane(OuterOffset.PointAtEnd, OuterOffset.TangentAtEnd, Vector3d.ZAxis);
                planes[4] = new Plane(InnerOffset.PointAtEnd, InnerOffset.TangentAtEnd, Vector3d.ZAxis);
                planes[5] = new Plane(OuterOffset.PointAtEnd, InnerOffset.PointAtEnd - OuterOffset.PointAtEnd, Vector3d.ZAxis);

                Point3d xp0, xp1;
                int startIndex = 0;
                if (endCut.Origin.X < MidPoint.X)
                    startIndex = 3;

                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endCut, planes[startIndex], planes[2 + startIndex], out xp0);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(endCut, planes[1 + startIndex], planes[2 + startIndex], out xp1);

                var maxDepth = Math.Min(xp0.Z, xp1.Z);
                double extra_depth = Math.Sin(Vector3d.VectorAngle(endCut.ZAxis, Vector3d.ZAxis)) * 2;
                //extra_depth = 0;
                endcutOp.ExtraDepth = Math.Min(10, -(beam.Width + maxDepth - extra_depth));
            }

            if (endCut.Origin.X > MidPoint.X)
                wp.E1.Operations.Add(endcutOp);
            else
                wp.E2.Operations.Add(endcutOp);
        }

        void ConstructDrillGroup(object obj, string name, Workpiece wp, Beam beam, bool end = true)
        {
            var dgd = obj as ArchivableDictionary;
            var plane = dgd.GetPlane("Plane0");

            // Work in local space
            plane.Transform(World2Local);

            var E1Point = (InnerOffset.PointAtStart + InnerOffset.PointAtStart) / 2;
            var E2Point = (InnerOffset.PointAtEnd + InnerOffset.PointAtEnd) / 2;
            var TopPoint = (InnerOffset.PointAtStart + InnerOffset.PointAtEnd) / 2;

            // *************************************************************
            // Make the vector to compare the drilling direction with:
            // If we are on the top of the piece, then we compare with the
            // Z-axis. If we are at the ends, we compare with a vector that
            // goes from the plane to the MidPoint (we want to point away
            // from the material.
            // *************************************************************
            var compare = end ? plane.Origin - MidPoint : Vector3d.ZAxis;
            var normal = plane.ZAxis;


            if ((plane.Origin.DistanceTo(E1Point) < plane.Origin.DistanceTo(TopPoint)) ||
              (plane.Origin.DistanceTo(E2Point) < plane.Origin.DistanceTo(TopPoint)))
                end = true;

            if (normal * compare > 0) normal.Reverse();

            plane = new Plane(plane.Origin, normal);

            Point3d highestStart = plane.Origin;
            Point3d localStart = highestStart;
            Point3d holeFrom;

            var TopProjection = Plane.WorldXY.ProjectAlongVector(normal);

            foreach (string key in dgd.Keys)
            {
                if (key.StartsWith("Dowel"))
                {
                    var tAxis = (Line)dgd[key];
                    tAxis.Transform(World2Local);

                    if (tAxis.Direction * compare > 0) tAxis.Flip();
                    holeFrom = tAxis.From;
                    if (!end)
                    {
                        holeFrom.Transform(TopProjection);
                    }

                    plane.RemapToPlaneSpace(holeFrom, out localStart);
                    if (localStart.Z < 0)
                        plane.Origin = holeFrom;
                }
            }

            double angle;
            AlignedPlane(plane.Origin, normal, out plane, out angle);
            plane.XAxis = -plane.XAxis; // Not sure why, but this is necessary

            /*
            debug.Add(new BakeFeature("ThatPeskyPlane", new List<object>{
            plane,
            new Line(plane.Origin, plane.XAxis * 75),
            new Line(plane.Origin, plane.YAxis * 150),
            new Line(plane.Origin, plane.ZAxis * 300)
            }));
            */
            var origin = plane.Origin;

            // ************************************************************
            // Project the highest start point to the XY-plane so that our
            // plane X-axis lies on the XY-plane
            // **********************************
            origin.Transform(Transform.ProjectAlong(Plane.WorldXY, plane.YAxis));

            plane.Origin = origin;

            // Shift plane back if it is at the end, to avoid collision with the clean cut
            if (end)
            {
                plane.Origin = plane.Origin + plane.ZAxis * 5.0;
            }

            // Make DrillGroup feature
            var drillgrp = new DrillGroup2(name);
            drillgrp.Plane = plane;


            var SpoilPlane = new Plane(Point3d.Origin - Vector3d.ZAxis * (beam.Width + 3.0), Vector3d.XAxis, Vector3d.YAxis);
            var spoilProject = SpoilPlane.ProjectAlongVector(plane.ZAxis);

            double depth = 0;
            foreach (string key in dgd.Keys)
            {
                if (key.StartsWith("Dowel"))
                {
                    //cix.Add(string.Format("(\t{0})", key));
                    var axis = (Line)dgd[key];

                    axis.Transform(World2Local);
                    if (axis.Direction * Vector3d.ZAxis > 0.5) axis.Flip();

                    Point3d pt2d = axis.From;

                    pt2d = plane.ClosestPoint(pt2d);

                    var holeBottom = axis.To;

                    if (!end)
                        holeBottom.Transform(spoilProject);

                    depth = Math.Min(MaxDepth, pt2d.DistanceTo(holeBottom));

                    plane.RemapToPlaneSpace(pt2d, out pt2d);

                    var pt2dreal = plane.PointAt(pt2d.X, pt2d.Y, pt2d.Z);

                    var drill = new Drill2d(pt2dreal, 16.0, depth);
                    drillgrp.Drillings.Add(drill);
                }
            }

            if (end)
            {
                if (plane.Origin.DistanceTo(beam.Centreline.PointAtStart) <
                  plane.Origin.DistanceTo(beam.Centreline.PointAtEnd))
                {
                    wp.E1.Operations.Add(drillgrp);
                }
                else
                {
                    wp.E2.Operations.Add(drillgrp);
                }
            }
            else
            {
                var drillgrpTop = new DrillGroupTop(name);
                drillgrpTop.Depth = depth;
                drillgrpTop.Diameter = 16.0;
                drillgrpTop.Point = plane.Origin;
                wp.Top.Operations.Add(drillgrpTop);
            }
        }
        
        void ConstructCleanCut(Workpiece wp, Beam beam)
        {
            Point3d ep;
            Vector3d ev;
            Plane cleanPlane;
            Plane BottomPlane = new Plane(new Point3d(0, 0, -beam.Width), Vector3d.XAxis, Vector3d.YAxis);

            var ends = new BeamSide[] { wp.E1, wp.E2 };
            var endCuts = new List<EndCut>[2];

            for (int i = 0; i < 2; ++i)
            {
                List<Plane> endPlanes = new List<Plane>();
                int N = 0;
                for (int j = 0; j < ends[i].Operations.Count; ++j)
                {
                    if (ends[i].Operations[j] is EndCut)
                    {
                        endPlanes.Add((ends[i].Operations[j] as EndCut).Plane);
                    }
                }
                Plane p0, p1;
                if (endPlanes.Count < 1)
                {
                    continue;
                }


                p0 = endPlanes[0];

                if (endPlanes.Count == 1)
                {
                    if (p0.ZAxis * -Vector3d.ZAxis < 0)
                        p1 = BottomPlane;
                    else
                        p1 = Plane.WorldXY;
                }
                else
                {
                    p1 = endPlanes[1];
                    double dot0 = p0.ZAxis * -Vector3d.ZAxis, dot1 = p1.ZAxis * -Vector3d.ZAxis;

                    if (dot0 < 0 && dot1 < 0)
                    {
                        if (dot0 < dot1)
                            p0 = BottomPlane;
                        else
                            p1 = BottomPlane;
                    }
                    else if (dot0 > 0 && dot1 > 0)
                    {
                        if (dot0 > dot1)
                            p0 = Plane.WorldXY;
                        else
                            p1 = Plane.WorldXY;
                    }
                }

                Line xLine;
                Rhino.Geometry.Intersect.Intersection.PlanePlane(p0, p1, out xLine);

                if (xLine.Direction * endPlanes[0].XAxis < 0) xLine.Flip();

                ep = xLine.ClosestPoint(endPlanes[0].Origin, false);

                ep = Plane.WorldXY.ClosestPoint(ep);
                ev = Plane.WorldXY.Project(xLine.Direction); ev.Unitize();

                cleanPlane = new Plane(ep, ev, Vector3d.ZAxis);

                //debug.Add(new BakeFeature("FuckedUpShit", new List<object>{xLine, endPlanes[0]}));
                //return;

                var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(InnerOffset, cleanPlane, 0.01);
                //if (res == null || res.Count < 1) throw new Exception("Couldn't intersect InnerOffset! " + CurrentName);
                if (res == null || res.Count < 1)
                {
                    Print("ERROR: Couldn't intersect InnerOffset! " + CurrentName);
                    var cfail = new CleanCut();
                    cfail.CutLine = Line.Unset;
                    ends[i].Operations.Add(cfail);
                    return;
                }
                var cp0 = res[0].PointA;

                res = Rhino.Geometry.Intersect.Intersection.CurvePlane(OuterOffset, cleanPlane, 0.01);
                //if (res == null || res.Count < 1) throw new Exception("Couldn't intersect OuterOffset! " + CurrentName);
                if (res == null || res.Count < 1)
                {
                    Print("Couldn't intersect OuterOffset! " + CurrentName);
                    var cfail = new CleanCut();
                    cfail.CutLine = Line.Unset;
                    ends[i].Operations.Add(cfail);
                    return;
                }
                var cp1 = res[0].PointA;

                var cpLine = new Line(cp0, cp1);
                if (cpLine.Direction * ev < 0) cpLine.Flip();

                Print("Created EndPlane for end {0}", i + 1);
                EndPlanes[i] = new Plane(cpLine.From, cpLine.Direction, Vector3d.ZAxis);

                var cc = new CleanCut();
                cc.CutLine = cpLine;

                ends[i].Operations.Add(cc);

            }
        }

        void ConstructCrossJointCut(object obj, string name, Workpiece wp, Beam beam)
        {
            var cjd = obj as ArchivableDictionary;
            if (cjd == null) return;

            var BottomPlane = new Plane(Point3d.Origin - Vector3d.ZAxis * beam.Width, Vector3d.XAxis, Vector3d.YAxis);

            var points = new Point3d[4];
            var plane = cjd.GetPlane("Plane");
            points[0] = cjd.GetPoint3d("P0", Point3d.Origin);
            points[1] = cjd.GetPoint3d("P1", Point3d.Origin);
            points[2] = cjd.GetPoint3d("P2", Point3d.Origin);
            points[3] = cjd.GetPoint3d("P3", Point3d.Origin);

            var sidePlanes = new Plane[2];
            sidePlanes[0] = cjd.GetPlane("SidePlane0");
            sidePlanes[1] = cjd.GetPlane("SidePlane1");


            plane.Transform(World2Local);
            plane = OrientPlane(plane, Plane.WorldXY, false);

            sidePlanes[0].Transform(World2Local);
            sidePlanes[1].Transform(World2Local);

            // Select the right offset and prefix based on the direction of the cutout normal
            var xaxis = Vector3d.CrossProduct(plane.ZAxis, Vector3d.ZAxis);
            bool isInner = plane.ZAxis * Vector3d.YAxis > 0;

            var side = isInner ? wp.Inside : wp.Outside;


            Curve Offset = isInner ? InnerOffset.DuplicateCurve() : OuterOffset.DuplicateCurve();
            string prefix = isInner ? "IN" : "OUT";

            // Swap side planes depending on orientation
            if ((sidePlanes[0].Origin - plane.Origin) * xaxis < 0)
            {
                var temp = sidePlanes[0];
                sidePlanes[0] = sidePlanes[1];
                sidePlanes[1] = temp;
            }

            var ppoints = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[0], plane, Plane.WorldXY, out ppoints[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[0], plane, BottomPlane, out ppoints[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[1], plane, Plane.WorldXY, out ppoints[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[1], plane, BottomPlane, out ppoints[3]);

            // **********************************
            // Compute the minimum cutout points
            // **********************************

            Point3d[] hakPts = new Point3d[7];
            Point3d[] maxPts = new Point3d[2]; // Also save the points that are furthest away from the plane origin
            if (ppoints[0].DistanceTo(plane.Origin) < ppoints[1].DistanceTo(plane.Origin))
            {
                hakPts[3] = Plane.WorldXY.ClosestPoint(ppoints[0]);
                maxPts[0] = ppoints[1];
            }
            else
            {
                hakPts[3] = Plane.WorldXY.ClosestPoint(ppoints[1]);
                maxPts[0] = ppoints[1];
            }

            if (ppoints[2].DistanceTo(plane.Origin) < ppoints[3].DistanceTo(plane.Origin))
            {
                hakPts[4] = Plane.WorldXY.ClosestPoint(ppoints[2]);
                maxPts[1] = ppoints[3];

            }
            else
            {
                hakPts[4] = Plane.WorldXY.ClosestPoint(ppoints[3]);
                maxPts[1] = ppoints[2];
            }



            Point3d[] xPts = new Point3d[2];
            var res = Rhino.Geometry.Intersect.Intersection.CurvePlane(Offset, sidePlanes[0], 0.01);

            if (res != null && res.Count > 0)
                xPts[0] = res[0].PointA;
            else
                xPts[0] = plane.Origin + plane.ZAxis * 20;
            res = Rhino.Geometry.Intersect.Intersection.CurvePlane(Offset, sidePlanes[1], 0.01);

            if (res != null && res.Count > 0)
                xPts[1] = res[0].PointA;
            else
                xPts[1] = plane.Origin + plane.ZAxis * 20;

            var midPlane = new Plane(xPts[0], plane.XAxis, plane.YAxis);

            if (xPts[1].DistanceTo(plane.Origin) > xPts[0].DistanceTo(plane.Origin))
                midPlane.Origin = xPts[1];

            // These points intersect the blank sides
            hakPts[2] = midPlane.ClosestPoint(hakPts[3]);
            hakPts[5] = midPlane.ClosestPoint(hakPts[4]);

            var endPlane = new Plane(midPlane.Origin + plane.ZAxis * 30, plane.XAxis, plane.YAxis);

            hakPts[1] = endPlane.ClosestPoint(hakPts[2]);
            hakPts[6] = endPlane.ClosestPoint(hakPts[5]);

            hakPts[0] = (hakPts[1] + hakPts[6]) * 0.5;

            var crossjoint = new CrossJointCutout(); // CJ
            crossjoint.Id = 1;
            crossjoint.Outline = new Polyline(hakPts); // CJ


            for (int i = 0; i < hakPts.Length; ++i)
            {
                //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_1_PKT_{1}", prefix, i + 1), hakPts[i]));
            }

            // Top outline - for visualization
            var poly = new Polyline(hakPts);
            //poly.Add(poly[0]);

            // *************************
            // Calculate cutout plane
            // *************************

            Point3d midPt0 = midPlane.ClosestPoint(maxPts[0]), midPt1 = midPlane.ClosestPoint(maxPts[1]);
            Point3d hakPt0 = Plane.WorldXY.ClosestPoint(midPt0), hakPt1 = Plane.WorldXY.ClosestPoint(midPt1);

            var hakLine = new Line(hakPt0, hakPt1);

            if (hakLine.Direction * xaxis > 0) hakLine.Flip();

            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_1_PL_PKT_1", prefix), hakLine.From));
            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_1_PL_PKT_2", prefix), hakLine.To));

            var hakPlane = new Plane(hakLine.From, hakLine.Direction, -Vector3d.ZAxis);

            crossjoint.MaxSpan = hakLine;
            crossjoint.Plane = hakPlane; // CJ

            //hakPlane = OrientPlane(hakPlane, Plane.WorldXY, false);


            // *************************
            // Calculate slanted points
            // *************************

            var slantPts = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[0], hakPlane, Plane.WorldXY, out slantPts[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[0], hakPlane, BottomPlane, out slantPts[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[1], hakPlane, Plane.WorldXY, out slantPts[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(sidePlanes[1], hakPlane, BottomPlane, out slantPts[3]);

            var localSlantPts = new Point3d[4];
            for (int i = 0; i < 4; ++i)
            {
                hakPlane.RemapToPlaneSpace(slantPts[i], out localSlantPts[i]);
            }

            crossjoint.SideLines[0] = new Line(localSlantPts[0], localSlantPts[1]); // CJ
            crossjoint.SideLines[1] = new Line(localSlantPts[2], localSlantPts[3]); // CJ


            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_1_PKT_1", prefix), localSlantPts[0]));
            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_1_PKT_2", prefix), localSlantPts[1]));

            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_2_PKT_1", prefix), localSlantPts[2]));
            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_2_PKT_2", prefix), localSlantPts[3]));

            slantPts[1] = plane.ClosestPoint(slantPts[0]);
            slantPts[3] = plane.ClosestPoint(slantPts[2]);

            crossjoint.SideLines[2] = new Line(slantPts[0], slantPts[1]); // CJ
            crossjoint.SideLines[3] = new Line(slantPts[2], slantPts[3]); // CJ

            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_3_PKT_1", prefix), slantPts[0]));
            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_3_PKT_2", prefix), slantPts[1]));

            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_4_PKT_1", prefix), slantPts[2]));
            //cix.AddRange(CreateCixPoint2d(string.Format("\t{0}_HAK_LINE_4_PKT_2", prefix), slantPts[3]));

            // *********************
            // Set depth and alpha
            // *********************
            var depthPoint = plane.ClosestPoint(hakPlane.Origin);
            double depth = depthPoint.DistanceTo(hakPlane.Origin);

            //Transform Local2World;
            //World2Local.TryGetInverse(out Local2World);

            //crossjoint.Transform(Local2World);

            crossjoint.Depth = depth;

            var hakVector = localSlantPts[1] - localSlantPts[0];
            var hakVectorSign = hakVector * Vector3d.XAxis < 0 ? -1 : 1;


            crossjoint.Alpha = RhinoMath.ToDegrees(Vector3d.VectorAngle(hakVector, Vector3d.YAxis)) * hakVectorSign;

            side.Operations.Add(crossjoint);

            //cix.Add(string.Format("\t{0}_HAK_DYBDE={1:0.###}", prefix, depth));
            //cix.Add(string.Format("\t{0}_HAK_ALFA=0", prefix));

            // *********************************
            // Transform for output and display
            // *********************************

            //hakPlane.Transform(Local2Plane);
            //objects.Add(hakPlane);
            //objects.Add(hakPlane.Origin);
            //objects.Add(new Line(hakPlane.Origin, hakPlane.YAxis * 150));

            //plane.Transform(Local2Plane);
            //poly.Transform(Local2Plane);

            //objects.Add(plane);
            //objects.Add(poly);
            //objects.Add(new Line(plane.Origin, plane.ZAxis * 150));
        }

        void ExportFlippedList(string filepath)
        {
            var lines = new List<string>();
            lines.Add("Element,IsFlipped");

            foreach (string key in FlippedList.Keys)
            {
                lines.Add(string.Format("{0},{1}", key, FlippedList[key] ? "yes" : "no"));
            }

            System.IO.File.WriteAllLines(filepath, lines);
        }

        Plane OrientPlane(Plane plane, Plane reference, bool translate = true)
        {
            Vector3d xaxis = Vector3d.CrossProduct(reference.ZAxis, plane.ZAxis); xaxis.Unitize();
            Vector3d yaxis = Vector3d.CrossProduct(plane.ZAxis, xaxis); yaxis.Unitize();

            if (yaxis * reference.ZAxis < 0 && false)
            {
                yaxis.Reverse();
                xaxis.Reverse();
            }

            var origin = plane.Origin;

            if (translate)
            {
                var proj = reference.ProjectAlongVector(yaxis);
                origin.Transform(proj);
            }

            return new Plane(origin, xaxis, yaxis);
        }

        void ParseLocatorHoles(object obj, StreamWriter cix, List<object> objects, Beam beam)
        {
            var lh = obj as Curve[];
            if (lh == null) return;

            cix.WriteLine(string.Format("\tTOP_DYVELHUL_DIA={0:0.###}", 10.0));
            cix.WriteLine(string.Format("\tTOP_DYVELHUL_DYBDE={0:0.###}", 12.0));

            var c0 = lh[0].DuplicateCurve();
            var c1 = lh[1].DuplicateCurve();

            var start = lh[0].PointAtStart;
            var end = lh[1].PointAtStart;

            if (start.DistanceTo(beam.Centreline.PointAtStart) > end.DistanceTo(beam.Centreline.PointAtStart))
            {
                var temp = start;
                start = end;
                end = temp;
            }

            start.Transform(World2Local);
            end.Transform(World2Local);

            c0.Transform(World2Local);
            c1.Transform(World2Local);

            cix.WriteLine("\tTOP_DYVELHUL_E_1=1");
            cix.WriteLine("\tTOP_DYVELHUL_E_1_N=1");
            CreateCixPoint2d(cix, "\tTOP_DYVELHUL_E_1_HUL_1", start);

            cix.WriteLine("\tTOP_DYVELHUL_E_2=1");
            cix.WriteLine("\tTOP_DYVELHUL_E_2_N=1");
            CreateCixPoint2d(cix, "\tTOP_DYVELHUL_E_2_HUL_1", end);

            cix.WriteLine("\tTOP_DYVELHUL=0");

            start.Transform(Local2Plane);
            end.Transform(Local2Plane);

            c0.Transform(Local2Plane);
            c1.Transform(Local2Plane);

            objects.Add(start);
            objects.Add(end);
            objects.Add(c0);
            objects.Add(c1);
        }

        void ParseEdgeCurves(object obj, TextWriter cix, List<object> objects, Beam beam)
        {
            //var edgeNames = new string[]{"TOP_OUT", "BOTTOM_OUT", "BOTTOM_IN", "TOP_IN"};

            var edgeNames = new string[4];

            // If the element is flipped, the top and bottom curves change
            //if (Flipped)
            //  edgeNames = new string[]{"BOTTOM_OUT", "TOP_OUT", "TOP_IN", "BOTTOM_IN"};

            Curve[] edgeCurves = new Curve[4];

            var tempCurves = obj as Curve[];
            for (int i = 0; i < 4; ++i)
            {
                var crv = tempCurves[i].DuplicateCurve();
                crv.Transform(World2Local);

                if (crv.TangentAtStart * -Vector3d.XAxis < 0)
                    crv.Reverse();

                edgeCurves[i] = crv;
            }

            // Get average of curves so we can compare which
            // is inside/outside, top/bottom
            Point3d average = Point3d.Origin;
            for (int i = 0; i < 4; ++i)
            {
                average += edgeCurves[i].PointAtNormalizedLength(0.5);
            }

            average = average / 4;

            var reverse_in = new bool[4];

            var innerCurves = new List<Curve>();
            var outerCurves = new List<Curve>();
            var InnerOffsetProjected = InnerOffset.DuplicateCurve();
            var OuterOffsetProjected = OuterOffset.DuplicateCurve();
            var proj = Transform.PlanarProjection(Plane.WorldXY);

            InnerOffsetProjected.Transform(proj);
            OuterOffsetProjected.Transform(proj);

            // Compare points
            for (int i = 0; i < 4; ++i)
            {
                var startPoint = edgeCurves[i].PointAtNormalizedLength(0.5);
                var name = "";

                if (startPoint.Z < average.Z)
                    name += "BOTTOM_";
                else
                    name += "TOP_";
                if (startPoint.Y < average.Y)
                {
                    name += "OUT";
                    outerCurves.Add(edgeCurves[i].DuplicateCurve());
                }
                else
                {
                    name += "IN";
                    innerCurves.Add(edgeCurves[i].DuplicateCurve());
                    reverse_in[i] = true;
                }
                edgeNames[i] = name;
            }

            double maxInner = 0, maxOuter = 0;
            double minInner = double.MaxValue, minOuter = double.MaxValue;
            for (int i = 0; i < 2; ++i)
            {
                innerCurves[i].Transform(proj);
                outerCurves[i].Transform(proj);


                double maxDin, maxDout, maxpA, maxpB, minDin, minDout, minpA, minpB;
                Curve.GetDistancesBetweenCurves(InnerOffsetProjected, innerCurves[i], 0.01, out maxDin, out maxpA, out maxpB, out minDin, out minpA, out minpB);
                Curve.GetDistancesBetweenCurves(OuterOffsetProjected, outerCurves[i], 0.01, out maxDout, out maxpA, out maxpB, out minDout, out minpA, out minpB);

                maxInner = Math.Max(maxDin, maxInner);
                maxOuter = Math.Max(maxDout, maxOuter);

                minInner = Math.Min(minDin, minInner);
                minOuter = Math.Min(minDout, minOuter);
            }

            maxInner -= minInner;
            maxOuter -= minOuter;

            if (CurrentName.StartsWith("J"))
            {
                maxInner = 20.0;
                maxOuter = 20.0;
            }
            if (CurrentName == "J-00" || CurrentName == "J-08")
            {
                maxInner = 2.0;
                maxOuter = 2.0;
            }

            cix.WriteLine("(Min edge to blank distances)");
            cix.WriteLine($"\tOM_IN={minInner:0.000}");
            cix.WriteLine($"\tOM_OUT={minOuter:0.000}");

            cix.WriteLine("(Max edge to blank distances)");
            cix.WriteLine($"\tIN_5_AX_D={maxInner:0.000}", maxInner);
            cix.WriteLine($"\tOUT_5_AX_D={maxOuter:0.000}", maxOuter);

            for (int i = 0; i < edgeCurves.Length; ++i)
            {
                cix.WriteLine(string.Format("({0})", edgeNames[i]));

                var edgeCurve = edgeCurves[i];
                if (reverse_in[i]) edgeCurve.Reverse();

                if (CurrentName == "P-04")
                    edgeCurve.Reverse();

                var edgett = edgeCurve.DivideByCount(25 - 1, true);
                for (int j = 0; j < 25; ++j)
                {
                    var ep = edgeCurve.PointAt(edgett[j]);
                    CreateCixPoint(cix, $"\t{edgeNames[i]}_SPL_P_{j + 1}", ep);

                    //        prog.Add(string.Format("\t{0}_SPL_P_{1}_X={2:0.###}", edgeNames[i], j + 1, ep.X));
                    //        prog.Add(string.Format("\t{0}_SPL_P_{1}_Y={2:0.###}", edgeNames[i], j + 1, ep.Y));
                    //        prog.Add(string.Format("\t{0}_SPL_P_{1}_Z={2:0.###}", edgeNames[i], j + 1, ep.Z));
                }

                edgeCurve.Transform(Local2Plane);
                if (objects != null)
                    objects.Add(edgeCurve);
            }
        }

        void CreateCixPoint(TextWriter cix, string prefix, Point3d p)
        {
            cix.WriteLine($"{prefix}_X={p.X:0.###}");
            cix.WriteLine($"{prefix}_Y={p.Y:0.###}");
            cix.WriteLine($"{prefix}_Z={p.Z:0.###}");
        }

        void CreateCixPoint2d(TextWriter cix, string prefix, Point3d p)
        {
            cix.WriteLine($"{prefix}_X={p.X:0.###}");
            cix.WriteLine($"{prefix}_Y={p.Y:0.###}");
        }

        void CreateCixElement(TextWriter cix, Element ele, SegmentedBlankX blank, bool replace = false)
        {
            CurrentName = ele.Name;

            EndPlanes = new Plane[] { Plane.Unset, Plane.Unset };

            Curve inner, outer;

            List<Line> lines;
            BoundingBox bb = BoundingBox.Empty;
            Point3d origo;

            bool do_replace = false;
            if (replace)
            {
                string replace_dir = Environment.ExpandEnvironmentVariables(ReplacementPath);
                var cix_path = System.IO.Path.Combine(replace_dir, ele.Name + ".cix");

                cix.WriteLine(string.Format("(CORRECTED)"));
                cix.WriteLine(string.Format("({0})", cix_path));

                try
                {
                    CixHelp.Load(cix_path);
                    do_replace = true;
                }
                catch (Exception e)
                {
                    var msg = string.Format("WARNING: Couldn't replace {0}. No CIX file found in {1}.", CurrentName, cix_path);
                    Print(msg);
                    Log.Add(msg);
                    do_replace = false;
                }
            }
            if (do_replace)
            {
                try
                {
                    lines = CixHelp.GetSegmentLines();
                }
                catch (Exception e)
                {
                    throw new Exception("Fucking hell: " + CurrentName);

                }

                CixHelp.Variables.TryGetValue("ORIGO_X", out double x);
                CixHelp.Variables.TryGetValue("ORIGO_Y", out double y);
                origo = new Point3d(x, y, 0);

                var cixBounds = CixHelp.GetBounds();
                cixBounds.Z = MidPoint.Z;
                //MidPoint = CixHelp.GetBounds() / 2;

                var blank_offsets = CixHelp.GetBlankOffsets();

                inner = blank_offsets[0];
                outer = blank_offsets[1];

                bb.Union(inner.GetBoundingBox(true));
                bb.Union(outer.GetBoundingBox(true));

                MidPoint = (bb.Max + bb.Min) / 2;

            }
            else
            {

                inner = blank.InnerOffset.DuplicateCurve();
                outer = blank.OuterOffset.DuplicateCurve();
                inner.Transform(World2Local);
                outer.Transform(World2Local);

                bb.Union(inner.GetBoundingBox(true));
                bb.Union(outer.GetBoundingBox(true));

                origo = -bb.Min;

                lines = blank.CreateDivisionLines();

                for (int i = 0; i < lines.Count; ++i)
                {
                    var temp_line = lines[i];
                    temp_line.Transform(World2Local);
                    lines[i] = temp_line;
                }
            }

            Print("SEG LINES: {0}", lines.Count);

            if (Flipped)
            {
                Print("FLIPPING BLANK");
                var Flip = Transform.Rotation(Math.PI, Vector3d.YAxis, MidPoint);

                for (int i = 0; i < lines.Count; ++i)
                {
                    var temp_line = lines[i];
                    if (Flipped)
                        temp_line.Transform(Flip);
                    //temp_line.Transform(Local2World);
                    lines[i] = temp_line;
                }

                lines.Reverse();

                inner.Transform(Flip);
                outer.Transform(Flip);
                inner.Reverse();
                outer.Reverse();
            }

            inner.Translate(0, 0, -inner.PointAtStart.Z);
            outer.Translate(0, 0, -outer.PointAtStart.Z);


            /*
            // Rest of blank data
            List<Line> lines;
            if (replace)
            {
              string replace_dir = Environment.ExpandEnvironmentVariables(ReplacementPath);
              var cix_path = System.IO.Path.Combine(replace_dir, ele.Name + ".cix");

              cix.Add(string.Format("(CORRECTED)"));
              cix.Add(string.Format("({0})", cix_path));

              CixHelp.Load(cix_path);

              lines = CixHelp.GetSegmentLines();
              //lines = lines.GetRange(1, lines.Count - 2);
              double x, y;
              CixHelp.Variables.TryGetValue("ORIGO_X", out x);
              CixHelp.Variables.TryGetValue("ORIGO_Y", out y);
              double dx = origo.X - x;
              double dy = origo.Y - y;
              //origo = new Point3d(x - dx, y - dx, 0);
              origo = new Point3d(x, y, 0);


              World2Local.TryGetInverse(out Local2World);

              var cixBounds = CixHelp.GetBounds();
              cixBounds.Z = MidPoint.Z;
              MidPoint = CixHelp.GetBounds() / 2;



              var Flip = Transform.Rotation(Math.PI, Vector3d.YAxis, MidPoint);
              //Flip = Transform.Rotation(Math.PI, Vector3d.YAxis, origo/2);

              for (int i = 0; i < lines.Count; ++i)
              {
                var temp_line = lines[i];
                if (Flipped)
                  temp_line.Transform(Flip);
                temp_line.Transform(Local2World);
                lines[i] = temp_line;
              }

              Print("SEG LINES: {0}", lines.Count);

              var blank_offsets = CixHelp.GetBlankOffsets();

              inner = blank_offsets[0];
              outer = blank_offsets[1];


              if (Flipped)
              {
                inner.Transform(Flip);
                outer.Transform(Flip);
                inner.Reverse();
                outer.Reverse();
              }
              InnerOffset = blank_offsets[0].DuplicateCurve();
              OuterOffset = blank_offsets[1].DuplicateCurve();

              //lines = lines.Where(x => x.IsValid).ToList();
            }
            else
            {
              lines = blank.CreateDivisionLines();
            }

            var inner = blank.InnerOffset.DuplicateCurve();
            var outer = blank.OuterOffset.DuplicateCurve();

            //var bb_inner = inner.GetBoundingBox(ele.Handle);
            //var bb_outer = outer.GetBoundingBox(ele.Handle);
            //var bb = BoundingBox.Union(bb_inner, bb_outer);

            var bb = blank.Bounds;
            ele.UserDictionary.Set("blank_plane", blank.Plane);

            inner.Transform(World2Local);
            outer.Transform(World2Local);

            if (Flipped)
            {
              inner.Reverse();
              outer.Reverse();
            }
            */



            int Nlayers = (int)(blank.Height / blank.LayerThickness);

            cix.WriteLine($"\tBL_LAG_N={Nlayers:0}");
            cix.WriteLine($"\tBL_LAG_1_TYPE={1:0}");

            if (inner.TangentAtStart * -Vector3d.XAxis < 0)
                inner.Reverse();
            if (outer.TangentAtStart * -Vector3d.XAxis < 0)
                outer.Reverse();


            InnerOffset = inner.DuplicateCurve();
            OuterOffset = outer.DuplicateCurve();

            cix.WriteLine($"\tORIGO_X={origo.X:0.###}");
            cix.WriteLine($"\tORIGO_Y={origo.Y:0.###}");


            //var bbInner = inner.GetBoundingBox(true);
            //var bbOuter = outer.GetBoundingBox(true);
            //bb = BoundingBox.Empty;
            //bb.Union(bbInner);
            //bb.Union(bbOuter);

            //MidPoint = (Point3d(bb.Max - bb.Min) / 2;

            cix.WriteLine($"\tBL_L={bb.Max.X - bb.Min.X:0.###}");
            cix.WriteLine($"\tBL_W={bb.Max.Y - bb.Min.Y:0.###}");


            /*
                for (int i = 0; i < lines.Count; ++i)
                {
                  var tempLine = lines[i];
                  tempLine.Transform(World2Local);
                  if (tempLine.Direction * Vector3d.YAxis > 0) tempLine.Flip();
                  lines[i] = tempLine;
                }
            */
            if (lines[2].From.X > lines[1].From.X)
                lines.Reverse();

            var firstLine = lines[0];
            //firstLine.Transform(World2Local);

            Vector3d vstart = firstLine.Direction;
            vstart.Unitize();

            double firstAngle = RhinoMath.ToDegrees(Math.Atan2(vstart.Y, vstart.X));
            if (firstAngle < 0) firstAngle += 360;


            cix.WriteLine($"\tSEC_N={lines.Count - 1}");
            cix.WriteLine($"\tSEC_E1_L={0:0.###}");
            cix.WriteLine($"\tSEC_E2_L={0:0.###}");
            cix.WriteLine($"\tV_START={firstAngle:0.###}");

            cix.WriteLine($"\tSEC_E_1_V={0:0.###}");
            cix.WriteLine($"\tSEC_E_2_V={0:0.###}");

            for (int i = 1; i < lines.Count; ++i)
            {
                var nor = Vector3d.CrossProduct(lines[i - 1].Direction, Vector3d.ZAxis);
                nor.Unitize();
                //if (Flipped) nor.Reverse();

                //var lineVec = lines[i].Direction;
                //var lineVecPrev = lines[i - 1].Direction;

                //if (lineVec * Vector3d.YAxis > 0) lineVec.Reverse();
                //if (lineVecPrev * Vector3d.YAxis > 0) lineVecPrev.Reverse();

                double angle = Vector3d.VectorAngle(lines[i - 1].Direction, lines[i].Direction);

                if (nor * lines[i].Direction < 0 && angle > 1e-5)
                {
                    angle = -angle;
                    //has_negative_angles = true;
                }

                double angled = RhinoMath.ToDegrees(angle);
                if (-0.08 < angled && angled < 0.001) angled = 0;

                cix.WriteLine($"\tSEC_{i}_V={angled:0.###}");
            }
            for (int i = lines.Count; i <= 16; ++i)
                cix.WriteLine($"\tSEC_{i}_V={0:0.###}");


            // Spline points for inner and outer curves
            // Divide blank offset curves
            int divisions = 45;

            var t_inner = inner.DivideByCount(divisions - 1, true);
            var t_outer = outer.DivideByCount(divisions - 1, true);

            Point3d pt;
            for (int i = 0; i < t_inner.Length; ++i)
            {
                pt = inner.PointAt(t_inner[i]);

                CreateCixPoint2d(cix, string.Format("\tBL_IN_CURVE_P_{0}", i + 1), pt);
            }



            for (int i = 0; i < divisions; ++i)
            {
                pt = outer.PointAt(t_outer[i]);

                CreateCixPoint2d(cix, string.Format("\tBL_OUT_CURVE_P_{0}", i + 1), pt);
            }




            // End points for inner and outer curves

            Point3d inner_start = inner.PointAtStart;
            //inner_start.Transform(World2Local);
            //plane.RemapToPlaneSpace(inner_start, out inner_start);

            Point3d inner_end = inner.PointAtEnd;
            //inner_end.Transform(World2Local);
            //plane.RemapToPlaneSpace(inner_end, out inner_end);

            Point3d outer_start = outer.PointAtStart;
            //outer_start.Transform(World2Local);
            //plane.RemapToPlaneSpace(outer_start, out outer_start);

            Point3d outer_end = outer.PointAtEnd;
            //outer_end.Transform(World2Local);
            //plane.RemapToPlaneSpace(outer_end, out outer_end);


            cix.WriteLine($"\tBL_E_1_IN_X={inner_start.X:0.###}");
            cix.WriteLine($"\tBL_E_1_IN_Y={inner_start.Y:0.###}");

            cix.WriteLine($"\tBL_E_1_OUT_X={outer_start.X:0.###}");
            cix.WriteLine($"\tBL_E_1_OUT_Y={outer_start.Y:0.###}");

            cix.WriteLine($"\tBL_E_2_IN_X={inner_end.X:0.###}");
            cix.WriteLine($"\tBL_E_2_IN_Y={inner_end.Y:0.###}");

            cix.WriteLine($"\tBL_E_2_OUT_X={outer_end.X:0.###}");
            cix.WriteLine($"\tBL_E_2_OUT_Y={outer_end.Y:0.###}");

            // If you have a curved extension, point between curved extension and section 1 (often same as curve start point)

            cix.WriteLine($"\tBL_SEC_E_1_SEC_1_IN_X={inner_start.X:0.###}");
            cix.WriteLine($"\tBL_SEC_E_1_SEC_1_IN_Y={inner_start.Y:0.###}");

            cix.WriteLine($"\tBL_SEC_E_1_SEC_1_OUT_X={outer_start.X:0.###}");
            cix.WriteLine($"\tBL_SEC_E_1_SEC_1_OUT_Y={outer_start.Y:0.###}");

            cix.WriteLine($"\tBL_SEC_E_2_SEC_N_IN_X={inner_end.X:0.###}");
            cix.WriteLine($"\tBL_SEC_E_2_SEC_N_IN_Y={inner_end.Y:0.###}");

            cix.WriteLine($"\tBL_SEC_E_2_SEC_N_OUT_X={outer_end.X:0.###}");
            cix.WriteLine($"\tBL_SEC_E_2_SEC_N_OUT_Y={outer_end.Y:0.###}");


            // Line between sections
            for (int i = 1; i < lines.Count - 1; ++i)
            {
                var p0 = lines[i].From;
                //p0.Transform(World2Local);
                //plane.RemapToPlaneSpace(p0, out p0);

                var p1 = lines[i].To;
                //p1.Transform(World2Local);
                //plane.RemapToPlaneSpace(p1, out p1);

                //debug.Add(p0);
                //debug.Add(p1);

                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_IN_X={p0.X:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_IN_Y={p0.Y:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_OUT_X={p1.X:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i+1}_OUT_Y={p1.X:0.###}");
            }

            // Fill in rest of blank sections
            for (int i = lines.Count - 1; i < 16; ++i)
            {
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_IN_X={0:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_IN_Y={0:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_OUT_X={0:0.###}");
                cix.WriteLine($"\tBL_SEC_{i}_{i + 1}_OUT_Y={0:0.###}");
            }

            // Transform to plane and exit

            var innerLocal = inner.DuplicateCurve();
            var outerLocal = outer.DuplicateCurve();
            innerLocal.Transform(Local2Plane);
            outerLocal.Transform(Local2Plane);

            var bf = new BakeFeature(string.Format("{0}_{1}", ele.Name, "BlankOffsets"), new List<object>());
            bf.Objects.Add(innerLocal);
            bf.Objects.Add(outerLocal);
            debug.Add(bf);

            //debug.Add(outer);
        }

        // </Custom additional code> 
    }

}
#endif