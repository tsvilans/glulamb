using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class CrossJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0.0;

        public Plane UnderPlane = Plane.Unset;
        public Plane OverPlane = Plane.Unset;

        public Plane UnderSide0Plane = Plane.Unset;
        public Plane UnderSide1Plane = Plane.Unset;
        public Plane UnderSide0AddedPlane = Plane.Unset;
        public Plane UnderSide1AddedPlane = Plane.Unset;

        public Plane OverSide0Plane = Plane.Unset;
        public Plane OverSide1Plane = Plane.Unset;
        public Plane OverSide0AddedPlane = Plane.Unset;
        public Plane OverSide1AddedPlane = Plane.Unset;

        public Plane LapPlane = Plane.Unset;
        public Vector3d Normal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public CrossJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!JointPartX.IsAtMiddle(parent.Parts[0].Case) || !JointPartX.IsAtMiddle(parent.Parts[1].Case))
                throw new ArgumentException($"{GetType().Name} requires intersections in beam middles.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override void Configure(Dictionary<string, double> values)
        {
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("Inset", out double _inset)) Inset = _inset;
        }

        public override List<object> GetDebugList()
        {
            return debug;
        }

        public int Construct(Beam[] beams, double[] parameters, List<Brep>[] geometries)
        {

            var under = beams[0];
            var over = beams[1];

            var underDirection = under.Centreline.TangentAt(parameters[0]);
            var overDirection = over.Centreline.TangentAt(parameters[1]);

            UnderPlane = under.GetPlane(parameters[0]);
            OverPlane = over.GetPlane(parameters[1]);

            // Normal = Vector3d.CrossProduct(overDirection, underDirection);
            // if (Normal * UnderPlane.YAxis < 0)
            //     Normal.Reverse();

            debug.Add(UnderPlane);
            debug.Add(OverPlane);
            debug.Add(underDirection);
            debug.Add(overDirection);
            // Over is a slave to Under

            var underSideDirection = Utility.ClosestAxis(UnderPlane, overDirection);
            var overSideDirection = Utility.ClosestAxis(OverPlane, underDirection);

            debug.Add(underSideDirection);
            debug.Add(overSideDirection);

            double underWidth, underHeight;
            if (Math.Abs(overDirection * UnderPlane.XAxis) > Math.Abs(overDirection * UnderPlane.YAxis))
            {
                underWidth = under.Width;
                underHeight = under.Height;
            }
            else
            {
                underWidth = under.Height;
                underHeight = under.Width;
            }

            double overWidth, overHeight;
            if (Math.Abs(underDirection * OverPlane.XAxis) > Math.Abs(underDirection * OverPlane.YAxis))
            {
                overWidth = over.Width;
                overHeight = over.Height;
            }
            else
            {
                overWidth = over.Height;
                overHeight = over.Width;
            }

            UnderSide0Plane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);
            UnderSide1Plane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);

            UnderSide0AddedPlane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);
            UnderSide1AddedPlane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);

            debug.Add(UnderSide0Plane);
            debug.Add(UnderSide1Plane);

            OverSide0Plane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);
            OverSide1Plane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);

            OverSide0AddedPlane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);
            OverSide1AddedPlane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);

            debug.Add(OverSide0Plane);
            debug.Add(OverSide1Plane);

            Normal = Vector3d.CrossProduct(underSideDirection, overSideDirection);
            debug.Add(Normal);

            var LapOrigin = Interpolation.Lerp(UnderPlane.Origin, OverPlane.Origin,
            (underHeight) / (overHeight + underHeight));
            debug.Add(LapOrigin);

            LapPlane = new Plane(LapOrigin, underSideDirection, overSideDirection);

            var points = new Point3d[4];

            // Do under geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide0AddedPlane, OverSide0Plane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide0Plane, UnderSide1AddedPlane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide1AddedPlane, OverSide1Plane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide1Plane, UnderSide0AddedPlane, out points[3]);

            var underPoints = points.Select(x => x + Normal * (underHeight + Added)).ToArray();

            var underGeo = new Brep[3];
            underGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            underGeo[1] = Brep.CreateFromCornerPoints(points[0], points[1], underPoints[1], underPoints[0], 0.001);
            underGeo[2] = Brep.CreateFromCornerPoints(underPoints[2], underPoints[3], points[3], points[2], 0.001);

            debug.AddRange(underGeo);

            // Do over geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide0Plane, OverSide0AddedPlane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide0AddedPlane, UnderSide1Plane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide1Plane, OverSide1AddedPlane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide1AddedPlane, UnderSide0Plane, out points[3]);

            var overPoints = points.Select(x => x - Normal * (overHeight + Added)).ToArray();

            var overGeo = new Brep[3];
            overGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            overGeo[1] = Brep.CreateFromCornerPoints(points[1], points[2], overPoints[2], overPoints[1], 0.001);
            overGeo[2] = Brep.CreateFromCornerPoints(points[3], points[0], overPoints[0], overPoints[3], 0.001);

            debug.AddRange(overGeo);

            var underGeoJoined = Brep.JoinBreps(underGeo, 0.001);
            //if (underGeoJoined == null) throw new Exception($"{GetType().Name}: underGeoJoined failed.");
            var overGeoJoined = Brep.JoinBreps(overGeo, 0.001);
            //if (overGeoJoined == null) throw new Exception($"{GetType().Name}: overGeoJoined failed.");

            geometries[0].AddRange(underGeoJoined);
            geometries[1].AddRange(overGeoJoined);

            return 0;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            for (int i = 0; i < Parts.Count; ++i)
            {
                Parts[i].Geometry.Clear();
            }

            return Construct(new Beam[] { beams[Parts[0].ElementIndex], beams[Parts[1].ElementIndex] },
                new double[] { Parts[0].Parameter, Parts[1].Parameter },
                new List<Brep>[] { Parts[0].Geometry, Parts[1].Geometry });

            var under = beams[Parts[0].ElementIndex];
            var over = beams[Parts[1].ElementIndex];

            var underDirection = Parts[0].Direction;
            var overDirection = Parts[1].Direction;

            UnderPlane = under.GetPlane(Parts[0].Parameter);
            OverPlane = over.GetPlane(Parts[1].Parameter);

            // Normal = Vector3d.CrossProduct(overDirection, underDirection);
            // if (Normal * UnderPlane.YAxis < 0)
            //     Normal.Reverse();

            debug.Add(UnderPlane);
            debug.Add(OverPlane);
            debug.Add(underDirection);
            debug.Add(overDirection);
            // Over is a slave to Under

            var underSideDirection = Utility.ClosestAxis(UnderPlane, overDirection);
            var overSideDirection = Utility.ClosestAxis(OverPlane, underDirection);

            debug.Add(underSideDirection);
            debug.Add(overSideDirection);

            double underWidth, underHeight;
            if (Math.Abs(overDirection * UnderPlane.XAxis) > Math.Abs(overDirection * UnderPlane.YAxis))
            {
                underWidth = under.Width;
                underHeight = under.Height;
            }
            else
            {
                underWidth = under.Height;
                underHeight = under.Width;
            }

            double overWidth, overHeight;
            if (Math.Abs(underDirection * OverPlane.XAxis) > Math.Abs(underDirection * OverPlane.YAxis))
            {
                overWidth = over.Width;
                overHeight = over.Height;
            }
            else
            {
                overWidth = over.Height;
                overHeight = over.Width;
            }

            UnderSide0Plane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);
            UnderSide1Plane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 - Inset), underSideDirection);

            UnderSide0AddedPlane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);
            UnderSide1AddedPlane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 + Added + Inset), underSideDirection);

            debug.Add(UnderSide0Plane);
            debug.Add(UnderSide1Plane);

            OverSide0Plane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);
            OverSide1Plane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 - Inset), overSideDirection);

            OverSide0AddedPlane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);
            OverSide1AddedPlane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 + Added + Inset), overSideDirection);

            debug.Add(OverSide0Plane);
            debug.Add(OverSide1Plane);

            Normal = Vector3d.CrossProduct(underSideDirection, overSideDirection);
            debug.Add(Normal);

            var LapOrigin = Interpolation.Lerp(UnderPlane.Origin, OverPlane.Origin,
            (underHeight) / (overHeight + underHeight));
            debug.Add(LapOrigin);

            LapPlane = new Plane(LapOrigin, underSideDirection, overSideDirection);

            var points = new Point3d[4];

            // Do under geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide0AddedPlane, OverSide0Plane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide0Plane, UnderSide1AddedPlane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide1AddedPlane, OverSide1Plane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide1Plane, UnderSide0AddedPlane, out points[3]);

            var underPoints = points.Select(x => x + Normal * (underHeight + Added)).ToArray();

            var underGeo = new Brep[3];
            underGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            underGeo[1] = Brep.CreateFromCornerPoints(points[0], points[1], underPoints[1], underPoints[0], 0.001);
            underGeo[2] = Brep.CreateFromCornerPoints(underPoints[2], underPoints[3], points[3], points[2], 0.001);

            debug.AddRange(underGeo);

            // Do over geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide0Plane, OverSide0AddedPlane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide0AddedPlane, UnderSide1Plane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSide1Plane, OverSide1AddedPlane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSide1AddedPlane, UnderSide0Plane, out points[3]);

            var overPoints = points.Select(x => x - Normal * (overHeight + Added)).ToArray();

            var overGeo = new Brep[3];
            overGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            overGeo[1] = Brep.CreateFromCornerPoints(points[1], points[2], overPoints[2], overPoints[1], 0.001);
            overGeo[2] = Brep.CreateFromCornerPoints(points[3], points[0], overPoints[0], overPoints[3], 0.001);

            debug.AddRange(overGeo);

            var underGeoJoined = Brep.JoinBreps(underGeo, 0.001);
            //if (underGeoJoined == null) throw new Exception($"{GetType().Name}: underGeoJoined failed.");
            var overGeoJoined = Brep.JoinBreps(overGeo, 0.001);
            //if (overGeoJoined == null) throw new Exception($"{GetType().Name}: overGeoJoined failed.");

            Parts[0].Geometry.AddRange(underGeoJoined);
            Parts[1].Geometry.AddRange(overGeoJoined);

            return 0;
        }
    }
}
