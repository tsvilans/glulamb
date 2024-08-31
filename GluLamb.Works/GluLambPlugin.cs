
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.PlugIns;
using Rhino.Geometry;
using Rhino.UI;
using Rhino.DocObjects.Custom;

using GluLamb;
using System.Runtime.CompilerServices;
using Rhino;
using Rhino.FileIO;
using System.Security.Cryptography;
using Rhino.DocObjects;
using Rhino.Display;
using Rhino.Input.Custom;
using System.Collections.ObjectModel;

namespace GluLamb
{ 
    //[System.Runtime.InteropServices.Guid("CB9FFD3F-DBFB-4C7D-A36B-83E6C954ED34")]
    public class GluLambPlugin : Rhino.PlugIns.PlugIn
    {
        Rhino.Display.DisplayMaterial MeshMaterial;

        public BeamModel Model { get; set; }

        public GluLambPlugin()
        {
            Instance = this;

            Rhino.Display.DisplayPipeline.DrawOverlay += DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;

            Model = new BeamModel();
        }

        ~GluLambPlugin()
        {
            Rhino.Display.DisplayPipeline.DrawOverlay -= DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            
            //Rhino.Display.DisplayPipeline.DrawForeground -= ETContext.DisplayPipeline_PostDrawObjects;
        }

        public static GluLambPlugin Instance
        {
            get; 
            private set;
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Rhino.RhinoApp.WriteLine("GluLamb v2.0");
            Rhino.RhinoApp.WriteLine(this.Name, this.Version);
            Panels.RegisterPanel(GluLambPlugin.Instance, typeof(GluLamb.Views.SampleCsEtoPanel), "EmaTimber", GWorks.Properties.Resources.GluLamb);
            Panels.OpenPanel(typeof(GluLamb.Views.SampleCsEtoPanel).GUID);

            return base.OnLoad(ref errorMessage);
        }

        protected override void ReadDocument(RhinoDoc doc, BinaryArchiveReader archive, FileReadOptions options)
        {
            base.ReadDocument(doc, archive, options);
            Model.ClearBeams();
            Model.LoadBeams(doc);
        }

        protected override void WriteDocument(RhinoDoc doc, BinaryArchiveWriter archive, FileWriteOptions options)
        {
            base.WriteDocument(doc, archive, options);
        }

        internal static void DisplayPipeline_CalculateBoundingBox(object sender, Rhino.Display.CalculateBoundingBoxEventArgs e)
        {
            BoundingBox bb = new BoundingBox();
            e.IncludeBoundingBox(bb);
            
            foreach (var beam in Instance.Model.Beams)
            {
                var box = beam.Bounds;

                if (beam.UserData != null)
                {
                    box.Transform(beam.UserData.Transform);
                }

                e.IncludeBoundingBox(box);
            }
            
        }

        internal static void DisplayPipeline_PostDrawObjects(object sender, Rhino.Display.DrawEventArgs e)
        {
            
            foreach (var beam in Instance.Model.Beams)
            {
                var obj = RhinoDoc.ActiveDoc.Objects.FindId(beam.Id);
                if (obj == null) continue;
                var ud = obj.Geometry.UserData.Find(typeof(TimberBeamUserData)) as TimberBeamUserData;
                if (ud != null)
                {
                    var xform = ud.Transform;

                    var xaxis = new Line(Point3d.Origin, Point3d.Origin + Vector3d.XAxis * 100);
                    var yaxis = new Line(Point3d.Origin, Point3d.Origin + Vector3d.YAxis * 100);

                    xaxis.Transform(xform);
                    yaxis.Transform(xform);

                    var bounds = Mesh.CreateFromBox(beam.Bounds, 1, 1, 1);
                    bounds.Transform(xform);

                    e.Display.DrawLine(xaxis, System.Drawing.Color.Red);
                    e.Display.DrawLine(yaxis, System.Drawing.Color.LimeGreen);
                    e.Display.DrawMeshWires(bounds, System.Drawing.Color.Cyan);
                }

                // e.Display.DrawLine(beam.Plane.Origin, beam.Plane.Origin + beam.Plane.XAxis * 100, System.Drawing.Color.Red);
                // e.Display.DrawLine(beam.Plane.Origin, beam.Plane.Origin + beam.Plane.YAxis * 100, System.Drawing.Color.LimeGreen);
                // e.Display.DrawMeshWires(Mesh.CreateFromBox(beam.Bounds, 1, 1, 1), System.Drawing.Color.Cyan);

            }
        }

        protected override void OptionsDialogPages(List<OptionsDialogPage> pages)
        {
            var page = new GluLamb.Views.SampleCsEtoOptionsPage();
            pages.Add(page);
        }

        protected override void ObjectPropertiesPages(ObjectPropertiesPageCollection collection)
        {
            var page = new Views.GluLambObjectPropertiesPage();
            collection.Add(page);
        }
    }
}
