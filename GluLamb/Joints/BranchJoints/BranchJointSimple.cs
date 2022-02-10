using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Factory;

namespace GluLamb.Joints
{
    public class BranchJointSimple : BranchJoint
    {
        public BranchJointSimple(List<Element> elements, JointCondition jc) : base(elements, jc)
        {

        }

        public override string ToString()
        {
            return "BranchJointSimple";
        }

        public override bool Construct(bool append = false)
        {

            var yaxes = new Vector3d[2];
            var zaxes = new Vector3d[2];
            var starts = new Point3d[2];
            var ends = new Point3d[2];
            var beams = new Beam[2];

            double width = 0;
            double extension = 1300;
            double mult = 2.0;

            beams[0] = (FirstHalf.Element as BeamElement).Beam;
            beams[1] = (SecondHalf.Element as BeamElement).Beam;

            for (int i = 0; i < 2; ++i)
            {
                var b = beams[i];

                if (b.Height > width) width = b.Height;

                yaxes[i] = b.GetPlane(b.Centreline.Domain.Min).YAxis;
                zaxes[i] = b.GetPlane(b.Centreline.Domain.Min).ZAxis;

                starts[i] = b.Centreline.PointAt(b.Centreline.Domain.Min);
                ends[i] = b.Centreline.PointAt(b.Centreline.Domain.Max);
            }

            //
            var d0 = Utility.OverlapCurves(beams[0].Centreline, beams[1].Centreline);
            var d1 = Utility.OverlapCurves(beams[1].Centreline, beams[0].Centreline);

            starts[0] = beams[0].Centreline.PointAt(d0.Min);
            starts[1] = beams[1].Centreline.PointAt(d1.Min);

            ends[0] = beams[0].Centreline.PointAt(d0.Max);
            ends[1] = beams[1].Centreline.PointAt(d1.Max);

            //

            if (zaxes[0] * zaxes[1] < 0)
            {
                //yaxes[1].Reverse();

                var temp = starts[1];
                starts[1] = ends[1];
                ends[1] = temp;
            }

            var start = (starts[0] + starts[1]) / 2;
            var end = (ends[0] + ends[1]) / 2;
            var mid = (start + end) / 2;

            var v = (yaxes[0] + yaxes[1]) / 2;
            v.Unitize();
            var n = end - start;
            n.Unitize();

            var p0 = start + v * width * 0.5 * mult - n * extension;
            var p1 = end + v * width * 0.5 * mult + n * extension;
            var p2 = end - v * width * 0.5 * mult + n * extension;
            var p3 = start - v * width * 0.5 * mult - n * extension;

            double t0, t1;
            beams[0].Centreline.ClosestPoint(mid, out t0);
            beams[1].Centreline.ClosestPoint(mid, out t1);

            var brep = Brep.CreateFromCornerPoints(p0, p1, p2, p3, 0.001);

            FirstHalf.Geometry.Add(brep);
            SecondHalf.Geometry.Add(brep);

            return true;

        }
    }
}
