using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class CornerJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0.0;
        public double BlindOffset = 0;

        public Plane Beam0Plane = Plane.Unset;
        public Plane Beam1Plane = Plane.Unset;

        public Plane Beam0Side0Plane = Plane.Unset;
        public Plane Beam0Side1Plane = Plane.Unset;
        public Plane Beam0Side0AddedPlane = Plane.Unset;
        public Plane Beam0Side1AddedPlane = Plane.Unset;

        public Plane Beam1Side0Plane = Plane.Unset;
        public Plane Beam1Side1Plane = Plane.Unset;
        public Plane Beam1Side0AddedPlane = Plane.Unset;
        public Plane Beam1Side1AddedPlane = Plane.Unset;

        public Plane LapPlane = Plane.Unset;
        public Vector3d Normal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public CornerJointX(JointX parent)
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
            if (values.TryGetValue("Inset", out double _inset)) Inset = _inset;
            if (values.TryGetValue("BlindOffset", out double _blindoffset)) Inset = _blindoffset;
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

            var beam0SideDirection = Utility.ClosestAxis(Beam0Plane, beam1Direction);
            var beam1SideDirection = Utility.ClosestAxis(Beam1Plane, beam0Direction);

            debug.Add(new GH_Vector(beam0SideDirection));
            debug.Add(new GH_Vector(beam1SideDirection));

            double beam0Width, beam0Height;
            if (Math.Abs(beam1Direction * Beam0Plane.XAxis) > Math.Abs(beam1Direction * Beam0Plane.YAxis))
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
            if (Math.Abs(beam0Direction * Beam1Plane.XAxis) > Math.Abs(beam0Direction * Beam1Plane.YAxis))
            {
                beam1Width = beam1.Width;
                beam1Height = beam1.Height;
            }
            else
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            Beam0Side0Plane = new Plane(Beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5 - Inset), beam0SideDirection);
            Beam0Side1Plane = new Plane(Beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5 - Inset), beam0SideDirection);

            Beam0Side0AddedPlane = new Plane(Beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5 + Added + Inset), beam0SideDirection);
            Beam0Side1AddedPlane = new Plane(Beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5 + Added + Inset), beam0SideDirection);

            debug.Add(Beam0Side0Plane);
            debug.Add(Beam0Side1Plane);

            Beam1Side0Plane = new Plane(Beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5 - Inset - BlindOffset), beam1SideDirection);
            Beam1Side1Plane = new Plane(Beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5), beam1SideDirection);

            Beam1Side0AddedPlane = new Plane(Beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5 + Added + Inset), beam1SideDirection);
            Beam1Side1AddedPlane = new Plane(Beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5 + Added + Inset), beam1SideDirection);

            debug.Add(Beam1Side0Plane);
            debug.Add(Beam1Side1Plane);

            Normal = Vector3d.CrossProduct(beam0SideDirection, beam1SideDirection);
            Normal.Unitize();
            debug.Add(new GH_Vector(Normal));

            var LapOrigin = Interpolation.Lerp(Beam0Plane.Origin, Beam1Plane.Origin,
            (beam0Height) / (beam1Height + beam0Height));
            debug.Add(new GH_Point(LapOrigin));

            LapPlane = new Plane(LapOrigin, beam0SideDirection, beam1SideDirection);

            var points = new Point3d[5];

            // Do beam0 geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam0Side0AddedPlane, Beam1Side0Plane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam1Side0Plane, Beam0Side1AddedPlane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam0Side1AddedPlane, Beam1Side1Plane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam1Side1Plane, Beam0Side0AddedPlane, out points[3]);

            var beam0Points = new Point3d[]
            {
                points[0] - Normal * (beam0Height + Added),
                points[1] - Normal * (beam0Height + Added),
                points[2] + Normal * (beam0Height + Added),
                points[3] + Normal * (beam0Height + Added)
            };

            var beam0Geo = new Brep[3];
            beam0Geo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            beam0Geo[1] = Brep.CreateFromCornerPoints(points[0], points[1], beam0Points[1], beam0Points[0], 0.001);
            beam0Geo[2] = Brep.CreateFromCornerPoints(beam0Points[2], beam0Points[3], points[3], points[2], 0.001);


            // Do beam1 geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam0Side0Plane, Beam1Side0Plane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam1Side0Plane, Beam0Side1Plane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam0Side1Plane, Beam1Side1AddedPlane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam1Side1AddedPlane, Beam0Side0Plane, out points[3]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, Beam1Side0AddedPlane, Beam0Side0Plane, out points[4]);

            var beam1Points = new Point3d[]
            {
                points[0] - Normal * (beam1Height + Added),
                points[1] - Normal * (beam1Height + Added),
                points[2] - Normal * (beam1Height + Added),
                points[3] - Normal * (beam1Height + Added),
                points[3] + Normal * (beam1Height + Added),
                points[4] + Normal * (beam1Height + Added),
                points[4] - Normal * (beam1Height + Added)

            };

            var beam1Geo = new Brep[4];
            beam1Geo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            beam1Geo[1] = Brep.CreateFromCornerPoints(points[1], points[2], beam1Points[2], beam1Points[1], 0.001);
            beam1Geo[2] = Brep.CreateFromCornerPoints(points[0], points[1], beam1Points[1], beam1Points[0], 0.001);
            // beam1Geo[3] = Brep.CreateFromCornerPoints(points[3], points[0], beam1Points[0], beam1Points[3], 0.001);

            var boundary = new Polyline() { points[0], points[3], beam1Points[4], beam1Points[5], beam1Points[6], beam1Points[0], points[0] };
            beam1Geo[3] = Brep.CreatePlanarBreps(boundary.ToNurbsCurve(), 0.001)[0];

            var beam0GeoJoined = Brep.JoinBreps(beam0Geo, 0.001);
            if (beam0GeoJoined == null) throw new Exception($"{GetType().Name}: beam0GeoJoined failed.");
            var beam1GeoJoined = Brep.JoinBreps(beam1Geo, 0.001);
            if (beam1GeoJoined == null) throw new Exception($"{GetType().Name}: beam1GeoJoined failed.");

            Parts[0].Geometry.AddRange(beam0GeoJoined);
            Parts[1].Geometry.AddRange(beam1GeoJoined);

            return 0;
        }
    }
}
