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
    public class DovetailTenonJointX : JointX
    {
        public double Added = 10.0;
        public double NeckOffset = 20;
        public double Inset = 0.0;
        public double Depth = 10;
        public bool FlipDirection = false;
        public double Angle = RhinoMath.ToRadians(30);

        public Plane MortisePlane = Plane.Unset;
        public Plane TenonPlane = Plane.Unset;

        public Plane TopPlane = Plane.Unset;
        public Plane MiddlePlane = Plane.Unset;
        public Plane BottomPlane = Plane.Unset;

        public Plane TenonFacePlane = Plane.Unset;
        public Plane TenonFaceOffsetPlane = Plane.Unset;
        public Plane TenonBackPlane = Plane.Unset;

        public Plane[] TenonSidePlanes;
        public Plane[] TenonSideOffsetPlanes;
        public Plane[] TenonNeckPlanes;
        public Plane[] MortiseSidePlanes;

        public Vector3d Normal = Vector3d.Unset;

        public List<object> debug = new List<object>();

        public DovetailTenonJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            if (!(JointPartX.IsAtEnd(parent.Parts[0].Case) && JointPartX.IsAtMiddle(parent.Parts[1].Case)))
                throw new ArgumentException($"{GetType().Name} requires one intersection at the end and " +
                "one in the middle.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override void Configure(Dictionary<string, double> values)
        {
            if (values.TryGetValue("Depth", out double _depth)) Depth = _depth;
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("NeckOffset", out double _neckoffset)) NeckOffset = _neckoffset;
            if (values.TryGetValue("Angle", out double _angle)) Angle = _angle;
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

            // Get beam elements
            var tenon = beams[Parts[0].ElementIndex];
            var mortise = beams[Parts[1].ElementIndex];

            // Get corrected element directions
            var tenonDirection = Parts[0].Direction;
            var mortiseDirection = Parts[1].Direction;

            // Get raw element planes at joint
            TenonPlane = tenon.GetPlane(Parts[0].Parameter);
            MortisePlane = mortise.GetPlane(Parts[1].Parameter);

            var beam0Y = TenonPlane.YAxis;
            var beam1Y = TenonPlane.YAxis * MortisePlane.YAxis < 0 ? -MortisePlane.YAxis : MortisePlane.YAxis;

            // Get element dimensions
            double tenonWidth = tenon.Width, tenonHeight = tenon.Height;
            double mortiseWidth = mortise.Width, mortiseHeight = mortise.Height;

            Vector3d xAxis = TenonPlane.XAxis, yAxis = TenonPlane.YAxis;

            Vector3d Normal = GluLamb.Utility.ClosestAxis(MortisePlane,
                Vector3d.CrossProduct(tenonDirection, mortiseDirection));

            if (Normal * beam0Y < 0.0)
                Normal.Reverse();
            // Normal = GluLamb.Utility.ClosestAxis(MortisePlane, beam0Y);

            var mortiseToTenon = TenonPlane.Origin - MortisePlane.Origin;
            //mortiseToTenon.Unitize();

            //if (!mortiseToTenon.IsZero && Normal * mortiseToTenon < 0)
            //    Normal.Reverse();

            if (FlipDirection)
                Normal.Reverse();

            var tenonUp = GluLamb.Utility.ClosestAxis(TenonPlane, Normal);


            if (GluLamb.Utility.ClosestDimension2D(MortisePlane, tenonDirection) != 0)
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

            debug.Add(new Line(MortisePlane.Origin, tenonUp, mortiseHeight * 0.5 + 30));

            var MortiseSideDirection = GluLamb.Utility.ClosestAxis(MortisePlane, tenonDirection);
            var TenonSideDirection = GluLamb.Utility.ClosestAxis(TenonPlane, mortiseDirection);
            TenonSideDirection = Vector3d.CrossProduct(tenonUp, tenonDirection);

            debug.Add(new Line(MortisePlane.Origin, MortiseSideDirection, 100));

            TenonFacePlane = new Plane(
                MortisePlane.Origin - MortiseSideDirection * mortiseWidth * 0.5,
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            TenonFaceOffsetPlane = new Plane(
                TenonFacePlane.Origin - TenonFacePlane.ZAxis * Added,
                TenonFacePlane.XAxis,
                TenonFacePlane.YAxis);

            TenonBackPlane = new Plane(
                MortisePlane.Origin - MortiseSideDirection * (mortiseWidth * 0.5 - Depth),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            var dim = GluLamb.Utility.ClosestDimension2D(TenonFacePlane, TenonPlane.XAxis);

            if (dim != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            // debug.Add(new Line(MortisePlane.Origin, -Normal, tenonHeight * 0.5));

            TenonSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            TenonSideOffsetPlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            TenonNeckPlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 - NeckOffset), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 - NeckOffset), tenonDirection, tenonUp),
            };

            // debug.Add(TenonFacePlane);
            // debug.Add(new Line(TenonFacePlane.Origin, TenonSideDirection, 200));

            for (int i = 0; i < 2; ++i)
            {
                int sign = i > 0 ? 1 : -1;

                RX.PlanePlane(TenonNeckPlanes[i], TenonFacePlane, out Line pivot);
                TenonNeckPlanes[i].Origin = pivot.From;

                sign *= Normal * pivot.Direction < 0 ? 1 : -1;

                TenonNeckPlanes[i].Transform(Transform.Rotation(TenonNeckPlanes[i].XAxis, TenonFacePlane.ZAxis, pivot.From));
                TenonNeckPlanes[i].Transform(Transform.Rotation(Angle * sign, pivot.Direction, pivot.From));
            }

            MortiseSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5), tenonDirection, tenonUp),
            };

            var centre = (TenonPlane.Origin + MortisePlane.Origin) * 0.5;
            debug.Add(centre);

            TenonFacePlane.Origin = centre.ProjectToPlane(TenonFacePlane);
            TenonBackPlane.Origin = centre.ProjectToPlane(TenonBackPlane);
            TenonSidePlanes[0].Origin = centre.ProjectToPlane(TenonSidePlanes[0]);
            TenonSidePlanes[1].Origin = centre.ProjectToPlane(TenonSidePlanes[1]);

            // debug.Add(new GH_Plane(TenonFacePlane));
            // debug.Add(new GH_Plane(TenonFaceOffsetPlane));
            // debug.Add(new GH_Plane(TenonBackPlane));
            // debug.Add(new GH_Plane(TenonSidePlanes[0]));
            // debug.Add(new GH_Plane(TenonSidePlanes[1]));
            // debug.Add(new GH_Plane(TenonNeckPlanes[0]));
            // debug.Add(new GH_Plane(TenonNeckPlanes[1]));

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

            // debug.Add(new GH_Plane(TopPlane));
            // debug.Add(new GH_Plane(MiddlePlane));
            // debug.Add(new GH_Plane(BottomPlane));

            var tenonPoints = new Point3d[12];

            for (int i = 0; i < 2; ++i)
            {
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonFacePlane, BottomPlane, out tenonPoints[0 + i * 6]);
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonFacePlane, TopPlane, out tenonPoints[1 + i * 6]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonFacePlane, TopPlane, out tenonPoints[2 + i * 6]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonFacePlane, MiddlePlane, out tenonPoints[3 + i * 6]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonBackPlane, MiddlePlane, out tenonPoints[4 + i * 6]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonBackPlane, TopPlane, out tenonPoints[5 + i * 6]);
            }

            // debug.Add(new GH_Point(tenonPoints[0]));
            // debug.Add(new GH_Point(tenonPoints[1]));
            // debug.Add(new GH_Point(tenonPoints[2]));
            // debug.Add(new GH_Point(tenonPoints[3]));
            // debug.Add(new GH_Point(tenonPoints[4]));
            // debug.Add(new GH_Point(tenonPoints[5]));

            // debug.AddRange(tenonPoints.Select(x => new GH_Point(x)));

            var tenonGeo = new Brep[5];
            tenonGeo[0] = Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], tenonPoints[4], tenonPoints[5], 0.001);
            tenonGeo[1] = Brep.CreateFromCornerPoints(tenonPoints[4], tenonPoints[5], tenonPoints[11], tenonPoints[10], 0.001);
            tenonGeo[2] = Brep.CreateFromCornerPoints(tenonPoints[8], tenonPoints[9], tenonPoints[10], tenonPoints[11], 0.001);
            tenonGeo[3] = Brep.CreateFromCornerPoints(tenonPoints[3], tenonPoints[4], tenonPoints[10], tenonPoints[9], 0.001);

            tenonGeo[4] = Brep.CreatePlanarBreps(new Polyline(){
                tenonPoints[0],
                tenonPoints[1],
                tenonPoints[2],
                tenonPoints[3],
                tenonPoints[9],
                tenonPoints[8],
                tenonPoints[7],
                tenonPoints[6],
                tenonPoints[0]}.ToNurbsCurve()
            )[0];

            // debug.AddRange(tenonGeo);

            var mortisePoints = new Point3d[8];
            for (int i = 0; i < 2; ++i)
            {
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonFaceOffsetPlane, MiddlePlane, out mortisePoints[0 + i * 4]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonFaceOffsetPlane, TopPlane, out mortisePoints[1 + i * 4]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonBackPlane, MiddlePlane, out mortisePoints[2 + i * 4]);
                RX.PlanePlanePlane(TenonNeckPlanes[i], TenonBackPlane, TopPlane, out mortisePoints[3 + i * 4]);
            }

            // debug.AddRange(TenonNeckPlanes.Select(x => new GH_Plane(x)));

            // debug.Add(new GH_Point(mortisePoints[0]));
            // debug.Add(new GH_Point(mortisePoints[1]));
            // debug.Add(new GH_Point(mortisePoints[2]));
            // debug.Add(new GH_Point(mortisePoints[3]));   

            var mortiseGeo = new Brep[4];
            mortiseGeo[0] = Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[2], mortisePoints[6], mortisePoints[4], 0.001);
            mortiseGeo[1] = Brep.CreateFromCornerPoints(mortisePoints[0], mortisePoints[1], mortisePoints[3], mortisePoints[2], 0.001);
            mortiseGeo[2] = Brep.CreateFromCornerPoints(mortisePoints[2], mortisePoints[3], mortisePoints[7], mortisePoints[6], 0.001);
            mortiseGeo[3] = Brep.CreateFromCornerPoints(mortisePoints[4], mortisePoints[5], mortisePoints[7], mortisePoints[6], 0.001);

            // debug.AddRange(mortiseGeo);


            var tenonGeoJoined = Brep.JoinBreps(tenonGeo, 0.001);
            if (tenonGeoJoined == null) throw new Exception($"{GetType().Name}: tenonGeoJoined failed.");
            var mortiseGeoJoined = Brep.JoinBreps(mortiseGeo, 0.001);
            if (mortiseGeoJoined == null) throw new Exception($"{GetType().Name}: mortiseGeoJoined failed.");

            Parts[0].Geometry.AddRange(tenonGeoJoined);
            Parts[1].Geometry.AddRange(mortiseGeoJoined);

            // Dowels
            var dowel = new Cylinder(
                new Circle(
                    new Plane(TenonPlane.Origin + tenonUp * tenonHeight * 0.25 - tenonDirection * tenonHeight, tenonDirection),
                    8), tenonHeight + mortiseWidth + 100).ToBrep(true, true);

            debug.Add(dowel);

            Parts[0].Geometry.Add(dowel);
            Parts[1].Geometry.Add(dowel);

            return 0;
        }
    }
}
