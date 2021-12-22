using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public partial class JointConstructor
    {
        public bool DefaultTenonJoint(TenonJoint tj)
        {
            var tbeam = (tj.Tenon.Element as BeamElement).Beam;
            var mbeam = (tj.Mortise.Element as BeamElement).Beam;

            var tplane = tbeam.GetPlane(tj.Tenon.Parameter);
            var mplane = mbeam.GetPlane(tj.Mortise.Parameter);

            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, tplane);

            // Align plane directions
            int sign = 1;
            var tz = Math.Abs(tj.Tenon.Parameter - tbeam.Centreline.Domain.Min) >
              Math.Abs(tj.Tenon.Parameter - tbeam.Centreline.Domain.Max) ?
              tplane.ZAxis : -tplane.ZAxis;

            Plane mSidePlane = Plane.Unset;

            sign = mplane.XAxis * tz > 0 ? -1 : 1;

            mSidePlane = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign,
              mplane.ZAxis, mplane.YAxis);

            var mSidePlane2 = new Plane(mplane.Origin - mplane.XAxis * mbeam.Width * 0.5 * sign,
              mplane.ZAxis, mplane.YAxis);


            // Create tenon cutting geometry

            double added = 10.0;
            {
                var proj0 = mSidePlane.ProjectAlongVector(tz);
                var proj1 = mSidePlane2.ProjectAlongVector(tz);

                var pt0 = new Point3d(-tbeam.Width * 0.5 - added, 0, 0);
                var pt1 = new Point3d(tbeam.Width * 0.5 + added, 0, 0);

                pt0.Transform(xform);
                pt1.Transform(xform);

                var pt2 = pt0 + tz * (mbeam.Width + added);
                var pt3 = pt1 + tz * (mbeam.Width + added);

                // Create projection along Tenon Z-axis onto the Mortise side plane

                pt0.Transform(proj0);
                pt1.Transform(proj0);

                var tsrf0 = Brep.CreateFromCornerPoints(pt0, pt1, pt2, pt3, 0.001);

                var pt4 = pt0 + tplane.YAxis * (tbeam.Height * 0.5 + added);
                var pt5 = pt1 + tplane.YAxis * (tbeam.Height * 0.5 + added);

                pt4.Transform(proj0);
                pt5.Transform(proj0);

                var tsrf1 = Brep.CreateFromCornerPoints(pt0, pt1, pt5, pt4, 0.001);

                var joined = Brep.JoinBreps(new Brep[] { tsrf0, tsrf1 }, 0.01);

                if (joined == null) joined = new Brep[] { tsrf0, tsrf1 };

                tj.Tenon.Geometry.AddRange(joined);

                // Create trim cut surface
                var pt6 = new Point3d(-tbeam.Width * 0.5 - added, -tbeam.Height * 0.5 - added, 0);
                var pt7 = new Point3d(tbeam.Width * 0.5 + added, -tbeam.Height * 0.5 - added, 0);
                var pt8 = new Point3d(-tbeam.Width * 0.5 + added, tbeam.Height * 0.5 + added, 0);
                var pt9 = new Point3d(tbeam.Width * 0.5 - added, tbeam.Height * 0.5 + added, 0);

                pt6.Transform(xform);
                pt7.Transform(xform);
                pt8.Transform(xform);
                pt9.Transform(xform);

                pt6.Transform(proj1);
                pt7.Transform(proj1);
                pt8.Transform(proj1);
                pt9.Transform(proj1);

                var tsrf2 = Brep.CreateFromCornerPoints(pt6, pt7, pt8, pt9, 0.001);

                tj.Tenon.Geometry.Add(tsrf2);

            }

            // Create mortise cutting geometry
            {
                var pt0 = new Point3d(-tbeam.Width * 0.5, 0, (mbeam.Width * 0.5 + added) * -sign);
                var pt1 = new Point3d(tbeam.Width * 0.5, 0, (mbeam.Width * 0.5 + added) * -sign);

                pt0.Transform(xform);
                pt1.Transform(xform);

                var mSidePlane0 = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign - tz * added,
                  mplane.ZAxis, mplane.YAxis);
                var mSidePlane1 = new Plane(mplane.Origin - mplane.XAxis * mbeam.Width * 0.5 * sign + tz * added,
                  mplane.ZAxis, mplane.YAxis);

                var proj0 = mSidePlane0.ProjectAlongVector(tz);
                var proj1 = mSidePlane1.ProjectAlongVector(tz);

                pt0.Transform(proj0);
                pt1.Transform(proj0);

                var pt2 = pt0; pt2.Transform(proj1);
                var pt3 = pt1; pt3.Transform(proj1);

                var msrf0 = Brep.CreateFromCornerPoints(pt0, pt1, pt3, pt2, 0.001);

                var pt4 = pt0 - tplane.YAxis * tbeam.Height;
                var pt5 = pt2 - tplane.YAxis * tbeam.Height;

                pt4.Transform(proj0);
                pt5.Transform(proj1);

                var msrf1 = Brep.CreateFromCornerPoints(pt0, pt2, pt5, pt4, 0.001);

                var pt6 = pt1 - tplane.YAxis * tbeam.Height;
                var pt7 = pt3 - tplane.YAxis * tbeam.Height;

                pt6.Transform(proj0);
                pt7.Transform(proj1);


                var msrf2 = Brep.CreateFromCornerPoints(pt1, pt3, pt7, pt6, 0.001);

                var joined = Brep.JoinBreps(new Brep[] { msrf0, msrf1, msrf2 }, 0.01);

                if (joined == null) joined = new Brep[] { msrf0, msrf1, msrf2 };

                tj.Mortise.Geometry.AddRange(joined);

            }

            return true;
        }
        public bool DefaultSpliceJoint(SpliceJoint sj)
        {
            var tbeam = (sj.FirstHalf.Element as BeamElement).Beam;
            var mbeam = (sj.SecondHalf.Element as BeamElement).Beam;


            var tplane = tbeam.GetPlane(sj.FirstHalf.Parameter);
            var mplane = mbeam.GetPlane(sj.SecondHalf.Parameter);

            var splicePlane = new Plane((tplane.Origin + mplane.Origin) / 2, tplane.XAxis, tplane.YAxis);

            var spliceCutter = Brep.CreatePlanarBreps(new Curve[]{
      new Rectangle3d(splicePlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

            sj.FirstHalf.Geometry.AddRange(spliceCutter);
            sj.SecondHalf.Geometry.AddRange(spliceCutter);

            return true;
        }
        public bool DefaultCornerJoint(CornerJoint cj)
        {
            var part0 = cj.Parts[0];
            var part1 = cj.Parts[1];
            var beam0 = (part0.Element as BeamElement).Beam;
            var beam1 = (part1.Element as BeamElement).Beam;

            var plane0 = beam0.GetPlane(part0.Parameter);
            var plane1 = beam1.GetPlane(part1.Parameter);

            var origin = (plane0.Origin + plane1.Origin) / 2;

            int sign0 = 1;
            int sign1 = -1;

            var v0Crv = (part0.Element as BeamElement).Beam.Centreline;
            var v1Crv = (part1.Element as BeamElement).Beam.Centreline;

            var vv0 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam0, origin);
            var vv1 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam1, origin);

            if (vv1 * plane0.XAxis > 0)
                sign0 = -sign0;

            if (vv0 * plane1.XAxis > 0)
                sign1 = -sign1;

            var trimPlane = new Plane(plane0.Origin + plane0.XAxis * beam0.Width * 0.5 * sign0, plane0.ZAxis, plane0.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part1.Geometry.AddRange(trimmers);

            trimPlane = new Plane(plane1.Origin + plane1.XAxis * beam1.Width * 0.5 * sign1, plane1.ZAxis, plane1.YAxis);
            trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part0.Geometry.AddRange(trimmers);

            return true;
        }
        public bool DefaultCrossJoint(CrossJoint cj)
        {
            //var breps = new DataTree<Brep>();

            var obeam = (cj.Over.Element as BeamElement).Beam;
            var ubeam = (cj.Under.Element as BeamElement).Beam;

            var oPlane = obeam.GetPlane(cj.Over.Parameter);
            var uPlane = ubeam.GetPlane(cj.Under.Parameter);

            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, oPlane);
            double added = 10.0;
            double height = 0.0;

            var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, Vector3d.CrossProduct(oPlane.ZAxis, uPlane.ZAxis));

            // Create beam side planes
            var planes = new Plane[4];
            planes[0] = new Plane(oPlane.Origin + oPlane.XAxis * obeam.Width * 0.5,
              oPlane.ZAxis, oPlane.YAxis);
            planes[1] = new Plane(oPlane.Origin - oPlane.XAxis * obeam.Width * 0.5,
              -oPlane.ZAxis, oPlane.YAxis);
            planes[2] = new Plane(uPlane.Origin + uPlane.XAxis * ubeam.Width * 0.5,
              uPlane.ZAxis, uPlane.YAxis);
            planes[3] = new Plane(uPlane.Origin - uPlane.XAxis * ubeam.Width * 0.5,
              -uPlane.ZAxis, uPlane.YAxis);

            var planesOffset = planes.Select(x => new Plane(x.Origin + x.ZAxis * added, x.XAxis, x.YAxis)).ToArray();

            var points = new Point3d[4];
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[0], planes[2], out points[0]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[0], planes[3], out points[1]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[2], out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[3], out points[3]);

            // Create over cutter
            var oSrf = new Brep[3];
            height = obeam.Height * 0.5 + added;

            var oPoints = new Point3d[]{
                points[0] - oPlane.ZAxis * added,
                points[1] + oPlane.ZAxis * added,
                points[2] - oPlane.ZAxis * added,
                points[3] + oPlane.ZAxis * added
                };

            oSrf[0] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[2], oPoints[3],
              0.01);
            oSrf[1] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[1] + plane.ZAxis * height, oPoints[0] + plane.ZAxis * height, 0.01);
            oSrf[2] = Brep.CreateFromCornerPoints(oPoints[2], oPoints[3], oPoints[3] + plane.ZAxis * height, oPoints[2] + plane.ZAxis * height, 0.01);

            var oJoined = Brep.JoinBreps(oSrf, 0.1);
            if (oJoined == null) oJoined = oSrf;

            cj.Under.Geometry.AddRange(oJoined);

            // Create under cutter
            var uSrf = new Brep[3];

            height = ubeam.Height * 0.5 + added;

            var uPoints = new Point3d[]{
              points[0] + uPlane.ZAxis * added,
              points[1] + uPlane.ZAxis * added,
              points[2] - uPlane.ZAxis * added,
              points[3] - uPlane.ZAxis * added
              };

            uSrf[0] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[1], uPoints[2], uPoints[3], 0.01);
            uSrf[1] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[2], uPoints[2] - plane.ZAxis * height, uPoints[0] - plane.ZAxis * height, 0.01);
            uSrf[2] = Brep.CreateFromCornerPoints(uPoints[1], uPoints[3], uPoints[3] - plane.ZAxis * height, uPoints[1] - plane.ZAxis * height, 0.01);

            var uJoined = Brep.JoinBreps(uSrf, 0.1);
            if (uJoined == null) uJoined = uSrf;
            cj.Over.Geometry.AddRange(uJoined);

            return true;
        }
        public bool DefaultVBeamJoint(VBeamJoint vj)
        {
            //var breps = new DataTree<Brep>();

            // Get beam for trimming the other ones
            var bPart = vj.Beam;
            var beam = (bPart.Element as BeamElement).Beam;
            var v0beam = (vj.V0.Element as BeamElement).Beam;
            var v1beam = (vj.V1.Element as BeamElement).Beam;

            var bplane = beam.GetPlane(bPart.Parameter);
            int sign = 1;

            var v0Crv = (vj.V0.Element as BeamElement).Beam.Centreline;
            if ((bplane.Origin - v0Crv.PointAt(v0Crv.Domain.Mid)) * bplane.XAxis > 0)
            {
                sign = -1;
            }

            // Get point and normal of intersection
            var points = new Point3d[3];
            var vectors = new Vector3d[3];

            var average = Point3d.Origin;

            for (int i = 0; i < vj.Parts.Length; ++i)
            {
                var crv = (vj.Parts[i].Element as BeamElement).Beam.Centreline;
                points[i] = crv.PointAt(vj.Parts[i].Parameter);
                vectors[i] = crv.TangentAt(vj.Parts[i].Parameter);

                var midpt = crv.PointAt(crv.Domain.Mid);

                if ((points[i] - midpt) * vectors[i] < 0)
                    vectors[i] = -vectors[i];

                average = average + points[i];
            }

            average = average / 3;

            // Create dividing surface for V-beams
            var tangent = (vectors[0] + vectors[1]) / 2;
            tangent.Unitize();

            var normal = Vector3d.CrossProduct(vectors[0], vectors[1]);
            var binormal = Vector3d.CrossProduct(tangent, normal);

            Point3d vpt0, vpt1;
            var res = v0beam.Centreline.ClosestPoints(v1beam.Centreline, out vpt0, out vpt1);


            var vx = (vpt0 + vpt1) / 2;

            var vv0 = JointUtil.GetEndConnectionVector(v0beam, vx);
            var vv1 = JointUtil.GetEndConnectionVector(v1beam, vx);

            var yaxis = (v0beam.GetPlane(vj.V0.Parameter).YAxis + v1beam.GetPlane(vj.V1.Parameter).YAxis) / 2;
            var v0plane = v0beam.GetPlane(vx);
            var v1plane = v1beam.GetPlane(vx);

            var xaxis0 = v0plane.XAxis;
            var xaxis1 = v1plane.XAxis;

            if (xaxis1 * xaxis0 < 0)
                xaxis1 = -xaxis1;

            yaxis = Vector3d.CrossProduct(xaxis0, xaxis1);

            var divPlane = new Plane(vx, (vv0 + vv1) / 2, yaxis);

            var divider = Brep.CreatePlanarBreps(new Curve[]{
      new Rectangle3d(divPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

            vj.V0.Geometry.AddRange(divider);
            vj.V1.Geometry.AddRange(divider);

            // Create temporary plate
            var plane = new Plane(average, binormal, tangent);
            var cyl = new Cylinder(new Circle(plane, 100), 20).ToBrep(true, true);

            for (int i = 0; i < 3; ++i)
            {
                vj.Parts[i].Geometry.Add(cyl);
            }

            var trimPlane = new Plane(bplane.Origin + bplane.XAxis * beam.Width * 0.5 * sign, bplane.ZAxis, bplane.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);

            vj.V0.Geometry.AddRange(trimmers);
            vj.V1.Geometry.AddRange(trimmers);

            //return breps;

            return true;
        }
        public bool DefaultFourWayJoint(FourWayJoint fj)
        {
            // Get point and normal of intersection
            var points = new Point3d[4];
            var vectors = new Vector3d[4];

            var average = fj.Plane.Origin;

            for (int i = 0; i < fj.Parts.Length; ++i)
            {
                var crv = (fj.Parts[i].Element as BeamElement).Beam.Centreline;
                points[i] = crv.PointAt(fj.Parts[i].Parameter);
                vectors[i] = crv.TangentAt(fj.Parts[i].Parameter);

                var midpt = crv.PointAt(crv.Domain.Mid);

                if ((points[i] - midpt) * vectors[i] < 0)
                    vectors[i] = -vectors[i];

                //average = average + points[i];
            }

            //average = average / 3;

            for (int i = 0; i < vectors.Length; ++i)
            {
                int ii = (i + 1).Modulus(4);

                var v0beam = (fj.Parts[i].Element as BeamElement).Beam;
                var v1beam = (fj.Parts[ii].Element as BeamElement).Beam;

                var tangent = (vectors[i] + vectors[ii]) / 2;
                tangent.Unitize();

                var normal = Vector3d.CrossProduct(vectors[i], vectors[ii]);
                //var normal = fj.Plane.ZAxis;
                var binormal = Vector3d.CrossProduct(tangent, normal);

                Point3d vpt0, vpt1;
                var res = v0beam.Centreline.ClosestPoints(v1beam.Centreline, out vpt0, out vpt1);

                var vx = (vpt0 + vpt1) / 2;

                var vv0 = GluLamb.Joints.JointUtil.GetEndConnectionVector(v0beam, vx);
                var vv1 = GluLamb.Joints.JointUtil.GetEndConnectionVector(v1beam, vx);

                var yaxis = (v0beam.GetPlane(fj.Parts[i].Parameter).YAxis + v1beam.GetPlane(fj.Parts[ii].Parameter).YAxis) / 2;
                var v0plane = v0beam.GetPlane(vx);
                var v1plane = v1beam.GetPlane(vx);

                var xaxis0 = v0plane.XAxis;
                var xaxis1 = v1plane.XAxis;

                if (xaxis1 * xaxis0 < 0)
                    xaxis1 = -xaxis1;

                yaxis = Vector3d.CrossProduct(xaxis0, xaxis1);

                var divPlane = new Plane(vx, (vv0 + vv1) / 2, yaxis);
                var divider = Brep.CreatePlanarBreps(new Curve[]{
                    new Rectangle3d(divPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

                fj.Parts[i].Geometry.AddRange(divider);
                fj.Parts[ii].Geometry.AddRange(divider);
            }

            return true;
        }
        public bool DefaultBranchJoint(BranchJoint bj)
        {
            var part0 = bj.Parts[0];
            var part1 = bj.Parts[1];
            var beam0 = (part0.Element as BeamElement).Beam;
            var beam1 = (part1.Element as BeamElement).Beam;

            var plane0 = beam0.GetPlane(part0.Parameter);
            var plane1 = beam1.GetPlane(part1.Parameter);

            var origin = (plane0.Origin + plane1.Origin) / 2;

            int sign0 = 1;
            int sign1 = -1;

            var v0Crv = (part0.Element as BeamElement).Beam.Centreline;
            var v1Crv = (part1.Element as BeamElement).Beam.Centreline;

            var vv0 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam0, origin);
            var vv1 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam1, origin);

            if (vv1 * plane0.XAxis > 0)
                sign0 = -sign0;

            if (vv0 * plane1.XAxis > 0)
                sign1 = -sign1;

            var trimPlane = new Plane(plane0.Origin + plane0.XAxis * beam0.Width * 0.5 * sign0, plane0.ZAxis, plane0.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part1.Geometry.AddRange(trimmers);

            trimPlane = new Plane(plane1.Origin + plane1.XAxis * beam1.Width * 0.5 * sign1, plane1.ZAxis, plane1.YAxis);
            trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part0.Geometry.AddRange(trimmers);

            return true;
        }

    }
}
