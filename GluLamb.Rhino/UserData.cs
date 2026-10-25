using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;

namespace GluLamb
{
    public class TimberBeamUserData : Rhino.DocObjects.Custom.UserData
    {
        public Guid BeamRef { get; set; }
        //public Transform GeometryTransform { get; set; }

        public TimberBeamUserData()
        {
        }

        internal void SetTransform(Transform transform)
        {
            Transform = transform;
            //GeometryTransform = transform;
            //RhinoApp.WriteLine($"SetTransform(): {GeometryTransform:0.000}");
        }

        protected override void OnTransform(Transform transform)
        {
            //base.OnTransform(transform);
            //return;

            var timberBeam = GluLambPlugin.Instance.ActiveModel.Beams.Where(x => x.Id == BeamRef).FirstOrDefault();
            if (timberBeam != null)
            {
                var plane = timberBeam.Plane;

                plane.Transform(transform);
                timberBeam.Plane = plane;
            }

            //transform = Transform * transform;

            base.OnTransform(transform);
        }
        
        public override string Description
        {
            get { return "UserData to link TimberBeam objects to their Rhino objects."; }
        }

        public override string ToString()
        {
            return String.Format("Reference to Beam({0})", BeamRef);
        }

        protected override void OnDuplicate(Rhino.DocObjects.Custom.UserData source)
        {
            TimberBeamUserData src = source as TimberBeamUserData;
            if (src != null)
            {
                BeamRef = src.BeamRef;
                Transform = src.Transform;
                //GeometryTransform = src.GeometryTransform;
            }
        }

        // return true if you have information to save
        public override bool ShouldWrite
        {
            get
            {
                return true;
            }
        }

        protected override bool Read(Rhino.FileIO.BinaryArchiveReader archive)
        {
            Rhino.Collections.ArchivableDictionary dict = archive.ReadDictionary();
            
            if (dict.Name == "TimberBeamUserData")
            {
                BeamRef = (Guid)dict.GetGuid("BeamReference");
                Transform = (Transform)dict["Transform"];
            }
            return true;
        }

        protected override bool Write(Rhino.FileIO.BinaryArchiveWriter archive)
        {
            var dict = new Rhino.Collections.ArchivableDictionary(1, "TimberBeamUserData");

            dict.Set("BeamReference", BeamRef);
            dict.Set("Transform", Transform);

            archive.WriteDictionary(dict);
            return true;
        }
    }
}
