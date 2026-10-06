using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Simple corner half-lap between two beam ends. Port of CornerJointX to the IJoint
    /// system: same geometry, output as Lap features. This is the default for corner and
    /// acute conditions.
    /// </summary>
    [JointType("glulamb.corner-lap", Name = "Corner lap", Arity = 2,
        Description = "Two beam ends meeting at an angle, half-lapped over each other.")]
    public class CornerLapJoint : JointBase
    {
        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Inset of the lap sides from the beam sides.", Unit = "length")]
        public double Inset { get; set; } = 0.0;

        [JointParameter(Description = "Material left at the end of the second beam's lap. 0 is a through lap.", Unit = "length")]
        public double BlindOffset { get; set; } = 0.0;

        public CornerLapJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Handles two beam ends meeting at an angle (corner or acute topology).
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 2) return 0;
            if (!JointPartX.IsAtEnd(condition.Parts[0].Case) || !JointPartX.IsAtEnd(condition.Parts[1].Case)) return 0;

            var topology = JointRegistry.Classify(condition, JointX.PerpendicularThreshold);
            return topology == JointTopology.Corner || topology == JointTopology.Acute ? 1.0 : 0.0;
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

            var beam0SideDirection = Utility.ClosestAxis(beam0Plane, beam1Direction);
            var beam1SideDirection = Utility.ClosestAxis(beam1Plane, beam0Direction);

            double beam0Width, beam0Height;
            if (Math.Abs(beam1Direction * beam0Plane.XAxis) > Math.Abs(beam1Direction * beam0Plane.YAxis))
            {
                beam0Width = beam0.Width;
                beam0Height = beam0.Height;
            }
            else
            {
                beam0Width = beam0.Height;
                beam0Height = beam0.Width;
            }

            double beam1Width, beam1Height;
            if (Math.Abs(beam0Direction * beam1Plane.XAxis) > Math.Abs(beam0Direction * beam1Plane.YAxis))
            {
                beam1Width = beam1.Width;
                beam1Height = beam1.Height;
            }
            else
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            var beam0Side0 = new Plane(beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5 - Inset), beam0SideDirection);
            var beam0Side1 = new Plane(beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5 - Inset), beam0SideDirection);
            var beam0Side0Added = new Plane(beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5 + Added + Inset), beam0SideDirection);
            var beam0Side1Added = new Plane(beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5 + Added + Inset), beam0SideDirection);

            var beam1Side0 = new Plane(beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5 - Inset - BlindOffset), beam1SideDirection);
            var beam1Side1 = new Plane(beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5), beam1SideDirection);
            var beam1Side0Added = new Plane(beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5 + Added + Inset), beam1SideDirection);
            var beam1Side1Added = new Plane(beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5 + Added + Inset), beam1SideDirection);

            var normal = Vector3d.CrossProduct(beam0SideDirection, beam1SideDirection);
            if (!normal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are parallel.");
                return;
            }

            // Beam 0 is on top along -normal. Orient the normal with beam 0's up axis, then put
            // whichever beam is higher on top (beam 0 when they are level).
            if (normal * NearestSectionAxis(beam0Plane, normal) < 0)
                normal.Reverse();
            if (TopPart(beam0Plane.Origin, beam1Plane.Origin, normal, context.Tolerance) == 0)
                normal.Reverse();

            var lapOrigin = Interpolation.Lerp(beam0Plane.Origin, beam1Plane.Origin, beam0Height / (beam1Height + beam0Height));
            var lapPlane = new Plane(lapOrigin, beam0SideDirection, beam1SideDirection);
            Position = lapPlane;
            result.Debug.Add(lapPlane);

            bool ok = true;
            var points = new Point3d[5];

            // Beam 0
            ok &= RX.PlanePlanePlane(lapPlane, beam0Side0Added, beam1Side0, out points[0]);
            ok &= RX.PlanePlanePlane(lapPlane, beam1Side0, beam0Side1Added, out points[1]);
            ok &= RX.PlanePlanePlane(lapPlane, beam0Side1Added, beam1Side1, out points[2]);
            ok &= RX.PlanePlanePlane(lapPlane, beam1Side1, beam0Side0Added, out points[3]);

            var up0 = normal * (beam0Height + Added);
            var beam0Geo = new[]
            {
                Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], tolerance),
                Brep.CreateFromCornerPoints(points[0], points[1], points[1] - up0, points[0] - up0, tolerance),
                Brep.CreateFromCornerPoints(points[2] + up0, points[3] + up0, points[3], points[2], tolerance),
            };

            // Beam 1
            ok &= RX.PlanePlanePlane(lapPlane, beam0Side0, beam1Side0, out points[0]);
            ok &= RX.PlanePlanePlane(lapPlane, beam1Side0, beam0Side1, out points[1]);
            ok &= RX.PlanePlanePlane(lapPlane, beam0Side1, beam1Side1Added, out points[2]);
            ok &= RX.PlanePlanePlane(lapPlane, beam1Side1Added, beam0Side0, out points[3]);
            ok &= RX.PlanePlanePlane(lapPlane, beam1Side0Added, beam0Side0, out points[4]);

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: lap planes did not intersect.");
                return;
            }

            // Both beams have to cover the whole overlap of the two cross-sections
            var overlap = new List<Point3d>();
            foreach (var s0 in new[] { 1, -1 })
                foreach (var s1 in new[] { 1, -1 })
                {
                    var side0 = new Plane(beam0Plane.Origin + beam0SideDirection * beam0Width * 0.5 * s0, beam0SideDirection);
                    var side1 = new Plane(beam1Plane.Origin + beam1SideDirection * beam1Width * 0.5 * s1, beam1SideDirection);
                    if (RX.PlanePlanePlane(lapPlane, side0, side1, out Point3d corner))
                        overlap.Add(corner);
                }

            ExtendToReach(result, beam0, m_parts[0], overlap);
            ExtendToReach(result, beam1, m_parts[1], overlap);

            var up1 = normal * (beam1Height + Added);
            var boundary = new Polyline { points[0], points[3], points[3] + up1, points[4] + up1, points[4] - up1, points[0] - up1, points[0] };

            var beam1Geo = new[]
            {
                Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], tolerance),
                Brep.CreateFromCornerPoints(points[1], points[2], points[2] - up1, points[1] - up1, tolerance),
                Brep.CreateFromCornerPoints(points[0], points[1], points[1] - up1, points[0] - up1, tolerance),
                Brep.CreatePlanarBreps(boundary.ToNurbsCurve(), tolerance)?.FirstOrDefault(),
            };

            AddLap(result, beam0, beam0Geo, lapPlane, tolerance);
            AddLap(result, beam1, beam1Geo, lapPlane, tolerance);

            int created = result.Features.Count;
            result.Status = created == 2 ? JointStatus.Ok : created == 1 ? JointStatus.Partial : JointStatus.Failed;
        }

        private void AddLap(JointResult result, Beam beam, Brep[] faces, Plane plane, double tolerance)
        {
            var joined = faces.Any(x => x == null) ? null : Brep.JoinBreps(faces, tolerance);
            if (joined == null || joined.Length < 1)
            {
                result.Messages.Add($"{GetType().Name}: failed to create cutter for beam {beam.Id}.");
                return;
            }

            result.Add(new Lap { BeamId = beam.Id, Plane = plane, Cutters = joined.ToList() });
        }
    }
}
