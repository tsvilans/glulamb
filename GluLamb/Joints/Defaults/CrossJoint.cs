using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class CrossJoint : Joint2
    {
        public CrossJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("CrossJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates a crossing joint between two beam elements.
        /// </summary>
        /// <param name="beamA">Beam element that goes over.</param>
        /// <param name="beamB">Beam element that goes under.</param>
        public CrossJoint(Element beamA, double parameterA, Element beamB, double parameterB) : base()
        {
            Parts[0] = new JointPart(beamA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(beamB as BeamElement, this, 1, parameterB);
        }

        public JointPart Over { get { return Parts[0]; } }
        public JointPart Under { get { return Parts[1]; } }

        public override string ToString()
        {
            return "CrossJoint";
        }

        public void Flip()
        {
            var temp = Parts[1];
            Parts[1] = Parts[0];
            Parts[0] = temp;
        }

        public override bool Construct(bool append = false)
        {
            //var breps = new DataTree<Brep>();

            var obeam = (Over.Element as BeamElement).Beam;
            var ubeam = (Under.Element as BeamElement).Beam;

            var oPlane = obeam.GetPlane(Over.Parameter);
            var uPlane = ubeam.GetPlane(Under.Parameter);

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

            Under.Geometry.AddRange(oJoined);

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
            Over.Geometry.AddRange(uJoined);

            return true;
        }

    }

}
