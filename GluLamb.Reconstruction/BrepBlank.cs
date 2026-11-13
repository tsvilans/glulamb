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
        /// <summary>How the long edges are grouped into runs of parallel edges. Also used for the
        /// mesh centreline (it replaces Mesh.Clustering).</summary>
        public EdgeClusterOptions Clustering = new EdgeClusterOptions();

        /// <summary>Number and range (0 to 1 along the centreline) of the sections that give the
        /// orientation.</summary>
        public int SectionSamples = 30;
        public Interval SectionRange = new Interval(0.02, 0.98);

        /// <summary>Passes after the first that start again from the centred centreline.</summary>
        public int Refinements = 2;

        /// <summary>Extra material on each side of the section and at each end.</summary>
        public double Allowance = 0;
        public double EndAllowance = 0;

        /// <summary>Use a PlanarOrientation when the section frames all lie in one plane.</summary>
        public bool SimplifyOrientation = true;
        public double PlanarAngleTolerance = RhinoMath.ToRadians(1);

        /// <summary>Take the centreline and frames from MeshBeam on a mesh of the Brep (sections,
        /// edge tracking to the ends), and only the envelope from the Brep. Otherwise, or if that
        /// fails, they come from the longest run of parallel edges and sections of the Brep.</summary>
        public bool UseMeshCentreline = true;
        public MeshBeamOptions Mesh = new MeshBeamOptions();

        /// <summary>Tolerance for Brep and plane intersections.</summary>
        public double Tolerance = Globals.FromMillimeters(0.01);
    }

    public class BrepBlankResult
    {
        /// <summary>The simplified beam: centred on its section, enclosing the Brep.</summary>
        public Beam Beam;

        /// <summary>The blank made from it (a Glulam, so its section may be rounded up to whole
        /// lamellae), and where the beam sits in it.</summary>
        public Glulam Blank;
        public Placement Placement;

        public bool Success => Beam != null;

        /// <summary>Edges compared, their cluster labels, and the edges of the cluster the
        /// centreline comes from.</summary>
        public List<Polyline> Edges = new List<Polyline>();
        public int[] Labels;
        public List<Polyline> Cluster = new List<Polyline>();

        /// <summary>Without the mesh centreline: the longest edge of the cluster, which the
        /// centreline starts from before it is moved to the section centre.</summary>
        public Curve LongestEdge;

        public List<Section> Sections = new List<Section>();

        /// <summary>Extents of the Brep in the beam's space (X across, Y up, Z along).</summary>
        public BoundingBox Extents;

        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// The blank of a detailed beam Brep: a rectangular section swept along a centreline that
    /// follows the Brep's most common run of parallel edges, sized to enclose the Brep. Port of
    /// parallel.py, with the sizing done in the beam's curved space.
    /// </summary>
    public static class BrepBlank
    {
        public static BrepBlankResult Reconstruct(Brep brep, BrepBlankOptions options = null)
        {
            options ??= new BrepBlankOptions();
            var result = new BrepBlankResult();
            if (brep == null) { result.Messages.Add("No Brep."); return result; }

            var tol = options.Mesh?.Tolerances ?? new ReconstructionTolerances();

            // One mesh of the Brep, fine enough to follow its curved faces: MeshBeam finds the
            // centreline on it, and its vertices (with points along the Brep's edges) give the
            // envelope
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
                // The mesh's sharp edges and their longest parallel run stand in for the Brep's
                result.Edges = fromMesh.Edges;
                result.Cluster = fromMesh.ParallelEdges;
                result.Messages.AddRange(fromMesh.Messages.Select(m => "Centreline: " + m));

                // Frames from true sections of the Brep along that centreline (the mesh's frames
                // come from its typical sections only), else the mesh's
                var sections = BrepSections(brep, fromMesh.Beam.Centreline, new EdgeSet(result.Cluster), options, tol);
                if (sections.Count == 0) sections = fromMesh.Sections;
                result.Sections = sections;
                if (!Envelope(brepPoints, fromMesh.Beam.Centreline, sections, options, tol, true, result, out beam))
                    return result;
            }
            else
            {
                // The longest run of parallel edges of the Brep, and the longest of them as the
                // starting centreline
                var curves = brep.Edges.Select(e => (Curve)e).Where(c => c != null).ToList();
                var parallel = EdgeClusters.FindLargest(curves, options.Clustering);
                if (parallel == null) { result.Messages.Add("The Brep has no edges."); return result; }

                result.Edges = parallel.Edges;
                result.Labels = parallel.Labels;
                result.Cluster = parallel.Cluster;
                result.LongestEdge = parallel.LongestEdge;
                result.Messages.Add(parallel.Summary);

                // The longest edge is off the section centre (on a face), which tilts its sections
                // on a twisted beam, so the pass is repeated from the centred curve.
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
        /// One pass along a centreline: sections of the Brep normal to it give the orientation, the
        /// Brep's extents in the curved space of a beam along it give the section, and the curve is
        /// moved to the section's centre and trimmed to the Brep's length.
        /// </summary>
        static bool Pass(Brep brep, List<Point3d> brepPoints, EdgeSet clusterEdges, Curve curve, BrepBlankOptions options, ReconstructionTolerances tol, bool last, BrepBlankResult result, out Beam beam)
        {
            beam = null;
            var sections = BrepSections(brep, curve, clusterEdges, options, tol);
            if (sections.Count == 0) { result.Messages.Add("No sections of the Brep found along the centreline."); return false; }
            result.Sections = sections;
            return Envelope(brepPoints, curve, sections, options, tol, last, result, out beam);
        }

        /// <summary>
        /// Sections of the Brep normal to a curve, with frames lined up with the parallel edges (a
        /// small correction) and made consistent.
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
        /// The Brep's extents in the curved space of a beam along a centreline (with the frames of
        /// the sections) give the section; the curve is moved to the section's centre and trimmed
        /// to the Brep's length.
        /// </summary>
        static bool Envelope(List<Point3d> brepPoints, Curve curve, List<Section> sections, BrepBlankOptions options, ReconstructionTolerances tol, bool last, BrepBlankResult result, out Beam beam)
        {
            beam = null;
            var messages = last ? result.Messages : new List<string>();

            // The Brep in the curved space of a beam along the (extended) curve
            var extended = CurveEnds.Extend(curve, tol.EnvelopeRunOutFraction * curve.GetLength(), EndExtension.Straight);
            extended.Domain = new Interval(0, 1);

            var probe = new Beam { Centreline = extended };
            probe.Orientation = MeshBeam.Orientation(extended, sections.Select(s => s.Plane).ToList(), false, 0, messages);

            var extents = new BoundingBox(probe.ToBeamSpace(brepPoints, true, 200));
            result.Extents = extents;

            // Trim to the Brep's length and centre the section on the curve
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

            // The orientation on the centred curve, from the section frames moved with it
            var moved = sections.Select(s =>
            {
                var p = s.Plane;
                p.Origin = p.PointAt(offset.OffsetX, offset.OffsetY);
                return p;
            }).ToList();

            beam = new Beam { Centreline = centreline, Width = offset.Width, Height = offset.Height };
            beam.Orientation = MeshBeam.Orientation(centreline, moved, last && options.SimplifyOrientation, options.PlanarAngleTolerance, messages);
            return true;
        }

        /// <summary>
        /// A mesh of a Brep with its faces' meshes joined, no longer-edged than MeshEdgeLength and
        /// the Brep's diagonal over MeshDivisions, so that it follows curved faces.
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
