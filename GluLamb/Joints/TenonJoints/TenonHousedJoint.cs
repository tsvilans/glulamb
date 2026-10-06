using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Collections;
using Rhino.Geometry;
using RX = Rhino.Geometry.Intersect.Intersection;

using GluLamb.Features;

namespace GluLamb.Joints
{
    /// <summary>
    /// Housed T tenon: the end of one beam is let into the side of another with its full section
    /// (a bearing seat), then continues as a half-lap tongue that stops short of the far face.
    /// The shoulders are cut along the bisector between the beams ("wings"), which takes up
    /// surface variations and dimensional differences and avoids thin slivers on oblique joints.
    /// Port of the TenonJoint2 prototype (TJointX in GluLamb.Joints.TenonJoints): same geometry,
    /// one shared cutter output as a Lap feature on each beam, with the machining outlines and
    /// depths in the features' Data. The part at the end of its beam is the tenon.
    /// </summary>
    [JointType("glulamb.t-housed", Name = "T housed tenon", Arity = 2, Topology = JointTopology.T,
        Description = "End of one beam housed into the side of another, with a half-lap tongue and angled shoulders.")]
    public class TenonHousedJoint : JointBase
    {
        [JointParameter(Description = "Extra length added to cutters so they clear the beams.", Unit = "length")]
        public double Added { get; set; } = 10.0;

        [JointParameter(Description = "Distance from the far side of the mortise beam to the end of the tongue. 0 runs through.", Unit = "length")]
        public double BlindOffset { get; set; } = 0;

        [JointParameter(Description = "How much narrower the tongue is than the tenon beam, on each side.", Unit = "length")]
        public double ThicknessOffset { get; set; } = 0;

        [JointParameter(Description = "Depth of the full-section housing past the shoulder.", Unit = "length")]
        public double BackOffset { get; set; } = 10;

        [JointParameter(Description = "Depth of the shoulder into the mortise beam's face.", Unit = "length")]
        public double OutInset { get; set; } = 5;

        public TenonHousedJoint(JointX condition) : base(condition)
        {
        }

        public static double Score(JointX condition, IJointContext context) => 0.5;

        protected override void ConstructCore(Beam[] beams, IJointContext context, JointResult result)
        {
            int ti = 0, mi = 1;
            if (JointPartX.IsAtMiddle(m_parts[0].Case) && JointPartX.IsAtEnd(m_parts[1].Case))
            {
                ti = 1; mi = 0;
            }

            var tenon = beams[ti];
            var mortise = beams[mi];
            var tolerance = context.Tolerance;

            var tenonDirection = m_parts[ti].Direction;
            var mortiseDirection = m_parts[mi].Direction;

            var tenonPlane = tenon.GetPlane(m_parts[ti].Parameter);
            var mortisePlane = mortise.GetPlane(m_parts[mi].Parameter);

            double tenonWidth = tenon.Width, tenonHeight = tenon.Height;
            double mortiseWidth = mortise.Width, mortiseHeight = mortise.Height;

            var normal = Utility.ClosestAxis(mortisePlane, Vector3d.CrossProduct(tenonDirection, mortiseDirection));

            // Orient the normal with the tenon's up axis, then let the higher beam decide (the
            // tenon when they are level); Flip inverts.
            if (normal * NearestSectionAxis(tenonPlane, normal) < 0.0)
                normal.Reverse();

            if (mortiseDirection * Vector3d.CrossProduct(tenonDirection, normal) < 0)
                mortiseDirection.Reverse();

            if (TopPart(tenonPlane.Origin, mortisePlane.Origin, normal, tolerance) != 0)
                normal.Reverse();

            var tenonUp = Utility.ClosestAxis(tenonPlane, normal);

            if (Utility.ClosestDimension2D(mortisePlane, tenonDirection) != 0)
            {
                mortiseWidth = mortise.Height;
                mortiseHeight = mortise.Width;
            }

            var mortiseSideDirection = Utility.ClosestAxis(mortisePlane, tenonDirection);
            var tenonSideDirection = Utility.ClosestAxis(tenonPlane, mortiseDirection);

            var jointSidePlane = new Plane(
                mortisePlane.Origin - mortiseSideDirection * mortiseWidth * 0.5,
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            var tenonBackPlane = new Plane(
                jointSidePlane.Origin + jointSidePlane.Normal * (BackOffset + OutInset),
                jointSidePlane.XAxis, jointSidePlane.YAxis);

            var tenonFrontPlane = new Plane(
                mortisePlane.Origin + mortiseSideDirection * (mortiseWidth * 0.5 - BlindOffset),
                Vector3d.CrossProduct(normal, mortiseSideDirection), normal);

            if (Utility.ClosestDimension2D(tenonBackPlane, tenonPlane.XAxis) != 0)
            {
                tenonWidth = tenon.Height;
                tenonHeight = tenon.Width;
            }

            var mortiseSidePlanes = new[]
            {
                new Plane(tenonPlane.Origin + tenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), tenonDirection, tenonUp),
                new Plane(tenonPlane.Origin - tenonSideDirection * (tenonWidth * 0.5 - ThicknessOffset), -tenonDirection, tenonUp),
            };

            var centre = (tenonPlane.Origin + mortisePlane.Origin) * 0.5;
            Position = new Plane(centre, tenonSideDirection, mortiseSideDirection);

            tenonBackPlane.Origin = centre.ProjectToPlane(tenonBackPlane);
            tenonFrontPlane.Origin = centre.ProjectToPlane(tenonFrontPlane);

            var tenonOutPlane = new Plane(
                jointSidePlane.Origin + jointSidePlane.ZAxis * OutInset,
                jointSidePlane.XAxis, jointSidePlane.YAxis);

            var tenonOutPlaneOffset = new Plane(
                jointSidePlane.Origin - jointSidePlane.ZAxis * Added,
                jointSidePlane.XAxis, jointSidePlane.YAxis);

            // Cutters have to reach past both beams even when their centrelines are offset
            var offset = Math.Abs((tenonPlane.Origin - mortisePlane.Origin) * normal);
            var halfDepth = Math.Max(tenonHeight, mortiseHeight) * 0.5 + offset;

            var topPlane = new Plane(tenonBackPlane.Origin + tenonBackPlane.YAxis * (halfDepth + Added), -tenonBackPlane.XAxis, tenonBackPlane.ZAxis);
            var middlePlane = new Plane(tenonBackPlane.Origin, -tenonBackPlane.XAxis, tenonBackPlane.ZAxis);
            var bottomPlane = new Plane(tenonBackPlane.Origin - tenonBackPlane.YAxis * (halfDepth + Added * 2), -tenonBackPlane.XAxis, tenonBackPlane.ZAxis);

            var biInt = tenonSideDirection - mortiseSideDirection;
            var biExt = tenonSideDirection + mortiseSideDirection;
            biInt.Unitize();
            biExt.Unitize();

            bool ok = true;
            ok &= RX.PlanePlanePlane(mortiseSidePlanes[1], tenonOutPlane, middlePlane, out Point3d internalPlaneOrigin);
            ok &= RX.PlanePlanePlane(mortiseSidePlanes[0], tenonOutPlane, middlePlane, out Point3d externalPlaneOrigin);

            var wingPlanes = new[]
            {
                new Plane(externalPlaneOrigin, normal, Vector3d.CrossProduct(biExt, -normal)),
                new Plane(internalPlaneOrigin, normal, Vector3d.CrossProduct(biInt, normal)),
            };

            var points = new Point3d[22];
            for (int i = 0; i < 2; ++i)
            {
                var ii = i * 11;
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonOutPlane, bottomPlane, out points[0 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonBackPlane, bottomPlane, out points[1 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonBackPlane, middlePlane, out points[2 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFrontPlane, middlePlane, out points[3 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFrontPlane, middlePlane, out points[4 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFrontPlane, topPlane, out points[5 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonFrontPlane, topPlane, out points[6 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonBackPlane, topPlane, out points[7 + ii]);
                ok &= RX.PlanePlanePlane(mortiseSidePlanes[i], tenonOutPlane, topPlane, out points[8 + ii]);
                ok &= RX.PlanePlanePlane(wingPlanes[i], tenonOutPlaneOffset, topPlane, out points[9 + ii]);
                ok &= RX.PlanePlanePlane(wingPlanes[i], tenonOutPlaneOffset, bottomPlane, out points[10 + ii]);
            }

            if (!ok)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: planes did not intersect.");
                return;
            }

            // The tenon beam has to reach the end of the tongue, across its full width
            ExtendToReach(result, tenon, m_parts[ti], new[] { points[3], points[14], points[6], points[17] });

            var faces = new Brep[7];
            for (int i = 0; i < 2; ++i)
            {
                var ii = i * 11;
                var outline = Math.Abs(BackOffset) > 1e-3
                    ? new Polyline { points[0 + ii], points[1 + ii], points[2 + ii], points[3 + ii], points[6 + ii], points[8 + ii], points[0 + ii] }
                    : new Polyline { points[2 + ii], points[3 + ii], points[6 + ii], points[7 + ii], points[2 + ii] };

                faces[0 + i * 2] = Brep.CreatePlanarBreps(outline.ToNurbsCurve(), tolerance)?.FirstOrDefault();
                faces[1 + i * 2] = Brep.CreateFromCornerPoints(points[0 + ii], points[8 + ii], points[9 + ii], points[10 + ii], tolerance);
            }

            faces[4] = Brep.CreateFromCornerPoints(points[1], points[12], points[13], points[2], tolerance);
            faces[5] = Brep.CreateFromCornerPoints(points[2], points[13], points[14], points[3], tolerance);
            faces[6] = Brep.CreateFromCornerPoints(points[3], points[14], points[17], points[6], tolerance);

            var joined = faces.Any(x => x == null) ? null : Brep.JoinBreps(faces, tolerance);
            if (joined == null || joined.Length < 1)
            {
                result.Status = JointStatus.Failed;
                result.Messages.Add($"{GetType().Name}: failed to create the cutter.");
                return;
            }

            // Machining data, as in the prototype
            var data = new ArchivableDictionary();
            var planes = new ArchivableDictionary();
            planes.Set("Top", topPlane);
            planes.Set("Middle", Position);
            planes.Set("Bottom", bottomPlane);
            planes.Set("Wing0", wingPlanes[0]);
            planes.Set("Wing1", wingPlanes[1]);
            planes.Set("TenonBack", tenonBackPlane);
            planes.Set("TenonFront", tenonFrontPlane);
            planes.Set("TenonOut", tenonOutPlane);
            planes.Set("TenonOutOffset", tenonOutPlaneOffset);
            planes.Set("MortiseSide0", mortiseSidePlanes[0]);
            planes.Set("MortiseSide1", mortiseSidePlanes[1]);
            data.Set("Planes", planes);

            data.Set("TopOutline", new Polyline { points[9], points[8], points[6], points[17], points[19], points[20] }.ToNurbsCurve());
            data.Set("TopPocket", new Polyline { points[8], points[6], points[17], points[19], points[8] }.ToNurbsCurve());
            data.Set("TopDepth", points[4].DistanceTo(points[5]));
            data.Set("WingEdge0", new Line(points[10], points[9]).ToNurbsCurve());
            data.Set("WingDepth0", points[0].DistanceTo(points[10]));
            data.Set("WingEdge1", new Line(points[20], points[21]).ToNurbsCurve());
            data.Set("WingDepth1", points[11].DistanceTo(points[21]));
            data.Set("TenonBackPocket", new Polyline { points[1], points[2], points[13], points[12], points[1] }.ToNurbsCurve());
            data.Set("TenonBackDepth", Math.Abs(tenonFrontPlane.DistanceTo(tenonBackPlane.Origin)));
            data.Set("TenonSeatDepth", tenonOutPlaneOffset.DistanceTo(tenonOutPlane.Origin));
            data.Set("TenonBack", new Polyline { points[1], points[7], points[18], points[12], points[1] }.ToNurbsCurve());
            data.Set("TenonFront", new Polyline { points[3], points[6], points[17], points[14], points[3] }.ToNurbsCurve());
            data.Set("TenonShoulder", new Polyline { points[0], points[8], points[19], points[11], points[0] }.ToNurbsCurve());
            data.Set("TraceDepth0", points[2].DistanceTo(points[3]));
            data.Set("TraceDepth1", points[13].DistanceTo(points[14]));

            foreach (var beam in new[] { tenon, mortise })
            {
                var lap = new Lap { BeamId = beam.Id, Plane = Position, Cutters = joined.Select(x => x.DuplicateBrep()).ToList() };
                lap.Data = data.Clone();
                result.Add(lap);
            }

            result.Status = JointStatus.Ok;
        }
    }
}
