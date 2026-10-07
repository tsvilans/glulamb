using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Birdsmouth splice: two aligned beam ends meeting on a V, one end cut to a point that sits
    /// in a V-notch in the other, with dowels along the beams across the joint and optional
    /// inclined screws from one beam into the other. Port of SpliceJoint_BirdsMouth. Both beams
    /// are cut by the same open V surface (a JackRafterCut-like Lap feature), the V running
    /// across the deeper side of the section unless rotated. Beam 0 gets the notch; Flip gives
    /// it the point.
    /// </summary>
    [JointType("glulamb.splice-birdsmouth", Name = "Birdsmouth splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two aligned beam ends meeting on a V, dowelled and optionally screwed.")]
    public class BirdsMouthSpliceJoint : JointBase
    {
        [JointParameter(Description = "Angle of the V faces to the cross-section.", Unit = "radians")]
        public double Angle { get; set; } = Rhino.RhinoMath.ToRadians(30.0);

        [JointParameter(Description = "Run the V across the shallower side of the section instead of the deeper one.")]
        public bool Rotate { get; set; } = false;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Dowel diameter. 0 = no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 12.0;

        [JointParameter(Description = "Dowel length, centred on the joint.", Unit = "length")]
        public double DowelLength { get; set; } = 150;

        [JointParameter(Description = "Extra depth drilled beyond each end of a dowel.", Unit = "length")]
        public double DowelDrillExtra { get; set; } = 5;

        [JointParameter(Description = "Screw diameter. 0 = no screws.", Unit = "length")]
        public double ScrewDiameter { get; set; } = 6;

        [JointParameter(Description = "Screw length.", Unit = "length")]
        public double ScrewLength { get; set; } = 180;

        [JointParameter(Description = "Angle of the screws to the beam face normal, leaning across the joint.", Unit = "radians")]
        public double ScrewAngle { get; set; } = Rhino.RhinoMath.ToRadians(45);

        [JointParameter(Description = "Distance of the screw heads from the joint, on beam 0.", Unit = "length")]
        public double ScrewDistance { get; set; } = 50;

        [JointParameter(Description = "Distance of the screws from the middle of the face.", Unit = "length")]
        public double ScrewSpacing { get; set; } = 30;

        [JointParameter(Description = "Countersink diameter. 0 = no countersink.", Unit = "length")]
        public double CountersinkDiameter { get; set; } = 14;

        [JointParameter(Description = "Countersink depth.", Unit = "length")]
        public double CountersinkDepth { get; set; } = 10;

        [JointParameter(Description = "Largest angle between the beams that the splice will handle.", Unit = "radians")]
        public double MaximumAngle { get; set; } = Rhino.RhinoMath.ToRadians(30.0);

        public BirdsMouthSpliceJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam0 = beams[0];
            var beam1 = beams[1];
            var tolerance = context.Tolerance;

            var dir0 = m_parts[0].Direction;
            var dir1 = m_parts[1].Direction;
            var plane0 = beam0.GetPlane(m_parts[0].Parameter);
            var plane1 = beam1.GetPlane(m_parts[1].Parameter);

            var divergence = Vector3d.VectorAngle(dir0, -dir1);
            if (divergence == Rhino.RhinoMath.UnsetValue) divergence = 0;
            if (divergence > MaximumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams diverge by {Rhino.RhinoMath.ToDegrees(divergence):0.0}°, " +
                    $"more than MaximumAngle ({Rhino.RhinoMath.ToDegrees(MaximumAngle):0.0}°).");
                return;
            }

            // Frame: along from beam 0 to beam 1; up across the V (beam 0's deeper section axis,
            // or the shallower one when rotated)
            var along = dir0 - dir1;
            along.Unitize();
            bool deepIsY = beam0.Height >= beam0.Width;
            var up0 = deepIsY != Rotate ? plane0.YAxis : plane0.XAxis;
            var frame0 = AlignSection(beam0, plane0, along, up0, out double w0, out double h0);
            var frame1 = AlignSection(beam1, plane1, along, frame0.YAxis, out double w1, out double h1);

            var up = frame0.YAxis + frame1.YAxis;
            up -= along * (up * along);
            up.Unitize();
            var across = Vector3d.CrossProduct(up, along);
            var mid = (plane0.Origin + plane1.Origin) * 0.5;

            double width = Math.Max(w0, w1), height = Math.Max(h0, h1);
            var drift = height * Math.Tan(divergence);
            var hw = width * 0.5 / Math.Cos(divergence) + drift + Added;
            var hh = height * 0.5 / Math.Cos(divergence) + drift + Added;

            // The V: z = s * tan(Angle) * (|y| - height / 4), so the point and the notch balance
            // about the middle. s = 1 puts the notch in beam 0.
            double s = Flip ? -1 : 1;
            var tan = Math.Tan(Angle);
            Point3d At(double x, double y) => mid + across * x + up * y + along * (s * tan * (Math.Abs(y) - height * 0.25));

            var faces = new[]
            {
                Brep.CreateFromCornerPoints(At(-hw, 0), At(hw, 0), At(hw, hh), At(-hw, hh), tolerance),
                Brep.CreateFromCornerPoints(At(hw, 0), At(-hw, 0), At(-hw, -hh), At(hw, -hh), tolerance),
            };
            var surface = faces.Any(x => x == null) ? null : Brep.JoinBreps(faces, tolerance)?.FirstOrDefault();
            if (surface == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the V surface.");
                return;
            }

            Position = new Plane(mid, across, up);
            foreach (var beam in beams)
            {
                var lap = new Lap { BeamId = beam.Id, Plane = new Plane(mid, across, up), Cutters = new List<Brep> { surface.DuplicateBrep() } };
                lap.Data.Set("Name", "BirdsMouth");
                result.Add(lap);
            }

            // Each beam has to reach the far end of the V on its side
            var tips = new[] { -1.0, 1.0 }.SelectMany(sx => new[] { At(sx * width * 0.5, height * 0.5), At(sx * width * 0.5, -height * 0.5) });
            var middle = new[] { At(-width * 0.5, 0), At(width * 0.5, 0) };
            ExtendToReach(result, beam0, m_parts[0], s > 0 ? tips : middle);
            ExtendToReach(result, beam1, m_parts[1], s > 0 ? middle : tips);

            // Dowels along the beams, a quarter of the way in from the faces across the V,
            // where the V crosses the middle of the joint
            if (DowelDiameter > 0 && DowelLength > 0)
            {
                foreach (var y in new[] { -height * 0.25, height * 0.25 })
                {
                    var centre = mid + up * y;
                    var dowel = new Line(centre - along * DowelLength * 0.5, along, DowelLength);
                    var hole = new Line(dowel.From - along * DowelDrillExtra, dowel.To + along * DowelDrillExtra);
                    result.Add(new Drilling(beam0.Id, hole, DowelDiameter));
                    result.Add(new Drilling(beam1.Id, hole, DowelDiameter));
                    result.Hardware.Add(new DowelItem(dowel, DowelDiameter, beam0.Id, beam1.Id));
                }
            }

            // Screws from the faces across the V of beam 0, leaning into beam 1. The pairs on
            // opposite faces are shifted sideways so they pass each other.
            if (ScrewDiameter > 0 && ScrewLength > 0)
            {
                var shift = (ScrewDiameter + 2) * 0.5;
                foreach (var j in new[] { -1, 1 })
                    foreach (var i in new[] { -1, 1 })
                    {
                        var head = frame0.Origin - along * ScrewDistance + up * (j * h0 * 0.5) + across * (i * ScrewSpacing + j * shift);
                        var dir = -up * j * Math.Cos(ScrewAngle) + along * Math.Sin(ScrewAngle);
                        var screw = new Line(head, dir, ScrewLength);
                        result.Add(new Drilling(beam0.Id, new Line(head - dir * Added, screw.To), ScrewDiameter));
                        result.Add(new Drilling(beam1.Id, new Line(head - dir * Added, screw.To), ScrewDiameter));
                        if (CountersinkDiameter > ScrewDiameter && CountersinkDepth > 0)
                            result.Add(new Drilling(beam0.Id, new Line(head - dir * Added, head + dir * CountersinkDepth), CountersinkDiameter));
                        result.Hardware.Add(new ScrewItem(screw, ScrewDiameter, beam0.Id, beam1.Id));
                    }
            }

            result.Status = JointStatus.Ok;
        }
    }
}
