using Rhino.Collections;
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
    public enum EndProfileType
    {
        LapTop,
        CenterTenon,
        LapBottom
    }

    public class TJointX : JointX
    {
        public double Added = 10.0;
        public double Inset = 0.0;
        public double BlindOffset = 0;
        public double ThicknessOffset = 0;
        public double BackOffset = 10;
        public double OutInset = 5;
        public double Radius = 0;

        public bool FlipDirection = false;

        public Plane MortisePlane = Plane.Unset;
        public Plane TenonPlane = Plane.Unset;

        public Plane TopPlane = Plane.Unset;
        public Plane MiddlePlane = Plane.Unset;
        public Plane BottomPlane = Plane.Unset;

        public Plane TenonBackPlane = Plane.Unset;
        public Plane TenonBackOffsetPlane = Plane.Unset;
        public Plane TenonFrontPlane = Plane.Unset;
        public Plane TenonFrontRadiusPlane = Plane.Unset;
        public Plane JointSidePlane = Plane.Unset;

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
            if (values.TryGetValue("BackOffset", out double _backoffset)) BackOffset = _backoffset;
            if (values.TryGetValue("OutInset", out double _outinset)) OutInset = _outinset;
            if (values.TryGetValue("Radius", out double _radius)) Radius = _radius;

            Console.WriteLine($"Radius: {Radius}");
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


            if (mortiseDirection * Vector3d.CrossProduct(tenonDirection, Normal) < 0)
                mortiseDirection.Reverse();
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


            debug.Add(new Line(TenonPlane.Origin, TenonSideDirection, 400));
            debug.Add(new Line(TenonPlane.Origin, MortiseSideDirection, 400));

            debug.Add(new Line(MortisePlane.Origin, tenonDirection, 300));
            debug.Add(new Line(TenonPlane.Origin, mortiseDirection, 300));
            debug.Add(new Line(TenonPlane.Origin, Normal, 300));

            JointSidePlane = new Plane(
                MortisePlane.Origin - (MortiseSideDirection * mortiseWidth * 0.5),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            TenonBackPlane = new Plane(
                JointSidePlane.Origin + JointSidePlane.Normal * (BackOffset + OutInset),
                JointSidePlane.XAxis, JointSidePlane.YAxis);

            TenonFrontPlane = new Plane(
                MortisePlane.Origin + MortiseSideDirection * (mortiseWidth * 0.5 - BlindOffset),
                Vector3d.CrossProduct(Normal, MortiseSideDirection), Normal);

            TenonFrontRadiusPlane = new Plane(
                TenonFrontPlane.Origin - TenonFrontPlane.Normal * Radius,
                TenonFrontPlane.XAxis, TenonFrontPlane.YAxis);


            dim = GluLamb.Utility.ClosestDimension2D(TenonBackPlane, TenonPlane.XAxis);

            if (dim != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }


            TenonSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 + Added), tenonDirection, tenonUp),
            };

            MortiseSidePlanes = new Plane[]{
                new Plane(TenonPlane.Origin + TenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), tenonDirection, tenonUp),
                new Plane(TenonPlane.Origin - TenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), -tenonDirection, tenonUp),
            };

            var centre = (TenonPlane.Origin + MortisePlane.Origin) * 0.5;

            Position = new Plane(centre, TenonSideDirection, MortiseSideDirection);

            TenonBackPlane.Origin = centre.ProjectToPlane(TenonBackPlane);
            TenonFrontPlane.Origin = centre.ProjectToPlane(TenonFrontPlane);
            TenonSidePlanes[0].Origin = centre.ProjectToPlane(TenonSidePlanes[0]);
            TenonSidePlanes[1].Origin = centre.ProjectToPlane(TenonSidePlanes[1]);

            var TenonOutPlane = new Plane(
                JointSidePlane.Origin + JointSidePlane.ZAxis * OutInset,
                JointSidePlane.XAxis,
                JointSidePlane.YAxis
                );

            var TenonOutPlaneOffset = new Plane(
                JointSidePlane.Origin - JointSidePlane.ZAxis * Added,
                JointSidePlane.XAxis,
                JointSidePlane.YAxis
                );

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

            TopPlane = new Plane(
                TenonBackPlane.Origin + TenonBackPlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added),
                -TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            MiddlePlane = new Plane(
                TenonBackPlane.Origin,
                -TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            BottomPlane = new Plane(
                TenonBackPlane.Origin - TenonBackPlane.YAxis * (Math.Max(tenonHeight, mortiseHeight) * 0.5 + Added * 2),
                -TenonBackPlane.XAxis,
                TenonBackPlane.ZAxis
                );

            var biInt = TenonSideDirection - MortiseSideDirection;
            var biExt = TenonSideDirection + MortiseSideDirection;

            biInt.Unitize();
            biExt.Unitize();

            RX.PlanePlanePlane(MortiseSidePlanes[1], TenonOutPlane, MiddlePlane, out Point3d internalPlaneOrigin);

            RX.PlanePlanePlane(MortiseSidePlanes[0], TenonOutPlane, MiddlePlane, out Point3d externalPlaneOrigin);

            var WingPlanes = new Plane[]
            {
                new Plane(externalPlaneOrigin, Normal, Vector3d.CrossProduct(biExt, -Normal)),
                new Plane(internalPlaneOrigin, Normal, Vector3d.CrossProduct(biInt, Normal)),
            };

            var points = new Point3d[22];

            for (int i = 0; i < 2; ++i)
            {
                var ii = i * 11;

                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonOutPlane, BottomPlane, out points[0 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, BottomPlane, out points[1 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, MiddlePlane, out points[2 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, MiddlePlane, out points[3 + ii]);
                // RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontRadiusPlane, MiddlePlane, out points[3 + ii]);
                // RX.PlanePlanePlane(MortiseSideRadiusPlanes[i], TenonFrontPlane, MiddlePlane, out points[4 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, MiddlePlane, out points[4 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, TopPlane, out points[5 + ii]);
                // RX.PlanePlanePlane(MortiseSideRadiusPlanes[i], TenonFrontPlane, TopPlane, out points[5 + ii]);
                // RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontRadiusPlane, TopPlane, out points[6 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonFrontPlane, TopPlane, out points[6 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonBackPlane, TopPlane, out points[7 + ii]);
                RX.PlanePlanePlane(MortiseSidePlanes[i], TenonOutPlane, TopPlane, out points[8 + ii]);

                RX.PlanePlanePlane(WingPlanes[i], TenonOutPlaneOffset, TopPlane, out points[9 + ii]);
                RX.PlanePlanePlane(WingPlanes[i], TenonOutPlaneOffset, BottomPlane, out points[10 + ii]);
            }

            var geo = new Brep[7];

            var topOutline = new Polyline(){
                points[9],
                points[8],
                points[6],
                points[17],
                points[19],
                points[20],
            };

            for (int i = 0; i < 2; ++i)
            {
                var ii = i * 11;
                // geo[0 + i * 2] = Brep.CreateFromCornerPoints(points[0 + ii], points[1 + ii], points[5 + ii], points[6 + ii], 1e-3);

                Polyline outline;

                if (Math.Abs(BackOffset) > 1e-3)
                {
                    outline = new Polyline{
                        points[0 + ii],
                        points[1 + ii],
                        points[2 + ii],
                        points[3 + ii],
                        points[6 + ii],
                        points[8 + ii],
                        points[0 + ii],
                    };
                }
                else
                {
                    outline = new Polyline{
                        points[2 + ii],
                        points[3 + ii],
                        points[6 + ii],
                        points[7 + ii],
                        points[2 + ii],
                    };
                }

                geo[0 + i * 2] = Brep.CreatePlanarBreps(outline.ToNurbsCurve(), 1e-3).FirstOrDefault();
                geo[1 + i * 2] = Brep.CreateFromCornerPoints(points[0 + ii], points[8 + ii], points[9 + ii], points[10 + ii], 1e-3);
            }

            // return 0;

            geo[4] = Brep.CreateFromCornerPoints(points[1], points[12], points[13], points[2], 1e-3);
            geo[5] = Brep.CreateFromCornerPoints(points[2], points[13], points[14], points[3], 1e-3);
            geo[6] = Brep.CreateFromCornerPoints(points[3], points[14], points[17], points[6], 1e-3);

            // debug.AddRange(geo);

            var joined = Brep.JoinBreps(geo, 1e-3);
            if (joined == null) throw new Exception($"{GetType().Name}: joined failed.");

            Parts[0].Geometry.AddRange(joined);
            Parts[1].Geometry.AddRange(joined);

            var topPocket = new Polyline(){
                points[8],
                points[6],
                points[17],
                points[19],
                points[8],
            };

            var planeDict = new ArchivableDictionary();
            planeDict.Set("Top", TopPlane);
            planeDict.Set("Middle", Position);
            planeDict.Set("Bottom", BottomPlane);
            planeDict.Set("Wing0", WingPlanes[0]);
            planeDict.Set("Wing1", WingPlanes[1]);
            planeDict.Set("TenonBack", TenonBackPlane);
            planeDict.Set("TenonFront", TenonFrontPlane);
            planeDict.Set("TenonOut", TenonOutPlane);
            planeDict.Set("TenonOutOffset", TenonOutPlaneOffset);
            planeDict.Set("MortiseSide0", MortiseSidePlanes[0]);
            planeDict.Set("MortiseSide1", MortiseSidePlanes[1]);

            // Create machining geometry
            for (int i = 0; i < 2; ++i)
            {
                Parts[i].Data.Set("Planes", planeDict);

                Parts[i].Data.Set("TopOutline", topOutline.ToNurbsCurve());
                Parts[i].Data.Set("TopPocket", topPocket.ToNurbsCurve());
                Parts[i].Data.Set("TopDepth", points[4].DistanceTo(points[5]));

                Parts[i].Data.Set("WingPlane0", WingPlanes[0]);
                Parts[i].Data.Set("WingPlane1", WingPlanes[1]);
                Parts[i].Data.Set("WingEdge0", new Line(points[10], points[9]).ToNurbsCurve());
                Parts[i].Data.Set("WingDepth0", points[0].DistanceTo(points[10]));
                Parts[i].Data.Set("WingEdge1", new Line(points[20], points[21]).ToNurbsCurve());
                Parts[i].Data.Set("WingDepth1", points[11].DistanceTo(points[21]));

                Parts[i].Data.Set("TenonBackPocket", new Polyline() { points[1], points[2], points[13], points[12], points[1] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonBackPlane", TenonBackPlane);
                Parts[i].Data.Set("TenonBackDepth", Math.Abs(TenonFrontPlane.DistanceTo(TenonBackPlane.Origin)));
                Parts[i].Data.Set("TenonSeatDepth", TenonOutPlaneOffset.DistanceTo(TenonOutPlane.Origin));
                Parts[i].Data.Set("TenonBack", new Polyline() { points[1], points[7], points[18], points[12], points[1] }.ToNurbsCurve());

                Parts[i].Data.Set("TenonFront", new Polyline() { points[3], points[6], points[17], points[14], points[3] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonFrontPlane", TenonFrontPlane);
                Parts[i].Data.Set("TenonFrontDepth", new Polyline() { points[3], points[6], points[17], points[14], points[3] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonShoulderPlane", TenonOutPlane);
                Parts[i].Data.Set("TenonShoulder", new Polyline() { points[0], points[8], points[19], points[11], points[0] }.ToNurbsCurve());

                Parts[i].Data.Set("TenonTrace0", new Polyline() { points[0], points[8] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonTraceDepth0", new Polyline() { points[0], points[8] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonTrace1", new Polyline() { points[19], points[11] }.ToNurbsCurve());
                Parts[i].Data.Set("TenonTraceDepth1", new Polyline() { points[0], points[8] }.ToNurbsCurve());

                Parts[i].Data.Set("TraceDepth0", points[2].DistanceTo(points[3]));
                Parts[i].Data.Set("TraceDepth1", points[13].DistanceTo(points[14]));

                Parts[i].Data.Set("MortiseSidePlane0", MortiseSidePlanes[0]);
                Parts[i].Data.Set("MortiseSidePlane1", MortiseSidePlanes[1]);
            }

            return 0;
        }

        public Curve FilletCurve(Curve crv, IEnumerable<int> corners, double radius = 10)
        {
            var segments = crv.DuplicateSegments();
            var newSegments = new List<Curve>();

            foreach (var corner in corners)
            {
                if (corner < 0 || corner > (segments.Length - 1)) continue;

                var c0 = segments[corner];
                var c1 = segments[corner + 1];

                var fillet = Curve.CreateFilletCurves(c0, c0.PointAtStart, c1, c1.PointAtEnd, radius, false, true, false, 1e-3, 1e-3);

                // Console.WriteLine($"Num. fillets: {fillet.Length}");

                if (fillet.Length == 3)
                {
                    segments[corner] = fillet[0];
                    segments[corner + 1] = fillet[1];
                    newSegments.Add(fillet[2]);
                }
            }

            newSegments.AddRange(segments);

            // Console.WriteLine($"New segments: {newSegments.Count}");

            var joined = Curve.JoinCurves(newSegments);

            // Console.WriteLine($"joined {joined.Length}");
            return joined.FirstOrDefault();
        }
    }
}
