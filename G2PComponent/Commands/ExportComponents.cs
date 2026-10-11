using CsvHelper;
using D2P_Core;
using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Exports each selected component in its local coordinate
    /// system. Detailed geometry - if present - is prioritized.
    /// </summary>
    public class ExportComponentsCommand : Rhino.Commands.Command
    {
        public override string EnglishName => "ExportComponents";

        public bool ExportBoM = true;
        public bool GroupByType = true;
        /// <summary>
        /// 0: no master file, 1: master file and the part files, 2: master file only.
        /// </summary>
        public int MasterFile = 0;
        /// <summary>Index into the Format list, kept between runs.</summary>
        public int FormatIndex = 0;
        static readonly string[] MasterFileModes = { "No", "Yes", "Only" };

        /// <summary>Gap between parts (label included) in the master file.</summary>
        public double MasterGap = 100;
        /// <summary>Gap between the projections in a DWG.</summary>
        public double ViewGap = 50;
        /// <summary>Gap between a part and its name label.</summary>
        public double LabelGap = 10;
        /// <summary>Text height of a part's name label.</summary>
        public double LabelHeight = 10;
        /// <summary>Text height of the dimensions line under the name in a DWG.</summary>
        public double DimensionsHeight = 5;
        /// <summary>
        /// DWG end view next to the plan, turned so its edges line up with the
        /// plan's, or next to the top view.
        /// </summary>
        public bool EndViewOnPlan = true;
        /// <summary>
        /// DWG view arrangement: first angle (ISO E, European) or third angle (ISO A, American).
        /// </summary>
        public bool ThirdAngle = false;
        /// <summary>
        /// DWG shows the other three long faces rolled out under the plan instead of the top view.
        /// </summary>
        public bool RolledSides = false;
        /// <summary>
        /// DWG gets an isometric view (visible edges only) to the right of the other views.
        /// </summary>
        public bool Isometric = false;

        public ExportComponentsCommand()
        {
            Instance = this;
        }

        public static ExportComponentsCommand Instance
        {
            get; private set;
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Pick components
            ObjRef[] objRefs;
            var rc = RhinoGet.GetMultipleObjects("Select components", true, ObjectType.AnyObject, out objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            var objectNames = objRefs.Select(x => x.Object().Name).Distinct().ToList();

            var allComponents = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc);

            var componentNames = allComponents.Select(x => x.Name).Distinct().ToList();

            var skippedObjects = objectNames.Except(componentNames);
            foreach (var skipped in skippedObjects)
            {
                RhinoApp.WriteLine($"Skipped {skipped}...");
            }

            var components = allComponents
                .GroupBy(c => c.ShortName)
                .ToDictionary(
                    g => g.Key,
                    g => (Component: g.First(), Count: g.Count()));

            RhinoApp.WriteLine($"Got {allComponents.Count} total components, {components.Count} unique elements.");

            var bom = new List<BomLine>();
            // 3DM is the 3D model like STP; 3DMDrawing is the DWG's projections in a 3dm
            var formatList = new string[] { "DWG", "STP", "3DM", "3DMDrawing", "DryRun" };

            var gs = new GetOption();
            gs.SetCommandPrompt("Export options");
            gs.AcceptNothing(true);
            
            var formatIndex = FormatIndex;
            var groupToggle = new OptionToggle(GroupByType, "No", "Yes");
            var bomToggle = new OptionToggle(ExportBoM, "No", "Yes");

            while (true)
            {
                gs.ClearCommandOptions();
                var formatOption = gs.AddOptionList("Format", formatList, formatIndex);
                var groupOption = gs.AddOptionToggle("GroupByType", ref groupToggle);
                var bomOption = gs.AddOptionToggle("ExportBoM", ref bomToggle);
                var masterOption = gs.AddOptionList("MasterFile", MasterFileModes, MasterFile);
                var viewsOption = gs.AddOption("Views");
                var spacingOption = gs.AddOption("Spacing");

                var res = gs.Get();
                if (res == GetResult.Option)
                {
                    var option = gs.Option();
                    if (option.Index == formatOption)
                        FormatIndex = formatIndex = option.CurrentListOptionIndex;
                    else if (option.Index == groupOption)
                        GroupByType = groupToggle.CurrentValue;
                    else if (option.Index == bomOption)
                        ExportBoM = bomToggle.CurrentValue;
                    else if (option.Index == masterOption)
                        MasterFile = option.CurrentListOptionIndex;
                    else if (option.Index == viewsOption)
                        GetViews();
                    else if (option.Index == spacingOption)
                        GetSpacing();

                    continue;
                }
                else if (res == GetResult.Nothing)
                {
                    break;
                }
                else
                {
                    return Result.Cancel;
                }
            }

            var exportFormat = formatList[formatIndex];

            //string exportFormat = gs.StringResult();
            if (string.IsNullOrWhiteSpace(exportFormat))
                return Result.Failure;
            var formatExtension = exportFormat.ToLower();
            var fileExtension = formatExtension == "3dmdrawing" ? "3dm" : formatExtension;

            // Select export directory.
            SelectFolderDialog dialog = new SelectFolderDialog
            {
                Title = "Select output directory",
                Directory = "C:/tmp",
            };

            string directory = string.Empty;

            // Show the dialog and check if the user clicked OK
            if (dialog.ShowDialog(Rhino.UI.RhinoEtoApp.MainWindow) == DialogResult.Ok)
            {
                directory = dialog.Directory;
                Rhino.RhinoApp.WriteLine($"Exporting to: {directory}");
            }

            if (string.IsNullOrEmpty(directory))
                return Result.Failure;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Parts collected for the master file, in the order they were exported
            var masterParts = MasterFile > 0 && formatExtension != "dryrun" ? new List<MasterPart>() : null;
            var masterOnly = masterParts != null && MasterFile == 2;

            foreach ((string name, (IComponent component, int count)) in components)
            {
                var transform = Transform.Identity;

                RhinoApp.WriteLine($"Exporting {name}...");

                if (true) // TODO: Change this to a flag to either export in global space or local space
                {
                    transform = Transform.PlaneToPlane(component.Plane, Plane.WorldXY);
                }

                //GeometryBase geo = null;
                IEnumerable<GeometryBase> geometries = null;

                // --- Get geometry ---
                var detailedGeometry = Utility.GetMember(component, "DetailedGeometry", doc);
                if (detailedGeometry.Any())
                {
                    geometries = detailedGeometry;
                }
                else
                {
                    var geometry = Utility.GetMember(component, "Geometry", doc);
                    if (geometry.Any())
                    {
                        geometries = geometry;
                    }
                }

                if (geometries == null)
                {
                    RhinoApp.WriteLine($"{name} : Could not find suitable geometry to export.");
                    return Result.Failure;
                }

                // Get bounds for BoM
                var bounds = BoundingBox.Empty;
                foreach (var geometry in geometries)
                {
                    bounds.Union(geometry.GetBoundingBox(component.Label.Plane));
                }

                var width = Math.Round(bounds.Diagonal.Y, 1);
                var height = Math.Round(bounds.Diagonal.Z, 1);
                var length = Math.Round(bounds.Diagonal.X, 1);

                bom.Add(new BomLine(name, count, length, width, height));

                // No path: the part is only collected for the master file
                string? exportPath;
                if (masterOnly)
                {
                    exportPath = null;
                }
                else if (GroupByType)
                {
                    var typeDirectory = Path.Join(directory, component.TypeID);
                    if (!Directory.Exists(typeDirectory))
                    {
                        Directory.CreateDirectory(typeDirectory);
                    }
                    exportPath = Path.Join(typeDirectory, $"{name}.{fileExtension}");
                }
                else
                {
                    exportPath = Path.Join(directory, $"{name}.{fileExtension}");
                }

                Result res;
                switch (formatExtension)
                {
                    case ("stp"):
                        res = ExportStep(exportPath, geometries, name, bounds, transform, masterParts);
                        break;
                    case ("3dm"):
                        res = Export3dm(exportPath, geometries, name, bounds, transform, masterParts);
                        break;
                    case ("dwg"):
                        res = ExportDwg(exportPath, geometries, name, bounds, transform, masterParts);
                        break;
                    case ("3dmdrawing"):
                        res = ExportDwg(exportPath, geometries, name, bounds, transform, masterParts, as3dm: true);
                        break;
                    case ("dryrun"):
                        RhinoApp.WriteLine($"Exported {name} (dry run).");
                        res = Result.Success;
                        break;
                    default:
                        RhinoApp.WriteLine($"Unknown export format '{formatExtension}'.");
                        return Result.Failure;
                }

                if (res != Result.Success)
                    return Result.Failure;
            }

            if (masterParts != null && masterParts.Count > 0)
            {
                var masterPath = Path.Join(directory, $"master.{fileExtension}");
                var sortedParts = masterParts
                    .OrderBy(p => NameKey(p.Name).Prefix)
                    .ThenBy(p => NameKey(p.Name).Number)
                    .ToList();

                if (WriteMasterFile(masterPath, formatExtension, sortedParts) != Result.Success)
                {
                    RhinoApp.WriteLine($"Could not write master file {masterPath}.");
                    return Result.Failure;
                }
                RhinoApp.WriteLine($"Wrote master file with {sortedParts.Count} parts: {masterPath}");
            }

            if (ExportBoM)
            {
                // Export BoM
                var bomPath = Path.Join(directory, $"bom.csv");

                var sorted = bom
                    .OrderBy(b => NameKey(b.Name).Prefix)
                    .ThenBy(b => NameKey(b.Name).Number)
                    .ToList();

                using var writer = new StreamWriter(bomPath);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                csv.Context.RegisterClassMap<BomLineMap>();
                csv.WriteRecords(sorted);
            }

            return Result.Success;
        }

        /// <summary>
        /// Sort key that orders "Name-2" before "Name-10".
        /// </summary>
        /// <summary>
        /// Sub-prompt for the spacing settings, so they stay off the main prompt.
        /// Escape leaves them as they were.
        /// </summary>
        void GetSpacing()
        {
            var go = new GetOption();
            go.SetCommandPrompt("Spacing (Enter to go back)");
            go.AcceptNothing(true);

            var masterGap = new OptionDouble(MasterGap, 0, 1e6);
            var viewGap = new OptionDouble(ViewGap, 0, 1e6);
            var labelGap = new OptionDouble(LabelGap, 0, 1e6);
            var labelHeight = new OptionDouble(LabelHeight, 0.01, 1e6);
            var dimensionsHeight = new OptionDouble(DimensionsHeight, 0.01, 1e6);

            go.AddOptionDouble("MasterGap", ref masterGap, "Gap between parts in the master file");
            go.AddOptionDouble("ViewGap", ref viewGap, "Gap between projections");
            go.AddOptionDouble("LabelGap", ref labelGap, "Gap between a part and its label");
            go.AddOptionDouble("LabelHeight", ref labelHeight, "Text height of the name label");
            go.AddOptionDouble("DimensionsHeight", ref dimensionsHeight, "Text height of the dimensions");

            while (true)
            {
                var res = go.Get();
                if (res == GetResult.Option)
                    continue;
                if (res != GetResult.Nothing)
                    return;
                break;
            }

            MasterGap = masterGap.CurrentValue;
            ViewGap = viewGap.CurrentValue;
            LabelGap = labelGap.CurrentValue;
            LabelHeight = labelHeight.CurrentValue;
            DimensionsHeight = dimensionsHeight.CurrentValue;
        }

        /// <summary>
        /// Sub-prompt for the DWG view settings. Escape leaves them as they were.
        /// </summary>
        void GetViews()
        {
            var go = new GetOption();
            go.SetCommandPrompt("DWG views (Enter to go back)");
            go.AcceptNothing(true);

            var projection = new OptionToggle(ThirdAngle, "FirstAngle", "ThirdAngle");
            var endView = new OptionToggle(EndViewOnPlan, "Top", "Plan");
            var rolledSides = new OptionToggle(RolledSides, "No", "Yes");
            var isometric = new OptionToggle(Isometric, "No", "Yes");

            go.AddOptionToggle("Projection", ref projection);
            go.AddOptionToggle("EndView", ref endView);
            go.AddOptionToggle("RolledSides", ref rolledSides);
            go.AddOptionToggle("Isometric", ref isometric);

            while (true)
            {
                var res = go.Get();
                if (res == GetResult.Option)
                    continue;
                if (res != GetResult.Nothing)
                    return;
                break;
            }

            ThirdAngle = projection.CurrentValue;
            EndViewOnPlan = endView.CurrentValue;
            RolledSides = rolledSides.CurrentValue;
            Isometric = isometric.CurrentValue;
        }

        static (string Prefix, int Number) NameKey(string name)
        {
            int dash = name.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(name.AsSpan(dash + 1), out int n))
                return (name.Substring(0, dash), n);
            return (name, int.MaxValue); // no number: sort after numbered ones
        }

        static Rhino.FileIO.FileDwgWriteOptions DwgOptions => new Rhino.FileIO.FileDwgWriteOptions()
        {
            Version = Rhino.FileIO.FileDwgWriteOptions.AutocadVersion.Acad2010,
        };

        static Rhino.FileIO.FileStpWriteOptions StpOptions => new Rhino.FileIO.FileStpWriteOptions()
        {
            Schema = Rhino.FileIO.FileStpWriteOptions.StepSchema.SF_203,
            Export2dCurves = true,
            ExportBlack = true,
        };

        public Result ExportDwg(string? dwgPath, IEnumerable<GeometryBase> geometries, string name, BoundingBox bounds, Transform? transform = null, List<MasterPart>? masterParts = null, bool as3dm = false)
        {
            transform = transform ?? Transform.Identity;

            using var doc = RhinoDoc.CreateHeadless("");
            var attributes = RhinoDoc.ActiveDoc.CreateDefaultAttributes();
            attributes.Name = name;

            var baseLayerName = "tas::2D";
            var baseLayerId = doc.Layers.FindByFullPath(baseLayerName, -1);
            if (baseLayerId < 0)
                baseLayerId = doc.Layers.AddPath(baseLayerName, System.Drawing.Color.Black);

            var attr = doc.CreateDefaultAttributes();
            attr.Name = name;
            attr.LayerIndex = baseLayerId;

            var width = bounds.Diagonal.Y;
            var height = bounds.Diagonal.Z;
            var length = bounds.Diagonal.X;

            // The plan (looking down -Z on the face) is the principal or "front"
            // view and sits at the origin; the top view looks along -Y and the end
            // views along X. First angle (ISO E) draws each view on the side
            // opposite the one it is seen from, third angle (ISO A) on the side it
            // is seen from. Views beside the plan are turned so that the edge on
            // the fold line between them is the same edge in both.
            var local = BoundingBox.Empty;
            foreach (var geometry in geometries)
                local.Union(geometry.GetBoundingBox(transform.Value));

            var nextToPlan = new Vector3d(local.Diagonal.X + ViewGap, 0, 0);
            var endViewDirection = ThirdAngle ? -Vector3d.XAxis : Vector3d.XAxis;

            Create2dDrawing(doc, geometries, name, transform.Value, -Vector3d.ZAxis, Vector3d.Zero, includeText: true);

            if (RolledSides)
            {
                // The part rolled a quarter turn at a time about its long axis, each
                // face seen from above and stacked under the plan so neighbouring
                // faces share an edge. Sides are numbered from the plan face.
                var sideTops = new List<double> { local.Diagonal.Y };
                var stripHeights = new[] { local.Diagonal.Z, local.Diagonal.Y, local.Diagonal.Z };
                var y = 0.0;
                for (int i = 1; i <= 3; i++)
                {
                    y -= stripHeights[i - 1] + ViewGap;
                    var roll = Transform.Rotation(RhinoMath.ToRadians(-90 * i), Vector3d.XAxis, local.Center) * transform.Value;
                    Create2dDrawing(doc, geometries, name, roll, -Vector3d.ZAxis, new Vector3d(0, y, 0));
                    sideTops.Add(y + stripHeights[i - 1]);
                }

                for (int i = 0; i < sideTops.Count; i++)
                    doc.Objects.AddText($"{i + 1}", new Plane(new Point3d(-LabelGap, sideTops[i], 0), Vector3d.ZAxis),
                        LabelHeight, "Martian Mono", false, false, TextJustification.TopRight, attr.Duplicate());
            }
            else
            {
                // Top view (seen from +Y): below the plan in first angle, above it in third
                var topOffset = ThirdAngle
                    ? new Vector3d(0, local.Diagonal.Y + ViewGap, 0)
                    : new Vector3d(0, -(local.Diagonal.Z + ViewGap), 0);
                Create2dDrawing(doc, geometries, name, transform.Value, -Vector3d.YAxis, topOffset, rotationDegrees: 180);

                if (!EndViewOnPlan)
                    Create2dDrawing(doc, geometries, name, transform.Value, endViewDirection, topOffset + nextToPlan, rotationDegrees: 180);
            }

            if (EndViewOnPlan || RolledSides)
                // Turned so the part's Y runs up the page as in the plan, with the top
                // face away from the plan in first angle and towards it in third angle
                Create2dDrawing(doc, geometries, name, transform.Value, endViewDirection, nextToPlan, rotationDegrees: ThirdAngle ? 90 : -90);

            var drawingBounds = BoundingBox.Empty;
            foreach (var obj in doc.Objects)
                drawingBounds.Union(obj.Geometry.GetBoundingBox(true));

            if (Isometric)
            {
                // Seen from the front-right corner, above the plan face: the part is
                // turned so that corner direction points up, then drawn like the plan
                var toCamera = new Vector3d(1, -1, 1);
                var isoRight = Vector3d.CrossProduct(-toCamera, Vector3d.ZAxis);
                var isoUp = Vector3d.CrossProduct(toCamera, isoRight);
                var isoTurn = Transform.PlaneToPlane(new Plane(local.Center, isoRight, isoUp), Plane.WorldXY) * transform.Value;

                var isoOffset = new Vector3d(drawingBounds.Max.X + ViewGap, 0, 0);
                Create2dDrawing(doc, geometries, name, isoTurn, -Vector3d.ZAxis, isoOffset, includeHidden: false);
            }

            // Labels go under the lowest view
            var labelTop = Math.Min(drawingBounds.Min.Y, 0) - LabelGap;

            doc.Objects.AddText(
                name,
                new Plane(new Point3d(0, labelTop, 0), Vector3d.ZAxis),
                LabelHeight,
                "Martian Mono",
                false,
                false,
                TextJustification.TopLeft,
                attr.Duplicate()
            );

            doc.Objects.AddText(
                $"{width:0.#} x {height:0.#} x {length:0.#}",
                new Plane(new Point3d(0, labelTop - LabelGap - LabelHeight, 0), Vector3d.ZAxis),
                DimensionsHeight,
                "Martian Mono",
                false,
                false,
                TextJustification.TopLeft,
                attr.Duplicate()
            );

            masterParts?.Add(MasterPart.FromDoc(doc, name));

            if (dwgPath == null)
                return Result.Success;

            var written = as3dm
                ? doc.Write3dmFile(dwgPath, new Rhino.FileIO.FileWriteOptions())
                : Rhino.FileIO.FileDwg.Write(dwgPath, doc, DwgOptions);
            if (written)
                return Result.Success;
            return Result.Failure;
        }

        public Result ExportStep(string? stepPath, IEnumerable<GeometryBase> geometries, string name, BoundingBox bounds, Transform? transform = null, List<MasterPart>? masterParts = null)
        {
            // STP is a geometry export only: no text
            using var headless = Create3dDoc(geometries, name, transform ?? Transform.Identity, includeText: false);
            masterParts?.Add(MasterPart.FromDoc(headless, name));

            if (stepPath == null || Rhino.FileIO.FileStp.Write(stepPath, headless, StpOptions))
                return Result.Success;
            return Result.Failure;
        }

        public Result Export3dm(string? path, IEnumerable<GeometryBase> geometries, string name, BoundingBox bounds, Transform? transform = null, List<MasterPart>? masterParts = null)
        {
            using var headless = Create3dDoc(geometries, name, transform ?? Transform.Identity);
            masterParts?.Add(MasterPart.FromDoc(headless, name));

            if (path == null || headless.Write3dmFile(path, new Rhino.FileIO.FileWriteOptions()))
                return Result.Success;
            return Result.Failure;
        }

        /// <summary>
        /// Headless document with the part's 3D geometry in its local frame.
        /// </summary>
        static RhinoDoc Create3dDoc(IEnumerable<GeometryBase> geometries, string name, Transform transform, bool includeText = true)
        {
            var headless = RhinoDoc.CreateHeadless("");
            var attributes = headless.CreateDefaultAttributes();
            attributes.Name = name;

            foreach (var geometry in geometries)
            {
                if (!includeText && geometry is TextEntity)
                    continue;

                var temp = geometry.Duplicate();
                temp.Transform(transform);
                headless.Objects.Add(temp, attributes.Duplicate());
            }
            return headless;
        }

        /// <summary>
        /// Copy of everything that went into one exported file, kept so the
        /// master file can be assembled without recomputing the drawings.
        /// </summary>
        public class MasterPart
        {
            public string Name;
            public List<(GeometryBase Geometry, ObjectAttributes Attributes, string LayerPath, System.Drawing.Color LayerColor)> Objects = new();
            public BoundingBox Bounds = BoundingBox.Empty;

            public MasterPart(string name)
            {
                Name = name;
            }

            public static MasterPart FromDoc(RhinoDoc doc, string name)
            {
                var part = new MasterPart(name);
                foreach (var obj in doc.Objects)
                {
                    var geometry = obj.Geometry?.Duplicate();
                    if (geometry == null)
                        continue;

                    var layer = doc.Layers[obj.Attributes.LayerIndex];
                    part.Objects.Add((geometry, obj.Attributes.Duplicate(), layer.FullPath, layer.Color));
                    part.Bounds.Union(geometry.GetBoundingBox(true));
                }
                return part;
            }
        }

        /// <summary>
        /// Lays the exported parts out in a row along Y, each one grouped under
        /// its name, and writes them to a single file in the export format.
        /// </summary>
        public Result WriteMasterFile(string path, string formatExtension, List<MasterPart> parts)
        {
            parts = parts.Where(p => p.Bounds.IsValid).ToList();
            if (parts.Count == 0)
                return Result.Failure;

            using var doc = RhinoDoc.CreateHeadless("");

            // Parts without a label of their own (3DM) get their name under them.
            // STP is geometry only, so it gets no labels.
            var labelled = formatExtension != "stp";
            var labelLayerIndex = labelled ? doc.Layers.AddPath("Labels", System.Drawing.Color.Black) : -1;

            var cursor = 0.0;
            foreach (var part in parts)
            {
                // Room for a master label under parts that bring no text of their own
                if (labelled && !part.Objects.Any(o => o.Geometry is TextEntity))
                    cursor += LabelHeight + LabelGap;

                var xform = Transform.Translation(-part.Bounds.Min.X, cursor - part.Bounds.Min.Y, 0);
                var ids = new List<Guid>();
                var textCount = 0;

                foreach (var (geometry, attributes, layerPath, layerColor) in part.Objects)
                {
                    var layerIndex = doc.Layers.FindByFullPath(layerPath, -1);
                    if (layerIndex < 0)
                        layerIndex = doc.Layers.AddPath(layerPath, layerColor);

                    var temp = geometry.Duplicate();
                    temp.Transform(xform);
                    var attr = attributes.Duplicate();
                    attr.LayerIndex = layerIndex;

                    Guid id;
                    if (temp is TextEntity textEntity)
                    {
                        // Re-created rather than copied: the copy keeps the source
                        // document's dimension style and does not come through
                        id = doc.Objects.AddText(
                            textEntity.PlainText,
                            textEntity.Plane,
                            textEntity.TextHeight,
                            textEntity.Font?.QuartetName ?? "Martian Mono",
                            false,
                            false,
                            textEntity.Justification,
                            attr);
                        if (id != Guid.Empty)
                            textCount++;
                    }
                    else
                    {
                        id = doc.Objects.Add(temp, attr);
                    }

                    if (id != Guid.Empty)
                        ids.Add(id);
                }

                if (labelled && textCount == 0)
                {
                    // Label sits under the part, top-left at its corner
                    var labelCorner = new Point3d(0, cursor - LabelGap, part.Bounds.Min.Z);
                    var labelAttr = new ObjectAttributes { Name = part.Name, LayerIndex = labelLayerIndex };
                    var id = doc.Objects.AddText(
                        part.Name,
                        new Plane(labelCorner, Vector3d.ZAxis),
                        LabelHeight,
                        "Martian Mono",
                        false,
                        false,
                        TextJustification.TopLeft,
                        labelAttr);
                    if (id != Guid.Empty)
                        ids.Add(id);
                }

                if (ids.Count > 0)
                    doc.Groups.Add(part.Name, ids);

                cursor += part.Bounds.Diagonal.Y + MasterGap;
            }

            bool ok = formatExtension switch
            {
                "dwg" => Rhino.FileIO.FileDwg.Write(path, doc, DwgOptions),
                "stp" => Rhino.FileIO.FileStp.Write(path, doc, StpOptions),
                "3dm" or "3dmdrawing" => doc.Write3dmFile(path, new Rhino.FileIO.FileWriteOptions()),
                _ => false,
            };
            return ok ? Result.Success : Result.Failure;
        }

        RhinoViewport CreateView(RhinoDoc doc, Vector3d direction)
        {
            var vp = new RhinoViewport();

            Point3d target = new Point3d(0, 0, 0);
            direction.Unitize();

            // Z up for every view except those looking straight up or down,
            // which get Y up (the up vector must not be parallel to the view)
            vp.CameraUp = Math.Abs(Math.Abs(direction * Vector3d.ZAxis) - 1) < 1e-06 ? Vector3d.YAxis : Vector3d.ZAxis;
            vp.SetCameraDirection(direction, false);
            vp.SetCameraTarget(target, false);

            vp.SetCameraLocation(target - direction * 1e5, false);

            // Last, so nothing above can leave the view in perspective
            vp.ChangeToParallelProjection(true);

            return vp;
        }

        Result Create2dDrawing(
            RhinoDoc doc,
            IEnumerable<GeometryBase> geometries,
            string geometryName,
            Transform transform,
            Vector3d direction,
            Vector3d offset,
            string baseLayerName = "tas::2D",
            double rotationDegrees = 0,
            bool includeHidden = true,
            bool includeText = false)
        {

            var baseLayerId = doc.Layers.FindByFullPath(baseLayerName, -1);
            if (baseLayerId < 0)
                baseLayerId = doc.Layers.AddPath(baseLayerName, System.Drawing.Color.Black);

            var visibleLayerName = baseLayerName + "::Visible";
            var visibleLayerId = doc.Layers.FindByFullPath(visibleLayerName, -1);
            if (visibleLayerId < 0)
                visibleLayerId = doc.Layers.AddPath(visibleLayerName, System.Drawing.Color.Black);

            var hiddenLayerName = baseLayerName + "::Hidden";
            var hiddenLayerId = doc.Layers.FindByFullPath(hiddenLayerName, -1);
            if (hiddenLayerId < 0)
                hiddenLayerId = doc.Layers.AddPath(hiddenLayerName, System.Drawing.Color.LightGray);


            var hld_params = new HiddenLineDrawingParameters
            {
                AbsoluteTolerance = doc.ModelAbsoluteTolerance,
                IncludeTangentEdges = false,
                IncludeHiddenCurves = includeHidden
            };

            var attr = doc.CreateDefaultAttributes();
            attr.Name = geometryName;
            attr.LayerIndex = baseLayerId;

            var vp = CreateView(doc, direction);
            hld_params.SetViewport(vp);

            var texts = new List<TextEntity>();
            foreach (var geometry in geometries)
            {
                if (geometry != null)
                {
                    if (geometry is TextEntity textEntity)
                        texts.Add(textEntity);
                    else
                        hld_params.AddGeometry(geometry, transform, geometryName);
                }
            }

            //RhinoApp.WriteLine($"   -- Computing...");

            var hld = HiddenLineDrawing.Compute(hld_params, true);

            if (hld != null)
            {
                //RhinoApp.WriteLine($"   -- Flattening...");

                var flatten = Transform.PlanarProjection(Plane.WorldXY);
                if (rotationDegrees != 0)
                    flatten = Transform.Rotation(RhinoMath.ToRadians(rotationDegrees), Vector3d.ZAxis, Point3d.Origin) * flatten;

                // Move the flattened drawing so its lower-left corner is at the origin
                BoundingBox page_box = hld.BoundingBox(true);
                page_box.Transform(flatten);
                var delta_3d = Transform.Translation(new Vector3d(-page_box.Min.X, -page_box.Min.Y, 0.0));
                flatten = delta_3d * flatten;

                // Text in the part's geometry (e.g. markings) is placed as seen from
                // above, so it only belongs in a plan drawn without rotation
                if (includeText)
                {
                    foreach (var text in texts)
                    {
                        var temp = (TextEntity)text.Duplicate();
                        temp.Transform(transform);
                        temp.Transform(delta_3d);
                        temp.Translate(offset - new Vector3d(0, 0, temp.Plane.OriginZ));
                        doc.Objects.AddText(temp, attr);
                    }
                }

                var h_attribs = new ObjectAttributes { Name = geometryName, LayerIndex = hiddenLayerId };
                var v_attribs = new ObjectAttributes { Name = geometryName, LayerIndex = visibleLayerId };

                //RhinoApp.WriteLine($"   -- Got {hld.Segments.Count()} segments.");
                foreach (var hld_curve in hld.Segments)
                {

                    if (hld_curve?.ParentCurve == null || hld_curve.ParentCurve.SilhouetteType == SilhouetteType.None)
                        continue;

                    var crv = hld_curve.CurveGeometry.DuplicateCurve();
                    if (crv != null)
                    {
                        crv.Transform(flatten);
                        crv.Translate(offset);
                        switch (hld_curve.SegmentVisibility)
                        {
                            case HiddenLineDrawingSegment.Visibility.Visible:
                                doc.Objects.AddCurve(crv, v_attribs);
                                break;
                            case HiddenLineDrawingSegment.Visibility.Hidden:
                                doc.Objects.AddCurve(crv, h_attribs);
                                break;
                        }
                    }
                }

                foreach (var hld_pt in hld.Points)
                {
                    if (hld_pt == null)
                        continue;

                    var pt = hld_pt.Location;
                    if (pt.IsValid)
                    {
                        pt.Transform(flatten);
                        pt += offset;
                        switch (hld_pt.PointVisibility)
                        {
                            case HiddenLineDrawingPoint.Visibility.Visible:
                                doc.Objects.AddPoint(pt, v_attribs);
                                break;
                            case HiddenLineDrawingPoint.Visibility.Hidden:
                                doc.Objects.AddPoint(pt, h_attribs);
                                break;
                        }
                    }
                }
                return Result.Success;
            }
            return Result.Failure;
        }
    }
}
