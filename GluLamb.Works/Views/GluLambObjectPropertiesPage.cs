using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using GluLamb;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using Rhino.UI.Controls;

namespace GluLamb.Views
{
    class GluLambObjectPropertiesPage : ObjectPropertiesPage
    {
        private GluLambPropertiesPageControl m_page_control;

        public override string EnglishPageTitle => "GluLamb";

        public override System.Drawing.Icon PageIcon(System.Drawing.Size sizeInPixels)
        {
            var icon = Rhino.UI.DrawingUtilities.LoadIconWithScaleDown(
              "GWorks.Properties.Resources.GluLamb.ico",
              sizeInPixels.Width,
              GetType().Assembly);
            return icon;
            
        }

        public override object PageControl => m_page_control ?? (m_page_control = new GluLambPropertiesPageControl());

        public override bool ShouldDisplay(ObjectPropertiesPageEventArgs e)
        {
            Debug.WriteLine("SampleCsEtoPropertiesPage.ShouldDisplay()");
            return true;
        }

        public override void UpdatePage(ObjectPropertiesPageEventArgs e)
        {
            var objs = e.GetObjects<BrepObject>();
            if (objs.Length > 0)
            {
                RhinoApp.WriteLine("Got {0} objects", objs.Length);
            }
            Debug.WriteLine("GluLambPropertiesPage.UpdatePage()");
        }
    }

    //[System.Runtime.InteropServices.Guid("555FF0BE-197A-45C1-80F5-19185693A4C8")]
    public class GluLambPropertiesPageControl : Panel
    {
        //public static Guid PanelId => typeof(GluLambPropertiesPageControl).GUID;

        private GridView gridView;
        private TimberBeam selectedBeam;

        public GluLambPropertiesPageControl()
        {

            //Title = GetType().Name;

            gridView = new GridView
            {
                DataStore = GluLambPlugin.Instance.Model.Beams
            };

            // Add Columns to the GridView
            gridView.Columns.Add(new GridColumn { HeaderText = "Name", DataCell = new TextBoxCell(nameof(TimberBeam.Name)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Width", DataCell = new TextBoxCell(nameof(TimberBeam.Width)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Height", DataCell = new TextBoxCell(nameof(TimberBeam.Height)) });
            gridView.Columns.Add(new GridColumn { HeaderText = "Length", DataCell = new TextBoxCell(nameof(TimberBeam.Length)) });

            // Handle GridView selection
            gridView.SelectionChanged += (sender, e) =>
            {
                selectedBeam = gridView.SelectedItem as TimberBeam;
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
            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };
            layout.BeginVertical();
            layout.Add(gridView, true, true);
            layout.EndVertical();

            layout.AddSeparateRow(selectButton, zoomButton, null);
            layout.AddSeparateRow(assignButton, removeButton, null);
            layout.AddRow(new Divider());
            layout.AddSeparateRow(flipButton, rotateButton, null);
            layout.AddSeparateRow(clearButton, null);
            layout.AddRow(new Divider());
            layout.AddSeparateRow(new Label { Text = "GluLamb" }, null);
            layout.Add(null);
            Content = layout;
        }

        //public string Title { get; }

        private void RotateBeam()
        {
            if (selectedBeam != null)
            {
                GluLambPlugin.Instance.Model.RotateBeam(selectedBeam);
                RhinoDoc.ActiveDoc.Views.Redraw();
            }
        }

        private void FlipBeam()
        {
            if (selectedBeam != null)
            {
                GluLambPlugin.Instance.Model.FlipBeam(selectedBeam);
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
                        GluLambPlugin.Instance.Model.Beams.Remove(new TimberBeam() { Id = obj.Id });
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
            else if (GluLambPlugin.Instance.Model.Beams.Contains(selectedBeam))
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
                GluLambPlugin.Instance.Model.Beams.Remove(selectedBeam);

                gridView.SelectedRow = Math.Min(currentRow, gridView.DataStore.Count() - 1);
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

            RhinoApp.WriteLine($"Got {getter.Objects().Length} objects.");

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

                    var bplane = Utility.FindBestBasePlane(brep, Vector3d.Unset);
                    //var bplane = Plane.WorldXY;
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

                    int index = GluLambPlugin.Instance.Model.Beams.IndexOf(timberBeam);
                    if (index >= 0)
                    {
                        RhinoApp.WriteLine($"Updating beam {timberBeam}");
                        GluLambPlugin.Instance.Model.Beams.RefreshItem(timberBeam);
                    }
                    else
                    {
                        GluLambPlugin.Instance.Model.Beams.Add(timberBeam);
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
            GluLambPlugin.Instance.Model.ClearBeams();
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

        protected void OnHelloButton()
        {
            Dialogs.ShowMessage("Hello Rhino!", "GluLamb");
        }

    }
}
