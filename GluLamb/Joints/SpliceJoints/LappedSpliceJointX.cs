using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{

    public class LappedSpliceJointX : JointX
    {
        public double Added = 10.0;
        public double AddedUp = 100.0;
        public double Inset = 0.0;
        //public double BlindOffset = 0;
        public double SpliceLength = 200;
        public double SpliceRatio = 0.25;
        public bool SideSplice = false;

        public double DowelEndOffset = 60;

        public Plane Beam0Plane = Plane.Unset;
        public Plane Beam1Plane = Plane.Unset;

        public Plane End0Plane = Plane.Unset;
        public Plane End1Plane = Plane.Unset;

        public Plane SplicePlane = Plane.Unset;

        public List<object> debug = new List<object>();

        public LappedSpliceJointX() { }

        public LappedSpliceJointX(JointX parent)
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
            if (values.TryGetValue("Inset", out double _inset)) Inset = _inset;
            if (values.TryGetValue("SpliceLength", out double _splicelength)) SpliceLength = _splicelength;
            if (values.TryGetValue("SpliceRatio", out double _spliceratio)) SpliceRatio = _spliceratio;
            if (values.TryGetValue("SideSplice", out double _sidesplice)) SideSplice = _sidesplice > 0;

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

            var dim = Math.Abs(Beam0Plane.Project(Beam1Plane.XAxis) * xAxis) > 0.5 ? 0 : 1;


            if (dim > 0)
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            End0Plane = new Plane(Beam0Plane.Origin - beam0Direction * (SpliceLength * 0.5), xAxis, yAxis);

            Vector3d slaveX = GluLamb.Utility.ClosestAxis(Beam1Plane, End0Plane.XAxis);
            Vector3d slaveY = GluLamb.Utility.ClosestAxis(Beam1Plane, End0Plane.YAxis);

            End1Plane = new Plane(Beam1Plane.Origin - beam1Direction * (SpliceLength * 0.5), slaveX, slaveY);

            SplicePlane = GluLamb.Interpolation.InterpolatePlanes2(End0Plane, End1Plane, 0.5);

            End0Plane = new Plane(End0Plane.Origin, SplicePlane.XAxis, End0Plane.YAxis);
            End1Plane = new Plane(End1Plane.Origin, SplicePlane.XAxis, End1Plane.YAxis);

            debug.Add((End0Plane));
            debug.Add((End1Plane));
            debug.Add((SplicePlane));

            double width = Math.Max(beam0Width, beam1Width);
            double height = Math.Max(beam0Height, beam1Height);
            double tenonWidth = Math.Min(beam0Width, beam1Width);
            double spliceHeight = Math.Min(beam0Height, beam1Height);

            var topProfile = new Polyline()
            {
                End0Plane.PointAt( - width * 0.5 - Added, height * 0.5 + AddedUp, 0),
                End0Plane.PointAt( - width * 0.5 - Added, spliceHeight * SpliceRatio, 0),
                End1Plane.PointAt( - width * 0.5 - Added, -spliceHeight * SpliceRatio, 0),
                End1Plane.PointAt( - width * 0.5 - Added, -height * 0.5 - AddedUp, 0),
            };

            var bottomProfile = new Polyline()
            {
                End0Plane.PointAt( width * 0.5 + Added, height * 0.5 + AddedUp, 0),
                End0Plane.PointAt( width * 0.5 + Added, spliceHeight * SpliceRatio, 0),
                End1Plane.PointAt( width * 0.5 + Added, -spliceHeight * SpliceRatio, 0),
                End1Plane.PointAt( width * 0.5 + Added, -height * 0.5 - AddedUp, 0),
            };

            var tenonGeo = Brep.CreateFromLoft(new Curve[] { topProfile.ToNurbsCurve(), bottomProfile.ToNurbsCurve() }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (tenonGeo == null || tenonGeo.Length < 1) throw new Exception($"{GetType().Name}: tenonGeo failed.");

            tenonGeo[0].Faces.SplitKinkyFaces();

            Parts[0].Geometry.AddRange(tenonGeo);
            Parts[1].Geometry.AddRange(tenonGeo);

            // Dowels
            var dowelSpan = End1Plane.Origin - End0Plane.Origin;
            var dowelSpacing = dowelSpan.Length - DowelEndOffset * 2;
            dowelSpan.Unitize();

            for (int i = 0; i < 2; ++i)
            {
                var dowelOrigin = End0Plane.Origin + dowelSpan * (DowelEndOffset + i * dowelSpacing)
                    - SplicePlane.YAxis * (beam0Height + Added);
                var dowel = new Cylinder(
                    new Circle(
                        new Plane(dowelOrigin, SplicePlane.YAxis), 8), beam0Height + beam1Height + Added * 2).ToBrep(true, true);

                Parts[0].Geometry.Add(dowel);
                Parts[1].Geometry.Add(dowel);
            }

            return 0;
        }
    }
}
