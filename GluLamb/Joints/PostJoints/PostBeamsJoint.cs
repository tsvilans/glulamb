using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Beams on a post running past them: two beam ends meet the side of a post that carries on
    /// past the joint, from different sides (e.g. the corner of a frame with the post going
    /// up). Each beam butts against its face of the post, optionally sitting in a seat (a
    /// housing of its full section) and optionally with a blind tenon into the post. Built from
    /// glulamb.t-butt (no tenon) or glulamb.t-tenon (tenon) for each beam. The post is the part
    /// in the middle of its beam, so no ordering is needed.
    /// </summary>
    [JointType("glulamb.post-beams", Name = "Beams on a post", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beam ends against the sides of a post running past them, optionally seated and tenoned.")]
    public class PostBeamsJoint : JointBase
    {
        [JointParameter(Description = "Depth of the seat (housing of the beam's full section) in the post. 0 = no seat.", Unit = "length")]
        public double SeatDepth { get; set; } = 20;

        [JointParameter(Description = "Length of a blind tenon into the post, from its face. 0 = no tenon.", Unit = "length")]
        public double TenonLength { get; set; } = 0;

        [JointParameter(Description = "Tenon thickness. 0 = a third of the beam, across the plane of the beam and the post.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Shoulder on each side of the tenon in the other direction.", Unit = "length")]
        public double TenonInset { get; set; } = 20;

        [JointParameter(Description = "Turn the tenons a quarter turn.")]
        public bool Rotate { get; set; } = false;

        [JointParameter(Description = "Clearance on each side of a tenon in its mortise.", Unit = "length")]
        public double Clearance { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of a tenon.", Unit = "length")]
        public double EndClearance { get; set; } = 2.0;

        [JointParameter(Description = "Tool diameter: mortise corners and tenon edges are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public PostBeamsJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// One part in the middle of its beam (the post), two ends meeting it from different
        /// sides, i.e. not in one plane with the post (that is a K joint).
        /// </summary>
        public static double Score(JointX condition, IJointContext context)
        {
            if (condition.Parts.Count != 3) return 0;
            var middle = condition.Parts.Where(x => JointPartX.IsAtMiddle(x.Case)).ToList();
            var ends = condition.Parts.Where(x => JointPartX.IsAtEnd(x.Case)).ToList();
            if (middle.Count != 1 || ends.Count != 2) return 0;
            return ArmsInPlane(middle[0].Direction, ends[0].Direction, ends[1].Direction) ? 0.0 : 1.0;
        }

        /// <summary>
        /// Whether two arms lie in roughly one plane with a beam they meet (within 30°).
        /// </summary>
        public static bool ArmsInPlane(Vector3d tangent, Vector3d arm0, Vector3d arm1)
        {
            var n0 = Vector3d.CrossProduct(tangent, arm0);
            var n1 = Vector3d.CrossProduct(tangent, arm1);
            if (!n0.Unitize() || !n1.Unitize()) return true;
            return Math.Abs(n0 * n1) > Math.Cos(Rhino.RhinoMath.ToRadians(30));
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int post = Enumerable.Range(0, 3).FirstOrDefault(i => JointPartX.IsAtMiddle(m_parts[i].Case));
            var arms = Enumerable.Range(0, 3).Where(i => i != post).ToArray();
            if (!JointPartX.IsAtMiddle(m_parts[post].Case) || arms.Any(i => !JointPartX.IsAtEnd(m_parts[i].Case)))
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: needs two beams ending on the side of a post.");
                return;
            }

            foreach (var arm in arms)
            {
                IJoint joint;
                if (TenonLength > 0)
                    joint = new TTenonJoint(SubCondition(arm, post))
                    {
                        TenonLength = TenonLength,
                        HousingDepth = SeatDepth,
                        TenonThickness = TenonThickness,
                        TenonInset = TenonInset,
                        Rotate = Rotate,
                        Clearance = Clearance,
                        EndClearance = EndClearance,
                        ToolDiameter = ToolDiameter,
                        Added = Added,
                        ExtensionTolerance = ExtensionTolerance,
                    };
                else
                    joint = new TButtJoint(SubCondition(arm, post))
                    {
                        Depth = SeatDepth,
                        DowelCount = 0,
                        Added = Added,
                        ExtensionTolerance = ExtensionTolerance,
                    };

                if (!Merge(result, joint.Construct(context)))
                {
                    result.Status = JointStatus.Failed;
                    return;
                }
            }

            var postPlane = beams[post].GetPlane(m_parts[post].Parameter);

            // Seats and tenons reach into the post from two sides, so the beams can run into
            // each other there: cut both back to the plane halfway between them, through the post axis
            if (SeatDepth > 0 || TenonLength > 0)
            {
                Vector3d IntoArm(int i)
                {
                    var plane = beams[i].GetPlane(m_parts[i].Parameter);
                    var d = beams[i].Centreline.PointAt(beams[i].Centreline.Domain.Mid) - postPlane.Origin;
                    var z = plane.ZAxis * d < 0 ? -plane.ZAxis : plane.ZAxis;
                    z -= postPlane.ZAxis * (z * postPlane.ZAxis);
                    z.Unitize();
                    return z;
                }

                var d0 = IntoArm(arms[0]);
                var d1 = IntoArm(arms[1]);
                var seam = d1 - d0;
                if (seam.Unitize())
                {
                    var cut0 = new JackRafterCut(beams[arms[0]].Id, new Plane(postPlane.Origin, seam));
                    cut0.Data.Set("Name", "SeamCut");
                    result.Add(cut0);
                    var cut1 = new JackRafterCut(beams[arms[1]].Id, new Plane(postPlane.Origin, -seam));
                    cut1.Data.Set("Name", "SeamCut");
                    result.Add(cut1);
                }

                // Mortises from two sides can still run into each other inside the post
                var mortises = result.AllFeatures.OfType<Mortise>().SelectMany(m => m.Cutters).ToList();
                if (mortises.Count == 2)
                {
                    var clash = Brep.CreateBooleanIntersection(mortises[0], mortises[1], context.Tolerance);
                    if (clash != null && clash.Length > 0)
                        result.Messages.Add($"{GetType().Name}: the two mortises run into each other in the post, leaving gaps beside the mitred tenons; shorten the tenons (TenonLength).");
                }
            }

            Position = new Plane(postPlane.Origin, postPlane.XAxis, postPlane.YAxis);
            result.Status = result.Status == JointStatus.Partial ? JointStatus.Partial : JointStatus.Ok;
        }
    }
}
