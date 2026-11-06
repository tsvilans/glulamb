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

        /// <summary>
        /// The board's visible face, for laying out connectors with MakeDivisors: the mating
        /// face's plane (from DeBox) moved across to where the visible face starts, and that
        /// face's width. The visible face is the board's face opposite the mating face, i.e. its
        /// front when it is fixed to the component behind it. It is read from the board's
        /// Geometry: the planar faces lying on the front of its box, so a profile modelled into
        /// the Geometry (a tongue, a lip that widens the front) is taken into account. A board
        /// whose edge profile is set with SetEdgeProfile (and not modelled) uses the profile's
        /// section instead. Otherwise, or if nothing is found, it is the face as it is.
        /// </summary>
        /// <param name="face">The mating face's plane, its normal pointing out of the board.</param>
        /// <param name="width">The box's width across the face (along the face's Y).</param>
        /// <param name="height">The box's depth through the board, from the mating face to the front.</param>
        public static (Plane Plane, double Width) VisibleFace(IComponent component, Plane face, double width, double height, RhinoDoc? doc = null)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;
            var whole = (face, width);

            var span = EdgeProfile.Read(component, doc) is EdgeProfile profile
                ? ProfileFront(component, profile, face, doc)
                : ModelledFront(component, face, height, doc);
            if (span == null) return whole;

            var (t0, t1) = span.Value;
            if (t1 - t0 < doc.ModelAbsoluteTolerance) return whole;
            var plane = face;
            plane.Origin = face.PointAt(0, t0);
            return (plane, t1 - t0);
        }

        /// <summary>
        /// The extent along the face's Y of the Geometry's planar faces that lie on the front of
        /// its box (the side opposite the mating face), facing out.
        /// </summary>
        private static (double, double)? ModelledFront(IComponent component, Plane face, double height, RhinoDoc doc)
        {
            double tol = Math.Max(doc.ModelAbsoluteTolerance * 10, 1e-3);
            var front = new Plane(face.Origin - face.ZAxis * height, face.XAxis, -face.YAxis); // normal -face.ZAxis

            double min = double.MaxValue, max = double.MinValue;
            foreach (var geometry in GetMember(component, Detailing.Basic, doc))
            {
                var brep = Brep.TryConvertBrep(geometry);
                if (brep == null) continue;
                foreach (var bf in brep.Faces)
                {
                    if (!bf.TryGetPlane(out var p, tol)) continue;
                    var normal = bf.OrientationIsReversed ? -p.ZAxis : p.ZAxis;
                    if (normal * front.ZAxis < 0.999) continue;
                    if (Math.Abs(front.DistanceTo(p.Origin)) > tol) continue;

                    // In the mating face's coordinates, Y across the board
                    var bounds = bf.DuplicateFace(false).GetBoundingBox(face);
                    min = Math.Min(min, bounds.Min.Y);
                    max = Math.Max(max, bounds.Max.Y);
                }
            }
            return min < max ? (min, max) : null;
        }

        /// <summary>
        /// The extent along the face's Y of the front of an edge profile's section (the side
        /// opposite the mating face), when the face is one of the board's broad faces.
        /// </summary>
        private static (double, double)? ProfileFront(IComponent component, EdgeProfile profile, Plane face, RhinoDoc doc)
        {
            if (!EdgeProfile.Frame(component, doc, out var section, out _, out var boardWidth, out var thickness)) return null;
            if (Math.Abs(face.ZAxis * section.YAxis) < 0.99 || Math.Abs(face.YAxis * section.XAxis) < 0.99) return null;

            var outline = profile.Section(boardWidth, thickness, new List<string>(), component.ShortName);
            if (outline == null) return null;

            // The front is the side of the section away from the mating face
            double y = face.ZAxis * section.YAxis > 0 ? -thickness / 2 : thickness / 2, tol = 1e-6;
            var xs = outline.Where(p => Math.Abs(p.Y - y) < tol).Select(p => p.X).ToList();
            if (xs.Count < 2) return null;

            double Across(double x) => (section.PointAt(x, 0) - face.Origin) * face.YAxis;
            double t0 = Across(xs.Min()), t1 = Across(xs.Max());
            return t0 < t1 ? (t0, t1) : (t1, t0);
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

            // By the component's group rather than by name (as Objects.ObjectIDsByLayer does), so
            // instances that share a name don't get each other's members
            var group = component.GroupIdx;
            if (group < 0) return Objects.ObjectIDsByLayer(component, layerIdx, doc);
            return doc.Groups.GroupMembers(group).Where(o => o.Attributes.LayerIndex == layerIdx).Select(o => o.Id).ToList();
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
