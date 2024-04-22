using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using Rhino.Collections;
using Microsoft.SqlServer.Server;

namespace GluLamb.Joints
{
    [Serializable]
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

        public void OrganizePlanes(ref Plane p0, ref Plane p1)
        {
            if (p0.YAxis * p1.YAxis < 0)
            {
                p1.YAxis = -p1.YAxis;
                p1.XAxis = -p1.XAxis;
            }

            double dotZ = p1.ZAxis * p0.ZAxis;
            double dotX = p1.XAxis * p0.ZAxis;

            if (dotX > 0)
            {
                p1.XAxis = -p1.XAxis;
            }
        }

        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach (var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }
            //var breps = new DataTree<Brep>();

            var obeam = (Over.Element as BeamElement).Beam;
            var ubeam = (Under.Element as BeamElement).Beam;

            var oPlane = obeam.GetPlane(Over.Parameter);
            var uPlane = ubeam.GetPlane(Under.Parameter);

            OrganizePlanes(ref oPlane, ref uPlane);

            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, oPlane);
            double added = 10.0;
            double height = 0.0;


            Line normalLine;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(
              new Plane(oPlane.Origin, oPlane.ZAxis, oPlane.YAxis),
              new Plane(uPlane.Origin, uPlane.ZAxis, uPlane.YAxis),
              out normalLine);

            var normal = normalLine.Direction;
            normal.Unitize();

            if (normal * (oPlane.Origin - uPlane.Origin) < 0.0)
                normal.Reverse();

            //var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, Vector3d.CrossProduct(oPlane.XAxis, uPlane.XAxis));
            //var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, oPlane.XAxis, uPlane.XAxis);
            var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, normal);

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
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[3], out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[2], out points[3]);

            // **************************
            // Create data for CIX files
            // **************************

            var poly = new Polyline(points);
            poly.Add(poly[0]);

            var overDic = new ArchivableDictionary();
            overDic.Set("Plane", new Plane(plane.Origin, -plane.XAxis, plane.YAxis));
            for (int i = 0; i < points.Length; ++i)
                overDic.Set(string.Format("P{0}", i), points[i]);

            overDic.Set("Outline", poly.ToNurbsCurve());
            overDic.Set("SidePlane0", planes[2]);
            overDic.Set("SidePlane1", planes[3]);

            var underDic = new ArchivableDictionary();
            underDic.Set("Plane", plane);
            for (int i = 0; i < points.Length; ++i)
                underDic.Set(string.Format("P{0}", i), points[i]);

            underDic.Set("Outline", poly.ToNurbsCurve());
            underDic.Set("SidePlane0", planes[0]);
            underDic.Set("SidePlane1", planes[1]);

            Over.Element.UserDictionary.Set(string.Format("D2_{0}", Under.Element.Name), overDic);
            Under.Element.UserDictionary.Set(string.Format("D2_{0}", Over.Element.Name), underDic);

            // Create over cutter
            var oSrf = new Brep[3];
            height = obeam.Height * 0.5 + added;

            // Adjust height based on distance from centreline (if centrelines don't intersect)
            double oadded = (oPlane.Origin - plane.Origin) * normal;
            height -= oadded;

            var oZAxis = plane.Project(oPlane.ZAxis); oZAxis.Unitize();
            if (oZAxis * uPlane.XAxis > 0)
                oZAxis.Reverse();

            var oPoints = new Point3d[]{
                points[0] - oZAxis * added,
                points[1] + oZAxis * added,
                points[2] + oZAxis * added,
                points[3] - oZAxis * added
                };

            oSrf[0] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[2], oPoints[3], 0.01);
            oSrf[1] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[1] + normal * height, oPoints[0] + normal * height, 0.01);
            oSrf[2] = Brep.CreateFromCornerPoints(oPoints[2], oPoints[3], oPoints[3] + normal * height, oPoints[2] + normal * height, 0.01);

            var oJoined = Brep.JoinBreps(oSrf, 0.1);
            if (oJoined == null) oJoined = oSrf;

            Under.Geometry.AddRange(oJoined);

            // Create under cutter
            var uSrf = new Brep[3];

            height = ubeam.Height * 0.5 + added;

            // Adjust height based on distance from centreline (if centrelines don't intersect)
            double uadded = (uPlane.Origin - plane.Origin) * normal;
            height += uadded;

            var uZAxis = plane.Project(uPlane.ZAxis); uZAxis.Unitize();
            if (uZAxis * oPlane.XAxis > 0)
                uZAxis.Reverse();
            //added = 0;
            var uPoints = new Point3d[]{
        points[0] - uZAxis * added,
        points[1] - uZAxis * added,
        points[2] + uZAxis * added,
        points[3] + uZAxis * added
        };

            uSrf[0] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[1], uPoints[2], uPoints[3], 0.01);
            uSrf[1] = Brep.CreateFromCornerPoints(uPoints[1], uPoints[2], uPoints[2] - normal * height, uPoints[1] - normal * height, 0.01);
            uSrf[2] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[3], uPoints[3] - normal * height, uPoints[0] - normal * height, 0.01);

            var uJoined = Brep.JoinBreps(uSrf, 0.1);
            if (uJoined == null) uJoined = uSrf;
            Over.Geometry.AddRange(uJoined);

            return true;
        }

    }

    [Serializable]
    public class CrossJoint2 : CrossJoint
    {
        public CrossJoint2(List<Element> elements, GluLamb.Factory.JointCondition jc) : base(elements, jc)
        {/*
      if (jc.Parts.Count != Parts.Length) throw new Exception("CrossJoint needs 2 elements.");
      for (int i = 0; i < Parts.Length; ++i)
      {
        Parts[i] = new JointPart(elements, jc.Parts[i], this);
      }*/
        }
        /// <summary>
        /// Creates a crossing joint between two beam elements.
        /// </summary>
        /// <param name="beamA">Beam element that goes over.</param>
        /// <param name="beamB">Beam element that goes under.</param>
        public CrossJoint2(Element beamA, double parameterA, Element beamB, double parameterB) : base(beamA, parameterA, beamB, parameterB)
        {
            /*
            Parts[0] = new JointPart(beamA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(beamB as BeamElement, this, 1, parameterB);
           */
        }


        //public JointPart Over { get { return Parts[0]; } }
        //public JointPart Under { get { return Parts[1]; } }

        public override string ToString()
        {
            return "CrossJoint";
        }

        //public void Flip()
        //{
        //    var temp = Parts[1];
        //    Parts[1] = Parts[0];
        //    Parts[0] = temp;
        //}

        /*
        public void OrganizePlanes(ref Plane p0, ref Plane p1)
        {
            if (p0.YAxis * p1.YAxis < 0)
            {
                p1.YAxis = -p1.YAxis;
                p1.XAxis = -p1.XAxis;
            }

            double dotZ = p1.ZAxis * p0.ZAxis;
            double dotX = p1.XAxis * p0.ZAxis;

            if (dotX > 0)
            {
                p1.XAxis = -p1.XAxis;
            }
        }
        */
        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach (var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }
            //var breps = new DataTree<Brep>();

            var obeam = (Over.Element as BeamElement).Beam;
            var ubeam = (Under.Element as BeamElement).Beam;

            var oPlane = obeam.GetPlane(Over.Parameter);
            var uPlane = ubeam.GetPlane(Under.Parameter);

            OrganizePlanes(ref oPlane, ref uPlane);

            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, oPlane);
            double added = 10.0;
            double height = 0.0;


            Line normalLine;
            Rhino.Geometry.Intersect.Intersection.PlanePlane(
              new Plane(oPlane.Origin, oPlane.ZAxis, oPlane.YAxis),
              new Plane(uPlane.Origin, uPlane.ZAxis, uPlane.YAxis),
              out normalLine);

            var normal = normalLine.Direction;
            normal.Unitize();

            //var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, Vector3d.CrossProduct(oPlane.XAxis, uPlane.XAxis));
            //var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, oPlane.XAxis, uPlane.XAxis);
            var plane = new Plane((oPlane.Origin + uPlane.Origin) / 2, normal);

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
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[3], out points[2]);
            Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(plane, planes[1], planes[2], out points[3]);

            // **************************
            // Create data for CIX files
            // **************************

            var poly = new Polyline(points);
            poly.Add(poly[0]);

            var overDic = new ArchivableDictionary();
            overDic.Set("Plane", new Plane(plane.Origin, -plane.XAxis, plane.YAxis));
            for (int i = 0; i < points.Length; ++i)
                overDic.Set(string.Format("P{0}", i), points[i]);

            overDic.Set("Outline", poly.ToNurbsCurve());
            overDic.Set("SidePlane0", planes[2]);
            overDic.Set("SidePlane1", planes[3]);

            var underDic = new ArchivableDictionary();
            underDic.Set("Plane", plane);
            for (int i = 0; i < points.Length; ++i)
                underDic.Set(string.Format("P{0}", i), points[i]);

            underDic.Set("Outline", poly.ToNurbsCurve());
            underDic.Set("SidePlane0", planes[0]);
            underDic.Set("SidePlane1", planes[1]);

            Over.Element.UserDictionary.Set(string.Format("D2_{0}", Under.Element.Name), overDic);
            Under.Element.UserDictionary.Set(string.Format("D2_{0}", Over.Element.Name), underDic);

            // Create over cutter
            var oSrf = new Brep[3];
            height = obeam.Height * 0.5 + added;

            var oZAxis = plane.Project(oPlane.ZAxis); oZAxis.Unitize();

            var oPoints = new Point3d[]{
        points[0] - oZAxis * added,
        points[1] + oZAxis * added,
        points[2] + oZAxis * added,
        points[3] - oZAxis * added
        };

            oSrf[0] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[2], oPoints[3], 0.01);
            oSrf[1] = Brep.CreateFromCornerPoints(oPoints[0], oPoints[1], oPoints[1] + normal * height, oPoints[0] + normal * height, 0.01);
            oSrf[2] = Brep.CreateFromCornerPoints(oPoints[2], oPoints[3], oPoints[3] + normal * height, oPoints[2] + normal * height, 0.01);

            var oJoined = Brep.JoinBreps(oSrf, 0.1);
            if (oJoined == null) oJoined = oSrf;

            Under.Geometry.AddRange(oJoined);

            // Create under cutter
            var uSrf = new Brep[3];

            height = ubeam.Height * 0.5 + added;

            var uZAxis = plane.Project(uPlane.ZAxis); uZAxis.Unitize();

            //added = 0;
            var uPoints = new Point3d[]{
        points[0] + uZAxis * added,
        points[1] + uZAxis * added,
        points[2] - uZAxis * added,
        points[3] - uZAxis * added
        };

            uSrf[0] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[1], uPoints[2], uPoints[3], 0.01);
            uSrf[1] = Brep.CreateFromCornerPoints(uPoints[1], uPoints[2], uPoints[2] - normal * height, uPoints[1] - normal * height, 0.01);
            uSrf[2] = Brep.CreateFromCornerPoints(uPoints[0], uPoints[3], uPoints[3] - normal * height, uPoints[0] - normal * height, 0.01);

            var uJoined = Brep.JoinBreps(uSrf, 0.1);
            if (uJoined == null) uJoined = uSrf;
            Over.Geometry.AddRange(uJoined);

            return true;
        }

    }

}
