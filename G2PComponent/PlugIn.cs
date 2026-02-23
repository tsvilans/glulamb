using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using GluLamb.Joints;

namespace G2PComponents
{
    public static class Loader
    {
        public static JointFactory Factory { get; } = new JointFactory();
    }

    public class GluLambPlugin : PlugIn
    {
        DisplayMaterial MeshMaterial;
        GluLamb.Joints.JointFactory JointLoader;

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


            //var jointsPath = Path.GetFullPath("./");
            var jointsPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            RhinoApp.WriteLine($"-- Loading joints from {jointsPath}...");
            Loader.Factory.LoadFromFolder(jointsPath);
            RhinoApp.WriteLine($"-- Loaded {Loader.Factory.Available.Count} joints.");

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
