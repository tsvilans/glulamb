using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

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
        /// The side of the arm (+1 or -1 along ArmFrame X) where its side meets the sill face at
        /// an obtuse angle outside the arm, i.e. where a seat wall along the arm would undercut
        /// the sill; 0 if the arm is square to the sill face (within 1°).
        /// </summary>
        protected int ObtuseSide()
        {
            var tilt = -ArmFrame.ZAxis;
            tilt -= Towards * (tilt * Towards);
            // Only the lean in the plane of the joint (across ArmFrame X) counts: an arm skewed
            // across the sill (leaning along the joint normal) is not handled
            var lean = ArmFrame.XAxis * tilt;
            if (Math.Abs(lean) < Math.Sin(Rhino.RhinoMath.ToRadians(1))) return 0;
            return lean > 0 ? 1 : -1;
        }

        /// <summary>
        /// The arm's footprint on a plane parallel to the sill face, grown by gx across the arm
        /// (in the plane of the joint) and gy along the joint normal. On the chamfered side (+1 or
        /// -1, or 0 for none) the corners are taken square to the sill face from where the arm's
        /// side meets the face (grown by chamferGrow), so a seat wall there is square to the sill.
        /// Corners in the order (-1,-1), (1,-1), (1,1), (-1,1) of ArmFrame X and Y.
        /// </summary>
        protected Point3d[] SeatCorners(Plane onto, double gx, double gy, int chamferSide, double chamferGrow = 0)
        {
            var alongArm = Transform.ProjectAlong(onto, ArmFrame.ZAxis);
            var toFace = Transform.ProjectAlong(SillFace, ArmFrame.ZAxis);
            var square = Transform.ProjectAlong(onto, Towards);
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c =>
            {
                if (c.Item1 == chamferSide)
                {
                    var p = ArmFrame.PointAt(c.Item1 * (ArmWidth * 0.5 + chamferGrow), c.Item2 * (ArmThickness * 0.5 + gy));
                    p.Transform(toFace);
                    p.Transform(square);
                    return p;
                }
                var q = ArmFrame.PointAt(c.Item1 * (ArmWidth * 0.5 + gx), c.Item2 * (ArmThickness * 0.5 + gy));
                q.Transform(alongArm);
                return q;
            }).ToArray();
        }

        /// <summary>
        /// Indices into SeatCorners of the two corners on a side, in outline order.
        /// </summary>
        protected static (int, int) SideCorners(int side) => side > 0 ? (1, 2) : (3, 0);

        /// <summary>
        /// A seat (housing) for the arm's full section in the sill face, depth deep, walls up past
        /// the face. Walls follow the arm, except on the chamfered side, where the wall is square
        /// to the sill face (see SeatCorners). Returns null if the surface fails.
        /// </summary>
        protected Lap SeatHousing(double depth, double clearance, int chamferSide, double added, double tolerance)
        {
            var bottomPlane = new Plane(SillFace.Origin - Towards * depth, Towards);
            var topPlane = new Plane(SillFace.Origin + Towards * added, Towards);
            var bottom = SeatCorners(bottomPlane, clearance, clearance, chamferSide, clearance);
            var top = SeatCorners(topPlane, clearance, clearance, chamferSide, clearance);

            var faces = new List<Brep> { Brep.CreateFromCornerPoints(bottom[0], bottom[1], bottom[2], bottom[3], tolerance) };
            for (int i = 0; i < 4; ++i)
            {
                int j = (i + 1) % 4;
                faces.Add(Brep.CreateFromCornerPoints(bottom[i], bottom[j], top[j], top[i], tolerance));
            }

            var housing = faces.Any(x => x == null) ? null : Brep.JoinBreps(faces, tolerance)?.FirstOrDefault();
            if (housing == null) return null;

            var lap = new Lap { BeamId = Sill.Id, Plane = new Plane(SillFace.Origin, SillTangent, JointNormal), Depth = depth, Cutters = new List<Brep> { housing } };
            lap.Data.Set("Name", "Housing");
            lap.Data.Set("SquareSide", chamferSide);
            return lap;
        }

        /// <summary>
        /// Faces of the arm's cut at the bottom of a seat: the end face (around an optional hole,
        /// e.g. a tenon's), and on the chamfered side a face square to the sill face from the end
        /// up past the sill face, which takes off the arm's acute toe. Join with any other faces.
        /// </summary>
        protected List<Brep> SeatEndFaces(Plane end, int chamferSide, double added, Curve hole, double tolerance)
        {
            var c = SeatCorners(end, added, added, chamferSide);
            var outline = new Polyline(c) { c[0] }.ToNurbsCurve();
            var faces = new List<Brep>(Brep.CreatePlanarBreps(hole == null ? new[] { outline } : new[] { outline, hole }, tolerance) ?? new Brep[0]);

            if (chamferSide != 0)
            {
                var (i, j) = SideCorners(chamferSide);
                var up = Transform.ProjectAlong(new Plane(SillFace.Origin + Towards * added, Towards), Towards);
                Point3d a = c[i], b = c[j], a1 = a, b1 = b;
                a1.Transform(up);
                b1.Transform(up);
                var chamfer = Brep.CreateFromCornerPoints(a, b, b1, a1, tolerance);
                if (chamfer != null) faces.Add(chamfer);
            }
            return faces;
        }

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
