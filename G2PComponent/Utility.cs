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

        public static (double Width, double Height) GetRelativeDimensions(Box box, Vector3d vector)
        {
            var plane = box.Plane;
            // Test against Y and Z since X is our longitudinal axis

            var dotZ = plane.ZAxis * vector;
            var dotY = plane.YAxis * vector;

            if (Math.Abs(dotZ) > Math.Abs(dotY)) return (box.Y.Max - box.Y.Min, box.Z.Max - box.Z.Min);

            return (box.Z.Max - box.Z.Min, box.Y.Max - box.Y.Min);
        }

        /// <summary>
        /// Deconstruct a box into separate Rectangle3d faces.
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        public static List<Rectangle3d> DeBox(Box box)
        {
            var data = new List<Rectangle3d>();

            var origin = box.Plane.Origin;
            var xaxis = box.Plane.XAxis;
            var yaxis = box.Plane.YAxis;
            var zaxis = box.Plane.ZAxis;

            var normX = new Interval(0, box.X.Length);
            var normY = new Interval(0, box.Y.Length);
            var normZ = new Interval(0, box.Z.Length);

            data.Add(new Rectangle3d(new Plane(
                origin + xaxis * box.X.Min + yaxis * box.Y.Min + zaxis * box.Z.Max,
                xaxis, yaxis
                ), normX, normY));

            data.Add(new Rectangle3d(new Plane(
                origin + xaxis * box.X.Min + yaxis * box.Y.Max + zaxis * box.Z.Min,
                xaxis, -yaxis
                ), normX, normY));

            data.Add(new Rectangle3d(new Plane(
                origin + xaxis * box.X.Min + yaxis * box.Y.Min + zaxis * box.Z.Min,
                xaxis, zaxis
                ), normX, normY));

            data.Add(new Rectangle3d(new Plane(
                origin + xaxis * box.X.Min + yaxis * box.Y.Max + zaxis * box.Z.Max,
                xaxis, -zaxis
                ), normX, normY));

            // data.Add(new Rectangle3d(box.Plane, box.X, box.Y));

            return data;
        }

        /// <summary>
        /// Get the bounding box and world box of a Component, provided it has a member
        /// named Geometry.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="worldBox"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static BoundingBox GetComponentBounds(D2P_Core.Interfaces.IComponent component, out Box worldBox, RhinoDoc doc = null)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;

            var geometry = G2PComponents.Utility.GetMember(component, "Geometry", doc);
            var plane = component.Plane;

            var bounds = BoundingBox.Empty;

            foreach (var geom in geometry)
            {
                bounds.Union(geom.GetBoundingBox(component.Plane));
            }

            worldBox = new Box(component.Plane, bounds);

            return bounds;
        }

        /// <summary>
        /// Make a list of planes that divide a certain width in another plane.
        /// Useful for creating drilling spacings or grids.
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="width"></param>
        /// <param name="N"></param>
        /// <param name="margin"></param>
        /// <param name="minStep"></param>
        /// <returns></returns>
        public static List<Plane> MakeDivisors(Plane plane, double width, int N, double margin = 0, double minStep = 0)
        {
            if (N == 1 || width < margin * 2)
            {
                return new List<Plane> { new Plane(plane.PointAt(0, width / 2, 0), plane.XAxis, plane.ZAxis) };
            }

            width -= margin * 2;

            var divisors = new List<Plane>();

            if (minStep > 0)
            {
                if (width < minStep)
                {
                    return new List<Plane> { new Plane(plane.PointAt(0, margin + width / 2, 0), plane.XAxis, plane.ZAxis) };
                }

                var maxN = (int)Math.Floor(width / minStep) + 1;

                N = Math.Min(N, maxN);
            }

            var step = width / (N - 1);

            for (int i = 0; i < N; ++i)
            {
                var divisor = new Plane(plane.PointAt(0, margin + step * i, 0), plane.XAxis, plane.ZAxis);
                divisors.Add(divisor);
            }

            return divisors;

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


        public static BoxSide GetBoxSide(Point3d point, Box box, double epsilon = 1e-6)
        {
            if (!box.Plane.RemapToPlaneSpace(point, out Point3d wp))
                return BoxSide.Unknown;

            // Check if within Z bounds (i.e., not top/bottom)
            if (wp.Z + epsilon < box.Z.Max && wp.Z - epsilon > box.Z.Min)
            {
                // Check if within Y bounds
                if (wp.Y + epsilon < box.Y.Max && wp.Y - epsilon > box.Y.Min)
                {
                    // Ends (X direction)
                    if (wp.X - epsilon < box.X.Min)
                        return BoxSide.Left;

                    if (wp.X + epsilon > box.X.Max)
                        return BoxSide.Right;
                }
                else if (wp.Y - epsilon < box.Y.Min)
                {
                    return BoxSide.Inside;
                }
                else if (wp.Y + epsilon > box.Y.Max)
                {
                    return BoxSide.Outside;
                }
            }
            // Top / Bottom
            else if (wp.Z - epsilon < box.Z.Min)
            {
                return BoxSide.Bottom;
            }
            else if (wp.Z + epsilon > box.Z.Max)
            {
                return BoxSide.Top;
            }

            return BoxSide.Unknown;
        }
    }
}
