using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// A steel plate let into a face of a beam: a shallow rectangular pocket, an optional
    /// counterbore and bolt hole in its middle, and screws through the plate at its corners.
    /// For bearing plates, column feet and hangers placed at points along a beam. Port of
    /// FootJointX, with its hard-coded sizes as parameters. As with the drilling joint, the joint
    /// position picks the face (the one on its side of the centreline) and the place across it.
    /// The pocket is a Pocket feature, holes and screw pilots are Drillings, and the plate and
    /// screws are hardware.
    /// </summary>
    [JointType("glulamb.plate-pocket", Name = "Plate pocket", Arity = 1, Topology = JointTopology.Feature,
        Description = "A steel plate let into a beam face, with screws and an optional bolt.")]
    public class PlatePocketJoint : JointBase
    {
        [JointParameter(Description = "Pocket length, along the beam.", Unit = "length")]
        public double Length { get; set; } = 160;

        [JointParameter(Description = "Pocket width, across the face. 0 = the full face, running out at both sides.", Unit = "length")]
        public double Width { get; set; } = 0;

        [JointParameter(Description = "Pocket depth.", Unit = "length")]
        public double Depth { get; set; } = 10;

        [JointParameter(Description = "Plate thickness. 0 = the pocket depth.", Unit = "length")]
        public double PlateThickness { get; set; } = 0;

        [JointParameter(Description = "Tool diameter: the pocket corners are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 0;

        [JointParameter(Description = "Which face: 0 = the face towards the joint position, 1 = along the section X axis, 2 = along the section Y axis. Flip uses the opposite face.")]
        public int Axis { get; set; } = 0;

        [JointParameter(Description = "Counterbore diameter in the middle of the pocket, e.g. for a bolt head or nut. 0 = none.", Unit = "length")]
        public double CounterboreDiameter { get; set; } = 44;

        [JointParameter(Description = "Counterbore depth below the bottom of the pocket.", Unit = "length")]
        public double CounterboreDepth { get; set; } = 12;

        [JointParameter(Description = "Bolt hole diameter, through the beam from the middle of the pocket. 0 = none.", Unit = "length")]
        public double BoltDiameter { get; set; } = 0;

        [JointParameter(Description = "Screw diameter. 0 = no screws.", Unit = "length")]
        public double ScrewDiameter { get; set; } = 6;

        [JointParameter(Description = "Screw length, from the bottom of the pocket.", Unit = "length")]
        public double ScrewLength { get; set; } = 60;

        [JointParameter(Description = "Distance of the screws from the pocket ends.", Unit = "length")]
        public double ScrewEdgeOffsetX { get; set; } = 15;

        [JointParameter(Description = "Distance of the screws from the pocket sides (from the face sides for a full-width pocket).", Unit = "length")]
        public double ScrewEdgeOffsetY { get; set; } = 20;

        [JointParameter(Description = "Lean of the screws along the beam, away from the middle of the pocket.", Unit = "radians")]
        public double ScrewInclination { get; set; } = 0;

        [JointParameter(Description = "Extra size added to cutters so they clear the beam.", Unit = "length")]
        public double Added { get; set; } = 10;

        public PlatePocketJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Single parts, as an alternative to the drilling.
        /// </summary>
        public static double Score(JointX condition, IJointContext context) => condition.Parts.Count == 1 ? 0.5 : 0.0;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam = beams[0];
            var tolerance = context.Tolerance;
            var plane = beam.GetPlane(m_parts[0].Parameter);
            var point = Position.IsValid ? Position.Origin : plane.Origin;
            var offset = point - plane.Origin;
            offset -= plane.ZAxis * (offset * plane.ZAxis);

            Vector3d towards;
            switch (Axis)
            {
                case 1: towards = plane.XAxis; break;
                case 2: towards = plane.YAxis; break;
                default:
                    towards = offset.Length > tolerance ? offset : plane.YAxis;
                    break;
            }
            if (Axis != 0 && towards * offset < 0) towards.Reverse();
            if (Flip) towards.Reverse();

            // Frame on the face: X along the beam, Y across the face, Z out of the face
            var section = AlignSection(beam, plane, plane.ZAxis, towards, out double faceWidth, out double height);
            var width = Width > 0 ? Math.Min(Width, faceWidth) : faceWidth + Added * 2;
            var limit = Math.Max(0, (faceWidth - (Width > 0 ? width : faceWidth)) * 0.5);
            var across = Math.Max(-limit, Math.Min(limit, offset * section.XAxis));
            var face = new Plane(section.Origin + section.XAxis * across + section.YAxis * height * 0.5, section.ZAxis, section.XAxis);
            // face.ZAxis = section Z x section X = section Y: out of the face

            Curve Outline(Plane p, double w)
            {
                var rect = new Rectangle3d(p, new Interval(-Length * 0.5, Length * 0.5), new Interval(-w * 0.5, w * 0.5)).ToNurbsCurve();
                var r = ToolDiameter * 0.5;
                if (r <= tolerance || Width <= 0) return rect;
                return Curve.CreateFilletCornersCurve(rect, Math.Min(r, Math.Min(Length, w) * 0.5 - tolerance), tolerance, context.AngleTolerance) ?? rect;
            }

            // Pocket: the outline just outside the face, cut down to the pocket bottom
            var top = new Plane(face.Origin + face.ZAxis * Added, face.XAxis, face.YAxis);
            result.Add(new Pocket { BeamId = beam.Id, Plane = top, Outline = Outline(top, width), Depth = Depth + Added });

            var bottom = new Plane(face.Origin - face.ZAxis * Depth, face.XAxis, face.YAxis);

            if (CounterboreDiameter > 0 && CounterboreDepth > 0)
                result.Add(new Drilling(beam.Id, new Line(bottom.Origin + face.ZAxis * Added, bottom.Origin - face.ZAxis * CounterboreDepth), CounterboreDiameter));
            if (BoltDiameter > 0)
                result.Add(new Drilling(beam.Id, new Line(bottom.Origin + face.ZAxis * Added, face.Origin - face.ZAxis * (height + Added)), BoltDiameter));

            // Screws at the plate's corners, leaning along the beam away from the middle
            if (ScrewDiameter > 0 && ScrewLength > 0)
            {
                var screwWidth = Width > 0 ? width : faceWidth;
                foreach (var sx in new[] { -1, 1 })
                    foreach (var sy in new[] { -1, 1 })
                    {
                        var head = bottom.PointAt(sx * (Length * 0.5 - ScrewEdgeOffsetX), sy * (screwWidth * 0.5 - ScrewEdgeOffsetY));
                        var dir = -face.ZAxis * Math.Cos(ScrewInclination) + face.XAxis * (sx * Math.Sin(ScrewInclination));
                        var screw = new Line(head, dir, ScrewLength);
                        result.Add(new Drilling(beam.Id, new Line(head - dir * Added, screw.To), ScrewDiameter));
                        result.Hardware.Add(new ScrewItem(screw, ScrewDiameter, beam.Id));
                    }
            }

            // The plate itself, sitting in the pocket
            var thickness = PlateThickness > 0 ? PlateThickness : Depth;
            var plateOutline = Outline(bottom, Width > 0 ? width : faceWidth);
            var plateGeometry = Surface.CreateExtrusion(plateOutline, face.ZAxis * thickness)?.ToBrep()?.CapPlanarHoles(tolerance);
            if (plateGeometry != null && plateGeometry.IsSolid && plateGeometry.SolidOrientation == BrepSolidOrientation.Inward)
                plateGeometry.Flip();
            result.Hardware.Add(new PlateItem
            {
                Name = "Plate",
                Thickness = thickness,
                Plane = bottom,
                Outline = plateOutline,
                Geometry = plateGeometry,
                BeamIds = new List<string> { beam.Id },
            });

            Position = face;
            result.Status = JointStatus.Ok;
        }
    }
}
