using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class TaperedCrossJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0;
        public double OffsetX = 0;
        public double OffsetY = 10;

        public Plane UnderPlane = Plane.Unset;
        public Plane OverPlane = Plane.Unset;

        public Plane UnderSide0Plane = Plane.Unset;
        public Plane UnderSideInset0Plane = Plane.Unset;

        public Plane UnderSide1Plane = Plane.Unset;
        public Plane UnderSideInset1Plane = Plane.Unset;

        public Plane OverSide0Plane = Plane.Unset;
        public Plane OverSideInset0Plane = Plane.Unset;

        public Plane OverSide1Plane = Plane.Unset;
        public Plane OverSideInset1Plane = Plane.Unset;

        public Plane LapPlane = Plane.Unset;
        public Vector3d Normal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public TaperedCrossJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!JointPartX.IsAtMiddle(parent.Parts[0].Case) || !JointPartX.IsAtMiddle(parent.Parts[1].Case))
                throw new ArgumentException($"{GetType().Name} requires intersections in beam middles.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            for (int i = 0; i < Parts.Count; ++i)
            {
                Parts[i].Geometry.Clear();
            }

            var under = beams[Parts[0].ElementIndex];
            var over = beams[Parts[1].ElementIndex];

            var underDirection = Parts[0].Direction;
            var overDirection = Parts[1].Direction;

            UnderPlane = under.GetPlane(Parts[0].Parameter);
            OverPlane = over.GetPlane(Parts[1].Parameter);

            // Over is a slave to Under

            var underSideDirection = Utility.ClosestAxis(UnderPlane, overDirection);
            var overSideDirection = Utility.ClosestAxis(OverPlane, underDirection);


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

            // Calculate normal and the centre plane of the lap joint
            Normal = Vector3d.CrossProduct(underSideDirection, overSideDirection);
            Normal.Unitize();

            debug.Add(new GH_Vector(Normal));

            var LapOrigin = Interpolation.Lerp(UnderPlane.Origin, OverPlane.Origin,
                (underHeight) / (overHeight + underHeight));

            var underOverDistance = UnderPlane.Origin.DistanceTo(OverPlane.Origin);
            var lapHeight = Math.Abs((underOverDistance - underHeight * 0.5) - overHeight * 0.5) * 0.5;

            // Calculate vertical added distance, considering combinations of lap surface offsets
            double AddedUp = (OffsetX == 0 || OffsetY == 0) ?
            Math.Max(
                LapOrigin.DistanceTo(UnderPlane.Origin) + underHeight * 0.5,
                LapOrigin.DistanceTo(OverPlane.Origin) + overHeight * 0.5
                ) : Added;

            // Calculate horizontal added distances based on taper angle
            double AddedX = AddedUp > 0 ? (AddedUp * OffsetX) / (lapHeight) : 0;
            double AddedY = AddedUp > 0 ? (AddedUp * OffsetY) / (lapHeight) : 0;

            // Calculate offset planes
            UnderSide0Plane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 - Inset + AddedX), underSideDirection);
            UnderSideInset0Plane = new Plane(UnderPlane.Origin + underSideDirection * (underWidth * 0.5 - Inset - OffsetX), underSideDirection);

            UnderSide1Plane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 - Inset + AddedY), -underSideDirection);
            UnderSideInset1Plane = new Plane(UnderPlane.Origin - underSideDirection * (underWidth * 0.5 - Inset - OffsetY), -underSideDirection);

            OverSide0Plane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 - Inset + AddedX), overSideDirection);
            OverSideInset0Plane = new Plane(OverPlane.Origin + overSideDirection * (overWidth * 0.5 - Inset - OffsetX), overSideDirection);

            OverSide1Plane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 - Inset + AddedY), -overSideDirection);
            OverSideInset1Plane = new Plane(OverPlane.Origin - overSideDirection * (overWidth * 0.5 - Inset - OffsetY), -overSideDirection);


            debug.Add(new GH_Point(LapOrigin));

            LapPlane = new Plane(LapOrigin, underSideDirection, overSideDirection);
            var UnderLapPlane = new Plane(LapOrigin + Normal * (lapHeight + AddedUp), LapPlane.XAxis, LapPlane.YAxis);
            var OverLapPlane = new Plane(LapOrigin - Normal * (lapHeight + AddedUp), LapPlane.XAxis, LapPlane.YAxis);

            var points = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSideInset0Plane, OverSideInset0Plane, out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSideInset0Plane, UnderSideInset1Plane, out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, UnderSideInset1Plane, OverSideInset1Plane, out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, OverSideInset1Plane, UnderSideInset0Plane, out points[3]);

            debug.AddRange(points.Select(x => new GH_Point(x)));

            var geometry = new List<Brep>();

            var lapSrf = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
            debug.Add(lapSrf);

            geometry.Add(lapSrf);

            var underPoints = new Point3d[4];

            // Do under geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(UnderLapPlane, UnderSide0Plane, OverSide0Plane, out underPoints[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(UnderLapPlane, OverSide0Plane, UnderSide1Plane, out underPoints[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(UnderLapPlane, UnderSide1Plane, OverSide1Plane, out underPoints[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(UnderLapPlane, OverSide1Plane, UnderSide0Plane, out underPoints[3]);

            debug.AddRange(underPoints.Select(x => new GH_Point(x)));

            var underGeo = new Brep[2];
            underGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], underPoints[1], underPoints[0], 0.001);
            underGeo[1] = Brep.CreateFromCornerPoints(underPoints[2], underPoints[3], points[3], points[2], 0.001);

            geometry.AddRange(underGeo);

            var overPoints = new Point3d[4];

            // Do over geometry
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OverLapPlane, UnderSide0Plane, OverSide0Plane, out overPoints[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OverLapPlane, OverSide0Plane, UnderSide1Plane, out overPoints[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OverLapPlane, UnderSide1Plane, OverSide1Plane, out overPoints[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(OverLapPlane, OverSide1Plane, UnderSide0Plane, out overPoints[3]);

            var overGeo = new Brep[2];
            overGeo[0] = Brep.CreateFromCornerPoints(points[1], points[2], overPoints[2], overPoints[1], 0.001);
            overGeo[1] = Brep.CreateFromCornerPoints(points[3], points[0], overPoints[0], overPoints[3], 0.001);

            geometry.AddRange(overGeo);

            // Do wings

            var wingGeo = new Brep[4];
            var flapGeo = new Brep[4];
            var wingDirections = new Vector3d[4];

            var planes = new Plane[]
            {
                UnderSide0Plane,
                OverSide0Plane,
                UnderSide1Plane,
                OverSide1Plane
            };

            for (int i = 0; i < 4; ++i)
            {
                var wingLine = new Line(underPoints[i], overPoints[i]);
                var mid = wingLine.PointAt(0.5);

                var ii = (i + 1) % 4;
                Vector3d direction;

                var v0 = overPoints[i] - underPoints[i];
                var v1 = overPoints[i] - points[i];

                v0.Unitize();
                v1.Unitize();

                if (1.0 - Math.Abs(v0 * v1) < Globals.CosineTolerance)
                {
                    direction = mid - points[i];
                    direction.Unitize();
                }
                else
                {
                    direction = Interpolation.Lerp(planes[i].ZAxis, planes[ii].ZAxis, 0.5);
                }
                //debug.Add(planes[ii]);

                if (
                    Math.Abs(planes[i].ZAxis * direction) < Globals.CosineTolerance || 
                    Math.Abs(planes[ii].ZAxis * direction) < Globals.CosineTolerance) // direction is within one of the side planes
                {
                    var boundary = new Polyline()
                    {
                        points[i],
                        underPoints[i],
                        overPoints[i],
                        points[i]
                    };

                    wingGeo[i] = Brep.CreatePlanarBreps(boundary.ToNurbsCurve(), 0.001)[0];

                    var cp = OverLapPlane.ClosestPoint(LapOrigin);
                    var flapDirection = overPoints[i] - cp;
                    flapDirection.Unitize();

                    var flapBoundary = new Polyline()
                    {
                        underPoints[i],
                        underPoints[i] + flapDirection * Added,
                        overPoints[i] + flapDirection * Added,
                        overPoints[i],
                        underPoints[i]
                    };

                    var flapSrfs = Brep.CreatePlanarBreps(flapBoundary.ToNurbsCurve(), 0.001);
                    if (flapSrfs != null && flapSrfs.Length > 0)
                    {
                        flapGeo[i] = flapSrfs[0];
                    }
                }
                else
                {
                    var boundary = new Polyline()
                    {
                        points[i],
                        underPoints[i],
                        underPoints[i] + direction * (10 + Added),
                        overPoints[i] + direction * (10 + Added),
                        overPoints[i],
                        points[i]
                    };

                    wingGeo[i] = Brep.CreatePlanarBreps(boundary.ToNurbsCurve(), 0.001)[0];
                    debug.Add(boundary);
                }
            }

            geometry.AddRange(wingGeo);
            geometry.AddRange(flapGeo);

            // debug.AddRange(overGeo);

            var geoJoined = Brep.JoinBreps(geometry, 0.001);
            if (geoJoined == null) throw new Exception($"{GetType().Name}: geoJoined failed.");

            Parts[0].Geometry.AddRange(geoJoined);
            Parts[1].Geometry.AddRange(geoJoined);

            return 0;
        }
    }
}
