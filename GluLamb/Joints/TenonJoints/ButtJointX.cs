using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints
{
    public class TButtJointX : JointX
    {
        public double Added = 10.0;
        public double Depth = 0.0;
        public double BlindOffset = 0;
        public double SideOffset = 100;
        public double DowelLength = 180;
        public double DowelSpacing = 80;

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

        public TButtJointX()
        {
        }

        public TButtJointX(JointX parent)
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
            if (values.TryGetValue("Depth", out double _depth)) Depth = _depth;
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("SideOffset", out double _sideoffset)) SideOffset = _sideoffset;
            if (values.TryGetValue("DowelLength", out double _dowellength)) DowelLength = _dowellength;
            if (values.TryGetValue("DowelSpacing", out double _dowelspacing)) DowelSpacing = _dowelspacing;

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

            // debug.Add(TenonPlane);
            // debug.Add(MortisePlane);

            var beam0Y = TenonPlane.YAxis;
            var beam1Y = TenonPlane.YAxis * MortisePlane.YAxis < 0 ? -MortisePlane.YAxis : MortisePlane.YAxis;

            // Get element dimensions
            double tenonWidth = tenon.Width, tenonHeight = tenon.Height;
            double mortiseWidth = mortise.Width, mortiseHeight = mortise.Height;

            Vector3d Normal = GluLamb.Utility.ClosestAxis(MortisePlane, tenonDirection);

            if (GluLamb.Utility.ClosestDimension2D(MortisePlane, tenonDirection) > 0)
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

            var SidePlane = new Plane(MortisePlane.Origin + Normal * (mortiseWidth * 0.5 - Depth), mortiseDirection, Vector3d.CrossProduct(Normal, mortiseDirection));
            var SideOffsetPlane = new Plane(SidePlane.Origin + SidePlane.ZAxis * (Added + Depth), SidePlane.XAxis, SidePlane.YAxis);

            // debug.Add(SidePlane);
            // debug.Add(SideOffsetPlane);

            var TenonTopPlane = new Plane(TenonPlane.Origin + TenonPlane.YAxis * tenonHeight * 0.5, tenonDirection, TenonPlane.XAxis);
            var TenonBottomPlane = new Plane(TenonPlane.Origin - TenonPlane.YAxis * tenonHeight * 0.5, tenonDirection, -TenonPlane.XAxis);

            var TenonRightPlane = new Plane(TenonPlane.Origin + TenonPlane.XAxis * (tenonWidth * 0.5 + SideOffset), tenonDirection, TenonPlane.YAxis);
            var TenonLeftPlane = new Plane(TenonPlane.Origin - TenonPlane.XAxis * (tenonWidth * 0.5 + SideOffset), tenonDirection, -TenonPlane.YAxis);

            // debug.Add(TenonTopPlane);
            // debug.Add(TenonBottomPlane);
            // debug.Add(TenonRightPlane);
            // debug.Add(TenonLeftPlane);

            var tenonCutters = new Brep[1];
            var points = new Point3d[4];
            RX.PlanePlanePlane(SidePlane, TenonTopPlane, TenonRightPlane, out points[0]);
            RX.PlanePlanePlane(SidePlane, TenonBottomPlane, TenonRightPlane, out points[1]);
            RX.PlanePlanePlane(SidePlane, TenonBottomPlane, TenonLeftPlane, out points[2]);
            RX.PlanePlanePlane(SidePlane, TenonTopPlane, TenonLeftPlane, out points[3]);

            double tolerance = 0.001;
            tenonCutters[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], tolerance);

            var mortiseCutters = new Brep[5];
            var pointsLow = new Point3d[4];
            RX.PlanePlanePlane(SideOffsetPlane, TenonTopPlane, TenonRightPlane, out pointsLow[0]);
            RX.PlanePlanePlane(SideOffsetPlane, TenonBottomPlane, TenonRightPlane, out pointsLow[1]);
            RX.PlanePlanePlane(SideOffsetPlane, TenonBottomPlane, TenonLeftPlane, out pointsLow[2]);
            RX.PlanePlanePlane(SideOffsetPlane, TenonTopPlane, TenonLeftPlane, out pointsLow[3]);

            mortiseCutters[0] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], tolerance);
            mortiseCutters[1] = Brep.CreateFromCornerPoints(points[0], points[1], pointsLow[1], pointsLow[0], tolerance);
            mortiseCutters[2] = Brep.CreateFromCornerPoints(points[1], points[2], pointsLow[2], pointsLow[1], tolerance);
            mortiseCutters[3] = Brep.CreateFromCornerPoints(points[2], points[3], pointsLow[3], pointsLow[2], tolerance);
            mortiseCutters[4] = Brep.CreateFromCornerPoints(points[3], points[0], pointsLow[0], pointsLow[3], tolerance);

            var mortiseCuttersJoined = Brep.JoinBreps(mortiseCutters, tolerance);

            Parts[0].Geometry.AddRange(tenonCutters);
            Parts[1].Geometry.AddRange(mortiseCuttersJoined);

            // Dowel

            Vector3d DowelClosestAxis = GluLamb.Utility.ClosestAxis(TenonPlane, SidePlane.ZAxis);


            var DowelPlane = new Plane(SidePlane.Origin, DowelClosestAxis, tenonDirection);

            debug.Add(DowelPlane);

            var DowelAxis = DowelPlane.Project(SidePlane.ZAxis);

            var TenonFinPlane = new Plane(TenonPlane.Origin, tenonDirection, TenonPlane.YAxis);

            var TenonDowel0Plane = new Plane(TenonPlane.Origin + TenonPlane.YAxis * DowelSpacing * 0.5, tenonDirection, TenonPlane.XAxis);
            var TenonDowel1Plane = new Plane(TenonPlane.Origin - TenonPlane.YAxis * DowelSpacing * 0.5, tenonDirection, TenonPlane.XAxis);

            debug.Add(TenonDowel0Plane);
            debug.Add(TenonDowel1Plane);

            RX.PlanePlanePlane(TenonFinPlane, TenonDowel0Plane, SidePlane, out Point3d DowelPoint0);
            RX.PlanePlanePlane(TenonFinPlane, TenonDowel1Plane, SidePlane, out Point3d DowelPoint1);

            DowelPlane = new Plane(SidePlane.Origin - DowelAxis * DowelLength * 0.5, DowelAxis);

            var DowelPlane0 = new Plane(DowelPoint0 - DowelAxis * DowelLength * 0.5, DowelAxis);
            var DowelPlane1 = new Plane(DowelPoint1 - DowelAxis * DowelLength * 0.5, DowelAxis);

            var dowels = new Brep[]{
                new Cylinder(
                    new Circle(
                        DowelPlane0, 8
                    ),
                    DowelLength
                ).ToBrep(true, true),
                new Cylinder(
                    new Circle(
                        DowelPlane1, 8
                    ),
                    DowelLength
                ).ToBrep(true, true)

                };

            Parts[0].Geometry.AddRange(dowels);
            Parts[1].Geometry.AddRange(dowels);

            return 0;
        }
    }
}
