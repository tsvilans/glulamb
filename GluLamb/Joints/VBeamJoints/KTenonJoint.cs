using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// All-timber K joint: two arm beams end on the side of a sill. One arm (the tenon arm)
    /// goes into the sill with a mortise and tenon, through by default, exactly as the T mortise
    /// and tenon (glulamb.t-tenon); the other arm butts against the tenon arm's side and the sill
    /// face. Port of VBeam_ThruTenon1 (KJoint_ThruTenon1), with its hard-coded sizes as
    /// parameters. The legacy joint also bridled the second arm over the tenon arm near the
    /// sill; here it simply butts. Arm 0 has the tenon; Flip gives it to arm 1.
    /// </summary>
    [JointType("glulamb.k-tenon", Name = "K mortise and tenon", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beams ending on the side of a third: one tenoned through it, the other butting against the first.")]
    public class KTenonJoint : JointBase
    {
        [JointParameter(Description = "Tenon thickness. 0 = a third of the arm, across the plane of the joint.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Shoulder on each side of the tenon in the plane of the joint.", Unit = "length")]
        public double TenonInset { get; set; } = 0;

        [JointParameter(Description = "Tenon length into the sill, measured square to the sill face. 0 = through the sill.", Unit = "length")]
        public double TenonLength { get; set; } = 0;

        [JointParameter(Description = "Clearance on each side of the tenon in the mortise.", Unit = "length")]
        public double Clearance { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of a blind tenon.", Unit = "length")]
        public double EndClearance { get; set; } = 2.0;

        [JointParameter(Description = "Tool diameter: the mortise corners and tenon edges are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Peg diameter, across the sill and the tenon. 0 = no peg.", Unit = "length")]
        public double DowelDiameter { get; set; } = 0;

        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public KTenonJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Two parts at beam ends (the arms) and one in the middle of a beam (the sill).
        /// </summary>
        public static double Score(JointX condition, IJointContext context) => KPlateJoint.Score(condition, context) * 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int sill = Enumerable.Range(0, 3).FirstOrDefault(i => JointPartX.IsAtMiddle(m_parts[i].Case));
            var arms = Enumerable.Range(0, 3).Where(i => i != sill).ToArray();
            if (!JointPartX.IsAtMiddle(m_parts[sill].Case) || arms.Any(i => !JointPartX.IsAtEnd(m_parts[i].Case)))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: needs two beams ending on the side of a third.");
                return;
            }

            int ta = Flip ? arms[1] : arms[0];
            int ba = Flip ? arms[0] : arms[1];

            // The tenon arm and the sill: a T mortise and tenon
            var tenon = new TTenonJoint(new JointX(new List<JointPartX> { m_parts[ta], m_parts[sill] }, Position))
            {
                TenonThickness = TenonThickness,
                TenonInset = TenonInset,
                TenonLength = TenonLength,
                Clearance = Clearance,
                EndClearance = EndClearance,
                ToolDiameter = ToolDiameter,
                DowelDiameter = DowelDiameter,
                Added = Added,
                ExtensionTolerance = ExtensionTolerance,
            };

            var t = tenon.Construct(context);
            foreach (var feature in t.AllFeatures) result.Add(feature);
            result.Hardware.AddRange(t.Hardware);
            result.Messages.AddRange(t.Messages);
            result.Debug.AddRange(t.Debug);
            foreach (var kv in t.Extensions)
            {
                result.Extend(kv.Key, true, kv.Value.Start);
                result.Extend(kv.Key, false, kv.Value.End);
            }
            if (!t.Success)
            {
                result.Status = JointStatus.Failed;
                return;
            }

            // The other arm: cut at the sill face and at the tenon arm's side facing it
            var sillPlane = beams[sill].GetPlane(m_parts[sill].Parameter);
            var node = sillPlane.Origin;

            Vector3d IntoArm(int i)
            {
                var plane = beams[i].GetPlane(m_parts[i].Parameter);
                var d = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - node;
                return plane.ZAxis * d < 0 ? -plane.ZAxis : plane.ZAxis;
            }

            var tDir = IntoArm(ta);
            var bDir = IntoArm(ba);
            var jointNormal = Vector3d.CrossProduct(sillPlane.ZAxis, tDir + bDir);
            if (!jointNormal.Unitize())
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arms are parallel to the sill.");
                return;
            }

            var sillFrame = AlignSection(beams[sill], sillPlane, sillPlane.ZAxis, jointNormal, out double sillDepth, out _);
            var towards = sillFrame.XAxis * (tDir + bDir) < 0 ? -sillFrame.XAxis : sillFrame.XAxis;
            var sillFace = new Plane(node + towards * sillDepth * 0.5, -towards);

            var tFrame = AlignSection(beams[ta], beams[ta].GetPlane(m_parts[ta].Parameter), tDir, jointNormal, out double tWidth, out _);
            var side = tFrame.XAxis * bDir < 0 ? -tFrame.XAxis : tFrame.XAxis;
            var tenonArmSide = new Plane(tFrame.Origin + side * tWidth * 0.5, -side);

            if (Vector3d.VectorAngle(tDir, bDir) < Rhino.RhinoMath.ToRadians(1))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the arms are parallel.");
                return;
            }

            var sillCut = new JackRafterCut(beams[ba].Id, sillFace);
            sillCut.Data.Set("Name", "SillCut");
            result.Add(sillCut);

            var seamCut = new JackRafterCut(beams[ba].Id, tenonArmSide);
            seamCut.Data.Set("Name", "SeamCut");
            result.Add(seamCut);

            Position = new Plane(sillFace.Origin, sillPlane.ZAxis, jointNormal);
            result.Status = t.Status;
        }
    }
}
