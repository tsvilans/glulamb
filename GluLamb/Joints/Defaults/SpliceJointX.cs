using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class SpliceJointX : JointX
    {
        public double Added = 10.0;
        public double SpliceLength = 200;
        public double SpliceAngle = 0;

        // TODO : Implement side splice
        public bool SideSplice = false;

        public Plane Beam0Plane = Plane.Unset;
        public Plane Beam1Plane = Plane.Unset;

        public Plane End0Plane = Plane.Unset;
        public Plane End1Plane = Plane.Unset;

        public Plane LapPlane = Plane.Unset;
        public Vector3d Normal = Vector3d.Unset;
        public Vector3d Binormal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public SpliceJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!(JointPartX.IsAtEnd(parent.Parts[0].Case) && JointPartX.IsAtEnd(parent.Parts[1].Case)))
                throw new ArgumentException($"{GetType().Name} requires intersection at the ends.");

            Parts = parent.Parts;
            Position = parent.Position;
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

            Normal = (beam0Y + beam1Y) * 0.5;
            Binormal = Vector3d.CrossProduct(Normal, beam0Y);

            //var beam0SideDirection = ClosestAxis(Beam0Plane, beam1Direction);
            //var beam1SideDirection = ClosestAxis(Beam1Plane, beam0Direction);

            double beam0Width, beam0Height;
            if (Math.Abs(Beam0Plane.XAxis * Beam0Plane.XAxis) > Math.Abs(Beam0Plane.XAxis * Beam0Plane.YAxis))
            {
                beam0Width = beam0.Width;
                beam0Height = beam0.Height;
            }
            else
            {
                beam0Width = beam0.Height;
                beam0Height = beam0.Width;
            }

            double beam1Width, beam1Height;
            if (Math.Abs(Beam0Plane.XAxis * Beam1Plane.XAxis) > Math.Abs(Beam0Plane.XAxis * Beam1Plane.YAxis))
            {
                beam1Width = beam1.Width;
                beam1Height = beam1.Height;
            }
            else
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            End0Plane = new Plane(Beam0Plane.Origin - beam0Direction * (SpliceLength * 0.5), Beam0Plane.XAxis, Normal);
            End1Plane = new Plane(Beam1Plane.Origin - beam1Direction * (SpliceLength * 0.5), Beam1Plane.XAxis, Normal);

            var LapOrigin = Interpolation.Lerp(Beam0Plane.Origin, Beam1Plane.Origin,
            (beam0Height) / (beam1Height + beam0Height));

            debug.Add(new GH_Point(LapOrigin));

            LapPlane = new Plane(LapOrigin, Normal, Binormal);
            End0Plane.ClosestParameter(LapOrigin, out double u0, out double v0);
            End1Plane.ClosestParameter(LapOrigin, out double u1, out double v1);

            var diff = Math.Abs(v1 - v0);
            var angleOffset = Math.Tan(SpliceAngle) * SpliceLength * 0.5;


            var points = new Point3d[]
            {
                End0Plane.PointAt(-(beam0Width * 0.5 + Added), v0 + angleOffset),
                End0Plane.PointAt(beam0Width * 0.5 + Added, v0 + angleOffset),
                End1Plane.PointAt(beam0Width * 0.5 + Added, v1 - angleOffset),
                End1Plane.PointAt(-(beam0Width * 0.5 + Added), v1 - angleOffset),
            };

            var offsetPoints = new Point3d[]
            {
                points[0] + End0Plane.YAxis * (beam0Height * 0.5 + Added + Math.Abs(v1)),
                points[1] + End0Plane.YAxis * (beam0Height * 0.5 + Added + Math.Abs(v1)),
                points[2] - End1Plane.YAxis * (beam1Height * 0.5 + Added + Math.Abs(v0)),
                points[3] - End1Plane.YAxis * (beam1Height * 0.5 + Added + Math.Abs(v0)),
            };

            var spliceGeo = new Brep[3];
            spliceGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            spliceGeo[1] = Brep.CreateFromCornerPoints(points[0], points[1], offsetPoints[1], offsetPoints[0], 0.001);
            spliceGeo[2] = Brep.CreateFromCornerPoints(offsetPoints[2], offsetPoints[3], points[3], points[2], 0.001);

            var spliceGeoJoined = Brep.JoinBreps(spliceGeo, 0.001);
            if (spliceGeoJoined == null) throw new Exception($"{GetType().Name}: spliceGeoJoined failed.");

            Parts[0].Geometry.AddRange(spliceGeoJoined);
            Parts[1].Geometry.AddRange(spliceGeoJoined);

            return 0;
        }
    }
}
