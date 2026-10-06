using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Tenon (bridle) splice between two roughly aligned beam ends: the middle of one beam's end
    /// runs as a tongue into a slot in the other, through the full height, with dowels across
    /// the width. Port of TenonSpliceJointX to the IJoint system: same splice surface, output as
    /// a Lap feature on each beam and Drilling features for the dowels. Beam 0 has the tongue;
    /// Flip gives it to beam 1.
    /// </summary>
    [JointType("glulamb.splice-tenon", Name = "Tenon splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two aligned beam ends joined with a through tongue and slot, dowelled across.")]
    public class TenonSpliceJoint : JointBase
    {
        [JointParameter(Description = "Extra width added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Extra height added to cutters above and below the beams.", Unit = "length")]
        public double AddedUp { get; set; } = 100.0;

        [JointParameter(Description = "Length of the tongue.", Unit = "length")]
        public double SpliceLength { get; set; } = 200;

        [JointParameter(Description = "Width of the tongue as a ratio of the narrower beam's width.")]
        public double TenonRatio { get; set; } = 1.0 / 3.0;

        [JointParameter(Description = "Run the tongue through the width of the beams instead of the height.")]
        public bool SideSplice { get; set; } = false;

        [JointParameter(Description = "Distance of the dowels from the ends of the tongue.", Unit = "length")]
        public double DowelEndOffset { get; set; } = 60;

        [JointParameter(Description = "Dowel diameter. 0 means no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16;

        [JointParameter(Description = "Largest angle between the beams that the splice will handle.", Unit = "radians")]
        public double MaximumAngle { get; set; } = Rhino.RhinoMath.ToRadians(45.0);

        public TenonSpliceJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam0 = beams[0];
            var beam1 = beams[1];

            var beam0Direction = m_parts[0].Direction;
            var beam1Direction = m_parts[1].Direction;

            var beam0Plane = beam0.GetPlane(m_parts[0].Parameter);
            var beam1Plane = beam1.GetPlane(m_parts[1].Parameter);

            double beam0Width = beam0.Width, beam0Height = beam0.Height;
            double beam1Width = beam1.Width, beam1Height = beam1.Height;

            Vector3d xAxis = beam0Plane.XAxis, yAxis = beam0Plane.YAxis;
            if (SideSplice)
            {
                xAxis = beam0Plane.YAxis;
                yAxis = beam0Plane.XAxis;
                beam0Width = beam0.Height;
                beam0Height = beam0.Width;
            }

            if (Utility.ClosestDimension2D(beam0Plane, beam1Plane.XAxis) > 0)
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            var divergence = Vector3d.VectorAngle(beam0Direction, -beam1Direction);
            if (divergence == Rhino.RhinoMath.UnsetValue) divergence = 0;
            if (divergence > MaximumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams diverge by {Rhino.RhinoMath.ToDegrees(divergence):0.0}°, " +
                    $"more than MaximumAngle ({Rhino.RhinoMath.ToDegrees(MaximumAngle):0.0}°).");
                return;
            }

            var end0Plane = new Plane(beam0Plane.Origin - beam0Direction * (SpliceLength * 0.5), xAxis, yAxis);

            var slaveX = Utility.ClosestAxis(beam1Plane, end0Plane.XAxis);
            var slaveY = Utility.ClosestAxis(beam1Plane, end0Plane.YAxis);
            var end1Plane = new Plane(beam1Plane.Origin - beam1Direction * (SpliceLength * 0.5), slaveX, slaveY);

            var splicePlane = Interpolation.InterpolatePlanes2(end0Plane, end1Plane, 0.5);

            end0Plane = new Plane(end0Plane.Origin, splicePlane.XAxis, end0Plane.YAxis);
            end1Plane = new Plane(end1Plane.Origin, splicePlane.XAxis, end1Plane.YAxis);

            Position = splicePlane;
            result.Debug.Add(splicePlane);

            // Widen the surface when the beams aren't aligned, as in LappedSpliceJoint
            var drift = SpliceLength * Math.Tan(divergence);
            double width = Math.Max(beam0Width, beam1Width) / Math.Cos(divergence) + drift * 2;
            double height = Math.Max(beam0Height, beam1Height) / Math.Cos(divergence) + drift * 2;
            double halfTenon = Math.Min(beam0Width, beam1Width) * TenonRatio * 0.5;

            var points = new[]
            {
                splicePlane.PointAt(-width * 0.5 - Added, height * 0.5 + AddedUp, 0),
                splicePlane.PointAt(-halfTenon,           height * 0.5 + AddedUp, 0),
                splicePlane.PointAt(halfTenon,            height * 0.5 + AddedUp, 0),
                splicePlane.PointAt(width * 0.5 + Added,  height * 0.5 + AddedUp, 0),
                splicePlane.PointAt(-width * 0.5 - Added, -height * 0.5 - AddedUp, 0),
                splicePlane.PointAt(-halfTenon,           -height * 0.5 - AddedUp, 0),
                splicePlane.PointAt(halfTenon,            -height * 0.5 - AddedUp, 0),
                splicePlane.PointAt(width * 0.5 + Added,  -height * 0.5 - AddedUp, 0),
            };

            // The tongue reaches to the end plane of the beam that doesn't have it
            var outer = Transform.ProjectAlong(Flip ? end1Plane : end0Plane, splicePlane.ZAxis);
            var inner = Transform.ProjectAlong(Flip ? end0Plane : end1Plane, splicePlane.ZAxis);

            Polyline Profile(int a, int b, int c, int d) => new Polyline
            {
                points[a].Transformed(outer),
                points[b].Transformed(outer),
                points[b].Transformed(inner),
                points[c].Transformed(inner),
                points[c].Transformed(outer),
                points[d].Transformed(outer),
            };

            var surface = Brep.CreateFromLoft(new Curve[] { Profile(0, 1, 2, 3).ToNurbsCurve(), Profile(4, 5, 6, 7).ToNurbsCurve() },
                Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            if (surface == null || surface.Length < 1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the splice surface.");
                return;
            }

            surface[0].Faces.SplitKinkyFaces();

            for (int i = 0; i < 2; ++i)
            {
                result.Add(new Lap
                {
                    BeamId = beams[i].Id,
                    Plane = splicePlane,
                    Length = SpliceLength,
                    Width = halfTenon * 2,
                    Cutters = surface.Select(x => x.DuplicateBrep()).ToList()
                });
            }

            // Each beam has to reach the far end of the splice, across its full section
            Point3d[] SectionCorners(Plane p, double w, double h) => new[]
            {
                p.PointAt(-w * 0.5, -h * 0.5), p.PointAt(w * 0.5, -h * 0.5),
                p.PointAt(w * 0.5, h * 0.5), p.PointAt(-w * 0.5, h * 0.5),
            };

            ExtendToReach(result, beam0, m_parts[0], SectionCorners(end1Plane, beam0Width, beam0Height));
            ExtendToReach(result, beam1, m_parts[1], SectionCorners(end0Plane, beam1Width, beam1Height));

            // Dowels across the width, through the tongue and both cheeks. (The prototype placed
            // them below the beams, along the wrong axis.)
            if (DowelDiameter > 0)
            {
                var span = end1Plane.Origin - end0Plane.Origin;
                var spacing = span.Length - DowelEndOffset * 2;
                span.Unitize();

                var halfLength = width * 0.5 + Added;
                for (int i = 0; i < 2; ++i)
                {
                    var centre = end0Plane.Origin + span * (DowelEndOffset + i * spacing);
                    var axis = new Line(centre - splicePlane.XAxis * halfLength, centre + splicePlane.XAxis * halfLength);

                    foreach (var beam in beams)
                        result.Add(new Drilling(beam.Id, axis, DowelDiameter));
                }
            }

            result.Status = JointStatus.Ok;
        }
    }
}
