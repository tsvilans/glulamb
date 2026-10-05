using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// T joint where the end of one beam is lapped over the side of another. Port of
    /// TJointX (TenonJointX.cs) to the IJoint system: same geometry, output as Lap features.
    /// The part at the end of its beam is the tenon; the part in the middle is the mortise.
    /// </summary>
    [JointType("glulamb.t-lap", Name = "T lap", Arity = 2, Topology = JointTopology.T,
        Description = "End of one beam lapped onto the side of another.")]
    public class TLapJoint : JointBase
    {
        [JointParameter(Description = "Extra length added to cutters so they clear the beam.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Distance from the far side of the mortise beam to the end of the lap. 0 is a through lap.", Unit = "length")]
        public double BlindOffset { get; set; } = 0.0;

        [JointParameter(Description = "Flip which side of the beams the lap is cut from.")]
        public bool FlipDirection { get; set; } = false;

        [JointParameter(Description = "Diameter of a dowel through the middle of the lap. 0 means no dowel.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0;

        public TLapJoint(JointX condition) : base(condition)
        {
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            // The tenon is the part at the end of its beam
            int ti = 0, mi = 1;
            if (JointPartX.IsAtMiddle(m_parts[0].Case) && JointPartX.IsAtEnd(m_parts[1].Case))
            {
                ti = 1; mi = 0;
            }

            var tenonPart = m_parts[ti];
            var mortisePart = m_parts[mi];
            var tenon = beams[ti];
            var mortise = beams[mi];
            var tolerance = context.Tolerance;

            var tenonDirection = tenonPart.Direction;
            var mortiseDirection = mortisePart.Direction;

            var tenonPlane = tenon.GetPlane(tenonPart.Parameter);
            var mortisePlane = mortise.GetPlane(mortisePart.Parameter);

            double tenonWidth = tenon.Width, tenonHeight = tenon.Height;
            double mortiseWidth = mortise.Width, mortiseHeight = mortise.Height;

            var normal = Utility.ClosestAxis(mortisePlane, Vector3d.CrossProduct(tenonDirection, mortiseDirection));
            if (normal * tenonPlane.YAxis < 0.0)
                normal.Reverse();
            if (FlipDirection)
                normal.Reverse();

            var tenonUp = Utility.ClosestAxis(tenonPlane, normal);

            if (Utility.ClosestDimension2D(mortisePlane, tenonDirection) != 0)
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

            var mortiseSideDirection = Utility.ClosestAxis(mortisePlane, tenonDirection);
            var tenonSideDirection = Utility.ClosestAxis(tenonPlane, mortiseDirection);

            var tenonFacePlane = new Plane(
                mortisePlane.Origin - mortiseSideDirection * mortiseWidth * 0.5,
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            var tenonBackPlane = new Plane(
                mortisePlane.Origin + mortiseSideDirection * (mortiseWidth * 0.5 - BlindOffset),
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            if (Utility.ClosestDimension2D(tenonFacePlane, tenonPlane.XAxis) != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            var tenonSidePlanes = new Plane[]{
                new Plane(tenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(tenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            var mortiseSidePlanes = new Plane[]{
                new Plane(tenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
                new Plane(tenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
            };

            var centre = (tenonPlane.Origin + mortisePlane.Origin) * 0.5;

            tenonFacePlane.Origin = centre.ProjectToPlane(tenonFacePlane);
            tenonBackPlane.Origin = centre.ProjectToPlane(tenonBackPlane);
            for (int i = 0; i < 2; ++i)
                tenonSidePlanes[i].Origin = centre.ProjectToPlane(tenonSidePlanes[i]);

            var tenonFacePlaneOffset = new Plane(
                tenonFacePlane.Origin - tenonFacePlane.ZAxis * Added,
                tenonFacePlane.XAxis, tenonFacePlane.YAxis);

            var halfDepth = Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added;
            var topPlane = new Plane(tenonFacePlane.Origin + tenonFacePlane.YAxis * halfDepth, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);
            var middlePlane = new Plane(tenonFacePlane.Origin, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);
            var bottomPlane = new Plane(tenonFacePlane.Origin - tenonFacePlane.YAxis * halfDepth, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);

            Position = new Plane(centre, middlePlane.XAxis, middlePlane.YAxis);

            result.Debug.Add(tenonFacePlane);
            result.Debug.Add(tenonBackPlane);
            result.Debug.Add(topPlane);
            result.Debug.Add(middlePlane);
            result.Debug.Add(bottomPlane);

            var tenonPoints = new Point3d[8];
            var mortisePoints = new Point3d[8];
            bool ok = true;

            for (int i = 0; i < 2; ++i)
            {
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonFacePlane, bottomPlane, out tenonPoints[0 + i * 4]);
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonFacePlane, middlePlane, out tenonPoints[1 + i * 4]);
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonBackPlane, middlePlane, out tenonPoints[2 + i * 4]);
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonBackPlane, topPlane, out tenonPoints[3 + i * 4]);

                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFacePlaneOffset, middlePlane, out mortisePoints[0 + i * 4]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFacePlaneOffset, topPlane, out mortisePoints[1 + i * 4]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonBackPlane, middlePlane, out mortisePoints[2 + i * 4]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonBackPlane, topPlane, out mortisePoints[3 + i * 4]);
            }

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: planes are parallel; the beams may be aligned rather than meeting in a T.");
                return;
            }

            var tenonGeo = new[]
            {
                Brep.CreateFromCornerPoints(tenonPoints[0], tenonPoints[1], tenonPoints[5], tenonPoints[4], tolerance),
                Brep.CreateFromCornerPoints(tenonPoints[1], tenonPoints[2], tenonPoints[6], tenonPoints[5], tolerance),
                Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], tenonPoints[7], tenonPoints[6], tolerance),
            };

            var mortiseGeo = new[]
            {
                Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[2], mortisePoints[6], mortisePoints[4], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[1], mortisePoints[3], mortisePoints[2], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[2], mortisePoints[3], mortisePoints[7], mortisePoints[6], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[4], mortisePoints[5], mortisePoints[7], mortisePoints[6], tolerance),
            };

            var tenonJoined = tenonGeo.Any(x => x == null) ? null : Brep.JoinBreps(tenonGeo, tolerance);
            var mortiseJoined = mortiseGeo.Any(x => x == null) ? null : Brep.JoinBreps(mortiseGeo, tolerance);

            if (tenonJoined != null && tenonJoined.Length > 0)
            {
                result.Add(new Lap
                {
                    BeamId = tenon.Id,
                    Plane = middlePlane,
                    Cutters = tenonJoined.ToList()
                });
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create tenon cutter.");

            if (mortiseJoined != null && mortiseJoined.Length > 0)
            {
                result.Add(new Lap
                {
                    BeamId = mortise.Id,
                    Plane = middlePlane,
                    Cutters = mortiseJoined.ToList()
                });
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create mortise cutter.");

            int created = result.Features.Count;
            result.Status = created == 2 ? JointStatus.Ok : created == 1 ? JointStatus.Partial : JointStatus.Failed;

            // Optional dowel through the middle of the lap, across both beams
            if (DowelDiameter > 0 && result.Success)
            {
                var lapCentre = (tenonFacePlane.Origin + tenonBackPlane.Origin) * 0.5;
                var axis = new Line(lapCentre - normal * halfDepth, lapCentre + normal * halfDepth);

                result.Add(new Drilling(tenon.Id, axis, DowelDiameter));
                result.Add(new Drilling(mortise.Id, axis, DowelDiameter));
            }
        }
    }
}
