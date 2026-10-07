using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using Rhino.Geometry;

namespace GluLamb.Btlx
{
    /// <summary>
    /// Project data for a BTLx file.
    /// </summary>
    public class BtlxProject
    {
        public string Name = "GluLamb project";
        public string Number;
        public string Customer;
        public string Architect;
        public string Editor;
        public string Comment;
    }

    /// <summary>
    /// A beam as a BTLx part, with what BTLx records about it. The part's coordinate system
    /// (its reference point and axes) comes from the beam: see BtlxWriter.PartFrame.
    /// </summary>
    public class BtlxPart
    {
        public Beam Beam;
        public int SingleMemberNumber;
        /// <summary>Name of the part; defaults to the beam's id.</summary>
        public string Designation;
        public string Annotation;
        public string AssemblyNumber;
        public string Group;
        public string Package;
        public string Material;
        public string TimberGrade;
        public string QualityGrade;
        public int Count = 1;
    }

    /// <summary>
    /// Writes BTLx files (https://www.design2machine.com/btlx): a project and its parts. Parts
    /// only for now: their size, position and reference side; processings come later.
    ///
    /// Each part is the beam's straight blank. Its coordinate system follows BTLx: the reference
    /// point is the corner at the start of the beam, X runs along the beam, Y along the
    /// section's width and Z along its height, so the part spans 0..Length, 0..Width, 0..Height.
    /// In beam terms: X is the centreline tangent, Y the cross-section's X axis, Z its Y axis
    /// (Beam.GetPlane). Curved beams are written as the straight blank along their chord at the
    /// start frame, with a warning; camber isn't written yet.
    /// </summary>
    public static class BtlxWriter
    {
        public static readonly XNamespace Ns = "https://www.design2machine.com";
        public const string DefaultVersion = "2.3.0";

        /// <summary>
        /// The BTLx document. Messages collects anything the file can't represent exactly.
        /// </summary>
        public static XDocument Write(BtlxProject project, IEnumerable<BtlxPart> parts, List<string> messages, string version = DefaultVersion)
        {
            var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
            var schema = $"https://www.design2machine.com/btlx/BTLx_{version.Replace('.', '_')}.xsd";

            var root = new XElement(Ns + "BTLx",
                new XAttribute("Version", version),
                new XAttribute("Language", "en"),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(xsi + "schemaLocation", $"{Ns} {schema}"),
                new XElement(Ns + "FileHistory",
                    new XElement(Ns + "InitialExportProgram",
                        new XAttribute("CompanyName", "GluLamb"),
                        new XAttribute("ProgramName", "GluLamb"),
                        new XAttribute("ProgramVersion", typeof(BtlxWriter).Assembly.GetName().Version?.ToString() ?? ""),
                        new XAttribute("Date", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                        new XAttribute("Time", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)))));

            var projectElement = new XElement(Ns + "Project", new XAttribute("Name", project?.Name ?? "GluLamb project"));
            Optional(projectElement, "Number", project?.Number);
            Optional(projectElement, "Customer", project?.Customer);
            Optional(projectElement, "Architect", project?.Architect);
            Optional(projectElement, "Editor", project?.Editor);
            Optional(projectElement, "Comment", project?.Comment);

            var partsElement = new XElement(Ns + "Parts");
            foreach (var part in parts)
            {
                var element = Part(part, messages);
                if (element != null) partsElement.Add(element);
            }
            projectElement.Add(partsElement);
            root.Add(projectElement);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        /// <summary>
        /// The part's coordinate system: origin at the reference corner, X along the beam, Y along
        /// the width, Z along the height. Also its length, width and height.
        /// </summary>
        public static Plane PartFrame(Beam beam, out double length, out double width, out double height)
        {
            var curve = beam.Centreline;
            var start = beam.GetPlane(curve.Domain.Min);

            var x = start.ZAxis;
            length = curve.GetLength();
            if (!curve.IsLinear())
            {
                x = curve.PointAtEnd - curve.PointAtStart;
                length = x.Length;
                x.Unitize();
            }

            width = beam.Width;
            height = beam.Height;

            // Width along the section's X, height along its Y, both square to X; the section can be
            // offset from the centreline (Beam.OffsetX, OffsetY)
            var y = start.XAxis - x * (start.XAxis * x);
            y.Unitize();
            var z = Vector3d.CrossProduct(x, y);
            var corner = start.Origin + y * (beam.OffsetX - width * 0.5) + z * (beam.OffsetY - height * 0.5);
            return new Plane(corner, x, y);
        }

        private static XElement Part(BtlxPart part, List<string> messages)
        {
            var beam = part.Beam;
            if (beam?.Centreline == null)
            {
                messages?.Add($"Part {part.SingleMemberNumber}: no beam.");
                return null;
            }

            if (!beam.Centreline.IsLinear())
                messages?.Add($"Part {part.Designation ?? beam.Id}: the beam is curved; written as a straight blank along its chord (camber isn't written yet).");

            var frame = PartFrame(beam, out double length, out double width, out double height);

            var element = new XElement(Ns + "Part",
                new XAttribute("SingleMemberNumber", part.SingleMemberNumber),
                new XAttribute("Designation", part.Designation ?? beam.Id ?? ""),
                new XAttribute("Count", Math.Max(1, part.Count)),
                new XAttribute("Length", N(length)),
                new XAttribute("Height", N(height)),
                new XAttribute("Width", N(width)));
            Optional(element, "AssemblyNumber", part.AssemblyNumber);
            Optional(element, "Annotation", part.Annotation);
            Optional(element, "Group", part.Group);
            Optional(element, "Package", part.Package);
            Optional(element, "Material", part.Material);
            Optional(element, "TimberGrade", part.TimberGrade);
            Optional(element, "QualityGrade", part.QualityGrade);

            element.Add(new XElement(Ns + "Transformations",
                new XElement(Ns + "Transformation",
                    new XAttribute("GUID", "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}"),
                    new XElement(Ns + "Position",
                        Coordinate("ReferencePoint", frame.Origin),
                        Coordinate("XVector", frame.XAxis),
                        Coordinate("YVector", frame.YAxis)))));

            // Grain along the part; reference side 1, aligned with the part's own frame
            element.Add(new XElement(Ns + "GrainDirection", new XAttribute("X", "1"), new XAttribute("Y", "0"), new XAttribute("Z", "0"), new XAttribute("Align", "no")));
            element.Add(new XElement(Ns + "ReferenceSide", new XAttribute("Side", "1"), new XAttribute("Align", "no")));

            return element;
        }

        private static XElement Coordinate(string name, Point3d p) =>
            new XElement(Ns + name, new XAttribute("X", N(p.X)), new XAttribute("Y", N(p.Y)), new XAttribute("Z", N(p.Z)));

        private static XElement Coordinate(string name, Vector3d v) =>
            new XElement(Ns + name, new XAttribute("X", N(v.X)), new XAttribute("Y", N(v.Y)), new XAttribute("Z", N(v.Z)));

        private static void Optional(XElement element, string name, string value)
        {
            if (!string.IsNullOrEmpty(value)) element.Add(new XAttribute(name, value));
        }

        private static string N(double v) => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
