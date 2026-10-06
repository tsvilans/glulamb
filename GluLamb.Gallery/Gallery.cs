using System.Globalization;
using System.Text;

using GluLamb.Features;
using GluLamb.Joints;
using Rhino.FileIO;
using Rhino.Geometry;

namespace GluLamb.Gallery
{
    /// <summary>
    /// One joint type on one condition in one variant.
    /// </summary>
    internal class Cell
    {
        public JointTypeInfo Type;
        public Case Case;
        public string Variant;
        public bool Default;          // the type the registry picks for this condition
        public Point3d Origin;        // the joint condition's position
        public JointStatus Status;
        public List<string> Messages = new List<string>();
        public List<string> Problems = new List<string>();
        public List<Brep> Pieces = new List<Brep>();      // cut beams, by beam index
        public List<GeometryBase> Hardware = new List<GeometryBase>();
        public bool Pass => Problems.Count == 0;
    }

    internal class Gallery
    {
        private readonly Options m_options;
        private const double Tolerance = 0.01;

        // Variants: section rotations (quarter turns per beam) and Flip
        private static readonly (string Name, Func<int, int> Rotation, bool Flip)[] Variants =
        {
            ("as drawn", i => 0, false),
            ("as drawn, Flip", i => 0, true),
            ("sections turned", i => i % 2 == 0 ? 1 : 3, false),
            ("sections turned, Flip", i => i % 2 == 0 ? 1 : 3, true),
        };

        public Gallery(Options options)
        {
            m_options = options;
        }

        /// <summary>
        /// One joint type over every condition the registry offers it for: writes its results
        /// (types/{id}.json), its drawing ({id}.png, and {id}.svg in the docs folder) and its
        /// grid ({id}.3dm). Returns whether every cell passed.
        /// </summary>
        public bool Run(string typeId)
        {
            var registry = JointRegistry.Default;
            var type = registry.Get(typeId) ?? throw new ArgumentException($"No joint type {typeId}.");
            var cases = Conditions.All();

            var record = new TypeRecord { Id = type.Id };
            var offered = new List<(Case Case, bool Default)>();
            foreach (var c in cases)
            {
                var (beams, condition) = c.Build(i => 0);
                if (condition == null) continue;
                var candidates = registry.Candidates(condition, new BeamCollection(beams, Tolerance));
                var rank = candidates.FindIndex(x => x.Info.Id == type.Id);
                if (rank < 0) continue;
                offered.Add((c, rank == 0));
                record.Offered.Add(new OfferRecord { Case = c.Name, Family = c.Family, Default = rank == 0 });
            }

            var cells = new List<Cell>();
            var defaults = new Dictionary<string, Dictionary<string, object>>();
            foreach (var (c, isDefault) in offered)
                foreach (var variant in Variants)
                {
                    var cell = Try(type, c, variant, isDefault, defaults);
                    cells.Add(cell);
                    Console.WriteLine($"{(cell.Pass ? "ok  " : "FAIL")} {type.Id,-32} {c.Name,-34} {variant.Name,-22} {string.Join("; ", cell.Problems.Concat(cell.Messages))}");
                }
            if (offered.Count == 0)
                Console.WriteLine($"{type.Id}: not offered for any condition in the gallery");

            if (defaults.TryGetValue(type.Id, out var values))
                record.Defaults = values.ToDictionary(x => x.Key, x => Format.Value(x.Value));
            record.Cells = cells.Select(x => new CellRecord { Case = x.Case.Name, Variant = x.Variant, Pass = x.Pass, Problems = x.Problems, Messages = x.Messages }).ToList();

            var folder = Path.Combine(m_options.Out, "types");
            Directory.CreateDirectory(folder);
            if (cells.Count > 0)
            {
                Drawing.Png(cells, Path.Combine(folder, type.Id + ".png"));
                if (m_options.Docs != null)
                {
                    Directory.CreateDirectory(m_options.Docs);
                    File.WriteAllText(Path.Combine(m_options.Docs, type.Id + ".svg"), Drawing.Svg(cells));
                }
                if (m_options.Write3dm)
                    Write3dm(cells, Path.Combine(folder, type.Id + ".3dm"));
            }
            File.WriteAllText(Path.Combine(folder, type.Id + ".json"),
                System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            return cells.All(x => x.Pass);
        }

        private Cell Try(JointTypeInfo type, Case c, (string Name, Func<int, int> Rotation, bool Flip) variant, bool isDefault,
            Dictionary<string, Dictionary<string, object>> defaults)
        {
            var cell = new Cell { Type = type, Case = c, Variant = variant.Name, Default = isDefault };
            try
            {
                var (beams, condition) = c.Build(variant.Rotation);
                cell.Origin = condition.Position.Origin;
                var context = new BeamCollection(beams, Tolerance);
                var joint = type.Create(condition);
                if (!defaults.ContainsKey(type.Id))
                    defaults[type.Id] = JointParameters.Get(joint);
                if (variant.Flip)
                    JointParameters.Set(joint, new Dictionary<string, object> { ["Flip"] = true });

                var result = joint.Construct(context);
                cell.Status = result.Status;
                cell.Messages.AddRange(result.Messages);
                if (!result.Success)
                    cell.Problems.Add($"status {result.Status}");

                // Cut each beam (extended as the joint asks) with its features, except holes
                var volumes = new List<double>();
                for (int i = 0; i < beams.Count; ++i)
                {
                    var beam = Extended(beams[i], result);
                    var box = Box(beam);
                    var features = result.Features.TryGetValue(beams[i].Id, out var f) ? f : new List<Feature>();
                    var cutters = features.Where(x => !(x is Drilling)).SelectMany(x => x.GetCutters(beam, Tolerance)).ToList();
                    var piece = cutters.Count > 0 ? box.Cut(cutters, Tolerance) : box;
                    cell.Pieces.Add(piece);

                    var kept = Volume(piece) / Volume(box);
                    if (m_options.Verbose)
                        Console.WriteLine($"     {beams[i].Id}: kept {kept * 100:0.0}%, " + string.Join(", ", features.Select(x =>
                            $"{x.ProcessingName}[{string.Join("/", x.GetCutters(beam, Tolerance).Select(k => k == null ? "null" : k.IsSolid ? "solid" : "open"))}]")));
                    volumes.Add(Volume(piece));
                    if (piece == null || kept < 0.3)
                        cell.Problems.Add($"beam {beams[i].Id} lost its body (kept {kept * 100:0}%)");
                }

                // No two cut beams may overlap
                for (int i = 0; i < cell.Pieces.Count; ++i)
                    for (int k = i + 1; k < cell.Pieces.Count; ++k)
                    {
                        var a = cell.Pieces[i];
                        var b = cell.Pieces[k];
                        if (a == null || b == null || !BoundingBox.Intersection(a.GetBoundingBox(true), b.GetBoundingBox(true)).IsValid) continue;
                        var overlap = Brep.CreateBooleanIntersection(a, b, Tolerance)?.Sum(Volume) ?? 0;
                        if (overlap > 1000)
                            cell.Problems.Add($"beams {beams[i].Id} and {beams[k].Id} overlap by {overlap / 1000:0.#} cm³");
                    }

                cell.Hardware.AddRange(result.Hardware.Select(x => x.GetGeometry()).Where(x => x != null));
            }
            catch (Rhino.Runtime.NotLicensedException)
            {
                throw;
            }
            catch (Exception e)
            {
                cell.Status = JointStatus.Failed;
                cell.Problems.Add($"exception: {e.Message}");
            }
            return cell;
        }

        private static Beam Extended(Beam beam, JointResult result)
        {
            result.Extensions.TryGetValue(beam.Id, out var e);
            var x = beam.Duplicate();
            var c = x.Centreline;
            if (e.Start > 0) c = c.Extend(CurveEnd.Start, e.Start, CurveExtensionStyle.Line);
            if (e.End > 0) c = c.Extend(CurveEnd.End, e.End, CurveExtensionStyle.Line);
            x.Centreline = c;
            return x;
        }

        private static Brep Box(Beam b)
        {
            var plane = b.GetPlane(b.Centreline.Domain.Min);
            return new Box(plane, new Interval(-b.Width / 2, b.Width / 2), new Interval(-b.Height / 2, b.Height / 2),
                new Interval(0, b.Centreline.GetLength())).ToBrep();
        }

        private static double Volume(Brep b) => b == null ? 0 : Math.Abs(VolumeMassProperties.Compute(b)?.Volume ?? 0);

        /// <summary>
        /// All cells in one file: a row per joint type, the type's cells along it; a layer per
        /// type, beams coloured by index, hardware grey, a dot on each cell.
        /// </summary>
        private static void Write3dm(List<Cell> cells, string path)
        {
            var file = new File3dm();
            var colours = new[] { System.Drawing.Color.SandyBrown, System.Drawing.Color.SteelBlue, System.Drawing.Color.OliveDrab, System.Drawing.Color.IndianRed };
            const double spacing = 3000;

            int row = 0;
            foreach (var group in cells.GroupBy(x => x.Type.Id))
            {
                var layer = new Rhino.DocObjects.Layer { Name = group.Key };
                file.AllLayers.Add(layer);
                var index = file.AllLayers.Count - 1;

                int col = 0;
                foreach (var cell in group)
                {
                    var move = Transform.Translation(col * spacing, -row * spacing, 0);
                    for (int i = 0; i < cell.Pieces.Count; ++i)
                    {
                        if (cell.Pieces[i] == null) continue;
                        var piece = cell.Pieces[i].DuplicateBrep();
                        piece.Transform(move);
                        file.Objects.AddBrep(piece, new Rhino.DocObjects.ObjectAttributes
                        {
                            LayerIndex = index,
                            ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                            ObjectColor = colours[i % colours.Length],
                        });
                    }
                    foreach (var hw in cell.Hardware)
                    {
                        var g = hw.Duplicate();
                        g.Transform(move);
                        if (g is Brep brep)
                            file.Objects.AddBrep(brep, new Rhino.DocObjects.ObjectAttributes
                            {
                                LayerIndex = index,
                                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                                ObjectColor = System.Drawing.Color.DimGray,
                            });
                    }
                    var label = $"{cell.Type.Id}\n{cell.Case.Name}\n{cell.Variant}\n{(cell.Pass ? "ok" : "FAIL: " + string.Join("; ", cell.Problems))}";
                    file.Objects.AddTextDot(label, new Point3d(col * spacing, -row * spacing + 900, 0), new Rhino.DocObjects.ObjectAttributes { LayerIndex = index });
                    col++;
                }
                row++;
            }

            file.Write(path, 8);
        }
    }

    internal static class Format
    {
        public static string Value(object value) => value switch
        {
            null => "",
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }
}
