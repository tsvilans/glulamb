using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Lapped splice between two roughly aligned beam ends, with two dowels through the lap.
    /// Port of LappedSpliceJointX to the IJoint system: same geometry, output as a Lap feature
    /// and Drilling features on each beam.
    /// </summary>
    [JointType("glulamb.splice-lap", Name = "Lapped splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two aligned beam ends lapped over each other and dowelled.")]
    public class LappedSpliceJoint : JointBase
    {
        [JointParameter(Description = "Extra width added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Extra height added to cutters above and below the beams.", Unit = "length")]
        public double AddedUp { get; set; } = 100.0;

        [JointParameter(Description = "Length of the lap.", Unit = "length")]
        public double SpliceLength { get; set; } = 200;

        [JointParameter(Description = "Height of the lap step as a ratio of the beam height.")]
        public double SpliceRatio { get; set; } = 0.25;

        [JointParameter(Description = "Lap through the width of the beams instead of the height.")]
        public bool SideSplice { get; set; } = false;

        [JointParameter(Description = "Distance of the dowels from the ends of the lap.", Unit = "length")]
        public double DowelEndOffset { get; set; } = 60;

        [JointParameter(Description = "Dowel diameter. 0 means no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16;

        public LappedSpliceJoint(JointX condition) : base(condition)
        {
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam0 = beams[0];
            var beam1 = beams[1];
            var tolerance = context.Tolerance;

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

            var dim = Math.Abs(beam0Plane.Project(beam1Plane.XAxis) * xAxis) > 0.5 ? 0 : 1;
            if (dim > 0)
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            var end0Plane = new Plane(beam0Plane.Origin - beam0Direction * (SpliceLength * 0.5), xAxis, yAxis);

            var slaveX = Utility.ClosestAxis(beam1Plane, end0Plane.XAxis);
            var slaveY = Utility.ClosestAxis(beam1Plane, end0Plane.YAxis);

            var end1Plane = new Plane(beam1Plane.Origin - beam1Direction * (SpliceLength * 0.5), slaveX, slaveY);

            var splicePlane = Interpolation.InterpolatePlanes2(end0Plane, end1Plane, 0.5);

            end0Plane = new Plane(end0Plane.Origin, splicePlane.XAxis, end0Plane.YAxis);
            end1Plane = new Plane(end1Plane.Origin, splicePlane.XAxis, end1Plane.YAxis);

            Position = splicePlane;

            result.Debug.Add(end0Plane);
            result.Debug.Add(end1Plane);
            result.Debug.Add(splicePlane);

            double width = Math.Max(beam0Width, beam1Width);
            double height = Math.Max(beam0Height, beam1Height);
            double spliceHeight = Math.Min(beam0Height, beam1Height);

            var topProfile = new Polyline()
            {
                end0Plane.PointAt(-width * 0.5 - Added, height * 0.5 + AddedUp, 0),
                end0Plane.PointAt(-width * 0.5 - Added, spliceHeight * SpliceRatio, 0),
                end1Plane.PointAt(-width * 0.5 - Added, -spliceHeight * SpliceRatio, 0),
                end1Plane.PointAt(-width * 0.5 - Added, -height * 0.5 - AddedUp, 0),
            };

            var bottomProfile = new Polyline()
            {
                end0Plane.PointAt(width * 0.5 + Added, height * 0.5 + AddedUp, 0),
                end0Plane.PointAt(width * 0.5 + Added, spliceHeight * SpliceRatio, 0),
                end1Plane.PointAt(width * 0.5 + Added, -spliceHeight * SpliceRatio, 0),
                end1Plane.PointAt(width * 0.5 + Added, -height * 0.5 - AddedUp, 0),
            };

            var lapGeo = Brep.CreateFromLoft(new Curve[] { topProfile.ToNurbsCurve(), bottomProfile.ToNurbsCurve() },
                Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            if (lapGeo == null || lapGeo.Length < 1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create lap surface.");
                return;
            }

            lapGeo[0].Faces.SplitKinkyFaces();

            for (int i = 0; i < 2; ++i)
            {
                result.Add(new Lap
                {
                    BeamId = beams[i].Id,
                    Plane = splicePlane,
                    Length = SpliceLength,
                    Cutters = lapGeo.Select(x => x.DuplicateBrep()).ToList()
                });
            }

            // Dowels, through both beams
            if (DowelDiameter > 0)
            {
                var dowelSpan = end1Plane.Origin - end0Plane.Origin;
                var dowelSpacing = dowelSpan.Length - DowelEndOffset * 2;
                dowelSpan.Unitize();

                for (int i = 0; i < 2; ++i)
                {
                    var dowelOrigin = end0Plane.Origin + dowelSpan * (DowelEndOffset + i * dowelSpacing)
                        - splicePlane.YAxis * (beam0Height + Added);
                    var axis = new Line(dowelOrigin, splicePlane.YAxis, beam0Height + beam1Height + Added * 2);

                    for (int j = 0; j < 2; ++j)
                        result.Add(new Drilling(beams[j].Id, axis, DowelDiameter));
                }
            }

            result.Status = JointStatus.Ok;
        }
    }
}
