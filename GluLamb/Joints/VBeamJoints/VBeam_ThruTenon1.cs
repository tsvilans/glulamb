using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class VBeam_ThruTenon1 : VBeamJoint
    {
        public VBeam_ThruTenon1(List<Element> elements, Factory.JointCondition jc): base (elements, jc)
        {

        }

        public override bool Construct(bool append = false)
        {
            var bPart = Beam;
            var beam = (bPart.Element as BeamElement).Beam;
            var v0beam = (V0.Element as BeamElement).Beam;
            var v1beam = (V1.Element as BeamElement).Beam;

            var bplane = beam.GetPlane(bPart.Parameter);
            int sign = 1;

            var v0Crv = (V0.Element as BeamElement).Beam.Centreline;
            if ((bplane.Origin - v0Crv.PointAt(v0Crv.Domain.Mid)) * bplane.XAxis > 0)
            {
                sign = -1;
            }

            // Get point and normal of intersection
            var points = new Point3d[3];
            var vectors = new Vector3d[3];

            var average = Point3d.Origin;

            for (int i = 0; i < Parts.Length; ++i)
            {
                var crv = (Parts[i].Element as BeamElement).Beam.Centreline;
                points[i] = crv.PointAt(Parts[i].Parameter);
                vectors[i] = crv.TangentAt(Parts[i].Parameter);

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

            var yaxis = (v0beam.GetPlane(V0.Parameter).YAxis + v1beam.GetPlane(V1.Parameter).YAxis) / 2;
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

            //vj.V0.Geometry.AddRange(divider);
            V1.Geometry.AddRange(divider);


            // Create temporary plate
            var plane = new Plane(average, binormal, tangent);

            // Create trimmer on bottom of Beam
            var trimPlane = new Plane(bplane.Origin + bplane.XAxis * beam.Width * 0.5 * -sign, bplane.ZAxis, bplane.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);

            var sillPlane = new Plane(bplane.Origin + bplane.XAxis * beam.Width * 0.5 * sign, bplane.ZAxis, bplane.YAxis);
            var sillTrimmer = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(sillPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);

            var proj = sillPlane.ProjectAlongVector(divPlane.XAxis);

            V0.Geometry.AddRange(trimmers);
            V1.Geometry.AddRange(trimmers);
            V1.Geometry.AddRange(sillTrimmer);


            // Create cutter for through-tenon (V0)

            var zz = trimPlane.Project(divPlane.ZAxis); zz.Unitize();
            var yy = trimPlane.Project(divPlane.YAxis); yy.Unitize();
            var origin = divPlane.Origin;
            origin.Transform(proj);


            double zheight = 100.0;
            double tenonWidth = 60.0;

            var srfsTop = new Brep[3];

            sign = divPlane.ZAxis * vv0 < 0 ? 1 : -1;

            var p = new Point3d[9];
            for (int i = -1; i < 2; i += 2)
            {
                var srfs = new Brep[3];

                p[0] = origin + yy * tenonWidth * 0.5 * i;
                p[1] = p[0] + divPlane.XAxis * 200.0;
                p[2] = p[1] + yy * zheight * i;
                p[3] = p[0] + yy * zheight * i;
                p[4] = p[0] - zz * 100 * sign;
                p[5] = p[4] + yy * zheight * i;
                p[6] = p[1] + zz * 100 * sign;
                p[7] = p[6] - divPlane.XAxis * 500.0;
                p[8] = p[4] - divPlane.XAxis * 300.0;


                var poly = new Polyline(new Point3d[] { p[0], p[1], p[6], p[7], p[8], p[4], p[0] });

                srfs[0] = Brep.CreateFromCornerPoints(p[0], p[1], p[2], p[3], 0.01);
                srfs[1] = Brep.CreateFromCornerPoints(p[0], p[3], p[5], p[4], 0.01);
                srfs[2] = Brep.CreatePlanarBreps(new Curve[] { poly.ToNurbsCurve() }, 0.01)[0];


                var joined = Brep.JoinBreps(srfs, 0.01);

                V0.Geometry.AddRange(joined);
            }

            // Create cutter for tenon cover (V1)
            normal = Vector3d.CrossProduct(zz, divPlane.XAxis);
            var tPlane = new Plane(origin, normal);

            var ww = tPlane.Project(vv0);
            var vv = tPlane.Project(vv1);

            var buttPlane = new Plane(v0plane.Origin + v0plane.XAxis * v0beam.Width / 2, v0plane.ZAxis, v0plane.YAxis);
            var proj2 = buttPlane.ProjectAlongVector(divPlane.XAxis);

            var origin2 = origin;
            origin2.Transform(proj2);


            var srfs2 = new Brep[3];

            p[0] = origin2 + yy * tenonWidth * 0.5 + ww * 20.0;
            p[1] = p[0] - ww * 200.0;
            p[2] = p[1] - vv * 500.0;
            p[3] = p[0] - vv * 500.0;
            p[4] = origin2 - yy * tenonWidth * 0.5 + ww * 20.0;
            p[5] = p[4] - ww * 200.0;
            p[6] = p[5] - vv * 500.0;
            p[7] = p[4] - vv * 500.0;


            srfs2[0] = Brep.CreateFromCornerPoints(p[0], p[1], p[2], p[3], 0.01);
            srfs2[1] = Brep.CreateFromCornerPoints(p[4], p[5], p[6], p[7], 0.01);
            srfs2[2] = Brep.CreateFromCornerPoints(p[0], p[1], p[5], p[4], 0.01);


            var joined2 = Brep.JoinBreps(srfs2, 0.01);

            V1.Geometry.AddRange(joined2);

            // Create cutter for beam (Beam)
            var dot = vv0 * trimPlane.ZAxis;

            var srfTenon = new Brep[4];
            double hw = v0beam.Width * 0.5 / dot; // TODO
            int flip = -1;

            for (int i = 0; i < 2; i++)
            {
                p[0 + 4 * i] = origin + yy * tenonWidth * 0.5 + zz * hw - ww * 150.0 * flip;
                p[1 + 4 * i] = origin + yy * tenonWidth * 0.5 - zz * hw - ww * 150.0 * flip;
                p[2 + 4 * i] = origin - yy * tenonWidth * 0.5 - zz * hw - ww * 150.0 * flip;
                p[3 + 4 * i] = origin - yy * tenonWidth * 0.5 + zz * hw - ww * 150.0 * flip;
                flip = -flip;
            }

            srfTenon[0] = Brep.CreateFromCornerPoints(p[0], p[1], p[5], p[4], 0.01);
            srfTenon[1] = Brep.CreateFromCornerPoints(p[1], p[2], p[6], p[5], 0.01);
            srfTenon[2] = Brep.CreateFromCornerPoints(p[2], p[3], p[7], p[6], 0.01);
            srfTenon[3] = Brep.CreateFromCornerPoints(p[3], p[0], p[4], p[7], 0.01);


            var joinedTenon = Brep.JoinBreps(srfTenon, 0.01);
            Beam.Geometry.AddRange(joinedTenon);

            return true;
        }
    }
}
