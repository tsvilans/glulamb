using D2P_Core.Interfaces;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace G2PComponents
{
    public enum EdgeProfileType { None, TongueAndGroove }

    public enum EdgeKind { None, Tongue, Groove }

    /// <summary>
    /// An edge profile on a board component (siding, roofing boards): what its two long edges
    /// get, and the sizes. Stored as user strings on the component's label and applied by
    /// GenerateDetailed, which extrudes the profiled section along the board as the blank that
    /// joints and connectors are then cut into. The component's Geometry is never changed.
    ///
    /// The board's frame is its component plane: X along the board, the wider of Y and Z across
    /// it (the width) and the other through it (the thickness). Edge1 is the long edge on the
    /// positive side of the width axis, Edge2 the one on the negative side. Offset moves the
    /// tongue and groove off centre, along the positive thickness axis.
    /// </summary>
    public class EdgeProfile
    {
        public EdgeProfileType Type = EdgeProfileType.None;
        public EdgeKind Edge1 = EdgeKind.Tongue;
        public EdgeKind Edge2 = EdgeKind.Groove;
        public double TongueWidth = 8;
        public double TongueDepth = 8;
        /// <summary>0 = the tongue's width.</summary>
        public double GrooveWidth = 0;
        /// <summary>0 = the tongue's depth.</summary>
        public double GrooveDepth = 0;
        public double Offset = 0;
        /// <summary>
        /// Whether the tongue sticks out of the Geometry (which is then the cover width) or is
        /// cut back into it (the Geometry is then the overall width, tongue included).
        /// </summary>
        public bool Protrude = true;

        public double GrooveWidthOrTongue => GrooveWidth > 0 ? GrooveWidth : TongueWidth;
        public double GrooveDepthOrTongue => GrooveDepth > 0 ? GrooveDepth : TongueDepth;

        /// <summary>How far the profile reaches outside the Geometry.</summary>
        public double Overhang => Type != EdgeProfileType.None && Protrude && (Edge1 == EdgeKind.Tongue || Edge2 == EdgeKind.Tongue) ? TongueDepth : 0;

        private const string Prefix = "EdgeProfile";
        private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

        public EdgeProfile Duplicate() => (EdgeProfile)MemberwiseClone();

        /// <summary>
        /// The component's edge profile, or null if it has none.
        /// </summary>
        public static EdgeProfile Read(IComponent component, RhinoDoc doc)
        {
            var attributes = doc.Objects.FindId(component.ID)?.Attributes;
            return attributes == null ? null : Read(attributes);
        }

        public static EdgeProfile Read(ObjectAttributes attributes)
        {
            var type = attributes.GetUserString(Prefix);
            if (string.IsNullOrEmpty(type) || !Enum.TryParse(type, out EdgeProfileType profileType) || profileType == EdgeProfileType.None)
                return null;

            var p = new EdgeProfile { Type = profileType };
            string Get(string key) => attributes.GetUserString($"{Prefix}.{key}");
            double Number(string key, double fallback) =>
                double.TryParse(Get(key), System.Globalization.NumberStyles.Float, Invariant, out var x) ? x : fallback;
            EdgeKind Kind(string key, EdgeKind fallback) => Enum.TryParse(Get(key), out EdgeKind k) ? k : fallback;

            p.Edge1 = Kind(nameof(Edge1), p.Edge1);
            p.Edge2 = Kind(nameof(Edge2), p.Edge2);
            p.TongueWidth = Number(nameof(TongueWidth), p.TongueWidth);
            p.TongueDepth = Number(nameof(TongueDepth), p.TongueDepth);
            p.GrooveWidth = Number(nameof(GrooveWidth), p.GrooveWidth);
            p.GrooveDepth = Number(nameof(GrooveDepth), p.GrooveDepth);
            p.Offset = Number(nameof(Offset), p.Offset);
            p.Protrude = Get(nameof(Protrude)) != "No";
            return p;
        }

        /// <summary>
        /// Writes the profile to the attributes; a profile of type None removes it.
        /// </summary>
        public void Write(ObjectAttributes attributes)
        {
            if (Type == EdgeProfileType.None)
            {
                attributes.DeleteUserString(Prefix);
                foreach (var key in new[] { nameof(Edge1), nameof(Edge2), nameof(TongueWidth), nameof(TongueDepth), nameof(GrooveWidth), nameof(GrooveDepth), nameof(Offset), nameof(Protrude) })
                    attributes.DeleteUserString($"{Prefix}.{key}");
                return;
            }

            void Set(string key, object value) => attributes.SetUserString($"{Prefix}.{key}", Convert.ToString(value, Invariant));
            attributes.SetUserString(Prefix, Type.ToString());
            Set(nameof(Edge1), Edge1);
            Set(nameof(Edge2), Edge2);
            Set(nameof(TongueWidth), TongueWidth);
            Set(nameof(TongueDepth), TongueDepth);
            Set(nameof(GrooveWidth), GrooveWidth);
            Set(nameof(GrooveDepth), GrooveDepth);
            Set(nameof(Offset), Offset);
            Set(nameof(Protrude), Protrude ? "Yes" : "No");
        }

        /// <summary>
        /// The board's frame from its component plane and Geometry: a plane at the start of the
        /// board, centred on its section, with X along the width and Y along the thickness, and
        /// the board's length, width and thickness. False if there is no Geometry.
        /// </summary>
        public static bool Frame(IComponent component, RhinoDoc doc, out Plane section, out double length, out double width, out double thickness) =>
            Frame(component, component.Plane, doc, out section, out length, out width, out thickness);

        /// <summary>
        /// The board's frame as it would be with the given component plane.
        /// </summary>
        public static bool Frame(IComponent component, Plane plane, RhinoDoc doc, out Plane section, out double length, out double width, out double thickness)
        {
            section = Plane.Unset;
            length = width = thickness = 0;
            var geometry = Utility.GetMember(component, Detailing.Basic, doc).FirstOrDefault();
            if (geometry == null) return false;

            var bounds = geometry.GetBoundingBox(plane);
            double dy = bounds.Max.Y - bounds.Min.Y, dz = bounds.Max.Z - bounds.Min.Z;
            var start = plane.PointAt(bounds.Min.X, (bounds.Min.Y + bounds.Max.Y) / 2, (bounds.Min.Z + bounds.Max.Z) / 2);
            length = bounds.Max.X - bounds.Min.X;

            if (dy >= dz)
            {
                width = dy; thickness = dz;
                section = new Plane(start, plane.YAxis, plane.ZAxis);
            }
            else
            {
                width = dz; thickness = dy;
                section = new Plane(start, plane.ZAxis, -plane.YAxis);
            }
            // Keep the section's normal along the board
            if (section.ZAxis * plane.XAxis < 0)
                section = new Plane(section.Origin, section.XAxis, -section.YAxis);
            return true;
        }

        /// <summary>
        /// Keeps the component's edge profile on the same physical edges when its plane changes
        /// to newPlane (FlipComponentPlane, RollComponentPlane, TurnBasePlane): Edge1 and Edge2
        /// swap if the width axis turns round, and Offset changes sign if the thickness axis does.
        /// Call before the plane is changed. Returns false (with a message) only if the profile
        /// couldn't be carried over, e.g. on a square section whose width axis changed.
        /// </summary>
        public static bool FollowPlane(IComponent component, Plane newPlane, RhinoDoc doc, List<string> messages)
        {
            var obj = doc.Objects.FindId(component.ID);
            var profile = obj == null ? null : Read(obj.Attributes);
            if (profile == null) return true;

            if (!Frame(component, component.Plane, doc, out var before, out _, out _, out _)
                || !Frame(component, newPlane, doc, out var after, out _, out _, out _))
                return true;

            var across = before.XAxis * after.XAxis;
            if (Math.Abs(across) < 0.5)
            {
                messages.Add($"{component.ShortName}: the board's width axis changed, so its edge profile couldn't be carried over; check it with SetEdgeProfile.");
                return false;
            }
            bool swap = across < 0;
            bool negate = before.YAxis * after.YAxis < 0;
            if (!swap && !negate) return true;

            if (swap) (profile.Edge1, profile.Edge2) = (profile.Edge2, profile.Edge1);
            if (negate) profile.Offset = -profile.Offset;
            var attributes = obj.Attributes.Duplicate();
            profile.Write(attributes);
            doc.Objects.ModifyAttributes(obj, attributes, true);
            return true;
        }

        /// <summary>
        /// The profiled cross-section, closed, in the section plane's coordinates (x across the
        /// width, y through the thickness, centred). Null with a message if the sizes don't fit.
        /// </summary>
        public Polyline Section(double width, double thickness, List<string> messages, string name)
        {
            double halfT = thickness / 2;
            bool Fits(double size, string what)
            {
                if (Offset - size / 2 > -halfT + 1e-6 && Offset + size / 2 < halfT - 1e-6) return true;
                messages.Add($"{name}: the {what} ({size} wide, offset {Offset}) doesn't fit in the board's thickness ({thickness}).");
                return false;
            }

            // One long edge, bottom to top, as (distance out from the Geometry's edge, y)
            List<(double Out, double Y)> Edge(EdgeKind kind)
            {
                double a, b;
                switch (kind)
                {
                    case EdgeKind.Tongue:
                        if (!Fits(TongueWidth, "tongue")) return null;
                        a = Offset - TongueWidth / 2; b = Offset + TongueWidth / 2;
                        return Protrude
                            ? new() { (0, -halfT), (0, a), (TongueDepth, a), (TongueDepth, b), (0, b), (0, halfT) }
                            : new() { (-TongueDepth, -halfT), (-TongueDepth, a), (0, a), (0, b), (-TongueDepth, b), (-TongueDepth, halfT) };
                    case EdgeKind.Groove:
                        if (!Fits(GrooveWidthOrTongue, "groove")) return null;
                        a = Offset - GrooveWidthOrTongue / 2; b = Offset + GrooveWidthOrTongue / 2;
                        return new() { (0, -halfT), (0, a), (-GrooveDepthOrTongue, a), (-GrooveDepthOrTongue, b), (0, b), (0, halfT) };
                    default:
                        return new() { (0, -halfT), (0, halfT) };
                }
            }

            var edge1 = Edge(Edge1);
            var edge2 = Edge(Edge2);
            if (edge1 == null || edge2 == null) return null;

            double halfW = width / 2;
            if (GrooveDepthOrTongue * ((Edge1 == EdgeKind.Groove ? 1 : 0) + (Edge2 == EdgeKind.Groove ? 1 : 0))
                + (Protrude ? 0 : TongueDepth * ((Edge1 == EdgeKind.Tongue ? 1 : 0) + (Edge2 == EdgeKind.Tongue ? 1 : 0))) >= width)
            {
                messages.Add($"{name}: the profile is deeper than the board is wide ({width}).");
                return null;
            }

            // Edge1 on +x, bottom to top; Edge2 on -x, top to bottom, at the same heights so a
            // tongue meets the next board's groove
            var section = new Polyline();
            foreach (var (o, y) in edge1) section.Add(halfW + o, y, 0);
            foreach (var (o, y) in Enumerable.Reverse(edge2)) section.Add(-halfW - o, y, 0);
            section.Add(section[0]);
            section.DeleteShortSegments(1e-6);
            return section;
        }

        /// <summary>
        /// The board's profiled blank: the section extruded along the board's length, in place.
        /// Null (with a message) if the component has no Geometry or the profile doesn't fit.
        /// </summary>
        public Brep Build(IComponent component, RhinoDoc doc, List<string> messages)
        {
            if (!Frame(component, doc, out var plane, out var length, out var width, out var thickness))
            {
                messages.Add($"{component.ShortName}: no Geometry for the edge profile.");
                return null;
            }

            var section = Section(width, thickness, messages, component.ShortName);
            if (section == null) return null;

            var curve = section.ToNurbsCurve();
            curve.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));
            var surface = Surface.CreateExtrusion(curve, plane.ZAxis * length);
            var brep = surface?.ToBrep()?.CapPlanarHoles(doc.ModelAbsoluteTolerance);
            if (brep == null || !brep.IsSolid)
            {
                messages.Add($"{component.ShortName}: making the edge profile failed.");
                return null;
            }
            if (brep.SolidOrientation == BrepSolidOrientation.Inward)
                brep.Flip();
            return brep;
        }

        /// <summary>
        /// Where to show each edge's kind: the middle of each long edge, on the board's top face.
        /// </summary>
        public static bool EdgeMarkers(IComponent component, RhinoDoc doc, out Point3d edge1, out Point3d edge2)
        {
            edge1 = edge2 = Point3d.Unset;
            if (!Frame(component, doc, out var plane, out var length, out var width, out var thickness)) return false;
            edge1 = plane.PointAt(width / 2, 0, length / 2);
            edge2 = plane.PointAt(-width / 2, 0, length / 2);
            return true;
        }
    }
}
