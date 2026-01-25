using System;
using System.Collections.Generic;
using D2P_Core;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D2P_Core.Utility;
using Rhino.Geometry;
using GluLamb;

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

        public static IEnumerable<GeometryBase> GetMember(Component component, string name)
        {
            var layerIdx = Layers.FindLayerIndexByFullPath(component, name);
            if (layerIdx < 0) return null;

            return Objects.ObjectsByLayer(layerIdx, component, Objects.LayerScope.CurrentOnly);
        }
    }
}
