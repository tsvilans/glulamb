using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Keyed (stop-splayed) scarf between two aligned beam ends. Port of SteppedScarfJointX. The
    /// scarf face slopes along the splice and has a step in the middle; the two beams' steps are
    /// PinWidth apart, leaving a slot across the beams for a key that is driven in to pull the
    /// joint tight. The ends of the scarf and the step are square to the scarf face, so the
    /// beams go together square to it (SquareEnds cuts the ends square to the beams instead).
    /// With StepWidth 0 it is a plain sloped scarf, which also covers SpliceJoint_Lap1 (its
    /// BackCut is the default here). Two dowels pin the splice. Each beam is cut by one open
    /// surface (a Lap feature) and the key is reported as hardware.
    /// </summary>
    [JointType("glulamb.splice-scarf-keyed", Name = "Keyed scarf splice", Arity = 2, Topology = JointTopology.Splice,
        Description = "Two aligned beam ends joined with a sloped, stepped scarf, a key and dowels.")]
    public class SteppedScarfJoint : JointBase
    {
        [JointParameter(Description = "Extra width added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Extra height added to cutters above and below the beams.", Unit = "length")]
        public double AddedUp { get; set; } = 100.0;

        [JointParameter(Description = "Length of the scarf along the beams.", Unit = "length")]
        public double SpliceLength { get; set; } = 200;

        [JointParameter(Description = "Slope of the scarf face to the beam axis.", Unit = "radians")]
        public double SpliceAngle { get; set; } = Rhino.RhinoMath.ToRadians(15);

        [JointParameter(Description = "Height of the step in the scarf face (the key thickness). 0 = a plain sloped scarf.", Unit = "length")]
        public double StepWidth { get; set; } = 20;

        [JointParameter(Description = "Width of the key slot between the two steps. 0 = no key.", Unit = "length")]
        public double PinWidth { get; set; } = 20;

        [JointParameter(Description = "Scarf through the width of the beams instead of the height.")]
        public bool SideSplice { get; set; } = false;

        [JointParameter(Description = "Cut the ends of the scarf square to the beams instead of square to the scarf face.")]
        public bool SquareEnds { get; set; } = false;

        [JointParameter(Description = "Run the dowels square to the scarf face instead of square to the beams.")]
        public bool DowelsSquareToScarf { get; set; } = false;

        [JointParameter(Description = "Distance of the dowels from the ends of the scarf.", Unit = "length")]
        public double DowelEndOffset { get; set; } = 60;

        [JointParameter(Description = "Dowel diameter. 0 means no dowels.", Unit = "length")]
        public double DowelDiameter { get; set; } = 16;

        [JointParameter(Description = "Largest angle between the beams that the splice will handle.", Unit = "radians")]
        public double MaximumAngle { get; set; } = Rhino.RhinoMath.ToRadians(30.0);

        public SteppedScarfJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            var beam0 = beams[0];
            var beam1 = beams[1];
            var tolerance = context.Tolerance;

            var dir0 = m_parts[0].Direction;
            var dir1 = m_parts[1].Direction;
            var plane0 = beam0.GetPlane(m_parts[0].Parameter);
            var plane1 = beam1.GetPlane(m_parts[1].Parameter);

            var divergence = Vector3d.VectorAngle(dir0, -dir1);
            if (divergence == Rhino.RhinoMath.UnsetValue) divergence = 0;
            if (divergence > MaximumAngle)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: beams diverge by {Rhino.RhinoMath.ToDegrees(divergence):0.0}°, " +
                    $"more than MaximumAngle ({Rhino.RhinoMath.ToDegrees(MaximumAngle):0.0}°).");
                return;
            }

            // Splice frame: along from beam 0 to beam 1, up through the scarf (the section's Y,
            // or X for a side splice), across the scarf
            var along = dir0 - dir1;
            along.Unitize();
            var up0 = SideSplice ? plane0.XAxis : plane0.YAxis;
            var frame0 = AlignSection(beam0, plane0, along, up0, out double w0, out double h0);
            var frame1 = AlignSection(beam1, plane1, along, frame0.YAxis, out double w1, out double h1);

            var up = frame0.YAxis + frame1.YAxis;
            up -= along * (up * along);
            up.Unitize();
            var across = Vector3d.CrossProduct(up, along);
            var mid = (plane0.Origin + plane1.Origin) * 0.5;

            double width = Math.Max(w0, w1), height = Math.Max(h0, h1);
            var drift = SpliceLength * Math.Tan(divergence);
            var halfWidth = width * 0.5 / Math.Cos(divergence) + drift + Added;
            var yTop = height * 0.5 / Math.Cos(divergence) + drift + AddedUp;

            // Which beam is below the scarf face: beam 0 when the higher beam is beam 1 or they
            // are level; Flip inverts that. s mirrors the profile through the splice axis.
            double s = TopPart(plane0.Origin, plane1.Origin, up, tolerance) == 1 ? 1.0 : -1.0;

            // Profile coordinates: t along the scarf face, o square to it (from its centre line)
            var a = SpliceAngle;
            var d2 = (z: Math.Cos(a), y: -Math.Sin(a));
            var n2 = (z: Math.Sin(a), y: Math.Cos(a));
            Point3d AtZY(double z, double y, double x) => mid + along * z + up * (s * y) + across * x;
            Point3d At(double t, double o, double x) => AtZY(d2.z * t + n2.z * o, d2.y * t + n2.y * o, x);

            // The ends of the scarf (e = -1 at beam 0, 1 at beam 1): square to the scarf face
            // through its centre line at the ends of SpliceLength, or square to the beams
            var tEnd = SpliceLength * 0.5 / Math.Cos(a);
            Point3d EndAtO(int e, double o, double x) => SquareEnds
                ? At((e * SpliceLength * 0.5 - n2.z * o) / d2.z, o, x)
                : At(e * tEnd, o, x);
            Point3d EndAtY(int e, double y, double x) => SquareEnds
                ? AtZY(e * SpliceLength * 0.5, y, x)
                : At(e * tEnd, (y - d2.y * e * tEnd) / n2.y, x);

            var step = StepWidth * 0.5;
            Polyline Profile(double sigma, double x)
            {
                var p = new Polyline
                {
                    EndAtY(-1, yTop, x),
                    EndAtO(-1, -step, x),
                    At(sigma * PinWidth * 0.5, -step, x),
                    At(sigma * PinWidth * 0.5, step, x),
                    EndAtO(1, step, x),
                    EndAtY(1, -yTop, x),
                };
                p.DeleteShortSegments(tolerance);
                return p;
            }

            // Beam 0 (below the scarf face) steps up after the key slot, beam 1 before it, so
            // the slot is left between them
            Brep Cutter(double sigma)
            {
                var loft = Brep.CreateFromLoft(new Curve[] { Profile(sigma, -halfWidth).ToNurbsCurve(), Profile(sigma, halfWidth).ToNurbsCurve() },
                    Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (loft == null || loft.Length < 1) return null;
                loft[0].Faces.SplitKinkyFaces();
                return loft[0];
            }

            var cutter0 = Cutter(1);
            var cutter1 = Cutter(-1);
            if (cutter0 == null || cutter1 == null)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the scarf surfaces.");
                return;
            }

            Position = new Plane(mid, across, up);
            var splicePlane = new Plane(mid, across, along);

            result.Add(new Lap { BeamId = beam0.Id, Plane = splicePlane, Length = SpliceLength, Cutters = new List<Brep> { cutter0 } });
            result.Add(new Lap { BeamId = beam1.Id, Plane = splicePlane, Length = SpliceLength, Cutters = new List<Brep> { cutter1 } });

            // Key, in the slot between the steps
            if (StepWidth > tolerance && PinWidth > tolerance)
            {
                var keyPlane = new Plane(At(0, 0, 0), At(1, 0, 0) - At(0, 0, 0), At(0, 1, 0) - At(0, 0, 0));
                var key = new Box(keyPlane, new Interval(-PinWidth * 0.5, PinWidth * 0.5), new Interval(-step, step), new Interval(-width * 0.5, width * 0.5));
                result.Hardware.Add(new KeyItem(key, beam0.Id, beam1.Id));
            }

            // Each beam has to reach the far end of the scarf, across its full section
            IEnumerable<Point3d> EndCorners(int e) =>
                new[] { -1.0, 1.0 }.SelectMany(sy => new[] { -1.0, 1.0 }.Select(sx => EndAtY(e, sy * height * 0.5, sx * width * 0.5)));
            ExtendToReach(result, beam0, m_parts[0], EndCorners(1));
            ExtendToReach(result, beam1, m_parts[1], EndCorners(-1));

            // Dowels through both beams, square to the beams or to the scarf face, crossing the
            // scarf face DowelEndOffset from the ends
            if (DowelDiameter > 0)
            {
                var dir = DowelsSquareToScarf ? along * n2.z + up * (s * n2.y) : up;
                var du = dir * up;
                var reach = yTop - AddedUp + Added;
                foreach (var z in new[] { -SpliceLength * 0.5 + DowelEndOffset, SpliceLength * 0.5 - DowelEndOffset })
                {
                    var centre = At(z / d2.z, 0, 0);
                    var yc = (centre - mid) * up;
                    var axis = new Line(centre + dir * ((-reach - yc) / du), centre + dir * ((reach - yc) / du));
                    result.Add(new Drilling(beam0.Id, axis, DowelDiameter));
                    result.Add(new Drilling(beam1.Id, axis, DowelDiameter));

                    // The dowel itself spans from the lowest to the highest beam face it crosses
                    double lo = double.MaxValue, hi = double.MinValue;
                    foreach (var (o, h) in new[] { (frame0.Origin, h0), (frame1.Origin, h1) })
                    {
                        var yb = (o - mid) * up;
                        lo = Math.Min(lo, (yb - h * 0.5 - yc) / du);
                        hi = Math.Max(hi, (yb + h * 0.5 - yc) / du);
                    }
                    result.Hardware.Add(new DowelItem(new Line(centre + dir * lo, centre + dir * hi), DowelDiameter, beam0.Id, beam1.Id));
                }
            }

            result.Status = JointStatus.Ok;
        }
    }
}
