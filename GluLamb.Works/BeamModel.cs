using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class BeamModel
    {
        public ObservableUniqueCollection<TimberBeam> Beams { get; set; } = new ObservableUniqueCollection<TimberBeam>
        {
        };

        public ObservableUniqueCollection<Joint> Joints { get; set; } = new ObservableUniqueCollection<Joint>
        {
        };

        public void ClearBeams()
        {
            if (Beams != null)
            {
                // var doc = RhinoDoc.ActiveDoc;
                // foreach (var beam in Beams)
                // {
                //     var obj = doc.Objects.FindId(beam.Id);
                //     if (obj != null)
                //     {
                //         if (obj.UserDictionary.ContainsKey("tasBeam"))
                //             obj.UserDictionary.Remove("tasBeam");
                //     }
                // }
                Beams.Clear();
            }
        }

        public static void LoadJoints(RhinoDoc doc)
        {

        }

        public void RotateBeam(TimberBeam beam)
        {
            var obj = RhinoDoc.ActiveDoc.Objects.FindId(beam.Id);
            if (obj != null)
            {
                var ud = obj.Geometry.UserData.Find(typeof(TimberBeamUserData)) as TimberBeamUserData;
                if (ud != null)
                {
                    var new_plane = new Plane(
                            new Point3d(0, 0, beam.Bounds.Max.Z - beam.Bounds.Min.Z),
                            Vector3d.XAxis, -Vector3d.ZAxis);

                    var bounds = beam.Bounds;
                    bounds.Transform(Transform.PlaneToPlane(new_plane, Plane.WorldXY));
                    beam.Bounds = bounds;

                    ud.SetTransform(ud.Transform * Transform.PlaneToPlane(Plane.WorldXY, new_plane));

                    var temp = beam.Width;
                    beam.Width = beam.Height;
                    beam.Height = temp;
                }
            }
        }

        public void FlipBeam(TimberBeam beam)
        {
            var obj = RhinoDoc.ActiveDoc.Objects.FindId(beam.Id);
            if (obj != null)
            {
                var ud = obj.Geometry.UserData.Find(typeof(TimberBeamUserData)) as TimberBeamUserData;
                if (ud != null)
                {
                    var new_plane = new Plane(
                            new Point3d(beam.Bounds.Max.X - beam.Bounds.Min.X, beam.Bounds.Max.Y - beam.Bounds.Min.Y, 0),
                            -Vector3d.XAxis, -Vector3d.YAxis);

                    var bounds = beam.Bounds;
                    bounds.Transform(Transform.PlaneToPlane(new_plane, Plane.WorldXY));
                    beam.Bounds = bounds;

                    ud.SetTransform(ud.Transform * Transform.PlaneToPlane(Plane.WorldXY, new_plane));

                    var temp = beam.Width;
                    beam.Width = beam.Height;
                    beam.Height = temp;
                }
            }
        }

        public void LoadBeams(RhinoDoc doc)
        {
            var iter = doc.Objects.GetEnumerator();
            while (iter.MoveNext())
            {
                //RhinoApp.WriteLine($"{iter.Current.Geometry.UserDictionary}");
                var obj = iter.Current;
                if (obj.Geometry.UserDictionary == null)
                {
                    continue;
                }

                // Candidate for beam model
                if (obj.Geometry.UserDictionary.TryGetDictionary("tasBeam", out Rhino.Collections.ArchivableDictionary props))
                {

                    RhinoApp.WriteLine($"Found beam data for object {obj.Name} ({obj.Id}).");
                    double width = 0, height = 0, length = 0;
                    Plane plane = Plane.Unset;

                    props.TryGetDouble("width", out width);
                    props.TryGetDouble("height", out height);
                    props.TryGetDouble("length", out length);
                    props.TryGetPlane("plane", out plane);

                    var bb = obj.Geometry.GetBoundingBox(plane, out Box worldBox);


                    var name = obj.Name;
                    var id = obj.Id;

                    var timberBeam = new TimberBeam()
                    {
                        Width = width,
                        Height = height,
                        Length = length,
                        Plane = plane,
                        Name = name,
                        Id = id,
                        Bounds = bb,

                    };

                    RhinoApp.WriteLine($"Obj has user data: {obj.Geometry.UserData}");

                    Beams.Add(timberBeam);
                    var ud = obj.Geometry.UserData.Find(typeof(TimberBeamUserData)) as TimberBeamUserData;
                    if (ud == null)
                    {
                        ud = new TimberBeamUserData() { BeamRef = timberBeam.Id };
                        obj.Geometry.UserData.Add(ud);
                    }

                    ud.SetTransform(Transform.PlaneToPlane(Plane.WorldXY, plane));
                    //timberBeam.UserData = ud;
                }
            }
        }
    }
}
