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

        public Plane MortisePlane = Plane.Unset;
        public Plane TenonPlane = Plane.Unset;

        public Plane MortiseSide0Plane = Plane.Unset;
        public Plane MortiseSide1Plane = Plane.Unset;
        public Plane MortiseSide0AddedPlane = Plane.Unset;
        public Plane MortiseSide1AddedPlane = Plane.Unset;

        public Plane TenonSide0Plane = Plane.Unset;
        public Plane TenonSide1Plane = Plane.Unset;
        public Plane TenonSide0AddedPlane = Plane.Unset;
        public Plane TenonSide1AddedPlane = Plane.Unset;

        public Plane LapPlane = Plane.Unset;
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

            var tenonIndex = JointPartX.IsAtEnd(Parts[0].Case) ? 0 : 1;
            var mortiseIndex = JointPartX.IsAtMiddle(Parts[1].Case) ? 1 : 0;

            var tenon = beams[Parts[tenonIndex].ElementIndex];
            var mortise = beams[Parts[mortiseIndex].ElementIndex];

            var tenonDirection = Parts[tenonIndex].Direction;
            var mortiseDirection = Parts[mortiseIndex].Direction;

            TenonPlane = tenon.GetPlane(Parts[tenonIndex].Parameter);
            MortisePlane = mortise.GetPlane(Parts[mortiseIndex].Parameter);

            // Tenon is a slave to Mortise

            var tenonSideDirection = Utility.ClosestAxis(TenonPlane, mortiseDirection);
            var mortiseSideDirection = Utility.ClosestAxis(MortisePlane, tenonDirection);

            debug.Add(new GH_Vector(tenonSideDirection));
            debug.Add(new GH_Vector(mortiseSideDirection));

            double tenonWidth, tenonHeight;
            if (Math.Abs(mortiseDirection * TenonPlane.XAxis) > Math.Abs(mortiseDirection * TenonPlane.YAxis))
            {
                tenonWidth = tenon.Width;
                tenonHeight = tenon.Height;
            }
            else
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            double mortiseWidth, mortiseHeight;
            if (Math.Abs(tenonDirection * MortisePlane.XAxis) > Math.Abs(tenonDirection * MortisePlane.YAxis))
            {
                mortiseWidth = mortise.Width;
                mortiseHeight = mortise.Height;
            }
            else
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

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

            return 0;
        }
    }
}
