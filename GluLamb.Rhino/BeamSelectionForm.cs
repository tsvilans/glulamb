using Eto.Forms;
using Eto.Drawing;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.UI.Forms;

namespace GluLamb.Forms
{
    public class BeamSelectionForm : Form
    {
        private GridView gridView;
        private TimberBeam selectedBeam;

        public BeamSelectionForm()
        {
            Title = "Timber Beam Selection";
            ClientSize = new Size(400, 300);
            Topmost = true;
            // Initialize GridView
            gridView = new GridView
            {
                DataStore = GluLambPlugin.Instance.ActiveModel.Beams
            };

            // Add Columns to the GridView
            gridView.Columns.Add(new GridColumn { HeaderText = "Name", DataCell = new TextBoxCell(nameof(TimberBeam.Name)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Width", DataCell = new TextBoxCell(nameof(TimberBeam.Width)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Height", DataCell = new TextBoxCell(nameof(TimberBeam.Height)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Length", DataCell = new TextBoxCell(nameof(TimberBeam.Length)) });

            // Handle GridView selection
            gridView.SelectionChanged += (sender, e) =>
            {
                selectedBeam = (gridView.SelectedItem as TimberBeam);
            };

            // Create a button to select the beam in the Rhino model
            var selectButton = new Button { Text = "Select Beam in Model" };
            selectButton.Click += (sender, e) => SelectBeamInModel();

            var zoomButton = new Button { Text = "Zoom to Beam" };
            zoomButton.Click += (sender, e) => ZoomToBeam();

            var assignButton = new Button { Text = "Assign Beams" };
            assignButton.Click += (sender, e) => AssignBeams();

            var removeButton = new Button { Text = "Remove Beam" };
            removeButton.Click += (sender, e) => RemoveBeam();

            var clearButton = new Button { Text = "Clear Beams" };
            clearButton.Click += (sender, e) => ClearBeams();

            var rotateButton = new Button { Text = "Rotate Beam" };
            rotateButton.Click += (sender, e) => RotateBeam();

            var flipButton = new Button { Text = "Flip Beam" };
            flipButton.Click += (sender, e) => FlipBeam();

            // Arrange layout
            var layout = new DynamicLayout();
            layout.Add(gridView, true, true);
            layout.AddSeparateRow(null, selectButton, zoomButton, null);
            layout.AddSeparateRow(null, assignButton, removeButton, null);
            layout.AddSeparateRow(null, flipButton, rotateButton, null);
            layout.AddSeparateRow(null, clearButton, null, null);
            Content = layout;
        }

        private RhinoObject FindBeamByName(string name)
        {
            // Create ObjectEnumeratorSettings to find objects by name
            var settings = new ObjectEnumeratorSettings
            {
                NameFilter = name,
                ObjectTypeFilter = ObjectType.AnyObject
            };

            var doc = Rhino.RhinoDoc.ActiveDoc;

            var ids = new System.Collections.Generic.List<Guid>();
            var objects = doc.Objects.GetObjectList(settings).ToArray();

            if (objects.Length < 1)
            {
                MessageBox.Show($"No beam with name {name} found.");
            }

            return objects[0];
        }

        private void RotateBeam()
        {
            if (selectedBeam != null)
            {
                GluLambPlugin.Instance.ActiveModel.RotateBeam(selectedBeam);
                RhinoDoc.ActiveDoc.Views.Redraw();

            }
        }

        private void FlipBeam()
        {
            if (selectedBeam != null)
            {
                GluLambPlugin.Instance.ActiveModel.FlipBeam(selectedBeam);
                RhinoDoc.ActiveDoc.Views.Redraw();

            }
        }
        private void RemoveBeam()
        {
            if (gridView.SelectedRow < 0)
            {
                var getter = new Rhino.Input.Custom.GetObject();
                getter.GeometryFilter = Rhino.DocObjects.ObjectType.Brep;
                getter.GetMultiple(1, 0);

                if (getter.Objects().Length > 0)
                {
                    foreach (var got in getter.Objects())
                    {
                        var obj = got.Object();
                        GluLambPlugin.Instance.ActiveModel.Beams.Remove(new TimberBeam() { Id = obj.Id });
                        for (int i = obj.Geometry.UserData.Count - 1; i >= 0; --i)
                        {
                            if (obj.Geometry.UserData[i] is TimberBeamUserData)
                            {
                                obj.Geometry.UserData.Remove(obj.Geometry.UserData[i]);
                            }
                        }
                    }
                    RhinoDoc.ActiveDoc.Views.Redraw();

                }
            }
            else if (GluLambPlugin.Instance.ActiveModel.Beams.Contains(selectedBeam))
            {
                var currentRow = Math.Max(0, gridView.SelectedRow - 1);
                RhinoApp.WriteLine($"Removing {selectedBeam}...");

                var obj = RhinoDoc.ActiveDoc.Objects.FindId(selectedBeam.Id);

                for (int i = obj.Geometry.UserData.Count - 1; i >= 0; --i)
                {
                    if (obj.Geometry.UserData[i] is TimberBeamUserData)
                    {
                        obj.Geometry.UserData.Remove(obj.Geometry.UserData[i]);
                    }
                }
                GluLambPlugin.Instance.ActiveModel.Beams.Remove(selectedBeam);

                gridView.SelectedRow = Math.Min(currentRow, gridView.DataStore.Count() - 1);

                //selectedBeam = null;

                //gridView.DataStore = BeamModel.Beams.Values.ToList();
            }

            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private void SelectBeamInModel()
        {
            if (selectedBeam != null)
            {

                var doc = RhinoDoc.ActiveDoc;
                var rhinoObject = doc.Objects.FindId(selectedBeam.Id);
                if (rhinoObject != null)
                {
                    doc.Objects.Select(rhinoObject.Id, true);
                    RhinoApp.WriteLine($"Selected {rhinoObject.Name} in the Rhino model.");
                    doc.Views.Redraw();
                }
                else
                {
                    MessageBox.Show($"Beam {selectedBeam} not found in the Rhino model.", MessageBoxType.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a beam from the list.", MessageBoxType.Warning);
            }
        }

        private void AssignBeams()
        {
            var getter = new Rhino.Input.Custom.GetObject();
            getter.GeometryFilter = Rhino.DocObjects.ObjectType.Brep;
            getter.GetMultiple(1, 0);

            Rhino.RhinoApp.WriteLine($"Got {getter.Objects().Length} objects.");
            if (getter.Objects().Length > 0)
            {
                foreach (var got in getter.Objects())
                {
                    var obj = got.Object();

                    RhinoApp.WriteLine($"Assigning object {obj.Name} ({obj.Id} to beam)");

                    Brep brep = null;
                    if (obj.Geometry is Extrusion ext)
                    {
                        brep = ext.ToBrep(true);
                    }
                    else if (obj.Geometry is Brep b)
                    {
                        brep = b;
                    }

                    if (brep == null) continue;

                    var bplane = GluLamb.Utility.FindBestBasePlane(brep, Vector3d.Unset);
                    // var bplane = Plane.WorldXY;
                    var bb = brep.GetBoundingBox(bplane, out Box worldBox);

                    double width = Math.Ceiling(bb.Max.Z - bb.Min.Z),
                    height = Math.Ceiling(bb.Max.Y - bb.Min.Y),
                    length = Math.Ceiling(bb.Max.X - bb.Min.X);

                    var timberBeam = new TimberBeam()
                    {
                        Name = obj.Name,
                        Id = obj.Id,
                        Length = length,
                        Width = width,
                        Height = height,
                        Plane = bplane,
                        Bounds = bb,
                    };

                    int index = GluLambPlugin.Instance.ActiveModel.Beams.IndexOf(timberBeam);
                    if (index >= 0)
                    {
                        RhinoApp.WriteLine($"Updating beam {timberBeam}");
                        GluLambPlugin.Instance.ActiveModel.Beams.RefreshItem(timberBeam);
                    }
                    else
                    {
                        GluLambPlugin.Instance.ActiveModel.Beams.Add(timberBeam);
                    }

                    var props = new Rhino.Collections.ArchivableDictionary();
                    props.Set("width", width);
                    props.Set("height", height);
                    props.Set("length", length);
                    props.Set("plane", bplane);

                    obj.Geometry.UserDictionary.Set("tasBeam", props);

                    var ud = obj.Geometry.UserData.Find(typeof(TimberBeamUserData)) as TimberBeamUserData;
                    if (ud == null)
                    {
                        ud = new TimberBeamUserData() { BeamRef = timberBeam.Id };
                        obj.Geometry.UserData.Add(ud);
                    }
                    ud.SetTransform(Transform.PlaneToPlane(Plane.WorldXY, bplane));
                    timberBeam.UserData = ud;

                }
                gridView.SelectedRow = gridView.DataStore.Count() - 1;

            }
        }

        private void ClearBeams()
        {
            GluLambPlugin.Instance.ActiveModel.ClearBeams();
            //gridView.DataStore = BeamModel.Beams.Values.ToList();
        }

        private void ZoomToBeam()
        {
            if (selectedBeam != null)
            {

                var doc = RhinoDoc.ActiveDoc;
                var rhinoObject = doc.Objects.FindId(selectedBeam.Id);
                if (rhinoObject != null)
                {
                    var boundingBox = rhinoObject.Geometry.GetBoundingBox(true);
                    doc.Views.ActiveView.ActiveViewport.ZoomBoundingBox(boundingBox);
                    doc.Views.Redraw();
                    RhinoApp.WriteLine($"Zoomed to {rhinoObject.Name}.");
                }
                else
                {
                    MessageBox.Show($"Beam {selectedBeam} not found in the Rhino model.", MessageBoxType.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a beam from the list.", MessageBoxType.Warning);
            }
        }

    }

}
