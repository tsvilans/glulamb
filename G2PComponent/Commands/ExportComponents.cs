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
        public bool MasterFile = true;

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
            var formatList = new string[] { "DWG", "STP", "DryRun" };

            var gs = new GetOption();
            gs.SetCommandPrompt("Export options");
            gs.AcceptNothing(true);
            
            var formatIndex = 0;
            var groupToggle = new OptionToggle(true, "No", "Yes");
            var bomToggle = new OptionToggle(true, "No", "Yes");

            while (true)
            {
                gs.ClearCommandOptions();
                var formatOption = gs.AddOptionList("Format", formatList, formatIndex);
                var groupOption = gs.AddOptionToggle("GroupByType", ref groupToggle);
                var bomOption = gs.AddOptionToggle("ExportBoM", ref bomToggle);

                var res = gs.Get();
                if (res == GetResult.Option)
                {
                    var option = gs.Option();
                    if (option.Index == formatOption)
                        formatIndex = option.CurrentListOptionIndex;
                    else if (option.Index == groupOption)
                        GroupByType = groupToggle.CurrentValue;
                    else if (option.Index == bomOption)
                        ExportBoM = bomToggle.CurrentValue;

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

                string exportPath;
                if (GroupByType)
                {
                    var typeDirectory = Path.Join(directory, component.TypeID);
                    if (!Directory.Exists(typeDirectory))
                    {
                        Directory.CreateDirectory(typeDirectory);
                    }
                    exportPath = Path.Join(typeDirectory, $"{name}.{formatExtension}");
                }
                else
                {
                    exportPath = Path.Join(directory, $"{name}.{formatExtension}");
                }

                Result res;
                switch (formatExtension)
                {
                    case ("stp"):
                        res = ExportStep(exportPath, geometries, name, bounds, transform);
                        break;
                    case ("dwg"):
                        res = ExportDwg(exportPath, geometries, name, bounds, transform);
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

            if (ExportBoM)
            {
                // Export BoM
                var bomPath = Path.Join(directory, $"bom.csv");

                static (string Prefix, int Number) NameKey(string name)
                {
                    int dash = name.LastIndexOf('-');
                    if (dash >= 0 && int.TryParse(name.AsSpan(dash + 1), out int n))
                        return (name.Substring(0, dash), n);
                    return (name, int.MaxValue); // no number: sort after numbered ones
                }

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

        public Result ExportDwg(string dwgPath, IEnumerable<GeometryBase> geometries, string name, BoundingBox bounds, Transform? transform = null)
        {
            transform = transform ?? Transform.Identity;

            var doc = RhinoDoc.CreateHeadless("");
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

            doc.Objects.AddText(
                name,
                new Plane(new Point3d(0, -10, 0), Vector3d.ZAxis),
                10,
                "Martian Mono",
                false,
                false,
                TextJustification.TopLeft,
                attr.Duplicate()
            );

            doc.Objects.AddText(
                $"{width:0.#} x {height:0.#} x {length:0.#}",
                new Plane(new Point3d(0, -100, 0), Vector3d.ZAxis),
                5,
                "Martian Mono",
                false,
                false,
                TextJustification.TopLeft,
                attr.Duplicate()
            );

            Create2dDrawing(doc, geometries, name, transform.Value, -Vector3d.ZAxis, Vector3d.Zero);
            Create2dDrawing(doc, geometries, name, Transform.Multiply(Transform.Mirror(bounds.Center, Vector3d.XAxis), transform.Value), -Vector3d.YAxis, new Vector3d(0, 300, 0));

            var dwgOptions = new Rhino.FileIO.FileDwgWriteOptions()
            {
                Version = Rhino.FileIO.FileDwgWriteOptions.AutocadVersion.Acad2010,
            };

            if (Rhino.FileIO.FileDwg.Write(dwgPath, doc, dwgOptions))
                return Result.Success;
            return Result.Failure;
        }

        public Result ExportStep(string stepPath, IEnumerable<GeometryBase> geometries, string name, BoundingBox bounds, Transform? transform = null)
        {
            transform = transform ?? Transform.Identity;

            var headless = RhinoDoc.CreateHeadless("");
            var attributes = RhinoDoc.ActiveDoc.CreateDefaultAttributes();
            attributes.Name = name;


            foreach (var geometry in geometries)
            {
                var temp = geometry.Duplicate();
                temp.Transform(transform.Value);
                headless.Objects.Add(temp, attributes.Duplicate());
            }

            var stpOptions = new Rhino.FileIO.FileStpWriteOptions()
            {
                Schema = Rhino.FileIO.FileStpWriteOptions.StepSchema.SF_203,
                Export2dCurves = true,
                ExportBlack = true,
            };

            if (Rhino.FileIO.FileStp.Write(stepPath, headless, stpOptions))
                return Result.Success;
            return Result.Failure;
        }

        RhinoViewport CreateView(RhinoDoc doc, Vector3d direction)
        {
            var vp = new RhinoViewport();

            vp.ChangeToParallelProjection(true);

            Point3d target = new Point3d(0, 0, 0);
            direction.Unitize();

            vp.CameraUp = Math.Abs(direction * Vector3d.ZAxis) < 1e-06 ? Vector3d.YAxis : Vector3d.ZAxis;
            vp.SetCameraDirection(direction, false);
            vp.SetCameraTarget(target, false);

            vp.SetCameraLocation(target - direction * 1e5, false);

            return vp;
        }

        Result Create2dDrawing(
            RhinoDoc doc,
            IEnumerable<GeometryBase> geometries,
            string geometryName,
            Transform transform,
            Vector3d direction,
            Vector3d offset,
            string baseLayerName = "tas::2D")
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
                IncludeHiddenCurves = true
            };

            var attr = doc.CreateDefaultAttributes();
            attr.Name = geometryName;
            attr.LayerIndex = baseLayerId;

            var vp = CreateView(doc, direction);
            hld_params.SetViewport(vp);

            foreach (var geometry in geometries)
            {
                if (geometry != null)
                {
                    if (geometry is TextEntity textEntity)
                        doc.Objects.AddText(textEntity, attr);
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
                BoundingBox page_box = hld.BoundingBox(true);
                var delta_2d = new Vector2d(0, 0);
                delta_2d = delta_2d - new Vector2d(page_box.Min.X, page_box.Min.Y);
                var delta_3d = Transform.Translation(new Vector3d(delta_2d.X, delta_2d.Y, 0.0));
                flatten = delta_3d * flatten;

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
