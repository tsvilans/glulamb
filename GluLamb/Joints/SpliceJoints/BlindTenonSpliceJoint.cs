using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Blind tenon splice: two beams butt end to end, with a tenon on one end in a blind mortise
    /// in the other, pinned with a dowel across the tenon. Port of the legacy
    /// SpliceJoint_BlindTenon: one cutting surface per beam (the butt face with the tenon standing
    /// out of it), which Brep.Cut uses to leave the tenon on one beam and the mortise in the other.
    /// The dowel hole in the mortise beam is offset away from the shoulder (draw-boring), so the
    /// dowel pulls the joint tight. Beam 0 has the tenon; Flip gives it to beam 1.
    /// </summary>
    [JointType("glulamb.splice-tenon-blind", Name = "Blind tenon splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two beam ends butted together with a blind tenon and a dowel.")]
    public class BlindTenonSpliceJoint : JointBase
    {
        [JointParameter(Description = "Tenon length.", Unit = "length")]
        public double TenonLength { get; set; } = 100;

        [JointParameter(Description = "Tenon width (along the section X axis). Limited to the beam width.", Unit = "length")]
        public double TenonWidth { get; set; } = 40;

        [JointParameter(Description = "Tenon height (along the section Y axis). Limited to the beam height.", Unit = "length")]
        public double TenonHeight { get; set; } = 80;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10;

        [JointParameter(Description = "Tool diameter: the tenon and mortise edges are rounded to its radius.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16;

        [JointParameter(Description = "Clearance on each side of the tenon in the mortise.", Unit = "length")]
        public double ToleranceSide { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of the tenon.", Unit = "length")]
        public double ToleranceEnd { get; set; } = 1.5;

        [JointParameter(Description = "Dowel diameter. 0 = no dowel.", Unit = "length")]
        public double DowelDiameter { get; set; } = 12;

        [JointParameter(Description = "Dowel length.", Unit = "length")]
        public double DowelLength { get; set; } = 220;

        [JointParameter(Description = "How far the dowel hole starts before the beam face.", Unit = "length")]
        public double DowelLengthExtra { get; set; } = 15;

        [JointParameter(Description = "Draw-bore: offset of the dowel hole in the mortise beam, away from the shoulder.", Unit = "length")]
        public double DowelSideTolerance { get; set; } = 0.5;

        public BlindTenonSpliceJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int ti = Flip ? 1 : 0, mi = 1 - ti;
            var tenonBeam = beams[ti];
            var mortiseBeam = beams[mi];
            var tolerance = context.Tolerance;

            // Face frame at the end of the tenon beam, Z pointing out of it towards the other beam
            var face = tenonBeam.GetPlane(m_parts[ti].Parameter);
            var outward = m_parts[ti].Direction;
            if (face.ZAxis * outward < 0)
                face = new Plane(face.Origin, -face.XAxis, face.YAxis);

            // Section size: the larger of the two beams, measured along the tenon beam's axes
            var other = mortiseBeam.GetPlane(m_parts[mi].Parameter);
            bool otherSwapped = Math.Abs(other.XAxis * face.XAxis) < Math.Abs(other.YAxis * face.XAxis);
            double width = Math.Max(tenonBeam.Width, otherSwapped ? mortiseBeam.Height : mortiseBeam.Width);
            double height = Math.Max(tenonBeam.Height, otherSwapped ? mortiseBeam.Width : mortiseBeam.Height);

            var tenonWidth = Math.Min(width, TenonWidth);
            var tenonHeight = Math.Min(height, TenonHeight);

            Position = face;

            Brep Cutter(double sideTolerance, double endTolerance, out Curve baseOutline, out Plane tip)
            {
                var outer = new Rectangle3d(face,
                    new Interval(-width * 0.5 - Added, width * 0.5 + Added),
                    new Interval(-height * 0.5 - Added, height * 0.5 + Added)).ToNurbsCurve();

                Curve Section(Plane p)
                {
                    var rect = new Rectangle3d(p,
                        new Interval(-tenonWidth * 0.5 - sideTolerance, tenonWidth * 0.5 + sideTolerance),
                        new Interval(-tenonHeight * 0.5 - sideTolerance, tenonHeight * 0.5 + sideTolerance)).ToNurbsCurve();
                    var r = ToolDiameter * 0.5 - sideTolerance;
                    return r > tolerance ? Curve.CreateFilletCornersCurve(rect, r, tolerance, context.AngleTolerance) ?? rect : rect;
                }

                tip = new Plane(face.Origin + face.ZAxis * (TenonLength - endTolerance), face.XAxis, face.YAxis);
                baseOutline = Section(face);
                var tipOutline = Section(tip);

                var faces = new List<Brep>();
                faces.AddRange(Brep.CreatePlanarBreps(new[] { outer, baseOutline }, tolerance) ?? new Brep[0]);
                faces.AddRange(Brep.CreateFromLoft(new[] { baseOutline, tipOutline }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false) ?? new Brep[0]);
                faces.AddRange(Brep.CreatePlanarBreps(tipOutline, tolerance) ?? new Brep[0]);
                return faces.Count == 3 ? Brep.JoinBreps(faces, tolerance)?.FirstOrDefault() : null;
            }

            var tenonCutter = Cutter(0, ToleranceEnd, out Curve tenonBase, out Plane tenonTip);
            var mortiseCutter = Cutter(ToleranceSide, 0, out Curve mortiseBase, out Plane mortiseEnd);

            if (tenonCutter == null || mortiseCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the cutters.");
                return;
            }

            var tenon = new Tenon { BeamId = tenonBeam.Id, Plane = face, Length = TenonLength - ToleranceEnd, Width = tenonWidth, Thickness = tenonHeight };
            tenon.Cutters.Add(tenonCutter);
            tenon.Data.Set("BasePlane", face);
            tenon.Data.Set("TopPlane", tenonTip);
            tenon.Data.Set("Outline", tenonBase);
            result.Add(tenon);

            var mortise = new Mortise { BeamId = mortiseBeam.Id, Plane = face, Depth = TenonLength, Width = tenonWidth + ToleranceSide * 2, Thickness = tenonHeight + ToleranceSide * 2 };
            mortise.Cutters.Add(mortiseCutter);
            mortise.Data.Set("SlotPlane", face);
            mortise.Data.Set("EndPlane", mortiseEnd);
            mortise.Data.Set("Outline", mortiseBase);
            result.Add(mortise);

            // Dowel across the tenon, through the beam height, in the middle of the tenon
            if (DowelDiameter > 0)
            {
                var start = face.Origin + face.ZAxis * TenonLength * 0.5 - face.YAxis * height * 0.5;
                var dowel = new Line(start, face.YAxis, DowelLength);
                result.Hardware.Add(new DowelItem(new Line(start, face.YAxis, height), DowelDiameter, tenonBeam.Id, mortiseBeam.Id));

                var hole = new Line(start - face.YAxis * DowelLengthExtra, face.YAxis, DowelLength + DowelLengthExtra);
                result.Add(new Drilling(tenonBeam.Id, hole, DowelDiameter));

                var drawBore = hole;
                drawBore.Transform(Transform.Translation(face.ZAxis * DowelSideTolerance));
                result.Add(new Drilling(mortiseBeam.Id, drawBore, DowelDiameter));
            }

            // The tenon beam has to reach the tenon tip, the mortise beam the face
            var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
            ExtendToReach(result, tenonBeam, m_parts[ti], corners.Select(c => tenonTip.PointAt(c.Item1 * tenonWidth * 0.5, c.Item2 * tenonHeight * 0.5)));
            ExtendToReach(result, mortiseBeam, m_parts[mi], corners.Select(c => face.PointAt(c.Item1 * width * 0.5, c.Item2 * height * 0.5)));

            result.Status = JointStatus.Ok;
        }
    }
}
