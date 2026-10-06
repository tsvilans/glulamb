using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// K plate joint with a fourth beam, a joist, that ends at the sill next to the plate and is
    /// notched into it. Port of KJoint_Plate6Joist: the K joint as in KPlateJoint, plus the joist
    /// cutters on the sill and the joist, as Slot features. Of the three beams that end at the
    /// joint, the joist is the one furthest out of the plane of the other two and the sill.
    /// </summary>
    [JointType("glulamb.k-plate-joist", Name = "K plate with joist", Arity = 4, Topology = JointTopology.Node,
        Description = "K plate joint with an extra joist ending at the sill and notched into it.")]
    public class KPlateJoistJoint : KPlateJoint
    {
        [JointParameter(Description = "How far the joist notch runs back into the joist from the plate.", Unit = "length")]
        public double JoistInset { get; set; } = 40;

        [JointParameter(Description = "Length of the joist notch along the joist, before the angle correction.", Unit = "length")]
        public double JoistNotchLength { get; set; } = 70;

        public KPlateJoistJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Three parts at beam ends and one in the middle of a beam (the sill).
        /// </summary>
        public static new double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 4) return 0;
            int middles = condition.Parts.Count(x => JointPartX.IsAtMiddle(x.Case));
            return middles == 1 ? 1.0 : 0.0;
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int sill = Enumerable.Range(0, 4).FirstOrDefault(i => JointPartX.IsAtMiddle(m_parts[i].Case));
            var ends = Enumerable.Range(0, 4).Where(i => i != sill).ToArray();
            if (!JointPartX.IsAtMiddle(m_parts[sill].Case) || ends.Any(i => !JointPartX.IsAtEnd(m_parts[i].Case)))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: needs three beams ending on the side of a fourth.");
                return;
            }

            // The arms and the sill lie roughly in one plane; the joist is the end beam whose
            // removal leaves the most planar arrangement.
            var sillTangent = beams[sill].Centreline.TangentAt(m_parts[sill].Parameter);
            int joist = ends.OrderBy(j =>
            {
                var arms = ends.Where(i => i != j).ToArray();
                var n = Vector3d.CrossProduct(
                    beams[arms[0]].Centreline.TangentAt(m_parts[arms[0]].Parameter),
                    beams[arms[1]].Centreline.TangentAt(m_parts[arms[1]].Parameter));
                n.Unitize();
                return Math.Abs(n * sillTangent);
            }).First();

            var armIndices = ends.Where(i => i != joist).ToArray();

            ConstructK(new[] { beams[armIndices[0]], beams[armIndices[1]], beams[sill] },
                new[] { m_parts[armIndices[0]], m_parts[armIndices[1]], m_parts[sill] }, context, result);

            if (result.Status == JointStatus.Failed) return;

            var joistBeam = beams[joist];
            var joistPart = m_parts[joist];

            var sillCutter = CreateJoistCutter(joistBeam, joistPart, true, result, out _);
            var joistCutter = CreateJoistCutter(joistBeam, joistPart, false, result, out var joistEnd);

            if (sillCutter != null)
            {
                var slot = new Slot { BeamId = Beams[2].Id, Plane = PlatePlane };
                slot.Cutters.Add(sillCutter);
                slot.Data.Set("Name", $"JoistPocket_{Short(joistBeam)}");
                result.Add(slot);
            }

            if (joistCutter != null)
            {
                var slot = new Slot { BeamId = joistBeam.Id, Plane = PlatePlane };
                slot.Cutters.Add(joistCutter);
                slot.Data.Set("Name", $"JoistNotch_{Short(Beams[2])}");
                result.Add(slot);

                // The joist has to reach its end plane at the plate
                ExtendToReach(result, joistBeam, joistPart, joistEnd);
            }
            else
                result.Messages.Add($"{GetType().Name}: failed to create the joist cutters.");

            result.Status = result.Features.Count == 4 ? JointStatus.Ok : JointStatus.Partial;
        }

        protected Brep CreateJoistCutter(Beam joist, JointPartX joistPart, bool isSill, JointResult result, out List<Point3d> joistEnd)
        {
            joistEnd = new List<Point3d>();

            var jPlane = joist.GetPlane(joistPart.Parameter);
            var sPlane = Beams[2].GetPlane(KParts[2].Parameter);

            // Joist frame: Z into the joist, Y along the joint's "up" (from the sill towards the
            // arms), whichever side of the joist's section that is
            var intoJoist = joist.Centreline.PointAt(joist.Centreline.Domain.Mid) - jPlane.Origin;
            jPlane = AlignSection(joist, jPlane, intoJoist, Position.XAxis, out double joistWidth, out double joistHeight);
            var jDir = jPlane.ZAxis;

            var offsetPlatePlane = PlatePlane;
            var offsetDir = jDir * offsetPlatePlane.ZAxis < 0 ? -1 : 1;
            offsetPlatePlane.Origin = offsetPlatePlane.Origin + offsetPlatePlane.ZAxis * PlateThickness * 0.5 * offsetDir;

            var joistExtended = joist.Centreline.Extend(CurveEnd.Both, 200, CurveExtensionStyle.Line) ?? joist.Centreline;
            var res = RX.CurvePlane(joistExtended, offsetPlatePlane, 0.01);
            if (res == null || res.Count < 1)
            {
                result.Messages.Add($"{GetType().Name}: the joist does not reach the plate.");
                return null;
            }
            jPlane.Origin = res[0].PointA;
            Debug.Add(jPlane);

            // As in the original: compensates for the joist not being perpendicular to the sill,
            // so that the flat end of the joist cutter extends past the slanted end of the joist.
            // Note that this measures the angle in the world XY plane.
            var plateZProj = Plane.WorldXY.Project(sPlane.ZAxis);
            plateZProj.Unitize();
            var jDirProj = Plane.WorldXY.Project(jDir);
            jDirProj.Unitize();

            var angleFactor = 1 - Math.Abs(plateZProj * jDirProj);

            double thickness = PlateThickness + 5.0;
            double angleOffset = angleFactor > 0 ? thickness / angleFactor : thickness;

            double jhw = joistWidth * 0.5;
            double jhh = joistHeight * 0.5;

            var jEndPlane = new Plane(jPlane.Origin - jDir * angleOffset, jPlane.XAxis, jPlane.YAxis);

            jPlane.Origin = jPlane.Origin + jDir * JoistInset;

            var proj = isSill ? jEndPlane.ProjectAlongVector(jPlane.ZAxis) : offsetPlatePlane.ProjectAlongVector(jPlane.ZAxis);

            double endOffset = (angleFactor > 0 ? JoistNotchLength / angleFactor : JoistNotchLength) - JoistInset;

            var jpts = new Point3d[16];
            jpts[0] = jPlane.PointAt(jhw, jhh);
            jpts[1] = jPlane.PointAt(-jhw, jhh);
            jpts[2] = jPlane.PointAt(jhw, 0);
            jpts[3] = jPlane.PointAt(-jhw, 0);
            jpts[4] = jPlane.PointAt(jhw, 0);
            jpts[5] = jPlane.PointAt(-jhw, 0);
            jpts[6] = jPlane.PointAt(jhw, -jhh - Added);
            jpts[7] = jPlane.PointAt(-jhw, -jhh - Added);
            jpts[8] = jPlane.PointAt(jhw, jhh, endOffset);
            jpts[9] = jPlane.PointAt(-jhw, jhh, endOffset);
            jpts[10] = jPlane.PointAt(jhw, -jhh - Added, endOffset);
            jpts[11] = jPlane.PointAt(-jhw, -jhh - Added, endOffset);
            jpts[12] = jPlane.PointAt(jhw + Added, jhh + Added, endOffset);
            jpts[13] = jPlane.PointAt(-jhw - Added, jhh + Added, endOffset);
            jpts[14] = jPlane.PointAt(jhw + Added, -jhh - Added * 2, endOffset);
            jpts[15] = jPlane.PointAt(-jhw - Added, -jhh - Added * 2, endOffset);

            for (int i = 0; i < 4; ++i)
                jpts[i].Transform(proj);

            // The joist's section at its notched end
            if (!isSill)
            {
                var endProj = jEndPlane.ProjectAlongVector(jDir);
                foreach (var c in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    var p = jPlane.PointAt(c.Item1 * jhw, c.Item2 * jhh);
                    p.Transform(endProj);
                    joistEnd.Add(p);
                }
            }

            var faces = new List<Brep>
            {
                Brep.CreateFromCornerPoints(jpts[0], jpts[1], jpts[3], jpts[2], 0.01),
                Brep.CreateFromCornerPoints(jpts[2], jpts[3], jpts[5], jpts[4], 0.01),
                Brep.CreateFromCornerPoints(jpts[4], jpts[5], jpts[7], jpts[6], 0.01),
                Brep.CreateFromCornerPoints(jpts[6], jpts[7], jpts[11], jpts[10], 0.01),
                Brep.CreateFromCornerPoints(jpts[8], jpts[9], jpts[1], jpts[0], 0.01),
                Brep.CreatePlanarBreps(new Polyline { jpts[0], jpts[2], jpts[4], jpts[6], jpts[10], jpts[8], jpts[0] }.ToNurbsCurve(), 0.01)?.FirstOrDefault(),
                Brep.CreatePlanarBreps(new Polyline { jpts[1], jpts[3], jpts[5], jpts[7], jpts[11], jpts[9], jpts[1] }.ToNurbsCurve(), 0.01)?.FirstOrDefault(),
            };

            if (!isSill)
            {
                faces.Add(Brep.CreateFromCornerPoints(jpts[12], jpts[13], jpts[9], jpts[8], 0.01));
                faces.Add(Brep.CreateFromCornerPoints(jpts[13], jpts[15], jpts[11], jpts[9], 0.01));
                faces.Add(Brep.CreateFromCornerPoints(jpts[15], jpts[14], jpts[10], jpts[11], 0.01));
                faces.Add(Brep.CreateFromCornerPoints(jpts[14], jpts[12], jpts[10], jpts[8], 0.01));
            }

            var joined = Brep.JoinBreps(faces.Where(x => x != null), 0.01)?.FirstOrDefault();
            if (joined == null) return null;
            joined.MergeCoplanarFaces(0.01);

            double toolRadius = ToolDiameter * 0.5;
            var edges = new[] { 4, 6, 10, 12, 14, 15 }.Where(e => e < joined.Edges.Count).ToArray();
            var radii = edges.Select(_ => toolRadius).ToArray();

            var filleted = edges.Length > 0
                ? Brep.CreateFilletEdges(joined, edges, radii, radii, BlendType.Fillet, RailType.RollingBall, 0.01)
                : null;

            return filleted != null && filleted.Length > 0 ? filleted[0] : joined;
        }
    }
}
