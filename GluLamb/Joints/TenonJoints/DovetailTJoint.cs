using System;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// T joint where the end of one beam is a half-lapped dovetail let into the side of another,
    /// with an optional dowel along the tenon. Port of DovetailTenonJointX to the IJoint system:
    /// same geometry, output as Lap and Drilling features. The part at the end of its beam is the
    /// tenon; the part in the middle is the mortise.
    /// </summary>
    [JointType("glulamb.t-dovetail", Name = "T dovetail", Arity = 2, Topology = JointTopology.T,
        Description = "End of one beam dovetailed into the side of another.")]
    public class DovetailTJoint : JointBase
    {
        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Inset of the dovetail neck from the tenon sides.", Unit = "length")]
        public double NeckOffset { get; set; } = 20;

        [JointParameter(Description = "Depth of the dovetail into the mortise beam.", Unit = "length")]
        public double Depth { get; set; } = 10;

        [JointParameter(Description = "Dovetail angle.", Unit = "radians")]
        public double Angle { get; set; } = RhinoMath.ToRadians(30);

        [JointParameter(Description = "Flip which side of the beams the dovetail is cut from.")]
        public bool FlipDirection { get; set; } = false;

        [JointParameter(Description = "Diameter of the dowel along the tenon. 0 means no dowel.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16;

        public DovetailTJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int ti = 0, mi = 1;
            if (JointPartX.IsAtMiddle(m_parts[0].Case) && JointPartX.IsAtEnd(m_parts[1].Case))
            {
                ti = 1; mi = 0;
            }

            var tenon = beams[ti];
            var mortise = beams[mi];
            var tolerance = context.Tolerance;

            var tenonDirection = m_parts[ti].Direction;
            var mortiseDirection = m_parts[mi].Direction;

            var tenonPlane = tenon.GetPlane(m_parts[ti].Parameter);
            var mortisePlane = mortise.GetPlane(m_parts[mi].Parameter);

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
            var tenonSideDirection = Vector3d.CrossProduct(tenonUp, tenonDirection);

            var tenonFacePlane = new Plane(
                mortisePlane.Origin - mortiseSideDirection * mortiseWidth * 0.5,
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            var tenonFaceOffsetPlane = new Plane(
                tenonFacePlane.Origin - tenonFacePlane.ZAxis * Added,
                tenonFacePlane.XAxis, tenonFacePlane.YAxis);

            var tenonBackPlane = new Plane(
                mortisePlane.Origin - mortiseSideDirection * (mortiseWidth * 0.5 - Depth),
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            if (Utility.ClosestDimension2D(tenonFacePlane, tenonPlane.XAxis) != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            var tenonSidePlanes = new[]
            {
                new Plane(tenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(tenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            var neckPlanes = new[]
            {
                new Plane(tenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 - NeckOffset), tenonDirection, tenonUp),
                new Plane(tenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 - NeckOffset), tenonDirection, tenonUp),
            };

            for (int i = 0; i < 2; ++i)
            {
                int sign = i > 0 ? 1 : -1;

                if (!RX.PlanePlane(neckPlanes[i], tenonFacePlane, out Line pivot))
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: dovetail neck is parallel to the mortise face.");
                    return;
                }

                neckPlanes[i].Origin = pivot.From;
                sign *= normal * pivot.Direction < 0 ? 1 : -1;

                neckPlanes[i].Transform(Transform.Rotation(neckPlanes[i].XAxis, tenonFacePlane.ZAxis, pivot.From));
                neckPlanes[i].Transform(Transform.Rotation(Angle * sign, pivot.Direction, pivot.From));
            }

            var centre = (tenonPlane.Origin + mortisePlane.Origin) * 0.5;

            tenonFacePlane.Origin = centre.ProjectToPlane(tenonFacePlane);
            tenonBackPlane.Origin = centre.ProjectToPlane(tenonBackPlane);
            for (int i = 0; i < 2; ++i)
                tenonSidePlanes[i].Origin = centre.ProjectToPlane(tenonSidePlanes[i]);

            var halfDepth = Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added;
            var topPlane = new Plane(tenonFacePlane.Origin + tenonFacePlane.YAxis * halfDepth, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);
            var middlePlane = new Plane(tenonFacePlane.Origin, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);
            var bottomPlane = new Plane(tenonFacePlane.Origin - tenonFacePlane.YAxis * halfDepth, tenonFacePlane.XAxis, tenonFacePlane.ZAxis);

            Position = new Plane(centre, middlePlane.XAxis, middlePlane.YAxis);

            bool ok = true;
            var tenonPoints = new Point3d[12];
            var mortisePoints = new Point3d[8];

            for (int i = 0; i < 2; ++i)
            {
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonFacePlane, bottomPlane, out tenonPoints[0 + i * 6]);
                ok &= RX.PlanePlanePlane(tenonSidePlanes[i], tenonFacePlane, topPlane, out tenonPoints[1 + i * 6]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonFacePlane, topPlane, out tenonPoints[2 + i * 6]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonFacePlane, middlePlane, out tenonPoints[3 + i * 6]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonBackPlane, middlePlane, out tenonPoints[4 + i * 6]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonBackPlane, topPlane, out tenonPoints[5 + i * 6]);

                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonFaceOffsetPlane, middlePlane, out mortisePoints[0 + i * 4]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonFaceOffsetPlane, topPlane, out mortisePoints[1 + i * 4]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonBackPlane, middlePlane, out mortisePoints[2 + i * 4]);
                ok &= RX.PlanePlanePlane(neckPlanes[i], tenonBackPlane, topPlane, out mortisePoints[3 + i * 4]);
            }

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: planes did not intersect.");
                return;
            }

            var tenonOutline = new Polyline
            {
                tenonPoints[0], tenonPoints[1], tenonPoints[2], tenonPoints[3],
                tenonPoints[9], tenonPoints[8], tenonPoints[7], tenonPoints[6], tenonPoints[0]
            };

            var tenonGeo = new[]
            {
                Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], tenonPoints[4], tenonPoints[5], tolerance),
                Brep.CreateFromCornerPoints(tenonPoints[4], tenonPoints[5], tenonPoints[11], tenonPoints[10], tolerance),
                Brep.CreateFromCornerPoints(tenonPoints[8], tenonPoints[9], tenonPoints[10], tenonPoints[11], tolerance),
                Brep.CreateFromCornerPoints(tenonPoints[3], tenonPoints[4], tenonPoints[10], tenonPoints[9], tolerance),
                Brep.CreatePlanarBreps(tenonOutline.ToNurbsCurve(), tolerance)?.FirstOrDefault(),
            };

            var mortiseGeo = new[]
            {
                Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[2], mortisePoints[6], mortisePoints[4], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[1], mortisePoints[3], mortisePoints[2], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[2], mortisePoints[3], mortisePoints[7], mortisePoints[6], tolerance),
                Brep.CreateFromCornerPoints(mortisePoints[4], mortisePoints[5], mortisePoints[7], mortisePoints[6], tolerance),
            };

            CrossJointUtil.AddLap(this, result, tenon, tenonGeo, middlePlane, tolerance);
            CrossJointUtil.AddLap(this, result, mortise, mortiseGeo, middlePlane, tolerance);
            CrossJointUtil.SetStatus(result, 2);

            if (DowelDiameter > 0 && result.Success)
            {
                var axis = new Line(
                    tenonPlane.Origin + tenonUp * tenonHeight * 0.25 - tenonDirection * tenonHeight,
                    tenonDirection, tenonHeight + mortiseWidth + 100);

                result.Add(new Drilling(tenon.Id, axis, DowelDiameter));
                result.Add(new Drilling(mortise.Id, axis, DowelDiameter));
            }
        }
    }
}
