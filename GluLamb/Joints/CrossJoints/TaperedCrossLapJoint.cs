using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Tapered cross lap: a cross halving whose sides are tapered, so each beam is slightly
    /// narrower at the lap face than at the face of the other beam, and the joint wedges tight
    /// as it is pushed together. Port of TaperedCrossJointX. Each beam is cut with one open
    /// surface: the lap face, the tapered walls of its notch, and the wedges taken off its own
    /// sides where it sits in the other beam's notch. The taper on each side is set by how far
    /// the side is drawn in at the lap face; the side facing the other beam's start and the
    /// side facing its end can be tapered differently (a straight side as a reference stop).
    /// The higher beam goes on top; Flip inverts that.
    /// </summary>
    [JointType("glulamb.cross-tapered", Name = "Tapered cross lap", Arity = 2, Topology = JointTopology.Cross,
        Description = "Two beams crossing, each notched by half, with tapered sides that wedge tight.")]
    public class TaperedCrossLapJoint : JointBase
    {
        [JointParameter(Description = "Extra size added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Inset of the lap sides from the beam sides. Positive values make a tighter lap.", Unit = "length")]
        public double Inset { get; set; } = 0.0;

        [JointParameter(Description = "Taper on the side of each beam facing the other beam's start: how far the side is drawn in at the lap face.", Unit = "length")]
        public double TaperStart { get; set; } = 0.0;

        [JointParameter(Description = "Taper on the side of each beam facing the other beam's end: how far the side is drawn in at the lap face.", Unit = "length")]
        public double TaperEnd { get; set; } = 10.0;

        [JointParameter(Description = "Minimum crossing angle.", Unit = "radians")]
        public double MinimumAngle { get; set; } = Rhino.RhinoMath.ToRadians(5.0);

        public TaperedCrossLapJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var tolerance = context.Tolerance;

            var plane0 = beams[0].GetPlane(m_parts[0].Parameter);
            var plane1 = beams[1].GetPlane(m_parts[1].Parameter);
            var t0 = beams[0].Centreline.TangentAt(m_parts[0].Parameter);
            var t1 = beams[1].Centreline.TangentAt(m_parts[1].Parameter);

            var angle = Vector3d.VectorAngle(t0, t1);
            if (angle < MinimumAngle || Math.PI - angle < MinimumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams are nearly parallel ({Rhino.RhinoMath.ToDegrees(angle):0.0}°).");
                return;
            }

            // Up is the crossing normal, oriented with the first beam's nearest section axis
            var up = Vector3d.CrossProduct(t0, t1);
            up.Unitize();
            if (up * NearestSectionAxis(plane0, up) < 0) up.Reverse();

            int oi = TopPart(plane0.Origin, plane1.Origin, up, tolerance);
            int ui = 1 - oi;
            var planes = new[] { plane0, plane1 };
            var tangents = new[] { t0, t1 };

            // The lap faces the over beam: up, unless Flip has put the over beam below the under one
            if ((planes[oi].Origin - planes[ui].Origin) * up < -tolerance)
                up.Reverse();

            // Sections relative to the crossing: Y up, X across the beam (its side direction)
            var under = AlignSection(beams[ui], planes[ui], tangents[ui], up, out double uw, out double uh);
            var over = AlignSection(beams[oi], planes[oi], tangents[oi], up, out double ow, out double oh);

            var lapOrigin = Interpolation.Lerp(under.Origin, over.Origin, uh / (oh + uh));
            var lap = new Plane(lapOrigin, up);
            // Notch depths: from the lap face to the top of the under beam, and to the bottom of the over beam
            var du = (under.Origin + up * uh * 0.5 - lapOrigin) * up;
            var dov = (lapOrigin - (over.Origin - up * oh * 0.5)) * up;
            if (du <= tolerance || dov <= tolerance)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: the beams don't overlap.");
                return;
            }

            Position = new Plane(lapOrigin, under.ZAxis, over.ZAxis);

            // Taper of a beam's side, given the side's direction: the start or end taper,
            // depending on whether it faces the other beam's start or end
            double Taper(Vector3d side, Vector3d otherTangent) => side * otherTangent < 0 ? TaperStart : TaperEnd;

            var underCutter = Cutter(under, uw, Taper(under.XAxis, over.ZAxis), Taper(-under.XAxis, over.ZAxis),
                over, ow, Taper(over.XAxis, under.ZAxis), Taper(-over.XAxis, under.ZAxis), lap, du, dov, tolerance);
            var overCutter = Cutter(over, ow, Taper(over.XAxis, under.ZAxis), Taper(-over.XAxis, under.ZAxis),
                under, uw, Taper(under.XAxis, over.ZAxis), Taper(-under.XAxis, over.ZAxis), new Plane(lapOrigin, -up), dov, du, tolerance);

            AddLap(result, beams[ui], underCutter, new Plane(lapOrigin, under.ZAxis, under.XAxis));
            AddLap(result, beams[oi], overCutter, new Plane(lapOrigin, over.ZAxis, over.XAxis));

            int created = result.Features.Count;
            result.Status = created == 2 ? JointStatus.Ok : created == 1 ? JointStatus.Partial : JointStatus.Failed;
        }

        /// <summary>
        /// The cutting surface for one beam (self), notched on the lap normal's side. Its notch
        /// walls are the other beam's tapered sides, which reach the other's full width at the
        /// self beam's face (depth dSelf). Its own sides are tapered below the lap face, reaching
        /// full width at the other beam's face (depth dOther); the wedges outside the taper are
        /// taken off where the self beam passes through the other beam.
        /// </summary>
        private Brep Cutter(Plane self, double selfWidth, double selfTaperPos, double selfTaperNeg,
            Plane other, double otherWidth, double otherTaperPos, double otherTaperNeg,
            Plane lap, double dSelf, double dOther, double tolerance)
        {
            var n = lap.ZAxis;
            var top = new Plane(lap.Origin + n * (dSelf + Added), n);
            var bottom = new Plane(lap.Origin - n * (dOther + Added), n);

            // A side plane of a beam, offset from its centre along its side direction
            Plane Side(Plane frame, int k, double offset)
            {
                var d = frame.XAxis * k;
                d -= n * (d * n); d.Unitize();
                var o = lap.ClosestPoint(frame.Origin);
                return new Plane(o + d * offset, d);
            }

            // A tapered side: drawn in by taper at the lap face, reaching the side at height h
            Plane Tapered(Plane frame, double width, int k, double taper, double h)
            {
                var side = Side(frame, k, width * 0.5 - Inset);
                var tangent = Vector3d.CrossProduct(side.ZAxis, n);
                var a = side.Origin - side.ZAxis * taper;
                var b = side.Origin + n * h;
                var normal = Vector3d.CrossProduct(tangent, b - a);
                normal.Unitize();
                if (normal * side.ZAxis < 0) normal.Reverse();
                return new Plane(a, normal);
            }

            var to = new Dictionary<int, Plane>
            {
                [1] = Tapered(other, otherWidth, 1, otherTaperPos, dSelf),
                [-1] = Tapered(other, otherWidth, -1, otherTaperNeg, dSelf),
            };
            var so = new Dictionary<int, Plane> { [1] = Side(other, 1, otherWidth * 0.5 - Inset), [-1] = Side(other, -1, otherWidth * 0.5 - Inset) };
            var sa = new Dictionary<int, Plane> { [1] = Side(self, 1, selfWidth * 0.5 + Added), [-1] = Side(self, -1, selfWidth * 0.5 + Added) };
            // An untapered side has no wedge: the lap face runs out past the side instead, so
            // that no cutting face lies on the beam's own side
            var ts = new Dictionary<int, Plane>
            {
                [1] = selfTaperPos > tolerance ? Tapered(self, selfWidth, 1, selfTaperPos, -dOther) : sa[1],
                [-1] = selfTaperNeg > tolerance ? Tapered(self, selfWidth, -1, selfTaperNeg, -dOther) : sa[-1],
            };

            bool ok = true;
            Point3d X(Plane a, Plane b, Plane c)
            {
                ok &= RX.PlanePlanePlane(a, b, c, out Point3d p);
                return p;
            }

            var ks = new[] { 1, -1 };
            var P = new Dictionary<(int, int), Point3d>();
            var Q = new Dictionary<(int, int), Point3d>();
            var R = new Dictionary<(int, int), Point3d>();
            var S = new Dictionary<(int, int), Point3d>();
            var T = new Dictionary<(int, int), Point3d>();
            var U = new Dictionary<(int, int), Point3d>();
            var V = new Dictionary<(int, int), Point3d>();
            foreach (var k in ks)
                foreach (var m in ks)
                {
                    P[(k, m)] = X(lap, to[k], ts[m]);      // lap face corner
                    Q[(k, m)] = X(lap, to[k], sa[m]);      // notch wall, at the lap face, outside the self beam
                    R[(k, m)] = X(top, to[k], sa[m]);      // notch wall, above the self beam
                    S[(k, m)] = X(lap, so[k], ts[m]);      // wedge, at the lap face, at the other's side
                    T[(k, m)] = X(lap, so[k], sa[m]);      // wedge, at the lap face, outside
                    U[(k, m)] = X(bottom, so[k], sa[m]);   // wedge end, below
                    V[(k, m)] = X(bottom, so[k], ts[m]);   // wedge, below, on the taper
                }
            if (!ok) return null;

            var faces = new List<Brep>();
            void Face(params Point3d[] points)
            {
                var distinct = new List<Point3d>();
                foreach (var p in points)
                    if (distinct.All(x => x.DistanceTo(p) > tolerance)) distinct.Add(p);
                if (distinct.Count < 3) return;
                if (distinct.Count == 3 && Vector3d.CrossProduct(distinct[1] - distinct[0], distinct[2] - distinct[0]).Length < tolerance * tolerance) return;
                var brep = distinct.Count == 3
                    ? Brep.CreateFromCornerPoints(distinct[0], distinct[1], distinct[2], tolerance)
                    : Brep.CreateFromCornerPoints(distinct[0], distinct[1], distinct[2], distinct[3], tolerance);
                if (brep != null) faces.Add(brep);
            }

            // Lap face
            Face(P[(1, 1)], P[(1, -1)], P[(-1, -1)], P[(-1, 1)]);

            foreach (var k in ks)
            {
                // Notch wall on the other's tapered side k (a hexagon, in triangles that share
                // the lap face's and wedges' edges)
                Face(Q[(k, -1)], P[(k, -1)], R[(k, -1)]);
                Face(P[(k, -1)], P[(k, 1)], R[(k, 1)]);
                Face(P[(k, -1)], R[(k, 1)], R[(k, -1)]);
                Face(P[(k, 1)], Q[(k, 1)], R[(k, 1)]);
            }

            foreach (var m in ks)
            {
                // Self's tapered side m below the lap face, between the other's sides
                Face(P[(-1, m)], P[(1, m)], V[(1, m)]);
                Face(P[(-1, m)], V[(1, m)], V[(-1, m)]);
                Face(S[(-1, m)], P[(-1, m)], V[(-1, m)]);
                Face(P[(1, m)], S[(1, m)], V[(1, m)]);

                foreach (var k in ks)
                {
                    // Top of the wedge, on the lap face, and its end on the other's side
                    Face(P[(k, m)], Q[(k, m)], T[(k, m)], S[(k, m)]);
                    Face(S[(k, m)], T[(k, m)], U[(k, m)], V[(k, m)]);
                }
            }

            var joined = Brep.JoinBreps(faces, tolerance * 10);
            return joined == null || joined.Length != 1 ? null : joined[0];
        }

        private void AddLap(JointResult result, Beam beam, Brep cutter, Plane plane)
        {
            if (cutter == null)
            {
                result.Messages.Add($"{GetType().Name}: failed to create cutter for beam {beam.Id}.");
                return;
            }

            result.Add(new Lap { BeamId = beam.Id, Plane = plane, Cutters = new List<Brep> { cutter } });
        }
    }
}
