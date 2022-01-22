using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class CrossJoint_DoubleBackcut : CrossJoint
    {
        public double Offset1 = 3.0;
        public double Offset2 = 3.0;
        public double Extension = 2.0;
        public double OffsetCentre = 10.0;

        public CrossJoint_DoubleBackcut(List<Element> elements, Factory.JointCondition jc) : base(elements, jc)
        {
        }

        public override string ToString()
        {
            return "CrossJoint_DoubleBackcut";
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
            var m_glulam1 = (Over.Element as BeamElement).Beam as Glulam;
            var m_glulam2 = (Under.Element as BeamElement).Beam as Glulam;
            var m_offset1 = Offset1;
            var m_offset2 = Offset2;
            var m_extension = Extension;
            var offset_center = OffsetCentre;

            var m_result = new List<Brep>();

            var m_drill_depth = 100.0;

            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            double widthA = m_glulam1.Width;
            double heightA = m_glulam1.Height;
            double widthB = m_glulam2.Width;
            double heightB = m_glulam2.Height;

            bool flip = false;

            m_result = new List<Brep>();

            //
            // Find orientation relationship between glulams
            //

            Point3d ptA, ptB;
            m_glulam1.Centreline.ClosestPoints(m_glulam2.Centreline, out ptA, out ptB);

            Point3d CP = (ptA + ptB) / 2;
            Plane cPlane;

            double cDist = ptA.DistanceTo(ptB) / 2;

            Plane plA = m_glulam1.GetPlane(ptA);
            Plane plB = m_glulam2.GetPlane(ptB);

            double ofc = offset_center;
            double ofc2 = offset_center / 2;

            Vector3d vAB = ptB - ptA;
            Vector3d vBA = ptA - ptB;

            if (vAB.IsZero || vBA.IsZero)
            {
                vAB = plA.YAxis;
                vBA = -vAB;
            }
            else
            {
                vAB.Unitize();
                vBA.Unitize();
            }

            int
              yAFlip = vAB * plA.YAxis < 0 ? 1 : -1,
              yBFlip = vBA * plB.YAxis < 0 ? 1 : -1,
              xAFlip = plA.XAxis * plB.ZAxis < 0 ? 1 : -1,
              xBFlip = plB.XAxis * plA.ZAxis < 0 ? 1 : -1;

            m_result = new List<Brep>();

            //
            // Make center surface
            //

            Brep[] arrbrpCenterSides = new Brep[]
              {
      m_glulam1.GetSideSurface(0, (widthA / 2 - ofc2) * xAFlip, heightB * 3, m_extension, flip),
      m_glulam1.GetSideSurface(0, -(widthA / 2 - ofc2) * xAFlip, heightB * 3, m_extension, flip)
              };

            Brep brpCenterFlat = m_glulam2.GetSideSurface(1, cDist * yAFlip, widthB - ofc, m_extension, flip);


            Curve[] arrcrvCenterSides = new Curve[4];

            Curve[] xCurves;
            Point3d[] xPoints;
            Rhino.Geometry.Intersect.Intersection.BrepBrep(arrbrpCenterSides[0], brpCenterFlat, tolerance, out xCurves, out xPoints);

            if (xCurves.Length > 0)
                arrcrvCenterSides[0] = xCurves[0];
            else
            {
                m_result.Add(brpCenterFlat);
                m_result.AddRange(arrbrpCenterSides);
                //throw new Exception("Failed to intersect: 0");
                return false;
            }

            Rhino.Geometry.Intersect.Intersection.BrepBrep(arrbrpCenterSides[1], brpCenterFlat, tolerance, out xCurves, out xPoints);
            if (xCurves.Length > 0)
                arrcrvCenterSides[1] = xCurves[0];
            else
            {
                m_result.Add(brpCenterFlat);
                m_result.AddRange(arrbrpCenterSides);
                //throw new Exception("Failed to intersect: 1");
                return false;
            }

            Point3d[] cPoints = new Point3d[4];
            cPoints[0] = arrcrvCenterSides[0].PointAtStart;
            cPoints[1] = arrcrvCenterSides[0].PointAtEnd;
            cPoints[2] = arrcrvCenterSides[1].PointAtStart;
            cPoints[3] = arrcrvCenterSides[1].PointAtEnd;

            Plane.FitPlaneToPoints(cPoints, out cPlane);
            cPlane.Origin = CP;

            for (int i = 0; i < 4; ++i)
                cPoints[i] = cPlane.ClosestPoint(cPoints[i]);

            //arrcrvCenterSides[2] = new Line(arrcrvCenterSides[0].PointAtStart, arrcrvCenterSides[1].PointAtStart).ToNurbsCurve();
            //arrcrvCenterSides[3] = new Line(arrcrvCenterSides[0].PointAtEnd, arrcrvCenterSides[1].PointAtEnd).ToNurbsCurve();

            arrcrvCenterSides[0] = new Line(cPoints[0], cPoints[1]).ToNurbsCurve();
            arrcrvCenterSides[1] = new Line(cPoints[2], cPoints[3]).ToNurbsCurve();
            arrcrvCenterSides[2] = new Line(cPoints[0], cPoints[2]).ToNurbsCurve();
            arrcrvCenterSides[3] = new Line(cPoints[1], cPoints[3]).ToNurbsCurve();

            Brep brpCenterBrep = Brep.CreateEdgeSurface(arrcrvCenterSides);

            //
            // GlulamA Top
            //

            Brep brpGlulamATop = m_glulam1.GetSideSurface(1, (heightA / 2 + m_offset1) * -yAFlip, widthA + m_offset2, m_extension, false);

            //
            // GlulamA Sides
            //

            Brep[] arrbrpGlulamASides = new Brep[2];
            arrbrpGlulamASides[0] = m_glulam1.GetSideSurface(0, (widthA / 2 + m_offset1) * xAFlip, heightA * 2 + m_offset2, m_extension, flip);
            arrbrpGlulamASides[1] = m_glulam1.GetSideSurface(0, -(widthA / 2 + m_offset1) * xAFlip, heightA * 2 + m_offset2, m_extension, flip);

            //
            // GlulamB Bottom
            //

            Brep brpGlulamBBtm = m_glulam2.GetSideSurface(1, (heightB / 2 + m_offset1) * -yBFlip, widthB + m_offset2, m_extension, false);

            //
            // GlulamB Sides
            //

            Brep[] arrbrpGlulamBSides = new Brep[2];
            arrbrpGlulamBSides[0] = m_glulam2.GetSideSurface(0, (widthB / 2 + m_offset1) * xAFlip, heightB * 2 + m_offset2, m_extension, flip);
            arrbrpGlulamBSides[1] = m_glulam2.GetSideSurface(0, -(widthB / 2 + m_offset1) * xAFlip, heightB * 2 + m_offset2, m_extension, flip);

            //
            // Intersect GlulamA Top with GlulamB Sides
            //

            //m_result.Add(brpGlulamATop);
            //m_result.AddRange(arrbrpGlulamBSides);
            //return;

            Curve[] arrcrvATopBSides = new Curve[2];
            Rhino.Geometry.Intersect.Intersection.BrepBrep(brpGlulamATop, arrbrpGlulamBSides[0], tolerance, out xCurves, out xPoints);
            if (xCurves.Length > 0)
                arrcrvATopBSides[0] = xCurves[0];
            Rhino.Geometry.Intersect.Intersection.BrepBrep(brpGlulamATop, arrbrpGlulamBSides[1], tolerance, out xCurves, out xPoints);
            if (xCurves.Length > 0)
                arrcrvATopBSides[1] = xCurves[0];

            if (arrcrvATopBSides[0] == null || arrcrvATopBSides[1] == null)
            {
                //throw new Exception("Top sides are null.");
                return false;
            }
            //
            // Intersect GlulamB Bottom with GlulamA Sides
            //
            Curve[] arrcrvBBtmASides = new Curve[2];

            Rhino.Geometry.Intersect.Intersection.BrepBrep(brpGlulamBBtm, arrbrpGlulamASides[0], tolerance, out xCurves, out xPoints);
            if (xCurves.Length > 0)
                arrcrvBBtmASides[0] = xCurves[0];
            Rhino.Geometry.Intersect.Intersection.BrepBrep(brpGlulamBBtm, arrbrpGlulamASides[1], tolerance, out xCurves, out xPoints);
            if (xCurves.Length > 0)
                arrcrvBBtmASides[1] = xCurves[0];

            if (arrcrvBBtmASides[0] == null || arrcrvBBtmASides[1] == null) return false;

            //
            // Loft GlulamA Tops with Center
            //

            if (arrcrvCenterSides[3].TangentAtStart * arrcrvATopBSides[0].TangentAtStart < 0.0)
                arrcrvATopBSides[0].Reverse();

            Brep[] arrbrpTopCenterLoft1 =
              Brep.CreateFromLoft(
              new Curve[] { arrcrvCenterSides[3], arrcrvATopBSides[0] },
              Point3d.Unset, Point3d.Unset,
              LoftType.Straight, false);

            if (arrcrvCenterSides[2].TangentAtStart * arrcrvATopBSides[1].TangentAtStart < 0.0)
                arrcrvATopBSides[1].Reverse();

            Brep[] arrbrpTopCenterLoft2 =
              Brep.CreateFromLoft(
              new Curve[] { arrcrvCenterSides[2], arrcrvATopBSides[1] },
              Point3d.Unset, Point3d.Unset,
              LoftType.Straight, false);

            //
            // Loft GlulamB Bottoms with Center
            //

            if (arrcrvCenterSides[0].TangentAtStart * arrcrvBBtmASides[0].TangentAtStart < 0.0)
                arrcrvBBtmASides[0].Reverse();

            Brep[] arrbrpBtmCenterLoft1 =
              Brep.CreateFromLoft(
              new Curve[] { arrcrvCenterSides[0], arrcrvBBtmASides[0] },
              Point3d.Unset, Point3d.Unset,
              LoftType.Straight, false);

            if (arrcrvCenterSides[1].TangentAtStart * arrcrvBBtmASides[1].TangentAtStart < 0.0)
                arrcrvBBtmASides[1].Reverse();

            Brep[] arrbrpBtmCenterLoft2 =
              Brep.CreateFromLoft(
              new Curve[] { arrcrvCenterSides[1], arrcrvBBtmASides[1] },
              Point3d.Unset, Point3d.Unset,
              LoftType.Straight, false);

            //
            // Make webs
            //

            Brep web1 = Brep.CreateFromCornerPoints(
              arrcrvCenterSides[0].PointAtStart,
              arrcrvATopBSides[1].PointAtStart,
              arrcrvBBtmASides[0].PointAtStart,
              tolerance
              );

            Brep web2 = Brep.CreateFromCornerPoints(
              arrcrvCenterSides[0].PointAtEnd,
              arrcrvATopBSides[0].PointAtStart,
              arrcrvBBtmASides[0].PointAtEnd,
              tolerance
              );

            Brep web3 = Brep.CreateFromCornerPoints(
              arrcrvCenterSides[1].PointAtEnd,
              arrcrvATopBSides[0].PointAtEnd,
              arrcrvBBtmASides[1].PointAtEnd,
              tolerance
              );

            Brep web4 = Brep.CreateFromCornerPoints(
              arrcrvCenterSides[1].PointAtStart,
              arrcrvATopBSides[1].PointAtEnd,
              arrcrvBBtmASides[1].PointAtStart,
              tolerance
              );

            //
            // Populate the result list.
            //

            m_result.Add(brpCenterBrep);

            //m_result.Add(brpGlulamATop);
            //m_result.Add(brpGlulamBTop);
            m_result.AddRange(arrbrpTopCenterLoft1);
            m_result.AddRange(arrbrpTopCenterLoft2);
            m_result.AddRange(arrbrpBtmCenterLoft1);
            m_result.AddRange(arrbrpBtmCenterLoft2);

            m_result.Add(web1);
            m_result.Add(web2);
            m_result.Add(web3);
            m_result.Add(web4);

            var temp = Brep.JoinBreps(m_result, 0.001);
            if (temp.Length > 0)
            {
                m_result.Clear();
                m_result.AddRange(temp);
            }

            Under.Geometry.AddRange(m_result);
            Over.Geometry.AddRange(m_result);

            //var drill0 = new DoubleSidedCounterSunkDrill(
            //  cPlane,
            //  5.0, m_drill_depth, 10.0, 6.0);

            //drill0.Compute();

            //m_result.AddRange(drill0.GetCuttingGeometry());

            return true;
        }
    }

}
