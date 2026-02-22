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



namespace G2PComponents
{
    public class GluLambPlugin : PlugIn
    {
        DisplayMaterial MeshMaterial;

        public GluLambPlugin()
        {
            Instance = this;
            MeshMaterial = new DisplayMaterial();
        }

        ~GluLambPlugin()
        {
        }

        public static GluLambPlugin Instance
        {
            get;
            private set;
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Rhino.RhinoApp.WriteLine($"G2PComponents v{Version}");
            RhinoApp.WriteLine("An extension for D2PComponents (c) Design-to-Production GmbH.");

            return base.OnLoad(ref errorMessage);
        }

        protected override void ReadDocument(RhinoDoc doc, BinaryArchiveReader archive, FileReadOptions options)
        {
            base.ReadDocument(doc, archive, options);
        }



        protected override void ObjectPropertiesPages(ObjectPropertiesPageCollection collection)
        {
            //var page = new Views.BeamPropertiesPage();
            //collection.Add(page);
        }
    }
}
