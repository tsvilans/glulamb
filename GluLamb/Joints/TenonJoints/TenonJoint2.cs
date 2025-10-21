using Rhino.Geometry;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RX = Rhino.Geometry.Intersect.Intersection;

namespace GluLamb.Joints.TenonJoints
{
    public class TJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0.0;
        public double BlindOffset = 0;
        public double ThicknessOffset = 0;
        public double BackOffset = 10;

        public bool FlipDirection = false;

        public Plane MortisePlane = Plane.Unset;
        public Plane TenonPlane = Plane.Unset;

        public Plane TopPlane = Plane.Unset;
        public Plane MiddlePlane = Plane.Unset;
        public Plane BottomPlane = Plane.Unset;

        public Plane TenonBackPlane = Plane.Unset;
        public Plane TenonBackOffsetPlane = Plane.Unset;
        public Plane TenonFrontPlane = Plane.Unset;

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
            if (values.TryGetValue("ThicknessOffset", out double _thicknessoffset)) ThicknessOffset = _thicknessoffset;
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

            TenonBackPlane = new Plane(
                MortisePlane.Origin - (MortiseSideDirection * (mortiseWidth * 0.5 + BackOffset)),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            TenonFrontPlane = new Plane(
                MortisePlane.Origin + MortiseSideDirection * (mortiseWidth * 0.5 - BlindOffset),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);


            dim = GluLamb.Utility.ClosestDimension2D(TenonBackPlane, TenonPlane.XAxis);

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

            MortiseSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), tenonDirection, tenonUp),
            };

            var centre = (TenonPlane.Origin + MortisePlane.Origin) * 0.5;

            TenonBackPlane.Origin = centre.ProjectToPlane(TenonBackPlane);
            TenonFrontPlane.Origin = centre.ProjectToPlane(TenonFrontPlane);
            TenonSidePlanes[0].Origin = centre.ProjectToPlane(TenonSidePlanes[0]);
            TenonSidePlanes[1].Origin = centre.ProjectToPlane(TenonSidePlanes[1]);

            //debug.Add(new GH_Plane(TenonFacePlane));
            //debug.Add(new GH_Plane(TenonBackPlane));
            //debug.Add(new GH_Plane(TenonSidePlanes[0]));
            //debug.Add(new GH_Plane(TenonSidePlanes[1]));

            var TenonBackPlaneOffset = new Plane(
                TenonBackPlane.Origin - TenonBackPlane.ZAxis * Added,
                TenonBackPlane.XAxis,
                TenonBackPlane.YAxis
                );

            var TenonOutPlane = new Plane(
                TenonBackPlane.Origin - TenonBackPlane.ZAxis * BackOffset,
                TenonBackPlane.XAxis,
                TenonBackPlane.YAxis
                );

            // For a real tenon...
            var tenonThickness = Math.Min(tenonHeight, mortiseHeight) * 0.5;

            var TenonBottomPlane = new Plane(
                TenonBackPlane.Origin - TenonBackPlane.YAxis * tenonThickness * 0.5,
                TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            var TenonTopPlane = new Plane(
                TenonBackPlane.Origin + TenonBackPlane.YAxis * (tenonHeight * 0.5 + Added),
                TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );


            // For a lap joint...
            TopPlane = new Plane(
                TenonBackPlane.Origin + TenonBackPlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added),
                TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            MiddlePlane = new Plane(
                TenonBackPlane.Origin,
                TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            BottomPlane = new Plane(
                TenonBackPlane.Origin - TenonBackPlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added),
                TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            //debug.Add(new GH_Plane(TopPlane));
            //debug.Add(new GH_Plane(MiddlePlane));
            //debug.Add(new GH_Plane(BottomPlane));

            TryGetBisectingPlane(TenonPlane, MortisePlane, out Plane InternalPlane, false);
            TryGetBisectingPlane(TenonPlane, MortisePlane, out Plane ExternalPlane, true);

            RX.PlanePlanePlane(MortiseSidePlanes[0], TenonBackPlane, MiddlePlane, out Point3d internalPlaneOrigin);
            InternalPlane.Origin = internalPlaneOrigin;

            RX.PlanePlanePlane(MortiseSidePlanes[1], TenonBackPlane, MiddlePlane, out Point3d externalPlaneOrigin);
            ExternalPlane.Origin = externalPlaneOrigin;

            var points = new Point3d[18];

            for (int i = 0; i < 2; ++i)
            {
                var ii = i * 9;

                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, BottomPlane, out points[0 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackOffsetPlane, BottomPlane, out points[1 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, MiddlePlane, out points[2 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, MiddlePlane, out points[3 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, TopPlane, out points[4 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackOffsetPlane, TopPlane, out points[5 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, TopPlane, out points[6 + ii]);

                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, TopPlane, out points[7 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, TopPlane, out points[8 + ii]);
            }

            debug.Clear();
            //debug.AddRange(points.Select(x => new GH_Point(x)));
            //debug.Add(new GH_Plane(TenonBackPlane));

            //debug.Add(new GH_Plane(InternalPlane));
            //debug.Add(new GH_Plane(ExternalPlane));

            var tenonPoints = new Point3d[8];

            for (int i = 0; i < 2; ++i)
            {
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonBackPlane, BottomPlane, out tenonPoints[0 + i * 4]);
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonBackPlane, MiddlePlane, out tenonPoints[1 + i * 4]);
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonFrontPlane, MiddlePlane, out tenonPoints[2 + i * 4]);
                RX.PlanePlanePlane(TenonSidePlanes[i], TenonFrontPlane, TopPlane, out tenonPoints[3 + i * 4]);
            }

            var tenonGeo = new Brep[3];
            tenonGeo[0] = Brep.CreateFromCornerPoints(tenonPoints[0], tenonPoints[1], tenonPoints[5], tenonPoints[4], 0.001);
            tenonGeo[1] = Brep.CreateFromCornerPoints(tenonPoints[1], tenonPoints[2], tenonPoints[6], tenonPoints[5], 0.001);
            tenonGeo[2] = Brep.CreateFromCornerPoints(tenonPoints[2], tenonPoints[3], tenonPoints[7], tenonPoints[6], 0.001);

            //debug.AddRange(tenonGeo);

            var mortisePoints = new Point3d[8];
            for (int i = 0; i < 2; ++i)
            {
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlaneOffset, MiddlePlane, out mortisePoints[0 + i * 4]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlaneOffset, TopPlane, out mortisePoints[1 + i * 4]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, MiddlePlane, out mortisePoints[2 + i * 4]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, TopPlane, out mortisePoints[3 + i * 4]);
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

        /// <summary>
        /// Computes the plane that bisects the angle between two input planes.
        /// </summary>
        /// <param name="planeA">First plane.</param>
        /// <param name="planeB">Second plane.</param>
        /// <param name="bisector">Resulting bisecting plane.</param>
        /// <returns>True if successful, false if planes are parallel or nearly parallel.</returns>
        public static bool TryGetBisectingPlane(Plane planeA, Plane planeB, out Plane bisector, bool external = false)
        {
            bisector = Plane.Unset;

            // 1. Get unit normals
            Vector3d nA = planeA.Normal;
            Vector3d nB = planeB.Normal;
            nA.Unitize();
            nB.Unitize();

            // 2. Compute bisector normal (internal or external)
            Vector3d nBis = external ? nA - nB : nA + nB;
            if (!nBis.Unitize())
                return false; // parallel or opposite planes

            // 3. Find intersection line between the two planes
            if (!Rhino.Geometry.Intersect.Intersection.PlanePlane(planeA, planeB, out Line intersection))
                return false;

            // 4. Use midpoint of intersection line as the origin
            Point3d origin = intersection.PointAt(0.5);

            // 5. Construct bisector plane
            bisector = new Plane(origin, nBis);

            return true;
        }
    }
}
