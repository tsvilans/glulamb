using D2P_Core;
using D2P_Core.Utility;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Exports each selected component in its local coordinate
    /// system. Detailed geometry - if present - is prioritized.
    /// </summary>
    public class ExportComponentsCommand : Rhino.Commands.Command
    {
        public override string EnglishName => "ExportComponents";

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

            var components = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc);

            // Ask for file extension. TODO: Should be listed options, not free text.
            var gs = new GetString();
            gs.SetDefaultString("STP");
            gs.SetCommandPrompt("Export format");
            gs.AcceptNothing(false);

            gs.Get();
            if (gs.CommandResult() != Result.Success)
                return gs.CommandResult();

            string exportFormat = gs.StringResult();
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


            foreach (var component in components)
            {
                var transform = Transform.Identity;
                var name = component.ShortName;

                RhinoApp.WriteLine($"Exporting {name}...");

                if (true) // TODO: Change this to a flag to either export in global space or local space
                {
                    transform = Transform.PlaneToPlane(component.Plane, Plane.WorldXY);
                }

                GeometryBase geo = null;

                // --- Get geometry ---
                var detailedGeometry = Utility.GetMember(component, "DetailedGeometry", doc).FirstOrDefault();
                if (detailedGeometry != null)
                {
                    geo = detailedGeometry;
                }
                else
                {
                    var geometry = Utility.GetMember(component, "Geometry", doc).FirstOrDefault();
                    if (geometry != null)
                    {
                        geo = geometry;
                    }
                }

                if (geo == null)
                {
                    RhinoApp.WriteLine($"{name} : Could not find suitable geometry to export.");
                    return Result.Failure;
                }

                // --- Handle extrusion ---
                if (geo is Extrusion extrusion)
                    geo = extrusion.ToBrep(true);

                var brep = geo as Brep;

                // Transform
                brep.Transform(transform);

                var attributes = doc.CreateDefaultAttributes();
                attributes.Name = name;

                var headless = RhinoDoc.CreateHeadless("");
                headless.Objects.AddBrep(brep, attributes);

                var fileName = Path.Join(directory, $"{name}.{formatExtension}");

                bool res;
                switch(formatExtension)
                {
                    case ("stp"):
                        var stpOptions = new Rhino.FileIO.FileStpWriteOptions()
                        {
                            Schema = Rhino.FileIO.FileStpWriteOptions.StepSchema.SF_203,
                            Export2dCurves = true,
                            ExportBlack = true,
                        };

                        res = Rhino.FileIO.FileStp.Write(fileName, headless, stpOptions);

                        break;
                    case ("dwg"):
                        // TODO: Make2D of geometry, since DWG export doesn't save 3d data

                        var dwgOptions = new Rhino.FileIO.FileDwgWriteOptions()
                        {
                            Version = Rhino.FileIO.FileDwgWriteOptions.AutocadVersion.Acad2010,
                        };
                        res = Rhino.FileIO.FileDwg.Write(fileName, headless, dwgOptions);
                        break;
                    default:
                        RhinoApp.WriteLine($"Unknown export format '{formatExtension}'.");
                        return Result.Failure;
                }

                if (!res)
                {
                    RhinoApp.WriteLine($"Writing {fileName} failed.");
                    return Result.Failure;
                }
            }

            //if (select.Count > 0)
            //{
            //    doc.Objects.UnselectAll();
            //}
            //doc.Objects.Select(select, true);
            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
