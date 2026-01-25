using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Geometry;
using Rhino.UI;
using Rhino.UI.Controls;

namespace GluLamb.Views
{
  /// <summary>
  /// Required class GUID, used as the panel Id
  /// </summary>
  [System.Runtime.InteropServices.Guid("0E7780CA-F004-4AE7-B918-19E68BF7C7C9")]
  public class ModelPanel : Panel, IPanel
  {
    readonly uint m_document_sn = 0;

    /// <summary>
    /// Provide easy access to the SampleCsEtoPanel.GUID
    /// </summary>
    public static System.Guid PanelId => typeof(ModelPanel).GUID;

    private GridView gridView;
    private TimberBeam selectedBeam;

    public Label ActiveObjectLabel;

    /// <summary>
    /// Required public constructor with NO parameters
    /// </summary>
    public ModelPanel(uint documentSerialNumber)
    {
        m_document_sn = documentSerialNumber;

        Title = GetType().Name;

            RhinoApp.WriteLine($"Creating GluLambPropertiesPageControl...");
            //Title = GetType().Name;

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
                selectedBeam = gridView.SelectedItem as TimberBeam;
            };

            // Create a button to select the beam in the Rhino model
            var selectButton = new Eto.Forms.Button { Text = "Select Beam in Model" };
            var svgContents = @"
                <?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>
                <svg
                   width=""40mm""
                   height=""40mm""
                   viewBox=""0 0 40 40""
                    version=""1.1""
                   id=""svg1""
                   xmlns=""http://www.w3.org/2000/svg""
                   xmlns:svg=""http://www.w3.org/2000/svg"">
                  <defs
                     id=""defs1"" />
                  <g
                     id=""layer1"">
                    <ellipse
                       style=""opacity:0.615385;fill:#000000;stroke-width:1.4;stroke-linecap:round;stroke-linejoin:round;paint-order:stroke markers fill""
                       id=""path1""
                       cx=""20""
                       cy=""20""
                       rx=""7.5446095""
                       ry=""6.0724902"" />
                  </g>
                </svg>
                ";
            selectButton.Image = Rhino.UI.ImageResources.CreateEtoBitmap(svgContents, 50, 50, false);

            selectButton.Click += (sender, e) => SelectBeamInModel();

            var zoomButton = new Button { Text = "Zoom to Beam" };
            zoomButton.Click += (sender, e) => ZoomToBeam();

            var assignButton = new Button { Text = "Assign Beams" };
            assignButton.Click += (sender, e) => AssignBeams();

            var removeButton = new Button { Text = "Remove Beam" };
            removeButton.Click += (sender, e) => RemoveBeam();

            var clearButton = new Button { Text = "Clear Beams" };
            clearButton.Click += (sender, e) => GluLambPlugin.Instance.ActiveModel.ClearBeams();

            var rotateButton = new Button { Text = "Rotate Beam" };
            rotateButton.Click += (sender, e) =>
            {
                GluLambPlugin.Instance.ActiveModel.RotateBeam(selectedBeam);
                RhinoDoc.ActiveDoc.Views.Redraw();
            };

            var flipButton = new Button { Text = "Flip Beam" };
            flipButton.Click += (sender, e) => {
                GluLambPlugin.Instance.ActiveModel.FlipBeam(selectedBeam);
                RhinoDoc.ActiveDoc.Views.Redraw();
            };

            ActiveObjectLabel = new Label();

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
            layout.AddSeparateRow(ActiveObjectLabel, null);
            layout.Add(null);
            Content = layout;
        }


    public string Title { get; }
  
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
                if (obj != null)
                {


                    for (int i = obj.Geometry.UserData.Count - 1; i >= 0; --i)
                    {
                        if (obj.Geometry.UserData[i] is TimberBeamUserData)
                        {
                            obj.Geometry.UserData.Remove(obj.Geometry.UserData[i]);
                        }
                    }
                    GluLambPlugin.Instance.ActiveModel.Beams.Remove(selectedBeam);
                }

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

        #region IPanel methods
        public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
      // Called when the panel tab is made visible, in Mac Rhino this will happen
      // for a document panel when a new document becomes active, the previous
      // documents panel will get hidden and the new current panel will get shown.
      //Rhino.RhinoApp.WriteLine($"Panel shown for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
    {
      // Called when the panel tab is hidden, in Mac Rhino this will happen
      // for a document panel when a new document becomes active, the previous
      // documents panel will get hidden and the new current panel will get shown.
      //Rhino.RhinoApp.WriteLine($"Panel hidden for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
    }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
      // Called when the document or panel container is closed/destroyed
      //Rhino.RhinoApp.WriteLine($"Panel closing for document {documentSerialNumber}, this serial number {m_document_sn} should be the same");
    }
    #endregion IPanel methods
  }
}
