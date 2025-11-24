using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints
{
    public class CornerJoint3X : JointX
    {

        public double Inset = 3;
        public double InsetIn = 15;
        public double AngleLimit = 0.1;
        public double FilletRadius = 11;
        public double Tolerance = 1e-3;

        public double BackOffset = 100;
        public bool Reverse = false;

        public List<object> debug = new List<object>();

        public CornerJoint3X(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!(JointPartX.IsAtEnd(parent.Parts[0].Case) && JointPartX.IsAtEnd(parent.Parts[1].Case)))
                throw new ArgumentException($"{GetType().Name} requires intersection at the ends.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override void Configure(Dictionary<string, double> values)
        {
            if (values.TryGetValue("BackOffset", out double _backoffset)) BackOffset = _backoffset;
            if (values.TryGetValue("Inset", out double _inset)) Inset = _inset;
            if (values.TryGetValue("InsetIn", out double _insetin)) InsetIn = _insetin;
            if (values.TryGetValue("AngleLimit", out double _anglelimit)) AngleLimit = _anglelimit;
            if (values.TryGetValue("Reverse", out double _reverse)) Reverse = _reverse <= 0;
            if (values.TryGetValue("FilletRadius", out double _filletradius)) FilletRadius = _filletradius;
            if (values.TryGetValue("Tolerance", out double _tolerance)) Tolerance = _tolerance;
        }

        public override List<object> GetDebugList()
        {
            return debug;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            var b0 = beams[Parts[0].ElementIndex];
            var b1 = beams[Parts[1].ElementIndex];

            var v0 = -Parts[0].Direction;
            var v1 = -Parts[1].Direction;

            var origin = Position.Origin;
            // var angle = Vector3d.VectorAngle(v0, -v1);
            var angle = Math.Acos(v0 * -v1);


            // Check against beam-to-beam cross-section orientation, 
            // maybe b0.X is aligned with b1.Y, etc.
            var max_width = Math.Max(b0.Width, b1.Width);
            var max_height = Math.Max(b0.Height, b1.Height);

            var max_width_local = max_width;

            var normal = Vector3d.CrossProduct(v0, v1);
            normal.Unitize();

            var reverse = Reverse;
            if (reverse)
            {
                normal.Reverse();
            }

            var binormal = v0 + v1;
            binormal.Unitize();

            debug.Add(new Line(origin, binormal, 400));

            var plane = new Plane(origin, v0, v1);
            var mirror_plane = new Plane(origin, binormal, normal);

            var in0 = Vector3d.CrossProduct(v0, normal);
            if (in0 * binormal < 0) in0.Reverse();
            in0.Unitize();

            var in1 = Vector3d.CrossProduct(v1, normal);
            if (in1 * binormal < 0) in1.Reverse();
            in1.Unitize();

            var p0 = new Plane(origin + v0 * BackOffset, normal, in0);
            var p1 = new Plane(origin + v1 * BackOffset, normal, in1);

            debug.Add(new Line(origin, v0, 300));
            debug.Add(new Line(origin, v1, 300));

            var chamfer = new Plane(
                ((origin - v0 * BackOffset) + (origin - v1 * BackOffset)) * 0.5,
                normal,
                Vector3d.CrossProduct(normal, binormal)
            );

            var pout0 = new Plane(p0.Origin - p0.YAxis * (b0.Width * 0.5 - Inset), p0.ZAxis, normal);
            var pout1 = new Plane(p1.Origin - p1.YAxis * (b1.Width * 0.5 - Inset), p1.ZAxis, normal);

            var pout0_added = new Plane(pout0.Origin - p0.YAxis * 10, pout0.XAxis, pout0.YAxis);
            var pout1_added = new Plane(pout1.Origin - p1.YAxis * 10, pout1.XAxis, pout1.YAxis);

            var pin0 = new Plane(p0.Origin + p0.YAxis * (b0.Width * 0.5 - InsetIn), p0.ZAxis, normal);
            var pin1 = new Plane(p1.Origin + p1.YAxis * (b1.Width * 0.5 - InsetIn), p1.ZAxis, normal);

            var pin0_partial = new Plane(p0.Origin + p0.YAxis * (b0.Width * 0.3 - InsetIn), p0.ZAxis, normal);
            var pin1_partial = new Plane(p1.Origin + p1.YAxis * (b1.Width * 0.3 - InsetIn), p1.ZAxis, normal);

            var pin0_added = new Plane(pin0.Origin + p0.YAxis * 10, pin0.XAxis, pin0.YAxis);
            var pin1_added = new Plane(pin1.Origin + p1.YAxis * 10, pin1.XAxis, pin1.YAxis);


            // Quick maths
            var x0 = (b0.Width * 0.5 - Inset) / Math.Cos(angle);
            var z0 = (b1.Width * 0.5 - Inset * 2) * Math.Tan(Math.PI * 0.5 - angle);

            // Tenon 0
            Point3d[] points;
            int idx = 0;

            if (BackOffset < Math.Abs(z0))
            {
                points = new Point3d[10];
                RX.PlanePlanePlane(plane, p0, pout0_added, out points[idx]); idx++;
                RX.PlanePlanePlane(plane, p0, pout0, out points[idx]); idx++;

                RX.PlanePlanePlane(plane, pout0, pout1, out points[idx]); idx++;

                RX.PlanePlanePlane(plane, pout1, p1, out points[idx]); idx++;

                if (Math.Abs(angle) > AngleLimit)
                {
                    RX.PlanePlanePlane(plane, pin0, p1, out points[idx]); idx++;
                }
                else
                {
                    RX.PlanePlanePlane(plane, pin0_partial, p1, out points[idx]); idx++;
                }
            }
            else
            {
                points = new Point3d[10];
                RX.PlanePlanePlane(plane, p0, pout0_added, out points[idx]); idx++;
                RX.PlanePlanePlane(plane, pin1, pout0, out points[idx]); idx++;

                if (BackOffset < Math.Abs(x0))
                {
                    RX.PlanePlanePlane(plane, pout0, chamfer, out points[idx]); idx++;
                    RX.PlanePlanePlane(plane, chamfer, pout1, out points[idx]); idx++;
                }
                else
                {
                    RX.PlanePlanePlane(plane, pout0, pout1, out points[idx]); idx++;
                }

                RX.PlanePlanePlane(plane, pout1, pin0, out points[idx]); idx++;
            }

            RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;

            points[0] = points[1] - in0 * 20 + v0 * 20;
            points[idx] = points[idx - 1] + binormal * (InsetIn / Math.Cos(angle * 0.5) + 20); idx++;


            Curve tenon0_outline = new Polyline(points.Take(idx)).ToNurbsCurve();
            tenon0_outline = Curve.CreateFilletCornersCurve(
                tenon0_outline, FilletRadius, Tolerance,
                                RhinoDoc.ActiveDoc.ModelAngleToleranceRadians);

            idx = 0;

            // Mortise 0
            if (BackOffset < Math.Abs(z0))
            {
                points = new Point3d[10];
                RX.PlanePlanePlane(plane, p1, pout1_added, out points[0]); idx++;
                RX.PlanePlanePlane(plane, p1, pout1, out points[1]); idx++;
                points[0] = points[1] - in1 * 20 + v1 * 20;


                if (Math.Abs(angle) > AngleLimit)
                {
                    RX.PlanePlanePlane(plane, p1, pin0, out points[idx]); idx++;
                    RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;
                }
                else
                {
                    RX.PlanePlanePlane(plane, p1, pin0_partial, out points[idx]); idx++;
                    RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;
                }
            }
            else
            {
                points = new Point3d[10];
                // RX.PlanePlanePlane(plane, p1, pout1_added, out points[0]); idx++;
                RX.PlanePlanePlane(plane, pin0, pout1, out points[1]); idx++;
                points[0] = points[idx] - in1 * 20 + v1 * 20; idx++;

                // RX.PlanePlanePlane(plane, p1, pin0, out points[3]);
                RX.PlanePlanePlane(plane, pin0, pin1, out points[idx]); idx++;
            }

            points[idx] = points[idx - 1] + binormal * (InsetIn / Math.Cos(angle * 0.5) + 20); idx++;

            Curve mortise0_outline = new Polyline(points.Take(idx)).ToNurbsCurve();
            mortise0_outline = Curve.CreateFilletCornersCurve(
                mortise0_outline, FilletRadius, Tolerance,
                RhinoDoc.ActiveDoc.ModelAngleToleranceRadians);

            var mirror = Transform.Mirror(mirror_plane);

            var mortise1_outline = mortise0_outline.DuplicateCurve();
            var tenon1_outline = tenon0_outline.DuplicateCurve();

            mortise1_outline.Transform(mirror);
            tenon1_outline.Transform(mirror);

            Parts[0].Data.Clear();
            Parts[1].Data.Clear();
            Parts[0].Geometry.Clear();
            Parts[1].Geometry.Clear();

            Parts[0].Data.Set("MortiseOutline", mortise1_outline);
            Parts[1].Data.Set("MortiseOutline", mortise0_outline);

            Parts[0].Data.Set("TenonOutline", tenon0_outline);
            Parts[1].Data.Set("TenonOutline", tenon1_outline);

            Parts[0].Geometry = ConstructGeometry(mortise1_outline, tenon0_outline, normal).ToList();
            Parts[1].Geometry = ConstructGeometry(mortise0_outline, tenon1_outline, -normal).ToList();

            var all_planes = new Plane[]{
                p0,
                p1,
                pin0,
                pin1,
                pout0,
                pout1,
                pin0_added,
                pin1_added,
                pout0_added,
                pout1_added,
            };

            return 0;
        }

        private Brep[] ConstructGeometry(Curve c0, Curve c1, Vector3d up, double height = 120, double tolerance = 1e-3)
        {
            var curves = new Curve[] { c0, c1 };
            var trims = new Curve[2];

            var res = Rhino.Geometry.Intersect.Intersection.CurveCurve(curves[0], curves[1], 1e-3, 1e-3);

            var spans = new List<Interval>[] { new List<Interval>(), new List<Interval>() };

            for (int i = 0; i < res.Count; ++i)
            {
                spans[0].Add(res[i].OverlapA);
                spans[1].Add(res[i].OverlapB);
            }


            for (int i = 0; i < 2; ++i)
            {
                var subs = Utility.SpanSubtract(curves[i].Domain, spans[i]);
                if (subs.Count < 1)
                {
                    trims[i] = curves[i].DuplicateCurve();
                    continue;
                }

                var longest = subs.OrderBy(x => x.Length).Last();
                trims[i] = curves[i].Trim(longest);
            }

            var faces = new List<Brep>();

            var middle = Brep.CreatePlanarBreps(trims, tolerance);
            if (middle != null && middle.Length > 0)
                faces.AddRange(middle);

            // faces[0] = Brep.CreatePlanarBreps(trims).FirstOrDefault();
            faces.Add(Extrusion.CreateExtrusion(curves[0], up * height).ToBrep());
            faces.Add(Extrusion.CreateExtrusion(curves[1], up * -height).ToBrep());

            return Brep.JoinBreps(faces, tolerance);
        }
    }
}
