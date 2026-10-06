using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace GluLamb.Features
{
    /// <summary>
    /// A manufacturing-neutral description of material removed from one beam.
    /// Features say what is removed, not how it is machined: tool and strategy are
    /// chosen later, when features are mapped to a production format (e.g. BTLx).
    /// Feature names follow BTLx processings where there is an equivalent.
    /// </summary>
    public abstract class Feature
    {
        /// <summary>
        /// Identifier of the beam this feature is applied to (Beam.Id).
        /// </summary>
        public string BeamId;

        /// <summary>
        /// Reference plane of the feature, in world coordinates.
        /// </summary>
        public Plane Plane = Plane.Unset;

        /// <summary>
        /// Name of the equivalent BTLx processing, or a custom name if there is none.
        /// </summary>
        public abstract string ProcessingName { get; }

        /// <summary>
        /// Extra named data for production that has no typed parameter yet (e.g. pocket outlines
        /// and depths). Not transformed by Transform().
        /// </summary>
        public Rhino.Collections.ArchivableDictionary Data = new Rhino.Collections.ArchivableDictionary();

        /// <summary>
        /// Geometry for cutting the feature out of a beam or blank model. Closed breps are
        /// subtracted; open breps split the model and the largest piece is kept (see
        /// BrepExtensionMethods.Cut).
        /// </summary>
        /// <param name="beam">The beam the feature belongs to, used to size cutters.</param>
        /// <param name="tolerance">Modelling tolerance.</param>
        public abstract IList<Brep> GetCutters(Beam beam, double tolerance);

        public virtual void Transform(Transform xform)
        {
            if (Plane.IsValid)
                Plane.Transform(xform);
        }

        public abstract Feature Duplicate();

        public override string ToString() => $"{ProcessingName} ({BeamId})";

        protected T CopyBaseTo<T>(T other) where T : Feature
        {
            other.BeamId = BeamId;
            other.Plane = Plane;
            other.Data = Data?.Clone() ?? new Rhino.Collections.ArchivableDictionary();
            return other;
        }

        /// <summary>
        /// Copy of a cutter, with closed breps turned outward by the sign of their volume.
        /// (BrepSolidOrientation can report Outward for breps whose faces point inward, e.g.
        /// after filleting, and a boolean difference with those keeps the cutter instead.)
        /// </summary>
        public static Brep PrepareCutter(Brep cutter)
        {
            var copy = cutter.DuplicateBrep();
            if (copy.IsSolid)
            {
                var vmp = VolumeMassProperties.Compute(copy, true, false, false, false);
                if (vmp != null && vmp.Volume < 0)
                    copy.Flip();
            }
            return copy;
        }

        /// <summary>
        /// Size a square that is guaranteed to cover the beam's cross-section and length,
        /// for building plane cutters.
        /// </summary>
        protected static double CoverSize(Beam beam)
        {
            var bb = beam.Centreline.GetBoundingBox(true);
            return bb.Diagonal.Length + Math.Max(beam.Width, beam.Height) * 2;
        }
    }

    /// <summary>
    /// Plane cut through the whole beam. The plane normal points towards the material
    /// that is removed. BTLx: JackRafterCut.
    /// </summary>
    public class JackRafterCut : Feature
    {
        public override string ProcessingName => "JackRafterCut";

        public JackRafterCut() { }
        public JackRafterCut(string beamId, Plane plane)
        {
            BeamId = beamId;
            Plane = plane;
        }

        /// <summary>
        /// A planar surface in the cut plane, just covering where the beam crosses it (sampled
        /// along the centreline, with a small margin and a short tangent extension past each end,
        /// so it still works on a slightly extended beam). Brep.Cut splits the beam with it and
        /// keeps the largest piece; the plane itself (with its normal towards the removed side)
        /// is the production data.
        /// </summary>
        public override IList<Brep> GetCutters(Beam beam, double tolerance)
        {
            var curve = beam.Centreline;
            var size = Math.Max(beam.Width, beam.Height);
            var margin = size * 0.1 + tolerance * 10;
            var hw = beam.Width * 0.5 + Math.Abs(beam.OffsetX);
            var hh = beam.Height * 0.5 + Math.Abs(beam.OffsetY);

            var sections = new List<Plane>();
            const int samples = 64;
            for (int i = 0; i <= samples; ++i)
                sections.Add(beam.GetPlane(curve.Domain.ParameterAt(i / (double)samples)));

            // Past each end along the tangent
            var start = beam.GetPlane(curve.Domain.Min);
            var end = beam.GetPlane(curve.Domain.Max);
            sections.Add(new Plane(start.Origin - start.ZAxis * size, start.XAxis, start.YAxis));
            sections.Add(new Plane(end.Origin + end.ZAxis * size, end.XAxis, end.YAxis));

            // Corners of each section in the cut plane's coordinates
            var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
            var local = sections.Select(section => corners.Select(c =>
            {
                Plane.RemapToPlaneSpace(section.PointAt(c.Item1 * hw, c.Item2 * hh), out Point3d p);
                return p;
            }).ToArray()).ToArray();

            // Where the beam's edges cross the plane
            var points = new List<Point3d>();
            void AddEdge(Point3d a, Point3d b)
            {
                if ((a.Z < 0) != (b.Z < 0))
                    points.Add(a + (b - a) * (a.Z / (a.Z - b.Z)));
            }

            for (int i = 0; i < local.Length; ++i)
                for (int k = 0; k < 4; ++k)
                {
                    AddEdge(local[i][k], local[i][(k + 1) % 4]);   // around the section
                    if (i < samples)
                        AddEdge(local[i][k], local[i + 1][k]);     // along the beam
                }

            for (int k = 0; k < 4; ++k)
            {
                AddEdge(local[0][k], local[samples + 1][k]);       // past the start
                AddEdge(local[samples][k], local[samples + 2][k]); // past the end
            }

            if (points.Count < 1) return new Brep[0];

            // A planar surface just covering the beam's section in the plane; Brep.Cut splits the
            // beam with it and keeps the largest piece.
            var rectangle = new Rectangle3d(Plane,
                new Interval(points.Min(p => p.X) - margin, points.Max(p => p.X) + margin),
                new Interval(points.Min(p => p.Y) - margin, points.Max(p => p.Y) + margin));

            var surface = Brep.CreatePlanarBreps(rectangle.ToNurbsCurve(), tolerance);
            return surface == null ? new Brep[0] : surface.ToList();
        }

        public override Feature Duplicate() => CopyBaseTo(new JackRafterCut());
    }

    /// <summary>
    /// Stepped or notched removal described by its cutting geometry, used for laps,
    /// half-laps and notches. Plane is the lap base plane (normal pointing out of the
    /// material), Depth is measured along it. BTLx: Lap.
    /// </summary>
    public class Lap : Feature
    {
        public override string ProcessingName => "Lap";

        public double Depth = double.NaN;
        public double Width = double.NaN;
        public double Length = double.NaN;

        /// <summary>
        /// Cutting geometry, as open (split) or closed (subtract) breps.
        /// </summary>
        public List<Brep> Cutters = new List<Brep>();

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(PrepareCutter).ToList();

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            foreach (var cutter in Cutters)
                cutter.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var lap = CopyBaseTo(new Lap());
            lap.Depth = Depth;
            lap.Width = Width;
            lap.Length = Length;
            lap.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            return lap;
        }
    }

    /// <summary>
    /// Planar cut along the beam over part of its length (a rip cut), with the plane's normal
    /// towards the removed side. The cutter is the bounded planar surface. BTLx: LongitudinalCut.
    /// </summary>
    public class LongitudinalCut : Feature
    {
        public override string ProcessingName => "LongitudinalCut";

        /// <summary>
        /// Length of the cut along the beam.
        /// </summary>
        public double Length = double.NaN;

        public List<Brep> Cutters = new List<Brep>();

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(PrepareCutter).ToList();

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            foreach (var cutter in Cutters)
                cutter.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var cut = CopyBaseTo(new LongitudinalCut());
            cut.Length = Length;
            cut.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            return cut;
        }
    }

    /// <summary>
    /// Cylindrical hole along an axis. Axis.From is the entry point. BTLx: Drilling.
    /// </summary>
    public class Drilling : Feature
    {
        public override string ProcessingName => "Drilling";

        public Line Axis;
        public double Diameter;

        public Drilling() { }
        public Drilling(string beamId, Line axis, double diameter)
        {
            BeamId = beamId;
            Axis = axis;
            Diameter = diameter;
            Plane = new Plane(axis.From, axis.Direction);
        }

        public override IList<Brep> GetCutters(Beam beam, double tolerance)
        {
            var circle = new Circle(new Plane(Axis.From, Axis.Direction), Diameter * 0.5);
            var cylinder = new Cylinder(circle, Axis.Length);
            return new[] { cylinder.ToBrep(true, true) };
        }

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            Axis.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var drilling = CopyBaseTo(new Drilling());
            drilling.Axis = Axis;
            drilling.Diameter = Diameter;
            return drilling;
        }
    }

    /// <summary>
    /// Pocket defined by a closed planar outline and a depth. Plane is the outline plane
    /// with its normal pointing out of the material; the pocket goes down by Depth. BTLx: Pocket.
    /// </summary>
    public class Pocket : Feature
    {
        public override string ProcessingName => "Pocket";

        public Curve Outline;
        public double Depth;

        public override IList<Brep> GetCutters(Beam beam, double tolerance)
        {
            var extrusion = Surface.CreateExtrusion(Outline, -Plane.ZAxis * Depth);
            if (extrusion == null) return new Brep[0];
            var brep = extrusion.ToBrep().CapPlanarHoles(tolerance);
            return brep == null ? new Brep[0] : new[] { brep };
        }

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            Outline?.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var pocket = CopyBaseTo(new Pocket());
            pocket.Outline = Outline?.DuplicateCurve();
            pocket.Depth = Depth;
            return pocket;
        }
    }

    /// <summary>
    /// Arbitrary removal given only as geometry, for cuts with no parametric equivalent yet.
    /// BTLx: FreeContour.
    /// </summary>
    public class FreeContour : Feature
    {
        public override string ProcessingName => "FreeContour";

        public List<Brep> Cutters = new List<Brep>();

        /// <summary>
        /// Contours that define the cut, for production (e.g. tool paths or BTLx FreeContour).
        /// </summary>
        public List<Curve> Contours = new List<Curve>();

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(PrepareCutter).ToList();

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            foreach (var cutter in Cutters)
                cutter.Transform(xform);
            foreach (var contour in Contours)
                contour.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var contour = CopyBaseTo(new FreeContour());
            contour.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            contour.Contours = Contours.Select(x => x.DuplicateCurve()).ToList();
            return contour;
        }
    }
}
