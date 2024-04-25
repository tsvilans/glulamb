using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class TJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0.0;
        public double BlindOffset = 0;
        public bool FlipDirection = false;

        public Plane MortisePlane = Plane.Unset;
        public Plane TenonPlane = Plane.Unset;

        public Plane TopPlane = Plane.Unset;
        public Plane MiddlePlane = Plane.Unset;
        public Plane BottomPlane = Plane.Unset;

        public Plane TenonFacePlane = Plane.Unset;
        public Plane TenonFaceOffsetPlane = Plane.Unset;
        public Plane TenonBackPlane = Plane.Unset;

        public Plane[] TenonSidePlanes;
        public Plane[] MortiseSidePlanes;

        public Vector3d Normal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public TJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!(JointPartX.IsAtEnd(parent.Parts[0].Case) && JointPartX.IsAtMiddle(parent.Parts[1].Case)))
                throw new ArgumentException($"{GetType().Name} requires one intersection at the end and " +
                "one in the middle.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            for (int i = 0; i < Parts.Count; ++i)
            {
                Parts[i].Geometry.Clear();
            }

            var tenon = beams[Parts[0].ElementIndex];
            var mortise = beams[Parts[1].ElementIndex];

            var tenonDirection = Parts[0].Direction;
            var mortiseDirection = Parts[1].Direction;

            TenonPlane = tenon.GetPlane(Parts[0].Parameter);
            MortisePlane = mortise.GetPlane(Parts[1].Parameter);

            var beam0Y = TenonPlane.YAxis;
            var beam1Y = TenonPlane.YAxis * MortisePlane.YAxis < 0 ? -MortisePlane.YAxis : MortisePlane.YAxis;

            double tenonWidth = tenon.Width, tenonHeight = tenon.Height;
            double mortiseWidth = mortise.Width, mortiseHeight = mortise.Height;

            Vector3d xAxis = TenonPlane.XAxis, yAxis = TenonPlane.YAxis;

            Vector3d Normal = GluLamb.Utility.ClosestAxis(MortisePlane,
                Vector3d.CrossProduct(tenonDirection, mortiseDirection));

            var mortiseToTenon = TenonPlane.Origin - MortisePlane.Origin;
            mortiseToTenon.Unitize();

            if (!mortiseToTenon.IsZero && Normal * mortiseToTenon < 0)
                Normal.Reverse();

            if (FlipDirection)
                Normal.Reverse();

            var tenonUp = GluLamb.Utility.ClosestAxis(TenonPlane, Normal);

            int dim = GluLamb.Utility.ClosestDimension2D(MortisePlane, tenonDirection);

            if (dim != 0)
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

            debug.Add(new Line(MortisePlane.Origin, Normal, mortiseHeight * 0.5 + 10));

            var MortiseSideDirection = GluLamb.Utility.ClosestAxis(MortisePlane, tenonDirection);
            var TenonSideDirection = GluLamb.Utility.ClosestAxis(TenonPlane, mortiseDirection);



            TenonFacePlane = new Plane(
                MortisePlane.Origin - MortiseSideDirection * mortiseWidth * 0.5,
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            TenonBackPlane = new Plane(
                MortisePlane.Origin + MortiseSideDirection * (mortiseWidth * 0.5 - BlindOffset),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);


            dim = GluLamb.Utility.ClosestDimension2D(TenonFacePlane, TenonPlane.XAxis);

            if (dim != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            debug.Add(new Line(MortisePlane.Origin, -Normal, tenonHeight * 0.5));

            TenonSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            MortiseSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
            };

            var centre = (TenonPlane.Origin + MortisePlane.Origin) * 0.5;

            TenonFacePlane.Origin = centre.ProjectToPlane(TenonFacePlane);
            TenonBackPlane.Origin = centre.ProjectToPlane(TenonBackPlane);
            TenonSidePlanes[0].Origin = centre.ProjectToPlane(TenonSidePlanes[0]);
            TenonSidePlanes[1].Origin = centre.ProjectToPlane(TenonSidePlanes[1]);

            debug.Add(new GH_Plane(TenonFacePlane));
            debug.Add(new GH_Plane(TenonBackPlane));
            debug.Add(new GH_Plane(TenonSidePlanes[0]));
            debug.Add(new GH_Plane(TenonSidePlanes[1]));

            var TenonFacePlaneOffset = new Plane(
                TenonFacePlane.Origin - TenonFacePlane.ZAxis * Added,
                TenonFacePlane.XAxis,
                TenonFacePlane.YAxis
                );

            // For a real tenon...
            var tenonThickness = Math.Min(tenonHeight, mortiseHeight) * 0.5;

            var TenonBottomPlane = new Plane(
                TenonFacePlane.Origin - TenonFacePlane.YAxis * tenonThickness * 0.5,
                TenonFacePlane.XAxis,
                TenonFacePlane.ZAxis
                );

            var TenonTopPlane = new Plane(
                TenonFacePlane.Origin + TenonFacePlane.YAxis * (tenonHeight * 0.5 + Added),
                TenonFacePlane.XAxis,
                TenonFacePlane.ZAxis
                );


            // For a lap joint...
            TopPlane = new Plane(
                TenonFacePlane.Origin + TenonFacePlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added),
                TenonFacePlane.XAxis,
                TenonFacePlane.ZAxis
                );

            MiddlePlane = new Plane(
                TenonFacePlane.Origin,
                TenonFacePlane.XAxis,
                TenonFacePlane.ZAxis
                );

            BottomPlane = new Plane(
                TenonFacePlane.Origin - TenonFacePlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added),
                TenonFacePlane.XAxis,
                TenonFacePlane.ZAxis
                );

            debug.Add(new GH_Plane(TopPlane));
            debug.Add(new GH_Plane(MiddlePlane));
            debug.Add(new GH_Plane(BottomPlane));

            var tenonPoints = new Point3d[8];

            for (int i = 0; i < 2; ++i)
            {
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(TenonSidePlanes[i], TenonFacePlane, BottomPlane, out tenonPoints[0 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(TenonSidePlanes[i], TenonFacePlane, MiddlePlane, out tenonPoints[1 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(TenonSidePlanes[i], TenonBackPlane, MiddlePlane, out tenonPoints[2 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(TenonSidePlanes[i], TenonBackPlane, TopPlane, out tenonPoints[3 + i * 4]);
            }

            var tenonGeo = new Brep[3];
            tenonGeo[0] = Brep.CreateFromCornerPoints(tenonPoints[0], tenonPoints[1], tenonPoints[5], tenonPoints[4], 0.001);
            tenonGeo[1] = Brep.CreateFromCornerPoints(tenonPoints[1], tenonPoints[2], tenonPoints[6], tenonPoints[5], 0.001);
            tenonGeo[2] = Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], tenonPoints[7], tenonPoints[6], 0.001);

            //debug.AddRange(tenonGeo);

            var mortisePoints = new Point3d[8];
            for (int i = 0; i < 2; ++i)
            {
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(MortiseSidePlanes[i], TenonFacePlaneOffset, MiddlePlane, out mortisePoints[0 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(MortiseSidePlanes[i], TenonFacePlaneOffset, TopPlane, out mortisePoints[1 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, MiddlePlane, out mortisePoints[2 + i * 4]);
                Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, TopPlane, out mortisePoints[3 + i * 4]);
            }

            var mortiseGeo = new Brep[4];
            mortiseGeo[0] = Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[2], mortisePoints[6], mortisePoints[4], 0.001);
            mortiseGeo[1] = Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[1], mortisePoints[3], mortisePoints[2], 0.001);
            mortiseGeo[2] = Brep.CreateFromCornerPoints(mortisePoints[2], mortisePoints[3], mortisePoints[7], mortisePoints[6], 0.001);
            mortiseGeo[3] = Brep.CreateFromCornerPoints(mortisePoints[4], mortisePoints[5], mortisePoints[7], mortisePoints[6], 0.001);

            debug.AddRange(mortiseGeo);

            var tenonGeoJoined = Brep.JoinBreps(tenonGeo, 0.001);
            if (tenonGeoJoined == null) throw new Exception($"{GetType().Name}: tenonGeoJoined failed.");
            var mortiseGeoJoined = Brep.JoinBreps(mortiseGeo, 0.001);
            if (mortiseGeoJoined == null) throw new Exception($"{GetType().Name}: mortiseGeoJoined failed.");

            Parts[0].Geometry.AddRange(tenonGeoJoined);
            Parts[1].Geometry.AddRange(mortiseGeoJoined);

            /*
                        TenonSide0Plane = new Plane(TenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 - Inset), tenonSideDirection);
                        TenonSide1Plane = new Plane(TenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 - Inset), tenonSideDirection);

                        TenonSide0AddedPlane = new Plane(TenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 + Added + Inset), tenonSideDirection);
                        TenonSide1AddedPlane = new Plane(TenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 + Added + Inset), tenonSideDirection);

                        debug.Add(TenonSide0Plane);
                        debug.Add(TenonSide1Plane);

                        MortiseSide0Plane = new Plane(MortisePlane.Origin + mortiseSideDirection * (mortiseWidth * 0.5 - Inset - BlindOffset), mortiseSideDirection);
                        MortiseSide1Plane = new Plane(MortisePlane.Origin - mortiseSideDirection * (mortiseWidth * 0.5), mortiseSideDirection);

                        MortiseSide0AddedPlane = new Plane(MortisePlane.Origin + mortiseSideDirection * (mortiseWidth * 0.5 + Added + Inset), mortiseSideDirection);
                        MortiseSide1AddedPlane = new Plane(MortisePlane.Origin - mortiseSideDirection * (mortiseWidth * 0.5 + Added + Inset), mortiseSideDirection);

                        debug.Add(MortiseSide0Plane);
                        debug.Add(MortiseSide1Plane);

                        Normal = Vector3d.CrossProduct(mortiseSideDirection, tenonSideDirection);
                        debug.Add(new GH_Vector(Normal));

                        var LapOrigin = Interpolation.Lerp(TenonPlane.Origin, MortisePlane.Origin, 
                        (tenonHeight) / (mortiseHeight + tenonHeight));
                        debug.Add(new GH_Point(LapOrigin));

                        LapPlane = new Plane(LapOrigin, tenonSideDirection, mortiseSideDirection);

                        var points = new Point3d[4];

                        // Do under geometry
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, TenonSide0AddedPlane, MortiseSide0Plane, out points[0]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, MortiseSide0Plane, TenonSide1AddedPlane, out points[1]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, TenonSide1AddedPlane, MortiseSide1Plane, out points[2]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, MortiseSide1Plane, TenonSide0AddedPlane, out points[3]);

                        //var tenonPoints = points.Select(x => x + Normal * (tenonHeight + Added)).ToArray();
                        var tenonPoints = new Point3d[]
                        {
                            points[0] - Normal * (tenonHeight + Added),
                            points[1] - Normal * (tenonHeight + Added),
                            points[2] + Normal * (tenonHeight + Added),
                            points[3] + Normal * (tenonHeight + Added)
                        };

                        var tenonGeo = new Brep[3];
                        tenonGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
                        tenonGeo[1] = Brep.CreateFromCornerPoints(points[0], points[1], tenonPoints[1], tenonPoints[0], 0.001);
                        tenonGeo[2] = Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], points[3], points[2], 0.001);

                        //debug.AddRange(tenonGeo);

                        // Do over geometry
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, TenonSide0Plane, MortiseSide0Plane, out points[0]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, MortiseSide0Plane, TenonSide1Plane, out points[1]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, TenonSide1Plane, MortiseSide1AddedPlane, out points[2]);
                        Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(LapPlane, MortiseSide1AddedPlane, TenonSide0Plane, out points[3]);

                        var mortisePoints = points.Select(x => x - Normal * (mortiseHeight + Added)).ToArray();

                        var mortiseGeo = new Brep[4];
                        mortiseGeo[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);
                        mortiseGeo[1] = Brep.CreateFromCornerPoints(points[1], points[2], mortisePoints[2], mortisePoints[1], 0.001);
                        mortiseGeo[2] = Brep.CreateFromCornerPoints(points[3], points[0], mortisePoints[0], mortisePoints[3], 0.001);
                        mortiseGeo[3] = Brep.CreateFromCornerPoints(points[0], points[1], mortisePoints[1], mortisePoints[0], 0.001);

                        //debug.AddRange(mortiseGeo);

                        var tenonGeoJoined = Brep.JoinBreps(tenonGeo, 0.001);
                        if (tenonGeoJoined == null) throw new Exception($"{GetType().Name}: tenonGeoJoined failed.");
                        var mortiseGeoJoined = Brep.JoinBreps(mortiseGeo, 0.001);
                        if (mortiseGeoJoined == null) throw new Exception($"{GetType().Name}: mortiseGeoJoined failed.");

                        Parts[0].Geometry.AddRange(tenonGeoJoined);
                        Parts[1].Geometry.AddRange(mortiseGeoJoined);
            */
            return 0;
        }
    }
}
