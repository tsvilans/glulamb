using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Cross halving lap between two beams crossing in their middles. Port of CrossJointX to
    /// the IJoint system: same geometry, output as Lap features. The lap outline follows the
    /// beam sides, so crossings that aren't perpendicular are handled too. Parts[0] is the
    /// under beam, Parts[1] the over beam.
    /// </summary>
    [JointType("glulamb.cross-lap", Name = "Cross lap", Arity = 2, Topology = JointTopology.Cross,
        Description = "Two beams crossing, each notched by half.")]
    public class CrossLapJoint : JointBase
    {
        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Inset of the lap sides from the beam sides. Positive values make a tighter lap.", Unit = "length")]
        public double Inset { get; set; } = 0.0;

        [JointParameter(Description = "Depth of the notch in the under beam, from its top face. The over beam takes the rest. 0 = halfway between the beams.", Unit = "length")]
        public double LapDepth { get; set; } = 0;

        /// <summary>
        /// Minimum angle between the beams, below which they are treated as running alongside
        /// each other rather than crossing.
        /// </summary>
        [JointParameter(Description = "Minimum crossing angle.", Unit = "radians")]
        public double MinimumAngle { get; set; } = Rhino.RhinoMath.ToRadians(5.0);

        public CrossLapJoint(JointX condition) : base(condition)
        {
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var tolerance = context.Tolerance;

            // Up is the crossing normal, oriented with the first beam's up axis. The higher beam
            // goes on top (the first one when they are level); Flip inverts that.
            var plane0 = beams[0].GetPlane(m_parts[0].Parameter);
            var up = Vector3d.CrossProduct(
                beams[0].Centreline.TangentAt(m_parts[0].Parameter),
                beams[1].Centreline.TangentAt(m_parts[1].Parameter));
            up.Unitize();
            if (up * NearestSectionAxis(plane0, up) < 0) up.Reverse();

            int oi = TopPart(plane0.Origin, beams[1].GetPlane(m_parts[1].Parameter).Origin, up, tolerance);
            int ui = 1 - oi;

            var under = beams[ui];
            var over = beams[oi];

            var underDirection = under.Centreline.TangentAt(m_parts[ui].Parameter);
            var overDirection = over.Centreline.TangentAt(m_parts[oi].Parameter);

            var angle = Vector3d.VectorAngle(underDirection, overDirection);
            if (angle < MinimumAngle || Math.PI - angle < MinimumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are nearly parallel ({Rhino.RhinoMath.ToDegrees(angle):0.0}°).");
                return;
            }

            var underPlane = under.GetPlane(m_parts[ui].Parameter);
            var overPlane = over.GetPlane(m_parts[oi].Parameter);

            var underSideDirection = Utility.ClosestAxis(underPlane, overDirection);
            var overSideDirection = Utility.ClosestAxis(overPlane, underDirection);

            double underWidth, underHeight;
            if (Math.Abs(overDirection * underPlane.XAxis) > Math.Abs(overDirection * underPlane.YAxis))
            {
                underWidth = under.Width;
                underHeight = under.Height;
            }
            else
            {
                underWidth = under.Height;
                underHeight = under.Width;
            }

            double overWidth, overHeight;
            if (Math.Abs(underDirection * overPlane.XAxis) > Math.Abs(underDirection * overPlane.YAxis))
            {
                overWidth = over.Width;
                overHeight = over.Height;
            }
            else
            {
                overWidth = over.Height;
                overHeight = over.Width;
            }

            var underSide0 = new Plane(underPlane.Origin + underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);
            var underSide1 = new Plane(underPlane.Origin - underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);
            var underSide0Added = new Plane(underPlane.Origin + underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);
            var underSide1Added = new Plane(underPlane.Origin - underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);

            var overSide0 = new Plane(overPlane.Origin + overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);
            var overSide1 = new Plane(overPlane.Origin - overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);
            var overSide0Added = new Plane(overPlane.Origin + overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);
            var overSide1Added = new Plane(overPlane.Origin - overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);

            var normal = Vector3d.CrossProduct(underSideDirection, overSideDirection);
            normal.Unitize();

            // The under beam is notched on the normal side, so the normal points towards the over
            // beam: up, unless Flip has put the over beam below the under one
            var towardsOver = up;
            if ((overPlane.Origin - underPlane.Origin) * up < -tolerance)
                towardsOver = -up;
            if (normal * towardsOver < 0)
                normal.Reverse();

            var lapOrigin = Interpolation.Lerp(underPlane.Origin, overPlane.Origin, underHeight / (overHeight + underHeight));

            // A set lap depth: the notch in the under beam, from its top face; the over beam takes the rest
            if (LapDepth > 0)
            {
                var underFace = underPlane.Origin + normal * underHeight * 0.5;
                lapOrigin += normal * ((underFace - lapOrigin) * normal - LapDepth);
                if (LapDepth >= underHeight)
                    result.Messages.Add($"{GetType().Name}: LapDepth ({LapDepth}) cuts through the under beam ({underHeight}).");
            }
            var lapPlane = new Plane(lapOrigin, underSideDirection, overSideDirection);
            Position = lapPlane;

            result.Debug.Add(lapPlane);

            bool ok = true;
            var points = new Point3d[4];

            // Under geometry
            ok &= RX.PlanePlanePlane(lapPlane, underSide0Added, overSide0, out points[0]);
            ok &= RX.PlanePlanePlane(lapPlane, overSide0, underSide1Added, out points[1]);
            ok &= RX.PlanePlanePlane(lapPlane, underSide1Added, overSide1, out points[2]);
            ok &= RX.PlanePlanePlane(lapPlane, overSide1, underSide0Added, out points[3]);

            var underBase = points.ToArray();
            var underTop = points.Select(x => x + normal * (underHeight + Added)).ToArray();

            // Over geometry
            ok &= RX.PlanePlanePlane(lapPlane, underSide0, overSide0Added, out points[0]);
            ok &= RX.PlanePlanePlane(lapPlane, overSide0Added, underSide1, out points[1]);
            ok &= RX.PlanePlanePlane(lapPlane, underSide1, overSide1Added, out points[2]);
            ok &= RX.PlanePlanePlane(lapPlane, overSide1Added, underSide0, out points[3]);

            var overBase = points.ToArray();
            var overBottom = points.Select(x => x - normal * (overHeight + Added)).ToArray();

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: lap planes did not intersect.");
                return;
            }

            var underGeo = new[]
            {
                Brep.CreateFromCornerPoints(underBase[0], underBase[1], underBase[2], underBase[3], tolerance),
                Brep.CreateFromCornerPoints(underBase[0], underBase[1], underTop[1], underTop[0], tolerance),
                Brep.CreateFromCornerPoints(underTop[2], underTop[3], underBase[3], underBase[2], tolerance),
            };

            var overGeo = new[]
            {
                Brep.CreateFromCornerPoints(overBase[0], overBase[1], overBase[2], overBase[3], tolerance),
                Brep.CreateFromCornerPoints(overBase[1], overBase[2], overBottom[2], overBottom[1], tolerance),
                Brep.CreateFromCornerPoints(overBase[3], overBase[0], overBottom[0], overBottom[3], tolerance),
            };

            AddLap(result, under, underGeo, lapPlane, tolerance);
            AddLap(result, over, overGeo, new Plane(lapOrigin, overSideDirection, underSideDirection), tolerance);

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

            result.Add(new Lap
            {
                BeamId = beam.Id,
                Plane = plane,
                Cutters = joined.ToList()
            });
        }
    }
}
