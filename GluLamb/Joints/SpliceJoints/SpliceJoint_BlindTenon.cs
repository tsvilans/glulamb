﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class SpliceJoint_BlindTenon : SpliceJoint
    {
        public List<object> debug;

        public double TenonLength = 100;
        public double TenonWidth = 40;
        public double TenonHeight = 80;
        public double Added = 10.0;
        public double FilletRadius = 8.0;

        public double DowelLength = 220;
        public double DowelDiameter = 12.0;

        public SpliceJoint_BlindTenon(List<Element> elements, JointCondition jc) : base(elements, jc)
        {
        }

        public SpliceJoint_BlindTenon(SpliceJoint sj) : base(sj)
        {
        }

        public override string ToString()
        {
            return "SpliceJoint_BlindTenon";
        }

        public override bool Construct(bool append = false)
        {
            debug = new List<object>();

            var beams = new Beam[2];
            beams[0] = (FirstHalf.Element as BeamElement).Beam;
            beams[1] = (SecondHalf.Element as BeamElement).Beam;

            var planes = new Plane[2];
            for (int i = 0; i < 2; ++i)
            {
                planes[i] = beams[i].GetPlane(Parts[i].Parameter);
            }

            // TODO: Align curve tangents and ends

            var endPlane = new Plane(planes[0].Origin + planes[0].ZAxis * TenonLength, planes[0].XAxis, planes[0].YAxis);

            debug.Add(planes[0]);
            debug.Add(planes[1]);
            debug.Add(endPlane);

            double height = Math.Max(beams[0].Height, beams[1].Height);
            double width = Math.Max(beams[0].Width, beams[1].Width);

            TenonWidth = Math.Min(width, TenonWidth);
            TenonHeight = Math.Min(height, TenonHeight);

            var basePts = new Point3d[5];
            var baseTenonPts = new Point3d[5];
            var tipTenonPts = new Point3d[5];

            basePts[0] = planes[0].PointAt(width * 0.5 + Added, height * 0.5 + Added);
            basePts[1] = planes[0].PointAt(width * 0.5 + Added, -height * 0.5 - Added);
            basePts[2] = planes[0].PointAt(-width * 0.5 - Added, -height * 0.5 - Added);
            basePts[3] = planes[0].PointAt(-width * 0.5 - Added, height * 0.5 + Added);
            basePts[4] = basePts[0];

            var baseOutline = new Polyline(basePts).ToNurbsCurve();

            baseTenonPts[0] = planes[0].PointAt(TenonWidth * 0.5, TenonHeight * 0.5);
            baseTenonPts[1] = planes[0].PointAt(TenonWidth * 0.5, -TenonHeight * 0.5);
            baseTenonPts[2] = planes[0].PointAt(-TenonWidth * 0.5, -TenonHeight * 0.5);
            baseTenonPts[3] = planes[0].PointAt(-TenonWidth * 0.5, TenonHeight * 0.5);
            baseTenonPts[4] = baseTenonPts[0];
            var baseTenonOutline = new Polyline(baseTenonPts).ToNurbsCurve();
            baseTenonOutline = Curve.CreateFilletCornersCurve(baseTenonOutline, FilletRadius, 0.01, 0.1).ToNurbsCurve();

            tipTenonPts[0] = endPlane.PointAt(TenonWidth * 0.5, TenonHeight * 0.5);
            tipTenonPts[1] = endPlane.PointAt(TenonWidth * 0.5, -TenonHeight * 0.5);
            tipTenonPts[2] = endPlane.PointAt(-TenonWidth * 0.5, -TenonHeight * 0.5);
            tipTenonPts[3] = endPlane.PointAt(-TenonWidth * 0.5, TenonHeight * 0.5);
            tipTenonPts[4] = tipTenonPts[0];
            var tipTenonOutline = new Polyline(tipTenonPts).ToNurbsCurve();
            tipTenonOutline = Curve.CreateFilletCornersCurve(tipTenonOutline, FilletRadius, 0.01, 0.1).ToNurbsCurve();

            for (int i = 0; i < 4; ++i)
            {
                debug.Add(basePts[i]);
                debug.Add(baseTenonPts[i]);
                debug.Add(tipTenonPts[i]);
            }

            debug.Add(baseOutline);
            debug.Add(baseTenonOutline);
            debug.Add(tipTenonOutline);

            var baseSrf = Brep.CreatePlanarBreps(new Curve[] { baseOutline, baseTenonOutline }, 0.01)[0];
            var tenonSrf = Brep.CreateFromLoft(new Curve[] { baseTenonOutline, tipTenonOutline }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
            var tenonTipSrf = Brep.CreatePlanarBreps(new Curve[] { tipTenonOutline }, 0.01)[0];

            var cutterJoined = Brep.JoinBreps(new Brep[] { baseSrf, tenonSrf, tenonTipSrf }, 0.01);

            FirstHalf.Geometry.AddRange(cutterJoined);
            SecondHalf.Geometry.AddRange(cutterJoined);

            // Create dowels
            var dowelPlane = new Plane(planes[0].Origin + planes[0].ZAxis * TenonLength * 0.5, planes[0].YAxis);
            dowelPlane.Transform(Transform.Translation(dowelPlane.ZAxis * -DowelLength * 0.5));

            var dowel = new Cylinder(
              new Circle(dowelPlane, DowelDiameter * 0.5), DowelLength).ToBrep(true, true);

            FirstHalf.Geometry.Add(dowel);
            SecondHalf.Geometry.Add(dowel);

            return true;
        }
    }
}