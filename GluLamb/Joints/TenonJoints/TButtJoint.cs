using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Butt joint: one beam (the arm) ends square against the side of another (the sill), cut
    /// to the sill face and fixed with dowels through the sill into the end of the arm.
    /// Optionally the arm sits in a shallow housing in the sill. Port of TButtJointX and
    /// ButtJoint1, which are the same joint (the latter without the housing and with one
    /// dowel). The arm's cut is a JackRafterCut, the housing a Lap (an open surface following
    /// the arm's sides, also for oblique arms), and the dowels Drillings plus dowels.
    /// </summary>
    [JointType("glulamb.t-butt", Name = "T butt", Arity = 2, Topology = JointTopology.T,
        Description = "A beam butting against the side of another, dowelled through the other, optionally housed.")]
    public class TButtJoint : SillJointBase
    {
        [JointParameter(Description = "Depth of the seat (housing) for the arm in the sill, mostly for locating it. 0 = no seat.", Unit = "length")]
        public double Depth { get; set; } = 10;

        [JointParameter(Description = "On an angled arm, cut the seat wall and the arm's toe square to the sill face on the obtuse side, so no corner is under 90° (machinable, and no bearing on an edge).")]
        public bool SquareObtuseSide { get; set; } = true;

        [JointParameter(Description = "Clearance around the arm in the housing.", Unit = "length")]
        public double Clearance { get; set; } = 0;

        [JointParameter(Description = "Number of dowels, spread across the arm in the plane of the joint.")]
        public int DowelCount { get; set; } = 2;

        [JointParameter(Description = "Distance between dowels.", Unit = "length")]
        public double DowelSpacing { get; set; } = 80;

        [JointParameter(Description = "Dowel diameter. 0 = no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 12;

        [JointParameter(Description = "How far the dowels go into the arm, from the cut end.", Unit = "length")]
        public double DowelEmbedment { get; set; } = 100;

        [JointParameter(Description = "Extra depth drilled beyond the end of the dowel.", Unit = "length")]
        public double DowelDrillExtra { get; set; } = 10;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public TButtJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => IsArmOnSill(condition) ? 0.5 : 0.0;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            if (!SetUp(beams, result)) return;

            var tolerance = context.Tolerance;
            var armDir = ArmFrame.ZAxis;
            var cos = Math.Abs(armDir * Towards);
            if (cos < 0.1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arm is almost parallel to the sill face.");
                return;
            }

            // The arm's end, Depth into the sill; the cut's normal points to the removed end
            var endPlane = new Plane(SillFace.Origin - Towards * Depth, -Towards);
            bool seated = Depth > tolerance;
            int square = seated && SquareObtuseSide ? ObtuseSide() : 0;

            if (square == 0)
            {
                var cut = new JackRafterCut(Arm.Id, endPlane);
                cut.Data.Set("Name", "ButtCut");
                result.Add(cut);
            }
            else
            {
                // Angled into a seat: the end, and the acute toe cut square to the sill face
                var faces = SeatEndFaces(new Plane(endPlane.Origin, Towards), square, Added, null, tolerance);
                var endCutter = Brep.JoinBreps(faces, tolerance);
                if (endCutter == null || endCutter.Length != 1)
                {
                    result.Status = JointStatus.Failed;
                    result.Messages.Add($"{GetType().Name}: failed to create the end cut.");
                    return;
                }
                var end = new Lap { BeamId = Arm.Id, Plane = endPlane, Cutters = endCutter.ToList() };
                end.Data.Set("Name", "ButtCut");
                end.Data.Set("SquareSide", square);
                result.Add(end);
            }

            var endCorners = ArmCornersOn(endPlane).ToList();
            ExtendToReach(result, Arm, ArmPart, endCorners);

            // Seat: the arm's footprint from the end plane out past the sill face
            if (seated)
            {
                var housing = SeatHousing(Depth, Clearance, square, Added, tolerance);
                if (housing == null)
                {
                    result.Status = JointStatus.Partial;
                    result.Messages.Add($"{GetType().Name}: failed to create the housing.");
                }
                else
                    result.Add(housing);
            }

            // Dowels along the arm, from the back of the sill into the end of the arm
            if (DowelDiameter > 0 && DowelCount > 0)
            {
                var back = new Plane(SillFace.Origin - Towards * SillDepth, Towards);
                var toBack = Transform.ProjectAlong(back, armDir);
                var toEnd = Transform.ProjectAlong(endPlane, armDir);
                var inset = (ArmWidth - DowelDiameter) * 0.5;

                for (int i = 0; i < DowelCount; ++i)
                {
                    var offset = (i - (DowelCount - 1) * 0.5) * DowelSpacing;
                    if (Math.Abs(offset) > inset)
                        result.Messages.Add($"{GetType().Name}: dowel {i} is outside the arm.");

                    var start = ArmFrame.Origin + ArmFrame.XAxis * offset;
                    var atBack = start; atBack.Transform(toBack);
                    var atEnd = start; atEnd.Transform(toEnd);
                    var tip = atEnd + armDir * DowelEmbedment;

                    result.Add(new Drilling(Sill.Id, new Line(atBack - armDir * Added, atEnd + armDir * Added), DowelDiameter));
                    result.Add(new Drilling(Arm.Id, new Line(atEnd - armDir * Added, tip + armDir * DowelDrillExtra), DowelDiameter));
                    result.Hardware.Add(new DowelItem(new Line(atBack, tip), DowelDiameter, Arm.Id, Sill.Id));
                }
            }

            Position = new Plane(SillFace.Origin, SillTangent, JointNormal);
            result.Status = result.Status == JointStatus.Partial ? JointStatus.Partial : JointStatus.Ok;
        }
    }
}
