﻿using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints
{
    public class CornerJoint2X : JointX
    {
        public double Added = 10.0;
        //public double Inset = 0.0;
        public double BlindOffset = 0;
        public double EndOffset = 100;

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

        public CornerJoint2X(JointX parent)
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
            if (values.TryGetValue("EndOffset", out double _endoffset)) EndOffset = _endoffset;
            if (values.TryGetValue("BlindOffset", out double _blindoffset)) BlindOffset = _blindoffset;
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

            var Beam0BackPlane = new Plane(
                Beam0Plane.Origin - beam0Direction * EndOffset,
                Beam0Plane.XAxis,
                Beam0Plane.YAxis);

            var Beam1BackPlane = new Plane(
                Beam1Plane.Origin - beam1Direction * EndOffset,
                Beam1Plane.XAxis,
                Beam1Plane.YAxis);

            // debug.Add(Beam0BackPlane);
            // debug.Add(Beam1BackPlane);

            // double beam0Width = beam0.Width, beam0Height = beam1.Height;
            // double beam1Width = beam1.Width, beam1Height = beam1.Height;

            // Vector3d xAxis = Beam0Plane.XAxis, yAxis = Beam0Plane.YAxis;

            // if (Math.Abs(Beam0Plane.Project(Beam1Plane.XAxis) * xAxis) > 0.5)
            // {
            //     beam1Width = beam1.Height;
            //     beam1Height = beam1.Width;
            // }

            // Tenon is a slave to Mortise
            var beam0SideDirection = GluLamb.Utility.ClosestAxis(Beam0Plane, beam1Direction);
            var beam1SideDirection = GluLamb.Utility.ClosestAxis(Beam1Plane, beam0Direction);

            // debug.Add(new GH_Vector(beam0SideDirection));
            // debug.Add(new GH_Vector(beam1SideDirection));

            double beam0Width = beam0.Width, beam0Height = beam0.Height;
            if (Math.Abs(beam1Direction * Beam0Plane.XAxis) < Math.Abs(beam1Direction * Beam0Plane.YAxis))
            {
                beam0Width = beam0.Height;
                beam0Height = beam0.Width;
            }

            double beam1Width = beam1.Width, beam1Height = beam1.Height;
            if (Math.Abs(beam0Direction * Beam1Plane.XAxis) < Math.Abs(beam0Direction * Beam1Plane.YAxis))
            {
                beam1Width = beam1.Height;
                beam1Height = beam1.Width;
            }

            Beam0Side0Plane = new Plane(Beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5), beam0SideDirection);
            Beam0Side1Plane = new Plane(Beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5), beam0SideDirection);

            Beam0Side0AddedPlane = new Plane(Beam0Plane.Origin + beam0SideDirection * (beam0Width * 0.5 + Added), beam0SideDirection);
            Beam0Side1AddedPlane = new Plane(Beam0Plane.Origin - beam0SideDirection * (beam0Width * 0.5 + Added), beam0SideDirection);

            // debug.Add(Beam0Side0Plane);
            // debug.Add(Beam0Side1Plane);

            // debug.Add(Beam0Side0AddedPlane);
            // debug.Add(Beam0Side1AddedPlane);

            Beam1Side0Plane = new Plane(Beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5), beam1SideDirection);
            Beam1Side1Plane = new Plane(Beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5), beam1SideDirection);

            Beam1Side0AddedPlane = new Plane(Beam1Plane.Origin + beam1SideDirection * (beam1Width * 0.5 + Added), beam1SideDirection);
            Beam1Side1AddedPlane = new Plane(Beam1Plane.Origin - beam1SideDirection * (beam1Width * 0.5 + Added), beam1SideDirection);

            // debug.Add(Beam1Side0Plane);
            // debug.Add(Beam1Side1Plane);

            Normal = Vector3d.CrossProduct(beam0SideDirection, beam1SideDirection);
            Normal.Unitize();
            // debug.Add(new GH_Vector(Normal));

            var LapOrigin = Interpolation.Lerp(Beam0Plane.Origin, Beam1Plane.Origin,
            (beam0Height) / (beam1Height + beam0Height));

            // debug.Add(new GH_Point(LapOrigin));

            LapPlane = new Plane(LapOrigin, beam0SideDirection, beam1SideDirection);

            var points = new Point3d[8];

            // Do under geometry
            RX.PlanePlanePlane(LapPlane, Beam0Side0Plane, Beam1Side0Plane, out points[0]);
            RX.PlanePlanePlane(LapPlane, Beam1Side0Plane, Beam0Side1Plane, out points[1]);
            RX.PlanePlanePlane(LapPlane, Beam0Side1Plane, Beam1Side1Plane, out points[2]);
            RX.PlanePlanePlane(LapPlane, Beam1Side1Plane, Beam0Side0Plane, out points[3]);

            debug.Add(points[0]);
            debug.Add(points[1]);
            debug.Add(points[2]);
            debug.Add(points[3]);

            RX.PlanePlanePlane(LapPlane, Beam1Side1Plane, Beam0BackPlane, out points[4]);
            RX.PlanePlanePlane(LapPlane, Beam0Side0Plane, Beam0BackPlane, out points[5]);

            debug.Add(points[4]);
            debug.Add(points[5]);

            RX.PlanePlanePlane(LapPlane, Beam0Side1Plane, Beam1BackPlane, out points[6]);
            RX.PlanePlanePlane(LapPlane, Beam1Side0Plane, Beam1BackPlane, out points[7]);
            debug.Add(points[6]);
            debug.Add(points[7]);

            //var tenonPoints = points.Select(x => x + Normal * (tenonHeight + Added)).ToArray();
            var pointsLow = points.Select(x => x - Normal * (beam0Height + Added)).ToArray();
            var pointsHigh = points.Select(x => x + Normal * (beam0Height + Added)).ToArray();

            var beam0Geo = new Brep[6];
            beam0Geo[0] = Brep.CreateFromCornerPoints(points[0], points[2], points[4], points[5], 0.001);
            beam0Geo[1] = Brep.CreateFromCornerPoints(points[2], points[0], points[7], points[6], 0.001);

            beam0Geo[2] = Brep.CreateFromCornerPoints(pointsHigh[2], pointsHigh[4], points[4], points[2], 0.001);
            beam0Geo[3] = Brep.CreateFromCornerPoints(pointsHigh[4], pointsHigh[5], points[5], points[4], 0.001);
            beam0Geo[4] = Brep.CreateFromCornerPoints(pointsLow[7], pointsLow[0], points[0], points[7], 0.001);
            beam0Geo[5] = Brep.CreateFromCornerPoints(pointsLow[7], pointsLow[6], points[6], points[7], 0.001);

            debug.AddRange(beam0Geo);

            // Do over geometry
            var beam1Geo = new Brep[6];
            beam1Geo[0] = Brep.CreateFromCornerPoints(points[0], points[2], points[4], points[5], 0.001);
            beam1Geo[1] = Brep.CreateFromCornerPoints(points[2], points[0], points[7], points[6], 0.001);

            beam1Geo[2] = Brep.CreateFromCornerPoints(pointsHigh[5], pointsHigh[0], points[0], points[5], 0.001);
            beam1Geo[3] = Brep.CreateFromCornerPoints(pointsHigh[4], pointsHigh[5], points[5], points[4], 0.001);
            beam1Geo[4] = Brep.CreateFromCornerPoints(pointsLow[6], pointsLow[2], points[2], points[6], 0.001);
            beam1Geo[5] = Brep.CreateFromCornerPoints(pointsLow[7], pointsLow[6], points[6], points[7], 0.001);

            debug.AddRange(beam1Geo);

            var beam0GeoJoined = Brep.JoinBreps(beam0Geo, 0.001);
            if (beam0GeoJoined == null) throw new Exception($"{GetType().Name}: beam0GeoJoined failed.");
            var beam1GeoJoined = Brep.JoinBreps(beam1Geo, 0.001);
            if (beam1GeoJoined == null) throw new Exception($"{GetType().Name}: beam1GeoJoined failed.");

            Parts[0].Geometry.AddRange(beam0GeoJoined);
            Parts[1].Geometry.AddRange(beam1GeoJoined);

            // Dowels
            var dowel = new Cylinder(new Circle(new Plane(LapOrigin - Normal * beam0Height, Normal), 8), beam0Height + beam1Height).ToBrep(true, true);
            Parts[0].Geometry.Add(dowel);
            Parts[1].Geometry.Add(dowel);

            return 0;
        }
    }
}
