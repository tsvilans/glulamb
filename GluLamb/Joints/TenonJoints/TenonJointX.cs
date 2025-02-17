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

        public TJointX()
        {
        }

        public TJointX(JointX parent)
        {
            if (parent.Parts.Count != 2)
                throw new ArgumentException($"{GetType().Name} requires a 2-part joint.");

            // if (!(JointPartX.IsAtEnd(parent.Parts[0].Case) && JointPartX.IsAtMiddle(parent.Parts[1].Case)))
            //     throw new ArgumentException($"{GetType().Name} requires one intersection at the end and " + 
            //     "one in the middle.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override void Configure(Dictionary<string, double> values)
        {
            if (values.TryGetValue("Inset", out double _inset)) Inset = _inset;
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("BlindOffset", out double _blindoffset)) BlindOffset = _blindoffset;
            if (values.TryGetValue("FlipDirection", out double _flipdirection)) FlipDirection = _flipdirection > 0;
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

            // if (!mortiseToTenon.IsZero && Normal * mortiseToTenon < 0)
            //     Normal.Reverse();

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

            debug.Add(new Line(MortisePlane.Origin, MortiseSideDirection, 300));

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

            debug.Add((TenonFacePlane));
            debug.Add((TenonBackPlane));
            debug.Add((TenonSidePlanes[0]));
            debug.Add((TenonSidePlanes[1]));

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

            debug.Add(TopPlane);
            debug.Add(MiddlePlane);
            debug.Add(BottomPlane);

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

            return 0;
        }
    }
}
