using Rhino.Geometry;

namespace GluLamb.Reconstruction.Test
{
    internal static partial class Program
    {
        /// <summary>
        /// Cut both ends of a beam's Brep with planes turned 35 degrees about the section's X-axis,
        /// 100 mm in from the ends, so that sections near the ends lose a corner.
        /// </summary>
        static Brep SkewEnds(Beam beam, Brep brep, out bool cut)
        {
            var cutters = new List<Brep>();
            foreach (bool atStart in new[] { true, false })
            {
                var curve = beam.Centreline;
                double length = curve.GetLength();
                curve.LengthParameter(atStart ? 100 : length - 100, out double t);
                var frame = beam.GetPlane(t);

                var y = frame.YAxis;
                y.Rotate(Rhino.RhinoMath.ToRadians(35), frame.XAxis);
                var plane = new Plane(frame.Origin, frame.XAxis, y); // its Z is the section's Z turned by 35 degrees

                double w = beam.Width, h = beam.Height;
                var outward = atStart ? new Interval(-2000, 0) : new Interval(0, 2000);
                cutters.Add(new Box(plane, new Interval(-2 * w, 2 * w), new Interval(-2 * h, 2 * h), outward).ToBrep());
            }

            var result = Brep.CreateBooleanDifference(new[] { brep }, cutters, 0.01);
            cut = result != null && result.Length == 1;
            return cut ? result[0] : brep;
        }
    }
}
