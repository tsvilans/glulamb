using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    class BeamPropertiesPage : ObjectPropertiesPage
    {
        private BeamPropertiesPageControl m_page_control;

        public override string EnglishPageTitle => "GluLamb";

        public override System.Drawing.Icon PageIcon(System.Drawing.Size sizeInPixels)
        {
            var icon = Rhino.UI.DrawingUtilities.LoadIconWithScaleDown(
              "GluLamb.ico",
              sizeInPixels.Width,
              GetType().Assembly);
            return icon;
        }

        public override object PageControl => m_page_control ?? (m_page_control = new BeamPropertiesPageControl());

        public override bool ShouldDisplay(ObjectPropertiesPageEventArgs e)
        {
            Debug.WriteLine("SampleCsEtoPropertiesPage.ShouldDisplay()");
            return true;
        }

        public override void UpdatePage(ObjectPropertiesPageEventArgs e)
        {
            var objs = e.GetObjects<RhinoObject>();
            var control = PageControl as BeamPropertiesPageControl;

            if (objs.Length > 0)
            {
                //RhinoApp.WriteLine("Got {0} objects", objs.Length);
                //RhinoApp.WriteLine($"{objs[0].Name} -> {objs[0].Id}");
                control.ActiveObjectLabel.Text = $"{objs[0].Name} ({objs[0].Id})";
            }
            else
            {

                control.ActiveObjectLabel.Text = $"";
            }

            Debug.WriteLine("GluLambPropertiesPage.UpdatePage()");
        }
    }

    //[System.Runtime.InteropServices.Guid("555FF0BE-197A-45C1-80F5-19185693A4C8")]
    public class BeamPropertiesPageControl : Panel
    {
        //public static Guid PanelId => typeof(GluLambPropertiesPageControl).GUID;

        //private TimberBeam selectedBeam;

        public Label ActiveObjectLabel;

        public BeamPropertiesPageControl()
        {        

            ActiveObjectLabel = new Label();

            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };
            layout.AddSeparateRow(ActiveObjectLabel, null);
            layout.Add(null);
            Content = layout;
        }

        //public string Title { get; }

    }
}
