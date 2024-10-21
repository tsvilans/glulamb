using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints
{

    public class SteppedScarfJointX : JointX
    {
        public double Added = 10.0;
        public double AddedUp = 100.0;

        public double SpliceLength = 200;
        public double SpliceAngle = RhinoMath.ToRadians(15);
        public double StepWidth = 20;
        public double PinWidth = 20;

        public bool SideSplice = false;

        public double DowelEndOffset = 60;

        public Plane Beam0Plane = Plane.Unset;
        public Plane Beam1Plane = Plane.Unset;

        public Plane SplicePlane = Plane.Unset;

        public Plane End0Plane = Plane.Unset;
        public Plane End1Plane = Plane.Unset;

        public Plane SpliceOffset0Plane = Plane.Unset;
        public Plane SpliceOffset1Plane = Plane.Unset;

        public Plane SpliceEnd0Plane = Plane.Unset;
        public Plane SpliceEnd1Plane = Plane.Unset;

        public Plane Tenon0Plane = Plane.Unset;
        public Plane Tenon1Plane = Plane.Unset;

        public List<object> debug = new List<object>();

        public SteppedScarfJointX(JointX parent)
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
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("AddedUp", out double _addedup)) AddedUp = _addedup;

            if (values.TryGetValue("SpliceLength", out double _splicelength)) SpliceLength = _splicelength;
            if (values.TryGetValue("SpliceAngle", out double _spliceangle)) SpliceAngle = _spliceangle;
            if (values.TryGetValue("SideSplice", out double _sidesplice)) SideSplice = _sidesplice > 0;

            if (values.TryGetValue("StepWidth", out double _stepwidth)) StepWidth = _stepwidth;
            if (values.TryGetValue("PinWidth", out double _pinwidth)) PinWidth = _pinwidth;

            if (values.TryGetValue("DowelEndOffset", out double _dowelendoffset)) DowelEndOffset = _dowelendoffset;
        }

        public override List<object> GetDebugList()
        {
            return debug;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            for (int i = 0; i < Parts.Count; ++i)
            {
                Parts[i].Geometry.Clear();
            }

            var beam0 = beams[Parts[0].ElementIndex];
            var beam1 = beams[Parts[1].ElementIndex];

            var beam0Direction = Parts[0].Direction;
            var beam1Direction = Parts[1].Direction;

            Beam0Plane = beam0.GetPlane(Parts[0].Parameter);
            Beam1Plane = beam1.GetPlane(Parts[1].Parameter);

            var beam0Y = Beam0Plane.YAxis;
            var beam1Y = Beam0Plane.YAxis * Beam1Plane.YAxis < 0 ? -Beam1Plane.YAxis : Beam1Plane.YAxis;

            double beam0Width = beam0.Width, beam0Height = beam1.Height;
            double beam1Width = beam1.Width, beam1Height = beam1.Height;

            Vector3d xAxis = Beam0Plane.XAxis, yAxis = Beam0Plane.YAxis;

            if (SideSplice)
            {
                xAxis = Beam0Plane.YAxis;
                yAxis = Beam0Plane.XAxis;
                beam0Width = beam1.Height;
                beam0Height = beam1.Width;
            }

            if (GluLamb.Utility.ClosestDimension2D(Beam0Plane, Beam1Plane.XAxis) > 0)
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            //debug.Add(SplicePlane);

            End0Plane = new Plane(Beam0Plane.Origin - beam0Direction * (SpliceLength * 0.5), xAxis, yAxis);

            Vector3d slaveX = GluLamb.Utility.ClosestAxis(Beam1Plane, End0Plane.XAxis);
            Vector3d slaveY = GluLamb.Utility.ClosestAxis(Beam1Plane, End0Plane.YAxis);

            End1Plane = new Plane(Beam1Plane.Origin - beam1Direction * (SpliceLength * 0.5), slaveX, slaveY);
            SplicePlane = GluLamb.Interpolation.InterpolatePlanes2(End0Plane, End1Plane, 0.5);

            var Side0Plane = new Plane(SplicePlane.Origin + SplicePlane.XAxis * (Math.Max(beam0Width, beam1Width) * 0.5 + Added), SplicePlane.ZAxis, SplicePlane.YAxis);
            var Side1Plane = new Plane(SplicePlane.Origin - SplicePlane.XAxis * (Math.Max(beam0Width, beam1Width) * 0.5 + Added), SplicePlane.ZAxis, SplicePlane.YAxis);

            var TopPlane = new Plane(SplicePlane.Origin + SplicePlane.YAxis * (Math.Max(beam0Height, beam1Height) * 0.5 + Added), SplicePlane.ZAxis, SplicePlane.XAxis);
            var BottomPlane = new Plane(SplicePlane.Origin - SplicePlane.YAxis * (Math.Max(beam0Height, beam1Height) * 0.5 + Added), SplicePlane.ZAxis, SplicePlane.XAxis);

            SplicePlane.Transform(Transform.Rotation(Math.PI * 0.5 + SpliceAngle, SplicePlane.XAxis, SplicePlane.Origin));

            var tanOffset = SpliceLength * 0.5 * Math.Tan(SpliceAngle) * Math.Tan(SpliceAngle);

            SpliceEnd0Plane = new Plane(End0Plane.Origin + End0Plane.ZAxis * tanOffset, SplicePlane.XAxis, SplicePlane.ZAxis);
            SpliceEnd1Plane = new Plane(End1Plane.Origin - End0Plane.ZAxis * tanOffset, SplicePlane.XAxis, SplicePlane.ZAxis);

            SpliceOffset0Plane = new Plane(SplicePlane.Origin + SplicePlane.ZAxis * StepWidth * 0.5, SplicePlane.XAxis, SplicePlane.YAxis);
            SpliceOffset1Plane = new Plane(SplicePlane.Origin - SplicePlane.ZAxis * StepWidth * 0.5, SplicePlane.XAxis, SplicePlane.YAxis);

            Tenon0Plane = new Plane(SplicePlane.Origin + SplicePlane.YAxis * PinWidth * 0.5, SplicePlane.XAxis, SplicePlane.ZAxis);
            Tenon1Plane = new Plane(SplicePlane.Origin - SplicePlane.YAxis * PinWidth * 0.5, SplicePlane.XAxis, SplicePlane.ZAxis);


            var points = new Point3d[8];

            RX.PlanePlanePlane(Side0Plane, BottomPlane, SpliceEnd0Plane, out points[0]);
            RX.PlanePlanePlane(Side0Plane, SpliceEnd0Plane, SpliceOffset1Plane, out points[1]);
            RX.PlanePlanePlane(Side0Plane, SpliceOffset1Plane, Tenon0Plane, out points[2]);
            RX.PlanePlanePlane(Side0Plane, Tenon0Plane, SpliceOffset0Plane, out points[3]);

            RX.PlanePlanePlane(Side0Plane, SpliceOffset1Plane, Tenon1Plane, out points[4]);
            RX.PlanePlanePlane(Side0Plane, Tenon1Plane, SpliceOffset0Plane, out points[5]);

            RX.PlanePlanePlane(Side0Plane, SpliceOffset0Plane, SpliceEnd1Plane, out points[6]);
            RX.PlanePlanePlane(Side0Plane, SpliceEnd1Plane, TopPlane, out points[7]);

            var pointsLow = new Point3d[8];

            RX.PlanePlanePlane(Side1Plane, BottomPlane, SpliceEnd0Plane, out pointsLow[0]);
            RX.PlanePlanePlane(Side1Plane, SpliceEnd0Plane, SpliceOffset1Plane, out pointsLow[1]);
            RX.PlanePlanePlane(Side1Plane, SpliceOffset1Plane, Tenon0Plane, out pointsLow[2]);
            RX.PlanePlanePlane(Side1Plane, Tenon0Plane, SpliceOffset0Plane, out pointsLow[3]);

            RX.PlanePlanePlane(Side1Plane, SpliceOffset1Plane, Tenon1Plane, out pointsLow[4]);
            RX.PlanePlanePlane(Side1Plane, Tenon1Plane, SpliceOffset0Plane, out pointsLow[5]);

            RX.PlanePlanePlane(Side1Plane, SpliceOffset0Plane, SpliceEnd1Plane, out pointsLow[6]);
            RX.PlanePlanePlane(Side1Plane, SpliceEnd1Plane, TopPlane, out pointsLow[7]);

            debug.AddRange(points.Select(x => new GH_Point(x)));
            debug.AddRange(pointsLow.Select(x => new GH_Point(x)));

            double tolerance = 0.001;

            var spliceGeo0 = new Brep[5];
            spliceGeo0[0] = Brep.CreateFromCornerPoints(points[0], points[1], pointsLow[1], pointsLow[0], tolerance);
            spliceGeo0[1] = Brep.CreateFromCornerPoints(points[1], points[4], pointsLow[4], pointsLow[1], tolerance);
            if (StepWidth > 0)
            {
                spliceGeo0[2] = Brep.CreateFromCornerPoints(points[4], points[5], pointsLow[5], pointsLow[4], tolerance);
            }
            spliceGeo0[3] = Brep.CreateFromCornerPoints(points[5], points[6], pointsLow[6], pointsLow[5], tolerance);
            spliceGeo0[4] = Brep.CreateFromCornerPoints(points[6], points[7], pointsLow[7], pointsLow[6], tolerance);

            var spliceGeo1 = new Brep[5];
            spliceGeo1[0] = Brep.CreateFromCornerPoints(points[6], points[7], pointsLow[7], pointsLow[6], tolerance);
            spliceGeo1[1] = Brep.CreateFromCornerPoints(points[3], points[6], pointsLow[6], pointsLow[3], tolerance);
            if (StepWidth > 0)
            {
                spliceGeo1[2] = Brep.CreateFromCornerPoints(points[2], points[3], pointsLow[3], pointsLow[2], tolerance);
            }
            spliceGeo1[3] = Brep.CreateFromCornerPoints(points[1], points[2], pointsLow[2], pointsLow[1], tolerance);
            spliceGeo1[4] = Brep.CreateFromCornerPoints(points[0], points[1], pointsLow[1], pointsLow[0], tolerance);


            // debug.Add(new GH_Plane(End0Plane));
            // debug.Add(new GH_Plane(End1Plane));

            // debug.Add(new GH_Plane(TopPlane));
            // debug.Add(new GH_Plane(BottomPlane));

            // debug.Add(new GH_Plane(Tenon0Plane));
            // debug.Add(new GH_Plane(Tenon1Plane));

            // debug.Add(new GH_Plane(SpliceEnd0Plane));
            // debug.Add(new GH_Plane(SpliceEnd1Plane));

            // debug.Add(new GH_Plane(SpliceOffset0Plane));
            // debug.Add(new GH_Plane(SpliceOffset1Plane));

            // debug.Add(new GH_Plane(Side0Plane));
            // debug.Add(new GH_Plane(Side1Plane));

            // debug.Add(new GH_Plane(SplicePlane));

            debug.AddRange(spliceGeo1);

            var spliceGeo0Joined = Brep.JoinBreps(spliceGeo0, 0.001);
            var spliceGeo1Joined = Brep.JoinBreps(spliceGeo1, 0.001);

            double width = Math.Max(beam0Width, beam1Width);
            double height = Math.Max(beam0Height, beam1Height);
            double tenonWidth = Math.Min(beam0Width, beam1Width);
            double spliceHeight = Math.Min(beam0Height, beam1Height);


            Parts[0].Geometry.AddRange(spliceGeo0Joined);
            Parts[1].Geometry.AddRange(spliceGeo1Joined);

            // Dowels
            var dowelSpan = End1Plane.Origin - End0Plane.Origin;
            var dowelSpacing = dowelSpan.Length - DowelEndOffset * 2;
            dowelSpan.Unitize();

            var dowelZ = beam0Y;
            // dowelZ = SplicePlane.ZAxis;

            for (int i = 0; i < 2; ++i)
            {
                var dowelOrigin = End0Plane.Origin + dowelSpan * (DowelEndOffset + i * dowelSpacing)
                    - dowelZ * (beam0Height + Added);
                var dowel = new Cylinder(
                    new Circle(
                        new Plane(dowelOrigin, dowelZ), 8), beam0Height + beam1Height + Added * 2).ToBrep(true, true);

                Parts[0].Geometry.Add(dowel);
                Parts[1].Geometry.Add(dowel);
            }

            return 0;
        }
    }
}
