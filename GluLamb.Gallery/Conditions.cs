using GluLamb.Joints;
using Rhino.Geometry;

namespace GluLamb.Gallery
{
    /// <summary>
    /// A joint condition to try joint types on: some beams and the condition between them,
    /// built for a variant (section rotations).
    /// </summary>
    internal class Case
    {
        public string Family;
        public string Name;
        /// <summary>Beams and condition, given a quarter-turn rotation per beam section.</summary>
        public Func<Func<int, int>, (List<Beam> Beams, JointX Condition)> Build;
        public override string ToString() => Name;
    }

    internal static class Conditions
    {
        public static Beam Mk(Point3d a, Point3d b, double w, double h, Vector3d up, int rot, string id)
        {
            var t = b - a; t.Unitize();
            up -= t * (up * t); up.Unitize();
            up.Rotate(rot * Math.PI / 2, t);
            bool swap = rot % 2 == 1;
            return new Beam
            {
                Id = id,
                Centreline = new LineCurve(a, b),
                Orientation = new VectorOrientation(up),
                Width = swap ? h : w,
                Height = swap ? w : h,
            };
        }

        static double R(double deg) => Rhino.RhinoMath.ToRadians(deg);

        /// <summary>A condition between pairs of beams found by intersecting their centrelines, merged.</summary>
        static JointX Connect(List<Beam> beams)
        {
            var conditions = new List<JointX>();
            for (int i = 0; i < beams.Count; ++i)
                for (int k = i + 1; k < beams.Count; ++k)
                {
                    var jx = JointUtil.Connect(beams[i], i, beams[k], k, -1, 0.1, 50, 10);
                    if (jx != null) conditions.Add(jx);
                }
            var merged = JointX.MergeJoints(conditions, 50);
            if (merged.Count < 1) return null;
            var jc = merged[0];
            jc.Position = JointX.ConditionPlane(jc.Parts, jc.Position.Origin);
            return jc;
        }

        static JointX Single(Beam beam, double t, Point3d position)
        {
            JointUtil.ClassifyJointPosition(beam.Centreline, t, out int jointCase, out Vector3d direction, 10);
            return new JointX(new List<JointPartX> { new JointPartX { ElementIndex = 0, Case = jointCase, Parameter = t, Direction = direction } }, position);
        }

        /// <summary>The post first, as Classify Joints does for nodes of beam ends.</summary>
        static JointX PostFirst(JointX jc)
        {
            var post = jc.Parts.OrderByDescending(p => Math.Abs(p.Direction * Vector3d.ZAxis) / Math.Max(p.Direction.Length, 1e-9)).First();
            jc.Parts.Remove(post);
            jc.Parts.Insert(0, post);
            jc.Position = JointX.ConditionPlane(jc.Parts, jc.Position.Origin);
            return jc;
        }

        public static List<Case> All()
        {
            var z = Vector3d.ZAxis;
            var cases = new List<Case>();

            cases.Add(new Case
            {
                Family = "End", Name = "Free end",
                Build = rot =>
                {
                    var b = Mk(Point3d.Origin, new Point3d(1000, 0, 0), 120, 200, z, rot(0), "A");
                    return (new List<Beam> { b }, Single(b, b.Centreline.Domain.Max, new Point3d(1000, 0, 0)));
                }
            });

            cases.Add(new Case
            {
                Family = "Point", Name = "Point on a face",
                Build = rot =>
                {
                    var b = Mk(Point3d.Origin, new Point3d(1000, 0, 0), 120, 200, z, rot(0), "A");
                    var p = new Point3d(500, 0, 150);
                    b.Centreline.ClosestPoint(p, out double t);
                    return (new List<Beam> { b }, Single(b, t, p));
                }
            });

            foreach (var deg in new[] { 180.0, 165.0 })
                cases.Add(new Case
                {
                    Family = "Splice", Name = $"Splice {deg:0}°",
                    Build = rot =>
                    {
                        var a = R(deg);
                        var b0 = Mk(new Point3d(-800, 0, 0), Point3d.Origin, 120, 200, z, rot(0), "A");
                        var b1 = Mk(Point3d.Origin, new Point3d(-800 * Math.Cos(a), 800 * Math.Sin(a), 0), 120, 200, z, rot(1), "B");
                        return (new List<Beam> { b0, b1 }, JointUtil.ForceConnect(b0.Centreline, 0, b1.Centreline, 1, -1, 50));
                    }
                });

            foreach (var deg in new[] { 90.0, 60.0, 120.0 })
                cases.Add(new Case
                {
                    Family = "Corner", Name = $"Corner {deg:0}°",
                    Build = rot =>
                    {
                        var a = R(deg);
                        var b0 = Mk(new Point3d(-800, 0, 0), Point3d.Origin, 120, 200, z, rot(0), "A");
                        var b1 = Mk(Point3d.Origin, new Point3d(-800 * Math.Cos(a), 800 * Math.Sin(a), 0), 120, 200, z, rot(1), "B");
                        return (new List<Beam> { b0, b1 }, JointUtil.ForceConnect(b0.Centreline, 0, b1.Centreline, 1, -1, 50));
                    }
                });

            foreach (var deg in new[] { 10.0, 20.0 })
                cases.Add(new Case
                {
                    Family = "Fork", Name = $"Fork {deg:0}°",
                    Build = rot =>
                    {
                        var a = R(deg);
                        var b0 = Mk(Point3d.Origin, new Point3d(1000, 0, 0), 120, 200, z, rot(0), "A");
                        var b1 = Mk(Point3d.Origin, new Point3d(1000 * Math.Cos(a), 1000 * Math.Sin(a), 0), 120, 200, z, rot(1), "B");
                        return (new List<Beam> { b0, b1 }, JointUtil.ForceConnect(b0.Centreline, 0, b1.Centreline, 1, -1, 50));
                    }
                });

            foreach (var deg in new[] { 90.0, 60.0, 45.0 })
                cases.Add(new Case
                {
                    Family = "T", Name = $"T {deg:0}°",
                    Build = rot =>
                    {
                        var a = R(deg);
                        var arm = Mk(new Point3d(-800 * Math.Sin(a), -800 * Math.Cos(a), 0), Point3d.Origin, 120, 200, z, rot(0), "A");
                        var sill = Mk(new Point3d(0, -700, 0), new Point3d(0, 700, 0), 140, 240, z, rot(1), "B");
                        return (new List<Beam> { arm, sill }, JointUtil.ForceConnect(arm.Centreline, 0, sill.Centreline, 1, -1, 50));
                    }
                });

            foreach (var deg in new[] { 90.0, 60.0 })
                cases.Add(new Case
                {
                    Family = "Cross", Name = $"Cross {deg:0}°",
                    Build = rot =>
                    {
                        var a = R(deg);
                        var b0 = Mk(new Point3d(-600, 0, 0), new Point3d(600, 0, 0), 120, 200, z, rot(0), "A");
                        var b1 = Mk(new Point3d(-600 * Math.Cos(a), -600 * Math.Sin(a), 0), new Point3d(600 * Math.Cos(a), 600 * Math.Sin(a), 0), 140, 200, z, rot(1), "B");
                        return (new List<Beam> { b0, b1 }, JointUtil.ForceConnect(b0.Centreline, 0, b1.Centreline, 1, -1, 50));
                    }
                });

            foreach (var (deg0, deg1) in new[] { (45.0, 45.0), (60.0, 40.0) })
                cases.Add(new Case
                {
                    Family = "K", Name = $"K {deg0:0}°/{deg1:0}°",
                    Build = rot =>
                    {
                        var sill = Mk(new Point3d(-900, 0, 0), new Point3d(900, 0, 0), 140, 240, z, rot(0), "S");
                        var a0 = Mk(new Point3d(-800 * Math.Cos(R(deg0)), 800 * Math.Sin(R(deg0)), 0), Point3d.Origin, 120, 200, z, rot(1), "A");
                        var a1 = Mk(new Point3d(800 * Math.Cos(R(deg1)), 800 * Math.Sin(R(deg1)), 0), Point3d.Origin, 120, 200, z, rot(2), "B");
                        var beams = new List<Beam> { sill, a0, a1 };
                        return (beams, Connect(beams));
                    }
                });

            cases.Add(new Case
            {
                Family = "K", Name = "K 45°/45° with a joist",
                Build = rot =>
                {
                    var sill = Mk(new Point3d(-900, 0, 0), new Point3d(900, 0, 0), 140, 240, z, rot(0), "S");
                    var a0 = Mk(new Point3d(-800 * Math.Cos(R(45)), 800 * Math.Sin(R(45)), 0), Point3d.Origin, 120, 200, z, rot(1), "A");
                    var a1 = Mk(new Point3d(800 * Math.Cos(R(45)), 800 * Math.Sin(R(45)), 0), Point3d.Origin, 120, 200, z, rot(2), "B");
                    var joist = Mk(new Point3d(0, 0, 900), Point3d.Origin, 100, 200, Vector3d.XAxis, rot(3), "J");
                    var beams = new List<Beam> { sill, a0, a1, joist };
                    return (beams, Connect(beams));
                }
            });

            cases.Add(new Case
            {
                Family = "Post", Name = "Post corner",
                Build = rot =>
                {
                    var post = Mk(new Point3d(0, 0, -1000), Point3d.Origin, 160, 160, Vector3d.XAxis, rot(0), "P");
                    var b0 = Mk(Point3d.Origin, new Point3d(1000, 0, 0), 140, 200, z, rot(1), "A");
                    var b1 = Mk(Point3d.Origin, new Point3d(0, 1000, 0), 140, 200, z, rot(2), "B");
                    var beams = new List<Beam> { post, b0, b1 };
                    var jc = Connect(beams);
                    return (beams, jc == null ? null : PostFirst(jc));
                }
            });

            foreach (var skew in new[] { 0.0, 17.0 })
                cases.Add(new Case
                {
                    Family = "Post", Name = skew == 0 ? "Post running past" : $"Post running past, {skew:0}° skew",
                    Build = rot =>
                    {
                        var post = Mk(new Point3d(0, 0, -1000), new Point3d(0, 0, 1000), 160, 160, Vector3d.XAxis, rot(0), "P");
                        var b0 = Mk(Point3d.Origin, new Point3d(1000 * Math.Cos(R(skew)), 1000 * Math.Sin(R(skew)), 0), 140, 200, z, rot(1), "A");
                        var b1 = Mk(Point3d.Origin, new Point3d(0, 1000, 0), 140, 200, z, rot(2), "B");
                        var beams = new List<Beam> { post, b0, b1 };
                        return (beams, Connect(beams));
                    }
                });

            cases.Add(new Case
            {
                Family = "FourWay", Name = "Four ends",
                Build = rot =>
                {
                    var beams = new List<Beam>
                    {
                        Mk(new Point3d(-1000, 0, 0), Point3d.Origin, 120, 200, z, rot(0), "A"),
                        Mk(new Point3d(1000, 0, 0), Point3d.Origin, 120, 200, z, rot(1), "B"),
                        Mk(new Point3d(0, -1000, 0), Point3d.Origin, 120, 200, z, rot(2), "C"),
                        Mk(new Point3d(0, 1000, 0), Point3d.Origin, 120, 200, z, rot(3), "D"),
                    };
                    return (beams, Connect(beams));
                }
            });

            return cases;
        }
    }
}
