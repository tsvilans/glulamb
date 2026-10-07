using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Lapped beams on a post: the corner of a frame where two beams end on top of the end of a
    /// post. The beams are corner-lapped over each other (as glulamb.corner-lap) and sit on the
    /// post, which is cut off under them. With a tenon, the post continues as a stub tenon up
    /// through both laps (through) or into them (blind), in a mortise through both beams. The
    /// first part is the post (Classify Joints puts the part along its Post direction first);
    /// Flip swaps which beam is on top in the lap. This type sits the beams on the post with no
    /// tenon; glulamb.post-lap-tenon and glulamb.post-lap-tenon-blind add the tenon.
    /// </summary>
    [JointType("glulamb.post-lap", Name = "Lapped beams on a post", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beam ends corner-lapped on top of the end of a post.")]
    public class PostLapJoint : JointBase
    {
        [JointParameter(Description = "Tenon: 0 = none (the beams sit on the post), 1 = through both laps, 2 = blind.")]
        public int TenonMode { get; set; } = 0;

        [JointParameter(Description = "Tenon size along the post's section X axis. 0 = a third of the post.", Unit = "length")]
        public double TenonWidth { get; set; } = 0;

        [JointParameter(Description = "Tenon size along the post's section Y axis. 0 = a third of the post.", Unit = "length")]
        public double TenonThickness { get; set; } = 0;

        [JointParameter(Description = "Length of a blind tenon, from the underside of the beams. 0 = half their depth.", Unit = "length")]
        public double TenonLength { get; set; } = 0;

        [JointParameter(Description = "Clearance on each side of the tenon in the mortise.", Unit = "length")]
        public double Clearance { get; set; } = 0.5;

        [JointParameter(Description = "Clearance beyond the end of a blind tenon.", Unit = "length")]
        public double EndClearance { get; set; } = 2.0;

        [JointParameter(Description = "Tool diameter: the tenon edges and mortise corners along the post are rounded to its radius. 0 = sharp.", Unit = "length")]
        public double ToolDiameter { get; set; } = 16.0;

        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        public PostLapJoint(JointX condition) : base(condition)
        {
        }

        /// <summary>
        /// Three beam ends, the first (the post) roughly square to the other two.
        /// </summary>
        public static double Score(JointX condition, IJointContext context) => IsPostCorner(condition) ? 1.0 : 0.0;

        protected static bool IsPostCorner(JointX condition)
        {
            if (condition.Parts.Count != 3 || !condition.Parts.All(x => JointPartX.IsAtEnd(x.Case))) return false;
            var post = condition.Parts[0].Direction;
            if (!post.Unitize()) return false;
            return condition.Parts.Skip(1).All(p =>
            {
                var d = p.Direction;
                return d.Unitize() && Math.Abs(d * post) < 0.5;
            });
        }

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var tolerance = context.Tolerance;
            var post = beams[0];
            var postPart = m_parts[0];

            // The two beams: a corner lap
            var lap = new CornerLapJoint(SubCondition(1, 2)) { Added = Added, Flip = Flip, ExtensionTolerance = ExtensionTolerance };
            if (!Merge(result, lap.Construct(context)))
            {
                result.Status = JointStatus.Failed;
                return;
            }

            // Up: out of the end of the post
            var postPlane = post.GetPlane(postPart.Parameter);
            var intoPost = post.Centreline.PointAt(post.Centreline.Domain.Mid) - postPlane.Origin;
            var axis = postPlane.ZAxis * intoPost < 0 ? postPlane.ZAxis : -postPlane.ZAxis;   // out of the post
            var node = postPlane.Origin;

            // Underside and top of the lapped beams along the post axis
            double low = double.MaxValue, high = double.MinValue;
            for (int i = 1; i < 3; ++i)
            {
                var plane = beams[i].GetPlane(m_parts[i].Parameter);
                AlignSection(beams[i], plane, plane.ZAxis, axis, out _, out double h);
                var level = (plane.Origin - node) * axis;
                low = Math.Min(low, level - h * 0.5);
                high = Math.Max(high, level + h * 0.5);
            }

            var underside = new Plane(node + axis * low, axis);
            var topside = new Plane(node + axis * high, axis);
            // Z: up the post, the way the post (and its tenon) goes into the beams

            // Post frame along its axis, X towards the first beam
            var beamDir = beams[1].GetPlane(m_parts[1].Parameter).ZAxis;
            Position = new Plane(underside.Origin, beamDir, Vector3d.CrossProduct(axis, beamDir));
            var postFrame = AlignSection(post, postPlane, axis, Vector3d.CrossProduct(axis, beamDir), out double pw, out double ph);

            if (TenonMode <= 0)
            {
                // Cut off under the beams
                var cut = new JackRafterCut(post.Id, underside);
                cut.Data.Set("Name", "PostTop");
                result.Add(cut);
                ExtendToReach(result, post, postPart, Corners(postFrame, pw, ph, underside));
                result.Status = result.Status == JointStatus.Partial ? JointStatus.Partial : JointStatus.Ok;
                return;
            }

            var tw = TenonWidth > 0 ? Math.Min(TenonWidth, pw) : pw / 3.0;
            var tt = TenonThickness > 0 ? Math.Min(TenonThickness, ph) : ph / 3.0;
            bool through = TenonMode == 1;
            var length = through ? high - low : (TenonLength > 0 ? TenonLength : (high - low) * 0.5);
            if (length <= tolerance || length > high - low + tolerance)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the tenon length must be between 0 and the depth of the beams ({high - low:0.#}).");
                return;
            }
            var tip = new Plane(underside.Origin + axis * length, axis);

            // A section of the tenon, grown by a clearance, on a plane across the post, carried
            // along the post axis onto the given plane
            Curve Section(double grow, Plane onto)
            {
                var rect = new Rectangle3d(postFrame,
                    new Interval(-tw * 0.5 - grow, tw * 0.5 + grow),
                    new Interval(-tt * 0.5 - grow, tt * 0.5 + grow)).ToNurbsCurve();
                var r = Math.Min(ToolDiameter * 0.5, Math.Min(tw, tt) * 0.5 - tolerance);
                Curve c = r > tolerance ? Curve.CreateFilletCornersCurve(rect, r + grow, tolerance, context.AngleTolerance) ?? rect : rect;
                c.Transform(Transform.ProjectAlong(onto, axis));
                return c;
            }

            // Post: the shoulder under the beams with the tenon standing up from it
            var outer = new Polyline(Corners(postFrame, pw + Added * 2, ph + Added * 2, underside));
            outer.Add(outer[0]);
            var baseCurve = Section(0, underside);
            var tipCurve = Section(0, through ? topside : tip);

            var faces = new List<Brep>();
            faces.AddRange(Brep.CreatePlanarBreps(new[] { outer.ToNurbsCurve(), baseCurve }, tolerance) ?? new Brep[0]);
            faces.AddRange(Brep.CreateFromLoft(new[] { baseCurve, tipCurve }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false) ?? new Brep[0]);
            faces.AddRange(Brep.CreatePlanarBreps(tipCurve, tolerance) ?? new Brep[0]);
            var tenonCutter = faces.Count == 3 ? Brep.JoinBreps(faces, tolerance)?.FirstOrDefault() : null;
            if (tenonCutter == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the tenon.");
                return;
            }

            var tenon = new Tenon { BeamId = post.Id, Plane = new Plane(underside.Origin, postFrame.XAxis, postFrame.YAxis), Length = length, Width = tw, Thickness = tt };
            tenon.Cutters.Add(tenonCutter);
            tenon.Data.Set("Through", through);
            result.Add(tenon);
            ExtendToReach(result, post, postPart, Corners(postFrame, tw, tt, through ? topside : tip));

            // Beams: a mortise through both, from below the underside to above the tenon
            var start = new Plane(underside.Origin - axis * Added, axis);
            var end = through ? new Plane(topside.Origin + axis * Added, axis) : new Plane(tip.Origin + axis * EndClearance, axis);
            var sides = Brep.CreateFromLoft(new[] { Section(Clearance, start), Section(Clearance, end) }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            var mortiseBrep = sides != null && sides.Length > 0 ? sides[0].CapPlanarHoles(tolerance) : null;
            if (mortiseBrep == null || !mortiseBrep.IsSolid)
            {
                result.Status = JointStatus.Partial;
                result.Messages.Add($"{GetType().Name}: failed to create the mortise.");
            }
            else
            {
                for (int i = 1; i < 3; ++i)
                {
                    var mortise = new Mortise { BeamId = beams[i].Id, Plane = underside, Depth = (end.Origin - start.Origin) * axis, Width = tw + Clearance * 2, Thickness = tt + Clearance * 2 };
                    mortise.Cutters.Add(Mortise.PrepareCutter(mortiseBrep.DuplicateBrep()));
                    mortise.Data.Set("Through", through);
                    result.Add(mortise);
                }
            }

            result.Status = result.Status == JointStatus.Partial ? JointStatus.Partial : JointStatus.Ok;
        }

        // Corners of a w x h rectangle in the post frame, carried along the post axis onto a plane
        private static IEnumerable<Point3d> Corners(Plane frame, double w, double h, Plane onto)
        {
            var project = Transform.ProjectAlong(onto, frame.ZAxis);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var p = frame.PointAt(sx * w * 0.5, sy * h * 0.5);
                p.Transform(project);
                yield return p;
            }
        }
    }

    [JointType("glulamb.post-lap-tenon", Name = "Lapped beams on a post, through tenon", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beam ends corner-lapped on top of a post, with the post's tenon through both laps.")]
    public class PostLapTenonJoint : PostLapJoint
    {
        public PostLapTenonJoint(JointX condition) : base(condition) { TenonMode = 1; }

        public static new double Score(JointX condition, IJointContext context) => IsPostCorner(condition) ? 0.5 : 0.0;
    }

    [JointType("glulamb.post-lap-tenon-blind", Name = "Lapped beams on a post, blind tenon", Arity = 3, Topology = JointTopology.Node,
        Description = "Two beam ends corner-lapped on top of a post, with the post's blind tenon into the laps.")]
    public class PostLapBlindTenonJoint : PostLapJoint
    {
        public PostLapBlindTenonJoint(JointX condition) : base(condition) { TenonMode = 2; }

        public static new double Score(JointX condition, IJointContext context) => IsPostCorner(condition) ? 0.5 : 0.0;
    }
}
