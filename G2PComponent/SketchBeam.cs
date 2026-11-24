using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

using D2P_Core;
using D2P_Core.Interfaces;
using D2P_Core.Utility;
using GluLamb;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace G2PComponents
{
    /// <summary>
    /// Beams sketched from centrelines (MakeBeam, ChangeBeamSection). A beam is a component of
    /// type B{w}x{h} ("B45x95 - Beam 45x95") with the centreline on Axis and the body on
    /// Geometry. The label plane holds the orientation: X along the axis at its start, Y along
    /// the section's height. Width is across Y.
    /// </summary>
    public static class SketchBeam
    {
        public static readonly Color LayerColor = Color.Peru;
        public const double LabelSize = 5.0;

        /// <summary>
        /// Sections offered as options. Sections already in the document are offered too.
        /// </summary>
        public static readonly string[] Presets =
        {
            "45x95", "45x145", "45x195", "45x245", "70x200", "90x90", "115x315", "140x360", "190x450"
        };

        public static string LastSection = "45x95";

        private static readonly Regex SectionPattern = new Regex(@"^\s*B?\s*(\d+(?:[.,]\d+)?)\s*[xX*]\s*(\d+(?:[.,]\d+)?)\s*$");
        private static readonly Regex TypePattern = new Regex(@"^B\d+x\d+$");

        private const string CounterSection = "G2PComponents";
        private const string CounterEntry = "BeamCounter";
        private const string IdPrefix = "B-";

        public static bool IsBeamType(string typeId) => typeId != null && TypePattern.IsMatch(typeId);

        /// <summary>
        /// Parse "45x95", "B45x95" or "45 x 95". Sizes are rounded to whole units.
        /// </summary>
        public static bool TryParseSection(string text, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var m = SectionPattern.Match(text);
            if (!m.Success) return false;

            double w = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            double h = double.Parse(m.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);

            width = (int)Math.Round(w);
            height = (int)Math.Round(h);
            if (width != w || height != h)
                RhinoApp.WriteLine($"Section rounded to {width}x{height}.");

            return width > 0 && height > 0;
        }

        public static ComponentType BeamType(int width, int height) =>
            new ComponentType($"B{width}x{height}", $"Beam {width}x{height}", Context.settings, LabelSize, LayerColor);

        /// <summary>
        /// Ask for a section: type it in or pick a preset. Enter keeps the default.
        /// </summary>
        public static bool GetSection(RhinoDoc doc, string defaultSection, out int width, out int height)
        {
            width = height = 0;

            var options = new List<string>(Presets);
            foreach (var layer in Layers.FindAllExistentComponentTypeRootLayers(Context.settings, doc))
            {
                var typeId = Layers.GetComponentTypeID(layer, Context.settings);
                if (IsBeamType(typeId) && !options.Contains(typeId.Substring(1)))
                    options.Add(typeId.Substring(1));
            }

            var gs = new GetString();
            gs.SetCommandPrompt("Section (width x height)");
            gs.SetDefaultString(defaultSection);
            gs.AcceptNothing(true);
            foreach (var option in options)
                gs.AddOption($"B{option}");

            var res = gs.Get();
            if (gs.CommandResult() != Rhino.Commands.Result.Success)
                return false;

            string text = res == Rhino.Input.GetResult.Option ? gs.Option().EnglishName
                : res == Rhino.Input.GetResult.Nothing ? defaultSection
                : gs.StringResult();

            if (!TryParseSection(text, out width, out height))
            {
                RhinoApp.WriteLine($"'{text}' isn't a section: type it as 45x95.");
                return false;
            }

            LastSection = $"{width}x{height}";
            return true;
        }

        /// <summary>
        /// Next free beam ID from the document's beam counter.
        /// </summary>
        public static string NextId(RhinoDoc doc)
        {
            int.TryParse(doc.Strings.GetValue(CounterSection, CounterEntry), out int counter);

            // Skip IDs already in use (e.g. a file from before the counter, or pasted beams)
            var used = new HashSet<string>(doc.Objects
                .GetObjectList(ObjectType.Annotation)
                .Select(o => (o.Geometry as TextEntity)?.PlainText)
                .Where(x => x != null && x.StartsWith(IdPrefix))!);

            string id;
            do
            {
                counter++;
                id = $"{IdPrefix}{counter:000}";
            } while (used.Contains(id));

            doc.Strings.SetString(CounterSection, CounterEntry, counter.ToString());
            return id;
        }

        /// <summary>
        /// The section's Y-axis at the start of a centreline, by default:
        /// straight and vertical: +Y; straight: up (+Z); planar: in the curve's plane, as up
        /// as it gets along the curve; double-curved: up at the start.
        /// </summary>
        public static Vector3d DefaultStartY(Curve curve, double tolerance)
        {
            var t0 = curve.Domain.Min;
            var tangent = curve.TangentAt(t0);

            if (!curve.IsLinear(tolerance) && curve.TryGetPlane(out Plane plane, tolerance))
            {
                // Y = T x N: choose the side of N that points Y up the most along the curve
                var n = plane.Normal;
                double up = curve.DivideByCount(16, true)
                    .Sum(t => Vector3d.CrossProduct(curve.TangentAt(t), n) * Vector3d.ZAxis);
                if (up < -tolerance || (Math.Abs(up) <= tolerance && n * Vector3d.ZAxis < 0))
                    n.Reverse();

                return Vector3d.CrossProduct(tangent, n);
            }

            return Perpendicular(tangent, tangent.IsParallelTo(Vector3d.ZAxis, 1e-3) != 0 ? Vector3d.YAxis : Vector3d.ZAxis);
        }

        /// <summary>
        /// Orientation along the centreline from the section's Y-axis at its start (the label's Y).
        /// Straight: that vector. Planar, Y in the plane: in the plane all along. Planar, Y across
        /// the plane: across it all along. Otherwise: RMF from the start.
        /// </summary>
        public static CrossSectionOrientation Orientation(Curve curve, Vector3d startY, double tolerance)
        {
            var tangent = curve.TangentAt(curve.Domain.Min);
            startY = Perpendicular(tangent, startY);

            if (curve.IsLinear(tolerance))
                return new VectorOrientation(startY);

            if (curve.TryGetPlane(out Plane plane, tolerance))
            {
                var n = plane.Normal;
                double across = startY * n;

                if (Math.Abs(across) > Math.Sqrt(0.5))
                    return new VectorOrientation(across > 0 ? n : -n);

                // PlanarOrientation gives T x N, so N = Y x T (up to its side)
                if (Vector3d.CrossProduct(startY, tangent) * n < 0) n.Reverse();
                return new PlanarOrientation(new Plane(plane.Origin, n));
            }

            return new RmfOrientation(startY);
        }

        public static Beam MakeBeam(Curve centreline, Vector3d startY, int width, int height, double tolerance) => new Beam
        {
            Centreline = centreline,
            Orientation = Orientation(centreline, startY, tolerance),
            Width = width,
            Height = height,
        };

        /// <summary>
        /// Label plane: at the start, X along the axis, Y along the section's height.
        /// </summary>
        public static Plane LabelPlane(Beam beam)
        {
            var t0 = beam.Centreline.Domain.Min;
            return new Plane(beam.Centreline.PointAt(t0), beam.Centreline.TangentAt(t0), beam.Orientation.GetOrientation(beam.Centreline, t0));
        }

        /// <summary>
        /// Build a beam and its Brep from a centreline. Null, with a message, if the Brep fails.
        /// </summary>
        public static Beam? Build(RhinoDoc doc, string id, Curve centreline, Vector3d startY, int width, int height, out Brep? brep, out string? error)
        {
            error = null;
            brep = null;

            if (HasKinks(centreline, doc.ModelAngleToleranceRadians))
                RhinoApp.WriteLine($"{id}: the centreline has kinks; the beam goes round them smoothly.");

            var beam = MakeBeam(centreline, startY, width, height, doc.ModelAbsoluteTolerance);
            try { brep = beam.ToBrep(); }
            catch (Exception e) { error = e.Message; }

            if (brep == null || !brep.IsValid)
            {
                error = $"{id}: couldn't make the beam's Brep{(error != null ? $" ({error})" : "")}.";
                return null;
            }

            return beam;
        }

        /// <summary>
        /// Add a beam to the document as a component: label, Axis and Geometry, grouped and named.
        /// </summary>
        public static Guid Add(RhinoDoc doc, string id, Beam beam, Brep brep)
        {
            int width = (int)Math.Round(beam.Width), height = (int)Math.Round(beam.Height);
            var type = BeamType(width, height);
            var component = new Component(type, id, LabelPlane(beam));

            component.ReplaceMember(new ComponentMember(new LayerInfo("Geometry", type.LayerColor), new List<GeometryBase> { brep }, doc.CreateDefaultAttributes()));
            component.ReplaceMember(new ComponentMember(new LayerInfo("Axis", type.LayerColor), new List<GeometryBase> { beam.Centreline }, doc.CreateDefaultAttributes()));

            var attr = component.AttributeCollection[component.ID];
            attr.SetUserString("width", $"{width}");
            attr.SetUserString("height", $"{height}");
            attr.SetUserString("length", $"{Math.Round(beam.Centreline.GetLength())}");
            attr.SetUserString("orientation", beam.Orientation.ToString());

            return RHDoc.AddToRhinoDoc(component, doc, true);
        }

        /// <summary>
        /// The beam's centreline: its Axis member.
        /// </summary>
        public static Curve GetAxis(IComponent component, RhinoDoc doc) =>
            Utility.GetMember(component, "Axis", doc).OfType<Curve>().FirstOrDefault();

        private static Vector3d Perpendicular(Vector3d tangent, Vector3d v)
        {
            tangent.Unitize();
            var p = v - tangent * (v * tangent);
            if (p.IsTiny())
            {
                p = tangent.IsParallelTo(Vector3d.ZAxis, 1e-3) != 0 ? Vector3d.YAxis : Vector3d.ZAxis;
                p -= tangent * (p * tangent);
            }
            p.Unitize();
            return p;
        }

        private static bool HasKinks(Curve curve, double angleTolerance)
        {
            double t0 = curve.Domain.Min, t1 = curve.Domain.Max;
            while (curve.GetNextDiscontinuity(Continuity.G1_continuous, t0, t1, Math.Cos(angleTolerance), 1e-6, out double t))
            {
                if (t >= t1) break;
                return true;
            }
            return false;
        }
    }
}
