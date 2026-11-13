/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using GluLamb.Blanks;
using GluLamb.Numerics;

namespace GluLamb.Reconstruction
{
    public class BrepBlankOptions
    {
        /// <summary>How long edges are grouped into runs of parallel edges. Replaces Mesh.Clustering
        /// for the mesh centreline.</summary>
        public EdgeClusterOptions Clustering = new EdgeClusterOptions();

        /// <summary>Number and range (0 to 1) of the sections that give the orientation.</summary>
        public int SectionSamples = 30;

        /// <summary>Edges that the blank lies along (two or more, e.g. the edges of one face).
        /// They keep their places in the blank's section, so a beam that tapers has its taper on
        /// the other side. Null: the blank is centred on the sections. GuidePasses: number of
        /// passes. Clustering.EdgeSamples: points per guide.</summary>
        public List<Curve> Guides;
        public int GuidePasses = 2;
        public Interval SectionRange = new Interval(0.02, 0.98);

        /// <summary>Number of extra passes from the centred centreline.</summary>
        public int Refinements = 2;

        /// <summary>Extra material on each side of the section, and at each end.</summary>
        public double Allowance = 0;
        public double EndAllowance = 0;

        /// <summary>Use a PlanarOrientation if all section frames lie in one plane.</summary>
        public bool SimplifyOrientation = true;
        public double PlanarAngleTolerance = RhinoMath.ToRadians(1);

        /// <summary>Get the centreline and frames from MeshBeam on a mesh of the Brep. The Brep
        /// gives the envelope only. If off, or if MeshBeam fails: use the longest run of parallel
        /// edges and sections of the Brep.</summary>
        public bool UseMeshCentreline = true;
        public MeshBeamOptions Mesh = new MeshBeamOptions();

        /// <summary>Tolerance for Brep-plane intersections.</summary>
        public double Tolerance = Globals.FromMillimeters(0.01);
    }

    public class BrepBlankResult
    {
        /// <summary>The simplified beam: centred on its section, encloses the Brep.</summary>
        public Beam Beam;

        /// <summary>The blank from the beam (a Glulam: its section can be rounded up to whole
        /// lamellae), and the beam's placement in it.</summary>
        public Glulam Blank;
        public Placement Placement;

        public bool Success => Beam != null;

        /// <summary>The compared edges, their cluster labels, and the cluster that gives the centreline.</summary>
        public List<Polyline> Edges = new List<Polyline>();
        public int[] Labels;
        public List<Polyline> Cluster = new List<Polyline>();

        /// <summary>Without the mesh centreline: the longest edge of the cluster. The centreline
        /// starts from it.</summary>
        public Curve LongestEdge;

        public List<Section> Sections = new List<Section>();

        /// <summary>Extents of the Brep in beam space (X across, Y up, Z along).</summary>
        public BoundingBox Extents;

        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// The blank of a detailed beam Brep: a rectangular section along a centreline, sized in
    /// the beam's curved space to enclose the Brep.
    /// </summary>
    public static class BrepBlank
    {
        public static BrepBlankResult Reconstruct(Brep brep, BrepBlankOptions options = null)
        {
            options ??= new BrepBlankOptions();
            var result = new BrepBlankResult();
            if (brep == null) { result.Messages.Add("No Brep."); return result; }

            var tol = options.Mesh?.Tolerances ?? new ReconstructionTolerances();

            // One mesh of the Brep. MeshBeam finds the centreline on it. Its vertices and points
            // on the Brep's edges give the envelope.
            var mesh = MeshOf(brep, tol);
            var brepPoints = new List<Point3d>(new HashSet<Point3d>(mesh.Vertices.ToPoint3dArray()));
            foreach (var edge in brep.Edges)
                brepPoints.AddRange(Reconstruction.Edges.ToPolyline(edge, 20));

            Beam beam = null;
            MeshBeamResult fromMesh = null;
            if (options.UseMeshCentreline)
            {
                var meshOptions = options.Mesh ?? new MeshBeamOptions();
                meshOptions.Clustering = options.Clustering;
                fromMesh = MeshBeam.Reconstruct(mesh, meshOptions);
                if (!fromMesh.Success) result.Messages.Add("The mesh centreline failed; following the parallel edges instead.");
            }

            if (fromMesh != null && fromMesh.Success)
            {
                // Use the mesh's sharp edges and their longest parallel run in place of the Brep's
                result.Edges = fromMesh.Edges;
                result.Cluster = fromMesh.ParallelEdges;
                result.Messages.AddRange(fromMesh.Messages.Select(m => "Centreline: " + m));

                // Frames from sections of the Brep along the centreline. If none: the mesh's frames.
                var sections = BrepSections(brep, fromMesh.Beam.Centreline, new EdgeSet(result.Cluster), options, tol);
                if (sections.Count == 0) sections = fromMesh.Sections;
                result.Sections = sections;
                var curve = fromMesh.Beam.Centreline;
                var frames = sections.Select(s => s.Plane).ToList();

                // Guide edges give the place and rotation of the blank: they keep their places in
                // its section, so the blank lies along them
                if (options.Guides != null && options.Guides.Count >= 2)
                {
                    var polylines = options.Guides.Where(g => g != null)
                        .Select(g => new Polyline(g.DivideByCount(Math.Max(2, options.Clustering.EdgeSamples) - 1, true).Select(t => g.PointAt(t)))).ToList();
                    var guides = new EdgeSet(polylines, tol.RectangleAngle);
                    int passes = 0;
                    for (int pass = 0; pass < Math.Max(1, options.GuidePasses); ++pass)
                    {
                        var probe = new Beam { Centreline = curve, Width = fromMesh.Beam.Width, Height = fromMesh.Beam.Height };
                        probe.Orientation = MeshBeam.Orientation(curve, frames, false, 0, new List<string>());
                        var followed = EdgeClusters.EdgeFrames(probe, guides, 0, tol);
                        if (followed == null) break;
                        frames = followed.ToList();
                        curve = new InterpolatingSpline(followed.Select(f => f.Origin).ToArray()).ToNurbsCurve();
                        ++passes;
                    }
                    if (passes > 0) result.Cluster = polylines;
                    result.Messages.Add(passes > 0
                        ? $"Follows {polylines.Count} guide edges ({passes} passes)."
                        : "Couldn't follow the guide edges; using the sections.");
                }

                if (!Envelope(brepPoints, curve, frames, options, tol, true, result, out beam))
                    return result;
            }
            else
            {
                // The longest run of parallel Brep edges. Its longest edge is the start centreline.
                var curves = brep.Edges.Select(e => (Curve)e).Where(c => c != null).ToList();
                var parallel = EdgeClusters.FindLargest(curves, options.Clustering);
                if (parallel == null) { result.Messages.Add("The Brep has no edges."); return result; }

                result.Edges = parallel.Edges;
                result.Labels = parallel.Labels;
                result.Cluster = parallel.Cluster;
                result.LongestEdge = parallel.LongestEdge;
                result.Messages.Add(parallel.Summary);

                // The longest edge is on a face, not at the section centre. That tilts its sections on
                // a twisted beam. Repeat the pass from the centred curve.
                var clusterEdges = new EdgeSet(result.Cluster);
                Curve curve = parallel.LongestEdge;
                for (int pass = 0; pass <= options.Refinements; ++pass)
                {
                    bool last = pass == options.Refinements;
                    if (!Pass(brep, brepPoints, clusterEdges, curve, options, tol, last, result, out beam))
                        return result;
                    if (!last) curve = EdgeClusters.Recentre(beam, clusterEdges, 0, tol);
                }
            }
            result.Beam = beam;

            try
            {
                result.Blank = Glulam.CreateGlulam(beam, beam.Orientation);
                result.Placement = new Placement { BlankId = result.Blank.Id, BeamId = beam.Id };
            }
            catch (Exception ex)
            {
                result.Messages.Add($"Couldn't make a glulam blank: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// One pass along a centreline. Sections of the Brep give the orientation. The Brep's
        /// extents in beam space give the section. Centres the curve and trims it to the Brep.
        /// </summary>
        static bool Pass(Brep brep, List<Point3d> brepPoints, EdgeSet clusterEdges, Curve curve, BrepBlankOptions options, ReconstructionTolerances tol, bool last, BrepBlankResult result, out Beam beam)
        {
            beam = null;
            var sections = BrepSections(brep, curve, clusterEdges, options, tol);
            if (sections.Count == 0) { result.Messages.Add("No sections of the Brep found along the centreline."); return false; }
            result.Sections = sections;
            return Envelope(brepPoints, curve, sections.Select(s => s.Plane).ToList(), options, tol, last, result, out beam);
        }

        /// <summary>
        /// Sections of the Brep normal to a curve, aligned to the parallel edges, with consistent frames.
        /// </summary>
        static List<Section> BrepSections(Brep brep, Curve curve, EdgeSet clusterEdges, BrepBlankOptions options, ReconstructionTolerances tol)
        {
            var sections = new List<Section>();
            foreach (var t in Sampling.Linspace(options.SectionRange.Min, options.SectionRange.Max, options.SectionSamples))
            {
                double u = curve.Domain.ParameterAt(t);
                var plane = new Plane(curve.PointAt(u), curve.TangentAt(u));
                if (!Intersection.BrepPlane(brep, plane, options.Tolerance, out Curve[] sectionCurves, out Point3d[] sectionPoints)) continue;

                var points = new List<Point3d>(sectionPoints);
                foreach (var c in sectionCurves)
                    points.AddRange(Reconstruction.Edges.ToPolyline(c, 16));

                if (Sections.TryFromPoints(points, plane.Origin, plane.ZAxis, out Section section, tol.RectangleAngle))
                {
                    if (clusterEdges != null) EdgeClusters.AlignToEdges(ref section, clusterEdges, tol);
                    sections.Add(section);
                }
            }
            Sections.MakeConsistent(sections, Sections.PreferredUp(curve));
            return sections;
        }

        /// <summary>
        /// Sizes the section from the Brep's extents in the curved space of a beam along the
        /// centreline. Moves the curve to the section centre and trims it to the Brep's length.
        /// </summary>
        static bool Envelope(List<Point3d> brepPoints, Curve curve, List<Plane> frames, BrepBlankOptions options, ReconstructionTolerances tol, bool last, BrepBlankResult result, out Beam beam)
        {
            beam = null;
            var messages = last ? result.Messages : new List<string>();

            // The Brep in the curved space of a beam along the extended curve
            var extended = CurveEnds.Extend(curve, tol.EnvelopeRunOutFraction * curve.GetLength(), EndExtension.Straight);
            extended.Domain = new Interval(0, 1);

            var probe = new Beam { Centreline = extended };
            probe.Orientation = MeshBeam.Orientation(extended, frames, false, 0, messages);

            var extents = new BoundingBox(probe.ToBeamSpace(brepPoints, true, 200));
            result.Extents = extents;

            // Trim to the Brep's length. Centre the section on the curve.
            double a = options.Allowance, e = options.EndAllowance;
            double extLength = extended.GetLength();
            double z0 = Math.Max(0, extents.Min.Z - e), z1 = Math.Min(extLength, extents.Max.Z + e);
            if (!extended.LengthParameter(z0, out double t0) || !extended.LengthParameter(z1, out double t1) || t1 <= t0)
            { result.Messages.Add("Couldn't trim the centreline to the Brep."); return false; }

            var trimmed = extended.Trim(t0, t1);
            var orientation = probe.Orientation.Duplicate();
            orientation.Remap(extended, trimmed);

            var offset = new Beam
            {
                Centreline = trimmed,
                Orientation = orientation,
                OffsetX = (extents.Min.X + extents.Max.X) * 0.5,
                OffsetY = (extents.Min.Y + extents.Max.Y) * 0.5,
                Width = extents.Max.X - extents.Min.X + 2 * a,
                Height = extents.Max.Y - extents.Min.Y + 2 * a
            };

            var centreline = BeamOps.SectionCentreline(offset, out _);
            centreline.Domain = new Interval(0, 1);

            // Orientation on the centred curve, from the moved section frames
            var moved = frames.Select(f =>
            {
                var p = f;
                p.Origin = p.PointAt(offset.OffsetX, offset.OffsetY);
                return p;
            }).ToList();

            beam = new Beam { Centreline = centreline, Width = offset.Width, Height = offset.Height };
            beam.Orientation = MeshBeam.Orientation(centreline, moved, last && options.SimplifyOrientation, options.PlanarAngleTolerance, messages);
            return true;
        }

        /// <summary>
        /// One mesh of a Brep (face meshes joined). Maximum edge length: the smaller of
        /// MeshEdgeLength and the Brep's diagonal / MeshDivisions, so it follows curved faces.
        /// </summary>
        static Mesh MeshOf(Brep brep, ReconstructionTolerances tol)
        {
            double diagonal = brep.GetBoundingBox(false).Diagonal.Length;
            var parameters = new MeshingParameters
            {
                MaximumEdgeLength = Math.Min(tol.MeshEdgeLength, diagonal / tol.MeshDivisions),
                SimplePlanes = false
            };
            var mesh = new Mesh();
            foreach (var m in Mesh.CreateFromBrep(brep, parameters) ?? Array.Empty<Mesh>())
                mesh.Append(m);
            return mesh;
        }
    }
}
