using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints
{
    /// <summary>
    /// Shared set-up for T joints where one beam (the arm) ends on the side of another (the
    /// sill): frames for both beams relative to the joint, whichever way their sections are
    /// oriented, and the sill face the arm lands on.
    /// </summary>
    public abstract class SillJointBase : JointBase
    {
        protected Beam Arm, Sill;
        protected JointPartX ArmPart, SillPart;

        /// <summary>Point on the sill centreline at the joint.</summary>
        protected Point3d Node;
        /// <summary>Along the sill.</summary>
        protected Vector3d SillTangent;
        /// <summary>Normal of the plane of the joint (the plane of the arm and the sill).</summary>
        protected Vector3d JointNormal;
        /// <summary>Across the sill, towards the arm.</summary>
        protected Vector3d Towards;
        /// <summary>Sill extent towards the arm, and across the plane of the joint.</summary>
        protected double SillDepth, SillThickness;

        /// <summary>Arm frame at the sill face: Z into the arm, X in the plane of the joint, Y along JointNormal.</summary>
        protected Plane ArmFrame;
        /// <summary>Arm extent in the plane of the joint (along ArmFrame X), and across it (along ArmFrame Y).</summary>
        protected double ArmWidth, ArmThickness;

        /// <summary>The sill face the arm lands on, with Z pointing out of the sill.</summary>
        protected Plane SillFace;

        protected SillJointBase(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// One part at the end of a beam and one in the middle of another.
        /// </summary>
        protected static bool IsArmOnSill(JointX condition) =>
            condition.Parts.Count == 2 &&
            condition.Parts.Count(x => JointPartX.IsAtEnd(x.Case)) == 1 &&
            condition.Parts.Count(x => JointPartX.IsAtMiddle(x.Case)) == 1;

        protected bool SetUp(Beam[] beams, JointResult result)
        {
            int arm = JointPartX.IsAtEnd(m_parts[0].Case) ? 0 : 1;
            int sill = 1 - arm;
            if (!JointPartX.IsAtEnd(m_parts[arm].Case) || !JointPartX.IsAtMiddle(m_parts[sill].Case))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: needs a beam ending on the side of another.");
                return false;
            }

            Arm = beams[arm]; Sill = beams[sill];
            ArmPart = m_parts[arm]; SillPart = m_parts[sill];

            var sillPlane = Sill.GetPlane(SillPart.Parameter);
            Node = sillPlane.Origin;
            SillTangent = sillPlane.ZAxis;

            var armPlane = Arm.GetPlane(ArmPart.Parameter);
            var intoArm = Arm.Centreline.PointAt(Arm.Centreline.Domain.Mid) - Node;
            var armDir = armPlane.ZAxis * intoArm < 0 ? -armPlane.ZAxis : armPlane.ZAxis;

            JointNormal = Vector3d.CrossProduct(SillTangent, armDir);
            if (!JointNormal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams are parallel.");
                return false;
            }

            // Sill: X towards the arm, Y across the plane of the joint
            var sillFrame = AlignSection(Sill, sillPlane, SillTangent, JointNormal, out SillDepth, out SillThickness);
            Towards = sillFrame.XAxis * armDir < 0 ? -sillFrame.XAxis : sillFrame.XAxis;

            SillFace = new Plane(Node + Towards * SillDepth * 0.5, Towards);

            // Arm frame where its centreline crosses the sill face
            var res = RX.CurvePlane(Arm.Centreline.Extend(CurveEnd.Both, SillDepth + ArmLengthMargin, CurveExtensionStyle.Line) ?? Arm.Centreline, SillFace, 0.01);
            var crossing = res != null && res.Count > 0 ? res[0].PointA : SillFace.ClosestPoint(armPlane.Origin);
            Arm.Centreline.ClosestPoint(crossing, out double t);
            var armAt = Arm.GetPlane(t);
            armAt.Origin = crossing;
            ArmFrame = AlignSection(Arm, armAt, armDir, JointNormal, out ArmWidth, out ArmThickness);

            return true;
        }

        private double ArmLengthMargin => Math.Max(Arm.Width, Arm.Height) * 2;

        /// <summary>
        /// The arm's section corners at its frame, projected along the arm onto a plane.
        /// </summary>
        protected IEnumerable<Point3d> ArmCornersOn(Plane plane, double grow = 0)
        {
            var project = Transform.ProjectAlong(plane, ArmFrame.ZAxis);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var p = ArmFrame.PointAt(sx * (ArmWidth * 0.5 + grow), sy * (ArmThickness * 0.5 + grow));
                p.Transform(project);
                yield return p;
            }
        }
    }
}
