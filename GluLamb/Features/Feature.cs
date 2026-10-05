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
            return other;
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

        public override IList<Brep> GetCutters(Beam beam, double tolerance)
        {
            var size = CoverSize(beam);
            var box = new Box(Plane, new Interval(-size, size), new Interval(-size, size), new Interval(0, size));
            return new[] { box.ToBrep() };
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

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(x => x.DuplicateBrep()).ToList();

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

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(x => x.DuplicateBrep()).ToList();

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
