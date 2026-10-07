using System.Globalization;
using System.Text;

using Rhino.Geometry;

namespace GluLamb.Gallery
{
    /// <summary>
    /// Hidden-line drawings of gallery cells as SVG: one drawing per joint type, a row per
    /// condition, a column per variant.
    /// </summary>
    internal static class Drawing
    {
        private const double CellSize = 220;
        private const double LabelHeight = 34;
        private static readonly string[] Colours = { "#b5651d", "#2f6690", "#5b7f2a", "#a23b3b" };
        private const string HardwareColour = "#555555";
        private const double CropRadius = 450;   // half the size of the box around the joint that is drawn
        private const double Explode = 160;      // how far each beam is moved away from the joint

        // Looking down from the front right
        private static readonly Vector3d ViewDirection = new Vector3d(-1, 1.4, -1.1);

        public static string Svg(IList<Cell> cells)
        {
            var rows = cells.GroupBy(x => x.Case.Name).ToList();
            int columns = rows.Max(r => r.Count());
            double width = columns * CellSize, height = rows.Count * (CellSize + LabelHeight);

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\" font-family=\"sans-serif\" font-size=\"11\">");
            sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

            for (int r = 0; r < rows.Count; ++r)
            {
                int c = 0;
                foreach (var cell in rows[r])
                {
                    double x0 = c * CellSize, y0 = r * (CellSize + LabelHeight);
                    sb.AppendLine($"<g transform=\"translate({F(x0)},{F(y0)})\">");
                    sb.AppendLine($"<rect x=\"2\" y=\"2\" width=\"{F(CellSize - 4)}\" height=\"{F(CellSize + LabelHeight - 4)}\" fill=\"none\" stroke=\"{(cell.Pass ? "#dddddd" : "#d33")}\" stroke-width=\"{(cell.Pass ? 1 : 2)}\"/>");
                    sb.Append(CellPaths(cell));
                    sb.AppendLine($"<text x=\"8\" y=\"{F(CellSize + 12)}\" fill=\"#222\">{Escape(cell.Case.Name)}</text>");
                    sb.AppendLine($"<text x=\"8\" y=\"{F(CellSize + 26)}\" fill=\"{(cell.Pass ? "#666" : "#d33")}\">{Escape(cell.Pass ? cell.Variant : "FAIL " + cell.Variant)}</text>");
                    sb.AppendLine("</g>");
                    c++;
                }
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// A cell's visible lines, fitted into the cell, in cell coordinates (y down).
        /// </summary>
        private static List<(List<(double X, double Y)> Points, string Colour)> CellLines(Cell cell)
        {
            var lines = new List<(List<(double, double)>, string)>();
            var geometry = new List<(GeometryBase Geometry, string Colour)>();

            // Only the joint: each beam cropped to a box around the joint, then moved a little
            // away from it along the beam (exploded), so the cuts show
            var sum = Vector3d.Zero;    // where the beams go from the joint, together
            var crop = new Box(new Plane(cell.Origin, Vector3d.ZAxis), new Interval(-CropRadius, CropRadius), new Interval(-CropRadius, CropRadius), new Interval(-CropRadius, CropRadius)).ToBrep();
            for (int i = 0; i < cell.Pieces.Count; ++i)
            {
                if (cell.Pieces[i] == null) continue;
                var cropped = Brep.CreateBooleanIntersection(cell.Pieces[i], crop, 0.01);
                if (cropped == null || cropped.Length == 0) continue;
                var centre = BoundingBox.Empty;
                foreach (var b in cropped) centre.Union(b.GetBoundingBox(true));
                var away = centre.Center - cell.Origin;
                if (away.Unitize())
                    sum += away;
                if (away.Length > 0.5)
                    foreach (var b in cropped) b.Translate(away * Explode);
                foreach (var b in cropped)
                    geometry.Add((b, Colours[i % Colours.Length]));
            }
            geometry.AddRange(cell.Hardware.Select(h => (h, HardwareColour)));
            if (geometry.Count == 0) return lines;

            var view = View(sum);
            var curves = HiddenLines(geometry, view) ?? Wireframe(geometry, view);
            if (curves.Count == 0) return lines;

            var bb = BoundingBox.Empty;
            foreach (var (pts, _) in curves) bb.Union(new BoundingBox(pts));
            var margin = 12.0;
            var scale = Math.Min((CellSize - margin * 2) / Math.Max(bb.Max.X - bb.Min.X, 1e-6), (CellSize - margin * 2) / Math.Max(bb.Max.Y - bb.Min.Y, 1e-6));
            double ox = margin + ((CellSize - margin * 2) - (bb.Max.X - bb.Min.X) * scale) * 0.5;
            double oy = margin + ((CellSize - margin * 2) - (bb.Max.Y - bb.Min.Y) * scale) * 0.5;

            foreach (var (pts, colour) in curves)
                lines.Add((pts.Select(p => (ox + (p.X - bb.Min.X) * scale, oy + (bb.Max.Y - p.Y) * scale)).ToList(), colour));
            return lines;
        }

        /// <summary>
        /// Looking down at the joint from the side most beams come from (so the faces that
        /// meet them show), a little from the side; the default view if they balance out.
        /// </summary>
        private static Vector3d View(Vector3d beams)
        {
            var flat = new Vector3d(beams.X, beams.Y, 0);
            if (!flat.Unitize()) return ViewDirection;
            var side = Vector3d.CrossProduct(flat, Vector3d.ZAxis);
            return -flat * 0.45 - side * 1.0 + new Vector3d(0, 0, -0.9);
        }

        private static string CellPaths(Cell cell)
        {
            var sb = new StringBuilder();
            foreach (var group in CellLines(cell).GroupBy(x => x.Colour))
            {
                sb.Append($"<path fill=\"none\" stroke=\"{group.Key}\" stroke-width=\"0.8\" stroke-linejoin=\"round\" d=\"");
                foreach (var (pts, _) in group)
                    for (int i = 0; i < pts.Count; ++i)
                        sb.Append(i == 0 ? "M" : "L").Append(F(pts[i].X)).Append(',').Append(F(pts[i].Y)).Append(' ');
                sb.AppendLine("\"/>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// The same drawing as a PNG, for looking at without a browser.
        /// </summary>
        public static void Png(IList<Cell> cells, string path)
        {
            var rows = cells.GroupBy(x => x.Case.Name).ToList();
            int columns = rows.Max(r => r.Count());
            int width = (int)(columns * CellSize), height = (int)(rows.Count * (CellSize + LabelHeight));
            using (var bitmap = new System.Drawing.Bitmap(width, height))
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            using (var font = new System.Drawing.Font("Segoe UI", 8))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.White);
                for (int r = 0; r < rows.Count; ++r)
                {
                    int c = 0;
                    foreach (var cell in rows[r])
                    {
                        float x0 = (float)(c * CellSize), y0 = (float)(r * (CellSize + LabelHeight));
                        using (var frame = new System.Drawing.Pen(cell.Pass ? System.Drawing.Color.Gainsboro : System.Drawing.Color.Red, cell.Pass ? 1 : 2))
                            g.DrawRectangle(frame, x0 + 2, y0 + 2, (float)CellSize - 4, (float)(CellSize + LabelHeight) - 4);
                        foreach (var (pts, colour) in CellLines(cell))
                        {
                            if (pts.Count < 2) continue;
                            using (var pen = new System.Drawing.Pen(System.Drawing.ColorTranslator.FromHtml(colour), 1))
                                g.DrawLines(pen, pts.Select(p => new System.Drawing.PointF(x0 + (float)p.X, y0 + (float)p.Y)).ToArray());
                        }
                        g.DrawString(cell.Case.Name, font, System.Drawing.Brushes.Black, x0 + 6, y0 + (float)CellSize);
                        g.DrawString(cell.Pass ? cell.Variant : "FAIL " + cell.Variant, font, cell.Pass ? System.Drawing.Brushes.Gray : System.Drawing.Brushes.Red, x0 + 6, y0 + (float)CellSize + 14);
                        c++;
                    }
                }
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>Visible edges in a parallel view, flattened, as polylines in view coordinates.</summary>
        private static List<(List<Point3d> Points, string Colour)> HiddenLines(List<(GeometryBase Geometry, string Colour)> geometry, Vector3d view)
        {
            try
            {
                var bb = BoundingBox.Empty;
                foreach (var (g, _) in geometry) bb.Union(g.GetBoundingBox(true));
                var radius = bb.Diagonal.Length;
                var dir = view;
                dir.Unitize();

                var vp = new Rhino.DocObjects.ViewportInfo();
                vp.ChangeToParallelProjection(true);
                vp.SetCameraLocation(bb.Center - dir * radius * 4);
                vp.SetCameraDirection(dir);
                vp.SetCameraUp(Vector3d.ZAxis);
                vp.SetFrustum(-radius, radius, -radius, radius, radius, radius * 8);

                var parameters = new HiddenLineDrawingParameters
                {
                    AbsoluteTolerance = 0.1,
                    Flatten = true,
                    IncludeHiddenCurves = false,
                    IncludeTangentEdges = false,
                };
                parameters.SetViewport(vp);
                for (int i = 0; i < geometry.Count; ++i)
                    parameters.AddGeometry(geometry[i].Geometry, Transform.Identity, i);

                var drawing = HiddenLineDrawing.Compute(parameters, true);
                if (drawing == null) return null;

                var result = new List<(List<Point3d>, string)>();
                foreach (var segment in drawing.Segments)
                {
                    if (segment.SegmentVisibility != HiddenLineDrawingSegment.Visibility.Visible) continue;
                    var tag = segment.ParentCurve?.SourceObject?.Tag;
                    if (!(tag is int index) || segment.CurveGeometry == null) continue;
                    result.Add((Points(segment.CurveGeometry), geometry[index].Colour));
                }
                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>All edges, projected: the fallback when hidden-line drawing fails.</summary>
        private static List<(List<Point3d> Points, string Colour)> Wireframe(List<(GeometryBase Geometry, string Colour)> geometry, Vector3d direction)
        {
            var dir = direction;
            dir.Unitize();
            var screen = new Plane(Point3d.Origin, Vector3d.CrossProduct(Vector3d.ZAxis, dir), Vector3d.ZAxis);
            screen = new Plane(Point3d.Origin, screen.XAxis, Vector3d.CrossProduct(dir, screen.XAxis));

            var result = new List<(List<Point3d>, string)>();
            foreach (var (g, colour) in geometry)
            {
                if (!(g is Brep brep)) continue;
                foreach (var edge in brep.Edges)
                {
                    var pts = Points(edge.ToNurbsCurve()).Select(p =>
                    {
                        screen.RemapToPlaneSpace(p, out Point3d q);
                        return new Point3d(q.X, q.Y, 0);
                    }).ToList();
                    result.Add((pts, colour));
                }
            }
            return result;
        }

        private static List<Point3d> Points(Curve curve)
        {
            if (curve.TryGetPolyline(out Polyline pl)) return pl.ToList();
            var polyline = curve.ToPolyline(0.5, 0.1, 0, 0);
            if (polyline != null && polyline.TryGetPolyline(out pl)) return pl.ToList();
            return new List<Point3d> { curve.PointAtStart, curve.PointAtEnd };
        }

        private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
