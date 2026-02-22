using D2P_Core;
using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Eto.Forms;
using GluLamb;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.NodeInCode;
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents
{
    public static class Utility
    {
        public static GluLamb.Beam ComponentToBeam(object data)
        {
            if (data is Component component)
                return ComponentToBeam(component);
            return null;
        }

        public static GluLamb.Beam ComponentToBeam(Component component)
        {
            var plane = component.Label.Plane;
            var geometries = GetMember(component, "Geometry");
            if (geometries == null) return null;

            var geometry = geometries.First();
            var bounds = geometry.GetBoundingBox(plane, out Box worldBox);

            var cy = (bounds.Min.Y + bounds.Max.Y) / 2;
            var cz = (bounds.Min.Z + bounds.Max.Z) / 2;

            var start = plane.PointAt(bounds.Min.X, cy, cz);
            var end = plane.PointAt(bounds.Max.X, cy, cz);

            var centreline = new Line(start, end).ToNurbsCurve();
            var xplane = new Plane(plane.Origin, plane.ZAxis, plane.YAxis);
            var yaxis = xplane.YAxis;

            var beam = new Beam()
            {
                Centreline = centreline,
                Width = bounds.Max.Z - bounds.Min.Z,
                Height = bounds.Max.Y - bounds.Min.Y,
                Orientation = new VectorOrientation(yaxis)
            };

            return beam;
        }

        public static IEnumerable<GeometryBase> GetMember(IComponent component, string name, RhinoDoc? doc = null)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;

            var layerIdx = Layers.FindLayerIndexByFullPath(component, name);
            if (layerIdx < 0) return Enumerable.Empty<GeometryBase>();

            return Objects.ObjectsByLayer(layerIdx, component, Objects.LayerScope.CurrentOnly);
        }

        public static IEnumerable<Guid> GetMemberIDs(IComponent component, string name, RhinoDoc? doc = null)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;
            var layerIdx = Layers.FindLayerIndexByFullPath(component, name);
            if (layerIdx < 0) return Enumerable.Empty<Guid>();

            return Objects.ObjectIDsByLayer(component, layerIdx, doc);
        }

        public static Plane FlipBasePlane(Brep brep, Plane plane, bool aroundX = true)
        {
            var bounds = brep.GetBoundingBox(plane, out Box box);
            Point3d origin;
            if (aroundX)
            {
                origin = plane.PointAt(box.X.Max, box.Y.Min, box.Z.Max);
                return new Plane(origin, -plane.XAxis, plane.YAxis);
            }

            origin = plane.PointAt(box.X.Min, box.Y.Max, box.Z.Max);
            return new Plane(origin, plane.XAxis, -plane.YAxis);
        }

        public static Plane TurnBaseplane(Brep brep, Plane plane, bool quarter_turn = false)
        {
            var bounds = brep.GetBoundingBox(plane, out Box box);
            if (quarter_turn)
                return new Plane(plane.PointAt(box.X.Max, box.Y.Min, box.Z.Min), plane.YAxis, -plane.XAxis);
            return new Plane(plane.PointAt(box.X.Max, box.Y.Max, box.Z.Min), -plane.XAxis, -plane.YAxis);
        }

        public static Plane RollBaseplane(Brep brep, Plane plane)
        {
            var bounds = brep.GetBoundingBox(plane, out Box box);
            return new Plane(plane.PointAt(box.X.Min, box.Y.Min, box.Z.Max), plane.XAxis, -plane.ZAxis);
        }

        public static IEnumerable<IComponent> SelectComponents(RhinoDoc doc, bool single = false, bool preselect = false, bool select =false)
        {
            var getter = new GetObject();
            getter.EnablePreSelect(preselect, true);
            getter.EnableUnselectObjectsOnExit(!select);
            getter.SetCommandPrompt("Pick component(s)");

            if (single)
                getter.Get();
            else
                getter.GetMultiple(1, 0);

            var guids = getter.Objects().Select(x => x.ObjectId);

            return Instantiation.InstancesFromObjects(guids, null);
        }
    }
}
